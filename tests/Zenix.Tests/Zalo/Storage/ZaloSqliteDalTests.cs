// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;
using Zenix.Core.Zalo.Storage;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="ZaloSqliteDalService"/> verifying WAL mode, mandatory pragmas,
/// concurrency serialization, safe UNIQUE conflict tolerance, and in-memory operations.
/// </summary>
public sealed class ZaloSqliteDalTests : IDisposable
{
    private readonly string _tempDirectory;

    public ZaloSqliteDalTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"zenix_dal_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

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
            // Best effort cleanup
        }
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task FileDatabase_AppliesWalModeAndPerformancePragmas()
    {
        await using var dal = new ZaloSqliteDalService("test_account", isInMemory: false, _tempDirectory);
        await using var connection = await dal.CreateOpenConnectionAsync();

        // Check journal mode
        await using var modeCmd = connection.CreateCommand();
        modeCmd.CommandText = "PRAGMA journal_mode;";
        var mode = (string?)await modeCmd.ExecuteScalarAsync();
        mode.Should().BeEquivalentTo("wal");

        // Check synchronous mode (NORMAL is 1)
        await using var syncCmd = connection.CreateCommand();
        syncCmd.CommandText = "PRAGMA synchronous;";
        var sync = Convert.ToInt32(await syncCmd.ExecuteScalarAsync());
        sync.Should().Be(1);

        // Check busy timeout
        await using var timeoutCmd = connection.CreateCommand();
        timeoutCmd.CommandText = "PRAGMA busy_timeout;";
        var timeout = Convert.ToInt32(await timeoutCmd.ExecuteScalarAsync());
        timeout.Should().Be(5000);

        // Check foreign keys
        await using var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys;";
        var fk = Convert.ToInt32(await fkCmd.ExecuteScalarAsync());
        fk.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task ExecuteNonQueryAsync_SafeUniqueConstraintTolerance_SuppressesViolation()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");

        // Create table with unique constraint
        await dal.ExecuteNonQueryAsync("CREATE TABLE items (id INTEGER PRIMARY KEY, code TEXT UNIQUE);");

        // First insert succeeds
        var first = await dal.ExecuteNonQueryAsync(
            "INSERT INTO items (code) VALUES (@code);",
            new Dictionary<string, object?> { ["code"] = "unique_alpha" });

        first.RowsAffected.Should().Be(1);
        first.LastInsertedId.Should().Be(1);

        // Duplicate insert: safe UNIQUE constraint handling should return { LastInsertedId = 0, RowsAffected = 0 }
        var duplicate = await dal.ExecuteNonQueryAsync(
            "INSERT INTO items (code) VALUES (@code);",
            new Dictionary<string, object?> { ["code"] = "unique_alpha" });

        duplicate.RowsAffected.Should().Be(0);
        duplicate.LastInsertedId.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task ExecuteNonQueryAsync_NonUniqueError_ThrowsSqliteException()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");

        var act = async () => await dal.ExecuteNonQueryAsync("INSERT INTO non_existent_table (id) VALUES (1);");

        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task Concurrency_ConcurrentWrites_AreSerializedByWriteLock()
    {
        await using var dal = new ZaloSqliteDalService("concurrent_account", isInMemory: false, _tempDirectory);
        await dal.ExecuteNonQueryAsync("CREATE TABLE counter (val INTEGER);");
        await dal.ExecuteNonQueryAsync("INSERT INTO counter (val) VALUES (0);");

        var tasks = new List<Task>();
        const int concurrentCount = 10;

        for (var i = 0; i < concurrentCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await dal.ExecuteNonQueryAsync("UPDATE counter SET val = val + 1;");
            }));
        }

        await Task.WhenAll(tasks);

        var finalCount = await dal.ExecuteScalarAsync<int>("SELECT val FROM counter;");
        finalCount.Should().Be(concurrentCount);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task ExecuteQueryAsync_ConcurrentReads_ExecuteWithoutDeadlock()
    {
        await using var dal = new ZaloSqliteDalService("reads_account", isInMemory: false, _tempDirectory);
        await dal.ExecuteNonQueryAsync("CREATE TABLE messages (id INTEGER PRIMARY KEY, content TEXT);");

        for (var i = 0; i < 50; i++)
        {
            await dal.ExecuteNonQueryAsync(
                "INSERT INTO messages (content) VALUES (@c);",
                new Dictionary<string, object?> { ["c"] = $"message_{i}" });
        }

        var readTasks = new List<Task<int>>();
        for (var i = 0; i < 15; i++)
        {
            readTasks.Add(Task.Run(async () =>
            {
                var rows = await dal.ExecuteQueryAsync(
                    "SELECT id, content FROM messages WHERE id > 10;",
                    reader => reader.GetInt32(0));
                return rows.Count;
            }));
        }

        var results = await Task.WhenAll(readTasks);
        foreach (var count in results)
        {
            count.Should().Be(40);
        }
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task InMemoryDatabase_PreservesDataAcrossMultipleConnections()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        await dal.ExecuteNonQueryAsync("CREATE TABLE prefs (k TEXT PRIMARY KEY, v TEXT);");
        await dal.ExecuteNonQueryAsync(
            "INSERT INTO prefs (k, v) VALUES (@k, @v);",
            new Dictionary<string, object?> { ["k"] = "theme", ["v"] = "Dark" });

        var value = await dal.ExecuteScalarAsync<string>(
            "SELECT v FROM prefs WHERE k = @k;",
            new Dictionary<string, object?> { ["k"] = "theme" });

        value.Should().Be("Dark");
    }
}
