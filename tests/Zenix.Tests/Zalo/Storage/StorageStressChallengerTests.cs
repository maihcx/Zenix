// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Messages.Payloads;
using Zenix.Core.Zalo.Models.Profiles;
using Zenix.Core.Zalo.Storage;
using Zenix.Core.Zalo.Storage.Repositories;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Empirical Challenger Stress & Concurrency test suite targeting SQLite DAL,
/// duplicate insert race conditions, pagination sort stability, and account wipe cleanup.
/// </summary>
public sealed class StorageStressChallengerTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageStressChallengerTests"/> class.
    /// </summary>
    public StorageStressChallengerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"zenix_stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup in test tear-down
        }
    }

    /// <summary>
    /// Scenario 1: High concurrency write/read stress.
    /// Verifies that simultaneous read leases during active write operations do not throw SQLITE_BUSY or deadlock.
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task HighConcurrency_SimultaneousReadsAndWrites_DoNotThrowSqliteBusyOrDeadlock()
    {
        const string accountId = "stress_concurrent_wal";
        await using var service = new ZaloStorageService(forceInMemory: false, _tempDirectory);
        await service.InitializeAccountDatabaseAsync(accountId);

        const string convId = "conv_stress_wal";
        await service.Conversations.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Stress Thread",
            LastActivityTimestamp = 1000
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var exceptions = new ConcurrentBag<Exception>();

        const int writerCount = 6;
        const int readerCount = 12;
        const int operationsPerWriter = 40;

        var totalWritesCompleted = 0;
        var totalReadsCompleted = 0;

        // Writers continuously insert messages
        var writerTasks = Enumerable.Range(0, writerCount).Select(writerIndex => Task.Run(async () =>
        {
            for (var op = 0; op < operationsPerWriter; op++)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var msgId = $"cli_w{writerIndex}_{op}";
                    var msg = new ChatMessage
                    {
                        ClientMessageId = msgId,
                        MessageId = $"srv_w{writerIndex}_{op}",
                        ConversationId = convId,
                        SenderId = $"sender_{writerIndex}",
                        Payload = new TextPayload { Content = $"Stress message {op} from writer {writerIndex}" },
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    await service.Messages.InsertAsync(msg, cts.Token);
                    Interlocked.Increment(ref totalWritesCompleted);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }, cts.Token)).ToList();

        // Readers continuously read messages concurrently
        var readerTasks = Enumerable.Range(0, readerCount).Select(readerIndex => Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested && Volatile.Read(ref totalWritesCompleted) < writerCount * operationsPerWriter)
            {
                try
                {
                    var messages = await service.Messages.GetByConversationAsync(convId, null, 25, cts.Token);
                    messages.Should().NotBeNull();
                    Interlocked.Increment(ref totalReadsCompleted);
                    await Task.Delay(5, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }, cts.Token)).ToList();

        await Task.WhenAll(writerTasks);
        cts.Cancel();

        try
        {
            await Task.WhenAll(readerTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling readers
        }

        exceptions.Should().BeEmpty("Concurrent reads and writes must not throw SQLITE_BUSY, lock violations, or deadlocks");
        totalWritesCompleted.Should().Be(writerCount * operationsPerWriter);
        totalReadsCompleted.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Scenario 2: Race condition with duplicate cli_msg_id inserts.
    /// Asserts that multiple concurrent inserts of the same unique key return safe results
    /// (RowsAffected = 0, LastInsertedId = 0) without unhandled exceptions, and persist exactly one record.
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task DuplicateInsertRaceCondition_ConcurrentInserts_SafelyResolved()
    {
        const string accountId = "stress_race_dup";
        await using var dal = new ZaloSqliteDalService(accountId, isInMemory: false, _tempDirectory);

        // Initialize schema
        var migrator = new ZaloSqliteMigrator();
        await using (var conn = await dal.CreateOpenConnectionAsync())
        {
            await migrator.MigrateAsync(conn);
        }

        const string convId = "conv_race_dup";
        var convRepo = new ConversationRepository(dal);
        await convRepo.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Duplicate Race Thread",
            LastActivityTimestamp = 1000
        });

        const string duplicateCliId = "cli_duplicate_target_001";
        const int concurrentAttempts = 30;

        var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = new ConcurrentBag<SqliteExecutionResult>();
        var exceptions = new ConcurrentBag<Exception>();

        const string sql = @"
            INSERT INTO messages (
                cli_msg_id, server_msg_id, conversation_id, conversation_type,
                sender_id, sender_name, sender_avatar_url, is_self,
                status, kind, payload_type, payload_json,
                quote_msg_id, quote_json, mentions_json, reactions_json,
                timestamp, sequence_offset, is_edited, is_recalled, is_pinned
            ) VALUES (
                @cliId, @srvId, @convId, 0,
                'user_race', 'Race User', NULL, 1,
                1, 0, 'TextPayload', '{""content"":""Duplicate race test""}',
                NULL, NULL, NULL, NULL,
                1700000000000, 0, 0, 0, 0
            );";

        var tasks = Enumerable.Range(0, concurrentAttempts).Select(i => Task.Run(async () =>
        {
            await startBarrier.Task;
            try
            {
                var p = new Dictionary<string, object?>
                {
                    ["cliId"] = duplicateCliId,
                    ["srvId"] = $"srv_race_{i}",
                    ["convId"] = convId
                };

                var res = await dal.ExecuteNonQueryAsync(sql, p);
                results.Add(res);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToList();

        // Release all concurrent tasks at once
        startBarrier.SetResult();
        await Task.WhenAll(tasks);

        exceptions.Should().BeEmpty("Duplicate insert race condition must be safely handled without throwing SqliteException");
        results.Should().HaveCount(concurrentAttempts);

        var successfulInserts = results.Where(r => r.RowsAffected == 1).ToList();
        var suppressedDuplicates = results.Where(r => r.RowsAffected == 0 && r.LastInsertedId == 0).ToList();

        successfulInserts.Should().HaveCount(1, "Exactly one insert must succeed with RowsAffected == 1");
        suppressedDuplicates.Should().HaveCount(concurrentAttempts - 1, "All conflicting duplicate inserts must return RowsAffected == 0 and LastInsertedId == 0");

        var totalInDb = await dal.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM messages WHERE cli_msg_id = @id;",
            new Dictionary<string, object?> { ["id"] = duplicateCliId });
        totalInDb.Should().Be(1, "Database must contain exactly 1 row for the unique cli_msg_id");
    }

    /// <summary>
    /// Scenario 3: Stable tie-breaking under high volume.
    /// Inserts 1,000 messages where groups share identical timestamps and verifies ordering stability.
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task StableTieBreaking_1000MessagesWithTimestampCollisions_VerifiesStabilityAndPagination()
    {
        const string accountId = "stress_tie_break";
        await using var dal = new ZaloSqliteDalService(accountId, isInMemory: false, _tempDirectory);

        var migrator = new ZaloSqliteMigrator();
        await using (var conn = await dal.CreateOpenConnectionAsync())
        {
            await migrator.MigrateAsync(conn);
        }

        const string convId = "conv_tie_break_1000";
        var convRepo = new ConversationRepository(dal);
        await convRepo.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Tie Breaking 1000 Thread",
            LastActivityTimestamp = 50000
        });

        // Insert 1,000 messages directly into SQLite with timestamp and sequence_offset
        // 100 distinct timestamp groups (from 100 down to 1), with 10 messages per group sharing the same timestamp,
        // with sequence_offset from 0 to 9.
        const int groupCount = 100;
        const int perGroup = 10;
        const int totalMessages = groupCount * perGroup; // 1,000

        await dal.ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO messages (
                    cli_msg_id, server_msg_id, conversation_id, conversation_type,
                    sender_id, sender_name, sender_avatar_url, is_self,
                    status, kind, payload_type, payload_json,
                    quote_msg_id, quote_json, mentions_json, reactions_json,
                    timestamp, sequence_offset, is_edited, is_recalled, is_pinned
                ) VALUES (
                    @cliId, @srvId, @convId, 0,
                    'user_stress', 'User', NULL, 1,
                    1, 0, 'TextPayload', '{""content"":""Stress message""}',
                    NULL, NULL, NULL, NULL,
                    @ts, @offset, 0, 0, 0
                );";

            var pCli = cmd.Parameters.Add("@cliId", SqliteType.Text);
            var pSrv = cmd.Parameters.Add("@srvId", SqliteType.Text);
            var pConv = cmd.Parameters.Add("@convId", SqliteType.Text);
            pConv.Value = convId;
            var pTs = cmd.Parameters.Add("@ts", SqliteType.Integer);
            var pOffset = cmd.Parameters.Add("@offset", SqliteType.Integer);

            for (var g = groupCount; g >= 1; g--)
            {
                var ts = 1700000000000L + (g * 1000L);
                for (var o = 0; o < perGroup; o++)
                {
                    pCli.Value = $"cli_g{g:D3}_o{o:D2}";
                    pSrv.Value = $"srv_g{g:D3}_o{o:D2}";
                    pTs.Value = ts;
                    pOffset.Value = o;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        });

        // 1. Verify that SQL query ordering by `timestamp DESC, sequence_offset DESC` is 100% deterministic
        var orderedRows = await dal.ExecuteQueryAsync(
            "SELECT cli_msg_id, timestamp, sequence_offset FROM messages WHERE conversation_id = @convId ORDER BY timestamp DESC, sequence_offset DESC;",
            reader => new
            {
                CliId = reader.GetString(0),
                Timestamp = reader.GetInt64(1),
                SequenceOffset = reader.GetInt32(2)
            },
            new Dictionary<string, object?> { ["convId"] = convId });

        orderedRows.Should().HaveCount(totalMessages);

        // Verify ordering: descending timestamp, and within each timestamp, descending sequence_offset
        for (var i = 0; i < orderedRows.Count - 1; i++)
        {
            var cur = orderedRows[i];
            var next = orderedRows[i + 1];

            if (cur.Timestamp == next.Timestamp)
            {
                cur.SequenceOffset.Should().BeGreaterThan(next.SequenceOffset,
                    $"Within timestamp {cur.Timestamp}, sequence_offset must strictly descend (tie-breaker)");
            }
            else
            {
                cur.Timestamp.Should().BeGreaterThan(next.Timestamp, "Timestamps must strictly descend");
            }
        }

        // 2. Paginate through all 1,000 messages with page size 50 across 20 pages
        var allPaginatedIds = new List<string>();
        long? currentCutoffTs = null;
        const int pageSize = 50;

        for (var page = 0; page < totalMessages / pageSize; page++)
        {
            string pageSql;
            Dictionary<string, object?> parameters;

            if (currentCutoffTs.HasValue)
            {
                pageSql = @"
                    SELECT cli_msg_id, timestamp, sequence_offset 
                    FROM messages 
                    WHERE conversation_id = @convId AND timestamp < @cutoff
                    ORDER BY timestamp DESC, sequence_offset DESC 
                    LIMIT @pageSize;";
                parameters = new Dictionary<string, object?>
                {
                    ["convId"] = convId,
                    ["cutoff"] = currentCutoffTs.Value,
                    ["pageSize"] = pageSize
                };
            }
            else
            {
                pageSql = @"
                    SELECT cli_msg_id, timestamp, sequence_offset 
                    FROM messages 
                    WHERE conversation_id = @convId
                    ORDER BY timestamp DESC, sequence_offset DESC 
                    LIMIT @pageSize;";
                parameters = new Dictionary<string, object?>
                {
                    ["convId"] = convId,
                    ["pageSize"] = pageSize
                };
            }

            var pageResults = await dal.ExecuteQueryAsync(
                pageSql,
                reader => new
                {
                    CliId = reader.GetString(0),
                    Timestamp = reader.GetInt64(1),
                    SequenceOffset = reader.GetInt32(2)
                },
                parameters);

            pageResults.Should().HaveCount(pageSize);
            allPaginatedIds.AddRange(pageResults.Select(r => r.CliId));
            currentCutoffTs = pageResults.Last().Timestamp;
        }

        allPaginatedIds.Should().HaveCount(totalMessages);
        allPaginatedIds.Distinct().Should().HaveCount(totalMessages, "Zero duplicate rows allowed across pagination boundaries");
    }

    /// <summary>
    /// Scenario 3 (Adversarial Analysis): Evaluates MessageRepository pagination when timestamp collisions cross page boundaries.
    /// Demonstrates the architectural defect: using beforeTimestamp alone in GetByConversationAsync
    /// causes message loss when timestamp collisions cross a page boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task MessageRepository_PaginationBoundaryCollisions_ExposesCursorLimitations()
    {
        const string accountId = "stress_boundary_collision";
        await using var dal = new ZaloSqliteDalService(accountId, isInMemory: false, _tempDirectory);

        var migrator = new ZaloSqliteMigrator();
        await using (var conn = await dal.CreateOpenConnectionAsync())
        {
            await migrator.MigrateAsync(conn);
        }

        const string convId = "conv_boundary_test";
        var convRepo = new ConversationRepository(dal);
        await convRepo.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Boundary Test Thread",
            LastActivityTimestamp = 1000
        });

        var messageRepo = new MessageRepository(dal);

        // Seed 70 messages:
        // 30 messages with ts = 2000
        // 40 messages with ts = 1000
        var batch = new List<ChatMessage>();
        for (var i = 0; i < 30; i++)
        {
            batch.Add(new ChatMessage
            {
                ClientMessageId = $"cli_ts2000_{i:D2}",
                MessageId = $"srv_ts2000_{i:D2}",
                ConversationId = convId,
                SenderId = "u1",
                Payload = new TextPayload { Content = "ts 2000" },
                Timestamp = 2000L
            });
        }

        for (var i = 0; i < 40; i++)
        {
            batch.Add(new ChatMessage
            {
                ClientMessageId = $"cli_ts1000_{i:D2}",
                MessageId = $"srv_ts1000_{i:D2}",
                ConversationId = convId,
                SenderId = "u1",
                Payload = new TextPayload { Content = "ts 1000" },
                Timestamp = 1000L
            });
        }

        await messageRepo.InsertBatchAsync(batch);

        // Fetch page 1 with pageSize = 50:
        // Returns 30 messages of ts=2000 and 20 messages of ts=1000.
        var page1 = await messageRepo.GetByConversationAsync(convId, null, pageSize: 50);
        page1.Should().HaveCount(50);

        var lastOfPage1 = page1.Last();
        lastOfPage1.Timestamp.Should().Be(1000L);

        // Fetch page 2 using beforeTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(lastOfPage1.Timestamp) = 1000L
        var page2 = await messageRepo.GetByConversationAsync(
            convId,
            DateTimeOffset.FromUnixTimeMilliseconds(lastOfPage1.Timestamp),
            pageSize: 50);

        // Finding: page2 returns 0 items because `timestamp < @cutoff` drops the remaining 20 items sharing timestamp 1000!
        // This empirically proves that timestamp-only pagination cannot handle collisions across page boundaries.
        page2.Count.Should().Be(0, "Current MessageRepository implementation drops remaining colliding records at boundary");
    }

    /// <summary>
    /// Scenario 3 (Proof of Resolution): Demonstrates how composite cursor (timestamp, sequence_offset)
    /// achieves 100% stable sorting with zero skipped or duplicated rows across pagination boundaries
    /// even with irregular page sizes (e.g. pageSize = 37 across 1,000 items).
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task Pagination_CompositeCursor_AchievesZeroSkippedAndZeroDuplicatesAcrossCollisions()
    {
        const string accountId = "stress_composite_cursor";
        await using var dal = new ZaloSqliteDalService(accountId, isInMemory: false, _tempDirectory);

        var migrator = new ZaloSqliteMigrator();
        await using (var conn = await dal.CreateOpenConnectionAsync())
        {
            await migrator.MigrateAsync(conn);
        }

        const string convId = "conv_comp_1000";
        var convRepo = new ConversationRepository(dal);
        await convRepo.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Composite Cursor Thread",
            LastActivityTimestamp = 50000
        });

        // Insert 1,000 messages with 100 timestamps, 10 messages per timestamp
        const int totalItems = 1000;
        await dal.ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO messages (
                    cli_msg_id, server_msg_id, conversation_id, conversation_type,
                    sender_id, sender_name, sender_avatar_url, is_self,
                    status, kind, payload_type, payload_json,
                    quote_msg_id, quote_json, mentions_json, reactions_json,
                    timestamp, sequence_offset, is_edited, is_recalled, is_pinned
                ) VALUES (
                    @cliId, @srvId, @convId, 0,
                    'user_stress', 'User', NULL, 1,
                    1, 0, 'TextPayload', '{""content"":""Stress message""}',
                    NULL, NULL, NULL, NULL,
                    @ts, @offset, 0, 0, 0
                );";

            var pCli = cmd.Parameters.Add("@cliId", SqliteType.Text);
            var pSrv = cmd.Parameters.Add("@srvId", SqliteType.Text);
            var pConv = cmd.Parameters.Add("@convId", SqliteType.Text);
            pConv.Value = convId;
            var pTs = cmd.Parameters.Add("@ts", SqliteType.Integer);
            var pOffset = cmd.Parameters.Add("@offset", SqliteType.Integer);

            for (var i = 0; i < totalItems; i++)
            {
                var ts = 1700000000000L + ((i / 10) * 1000L);
                var offset = i % 10;
                pCli.Value = $"cli_item_{i:D4}";
                pSrv.Value = $"srv_item_{i:D4}";
                pTs.Value = ts;
                pOffset.Value = offset;
                await cmd.ExecuteNonQueryAsync();
            }
        });

        // Paginate using composite cursor: (timestamp, sequence_offset) < (cursorTs, cursorOffset)
        // With irregular pageSize = 37, which constantly splits the 10-item timestamp groups!
        const int pageSize = 37;
        var collected = new List<string>();

        long? cursorTs = null;
        int? cursorOffset = null;

        while (true)
        {
            string sql;
            Dictionary<string, object?> p;

            if (cursorTs.HasValue && cursorOffset.HasValue)
            {
                sql = @"
                    SELECT cli_msg_id, timestamp, sequence_offset
                    FROM messages
                    WHERE conversation_id = @convId
                      AND ((timestamp < @cTs) OR (timestamp = @cTs AND sequence_offset < @cOff))
                    ORDER BY timestamp DESC, sequence_offset DESC
                    LIMIT @pageSize;";

                p = new Dictionary<string, object?>
                {
                    ["convId"] = convId,
                    ["cTs"] = cursorTs.Value,
                    ["cOff"] = cursorOffset.Value,
                    ["pageSize"] = pageSize
                };
            }
            else
            {
                sql = @"
                    SELECT cli_msg_id, timestamp, sequence_offset
                    FROM messages
                    WHERE conversation_id = @convId
                    ORDER BY timestamp DESC, sequence_offset DESC
                    LIMIT @pageSize;";

                p = new Dictionary<string, object?>
                {
                    ["convId"] = convId,
                    ["pageSize"] = pageSize
                };
            }

            var rows = await dal.ExecuteQueryAsync(sql, r => new
            {
                CliId = r.GetString(0),
                Timestamp = r.GetInt64(1),
                Offset = r.GetInt32(2)
            }, p);

            if (rows.Count == 0)
            {
                break;
            }

            collected.AddRange(rows.Select(r => r.CliId));
            var last = rows.Last();
            cursorTs = last.Timestamp;
            cursorOffset = last.Offset;
        }

        collected.Should().HaveCount(totalItems, "Composite cursor must retrieve all 1,000 items without skipping");
        collected.Distinct().Should().HaveCount(totalItems, "Zero duplicated items allowed across boundaries");
    }

    /// <summary>
    /// Scenario 4: Account wipe cleanliness.
    /// Verifies that WipeAccountDataAsync deletes .db, .db-wal, and .db-shm files cleanly
    /// without leaving orphan lock handles, allowing immediate clean recreation.
    /// </summary>
    [Fact]
    [Trait("Category", "Storage")]
    public async Task WipeAccountDataAsync_DeletesDbWalAndShmFiles_WithoutLeavingLockHandles()
    {
        const string accountId = "stress_wipe_account";
        var service = new ZaloStorageService(forceInMemory: false, _tempDirectory);
        await service.InitializeAccountDatabaseAsync(accountId);

        var accountDir = Path.Combine(_tempDirectory, accountId);
        var primaryDb = Path.Combine(accountDir, "chat_storage.db");
        var walFile = Path.Combine(accountDir, "chat_storage.db-wal");
        var shmFile = Path.Combine(accountDir, "chat_storage.db-shm");

        // Write substantial data to ensure WAL file is populated
        for (var i = 0; i < 50; i++)
        {
            await service.Conversations.UpsertAsync(new ConversationSummary
            {
                ConversationId = $"conv_wipe_{i}",
                Title = $"Wipe Thread {i}",
                LastActivityTimestamp = 1000 + i
            });
        }

        // Perform reads to ensure read connection handles were created
        var convs = await service.Conversations.GetAllConversationsAsync();
        convs.Should().HaveCount(50);

        File.Exists(primaryDb).Should().BeTrue("Primary DB file must exist before wipe");

        // Wipe account data
        await service.WipeAccountDataAsync(accountId);

        // Verify all storage files are deleted
        File.Exists(primaryDb).Should().BeFalse("chat_storage.db must be deleted after wipe");
        File.Exists(walFile).Should().BeFalse("chat_storage.db-wal must be deleted after wipe");
        File.Exists(shmFile).Should().BeFalse("chat_storage.db-shm must be deleted after wipe");

        // Verify account folder is removed or empty
        if (Directory.Exists(accountDir))
        {
            Directory.GetFileSystemEntries(accountDir).Should().BeEmpty("Account directory must be empty if not deleted");
        }

        // Verify immediate re-initialization works without file locking errors
        var recreateAct = async () =>
        {
            await service.InitializeAccountDatabaseAsync(accountId);
            await service.Conversations.UpsertAsync(new ConversationSummary
            {
                ConversationId = "conv_recreated",
                Title = "Recreated Thread",
                LastActivityTimestamp = 2000
            });
            await service.CloseDatabaseAsync();
        };

        await recreateAct.Should().NotThrowAsync("Recreating database after wipe must not fail due to orphan locks");

        await service.DisposeAsync();
    }
}
