// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Storage;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="ZaloSqliteMigrator"/> verifying clean transition to schema version 400,
/// table and index presence in sqlite_master, idempotency, and operational trigger functionality.
/// </summary>
public sealed class SqliteMigrationTests
{
    [Fact]
    [Trait("Category", "Storage")]
    public async Task MigrateAsync_FreshDatabase_TransitionsToVersion400()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        using var migrator = new ZaloSqliteMigrator();

        await using var connection = await dal.CreateOpenConnectionAsync();

        var initialVersion = await migrator.GetUserVersionAsync(connection);
        initialVersion.Should().Be(0);

        var migrated = await migrator.MigrateAsync(connection);
        migrated.Should().BeTrue();

        var targetVersion = await migrator.GetUserVersionAsync(connection);
        targetVersion.Should().Be(400);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task MigrateAsync_CreatesAllNormalizedTablesAndViews()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        using var migrator = new ZaloSqliteMigrator();

        await using var connection = await dal.CreateOpenConnectionAsync();
        await migrator.MigrateAsync(connection);

        var tables = await dal.ExecuteQueryAsync(
            "SELECT name FROM sqlite_master WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%';",
            reader => reader.GetString(0));

        tables.Should().Contain(new[]
        {
            "conversations",
            "messages",
            "contacts",
            "group_profiles",
            "groups",
            "group_members",
            "drafts",
            "keychain_keys",
            "sync_checkpoints"
        });
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task MigrateAsync_CreatesAllRequiredPerformanceIndexes()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        using var migrator = new ZaloSqliteMigrator();

        await using var connection = await dal.CreateOpenConnectionAsync();
        await migrator.MigrateAsync(connection);

        var indexes = await dal.ExecuteQueryAsync(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%';",
            reader => reader.GetString(0));

        indexes.Should().Contain(new[]
        {
            "idx_conv_updated",
            "idx_messages_conv_ts",
            "idx_messages_cli_id",
            "idx_messages_server_id",
            "idx_contacts_sort"
        });
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task MigrateAsync_IsIdempotent_SecondCallReturnsFalse()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        using var migrator = new ZaloSqliteMigrator();

        await using var connection = await dal.CreateOpenConnectionAsync();

        var first = await migrator.MigrateAsync(connection);
        first.Should().BeTrue();

        var second = await migrator.MigrateAsync(connection);
        second.Should().BeFalse();

        var finalVersion = await migrator.GetUserVersionAsync(connection);
        finalVersion.Should().Be(400);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task Trigger_UpdateConversationOnNewMessage_UpdatesTimestampAndUnreadCount()
    {
        await using var dal = new ZaloSqliteDalService(":memory:");
        using var migrator = new ZaloSqliteMigrator();

        await using var connection = await dal.CreateOpenConnectionAsync();
        await migrator.MigrateAsync(connection);

        // Seed a conversation with unread_count = 0
        await dal.ExecuteNonQueryAsync(@"
            INSERT INTO conversations (conversation_id, type, title, last_activity_at, unread_count)
            VALUES ('conv_test_1', 0, 'Test Conv', 1000, 0);");

        // Insert incoming message (is_self = 0)
        await dal.ExecuteNonQueryAsync(@"
            INSERT INTO messages (
                cli_msg_id, conversation_id, sender_id, sender_name,
                is_self, status, kind, payload_type, payload_json, timestamp
            ) VALUES (
                'cli_1', 'conv_test_1', 'user_2', 'Alice',
                0, 1, 0, 'TextPayload', '{""Content"":""Hello""}', 2500
            );");

        var conv = await dal.ExecuteQueryAsync(
            "SELECT last_activity_at, unread_count, last_sender_name FROM conversations WHERE conversation_id = 'conv_test_1';",
            reader => new
            {
                LastActivity = reader.GetInt64(0),
                UnreadCount = reader.GetInt32(1),
                LastSender = reader.GetString(2)
            });

        conv.Should().HaveCount(1);
        conv[0].LastActivity.Should().Be(2500);
        conv[0].UnreadCount.Should().Be(1);
        conv[0].LastSender.Should().Be("Alice");
    }
}
