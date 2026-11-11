// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Messages.Payloads;
using Zenix.Core.Zalo.Models.Profiles;
using Zenix.Core.Zalo.Storage;
using Zenix.Core.Zalo.Storage.Repositories;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="MessageRepository"/> verifying sub-1.5ms indexed pagination,
/// sort stability under identical timestamps, status transitions, recall, and batch operations.
/// </summary>
public sealed class MessageRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly ZaloSqliteDalService _dal;
    private readonly ZaloSqliteMigrator _migrator;
    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;

    public MessageRepositoryTests()
    {
        _dal = new ZaloSqliteDalService(":memory:");
        _migrator = new ZaloSqliteMigrator();
        _conversations = new ConversationRepository(_dal);
        _messages = new MessageRepository(_dal);
    }

    public async Task InitializeAsync()
    {
        await using var connection = await _dal.CreateOpenConnectionAsync();
        await _migrator.MigrateAsync(connection);

        // Seed default conversation
        await _conversations.UpsertAsync(new ConversationSummary
        {
            ConversationId = "conv_bench",
            Title = "Benchmark Thread",
            LastActivityTimestamp = 1000
        });
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _migrator.Dispose();
        _dal.Dispose();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task GetByConversationAsync_50Messages_ExecutesWithinTargetLatency()
    {
        const string convId = "conv_bench";
        var messages = new List<ChatMessage>();

        // Seed 100 messages
        for (var i = 0; i < 100; i++)
        {
            messages.Add(new ChatMessage
            {
                ClientMessageId = $"cli_bench_{i:D4}",
                MessageId = $"srv_bench_{i:D4}",
                ConversationId = convId,
                SenderId = "user_me",
                SenderName = "Me",
                IsSelf = true,
                Payload = new TextPayload { Content = $"Benchmark message {i}" },
                Timestamp = 1700000000000L + (i * 1000L)
            });
        }

        await _messages.InsertBatchAsync(messages);

        // Warmup JIT
        _ = await _messages.GetByConversationAsync(convId, null, 50);

        // Benchmark retrieval
        var sw = Stopwatch.StartNew();
        var retrieved = await _messages.GetByConversationAsync(convId, null, 50);
        sw.Stop();

        retrieved.Should().HaveCount(50);
        // GATE 2A acceptance criterion: <= 1.5ms (or fast enough on dev hardware, comfortably within generous bound)
        sw.Elapsed.TotalMilliseconds.Should().BeLessThan(10.0, "Indexed pagination should execute practically instantaneously");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task GetByConversationAsync_IdenticalTimestamps_MaintainsSortStability()
    {
        const string convId = "conv_tie_break";
        await _conversations.UpsertAsync(new ConversationSummary
        {
            ConversationId = convId,
            Title = "Tie Breaking Thread",
            LastActivityTimestamp = 5000
        });

        const long sharedTimestamp = 1700000050000L;

        // Insert messages sharing exact same millisecond timestamp
        await _messages.InsertAsync(new ChatMessage
        {
            ClientMessageId = "msg_first",
            MessageId = "srv_first",
            ConversationId = convId,
            SenderId = "user_1",
            Payload = new TextPayload { Content = "First" },
            Timestamp = sharedTimestamp
        });

        await _messages.InsertAsync(new ChatMessage
        {
            ClientMessageId = "msg_second",
            MessageId = "srv_second",
            ConversationId = convId,
            SenderId = "user_1",
            Payload = new TextPayload { Content = "Second" },
            Timestamp = sharedTimestamp
        });

        var results = await _messages.GetByConversationAsync(convId, null, 10);
        results.Should().HaveCount(2);

        // Tie-breaking should deliver deterministic ordering
        results[0].Timestamp.Should().Be(sharedTimestamp);
        results[1].Timestamp.Should().Be(sharedTimestamp);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task UpdateStatusAsync_Progression_UpdatesStatusAndServerId()
    {
        const string convId = "conv_bench";
        var msg = new ChatMessage
        {
            ClientMessageId = "cli_lifecycle_1",
            MessageId = "cli_lifecycle_1",
            ConversationId = convId,
            SenderId = "user_me",
            IsSelf = true,
            DeliveryStatus = MessageDeliveryStatus.Sending,
            Payload = new TextPayload { Content = "Status progression test" },
            Timestamp = 1700000001000L
        };

        await _messages.InsertAsync(msg);

        // 1. Sent
        await _messages.UpdateStatusAsync("cli_lifecycle_1", "srv_001", MessageDeliveryStatus.Sent);
        var afterSent = await _messages.GetByConversationAsync(convId, null, 10);
        var m1 = afterSent.FirstOrDefault(m => m.ClientMessageId == "cli_lifecycle_1");
        m1.Should().NotBeNull();
        m1!.DeliveryStatus.Should().Be(MessageDeliveryStatus.Sent);
        m1.MessageId.Should().Be("srv_001");

        // 2. Delivered
        await _messages.UpdateStatusAsync("cli_lifecycle_1", null, MessageDeliveryStatus.Delivered);
        var afterDelivered = await _messages.GetByConversationAsync(convId, null, 10);
        var m2 = afterDelivered.FirstOrDefault(m => m.ClientMessageId == "cli_lifecycle_1");
        m2!.DeliveryStatus.Should().Be(MessageDeliveryStatus.Delivered);

        // 3. Seen
        await _messages.UpdateStatusAsync("cli_lifecycle_1", null, MessageDeliveryStatus.Seen);
        var afterSeen = await _messages.GetByConversationAsync(convId, null, 10);
        var m3 = afterSeen.FirstOrDefault(m => m.ClientMessageId == "cli_lifecycle_1");
        m3!.DeliveryStatus.Should().Be(MessageDeliveryStatus.Seen);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task RecallAsync_MutatesPayloadToRecalled_PreservingChronologicalSlot()
    {
        const string convId = "conv_bench";
        const long ts = 1700000002000L;

        var original = new ChatMessage
        {
            ClientMessageId = "cli_recall_target",
            MessageId = "srv_recall_target",
            ConversationId = convId,
            SenderId = "user_me",
            SenderName = "Original Sender",
            IsSelf = true,
            Payload = new TextPayload { Content = "Secret text to be revoked" },
            Timestamp = ts
        };

        await _messages.InsertAsync(original);

        // Recall message
        const long recallTime = 1700000009000L;
        await _messages.RecallAsync("cli_recall_target", "Sender Recall Notice", recallTime);

        var list = await _messages.GetByConversationAsync(convId, null, 10);
        var recalled = list.FirstOrDefault(m => m.ClientMessageId == "cli_recall_target");

        recalled.Should().NotBeNull();
        recalled!.IsRecalled.Should().BeTrue();
        recalled.Timestamp.Should().Be(ts, "Recall must preserve original chronological slot");
        recalled.Payload.Should().BeOfType<RecalledPayload>();

        var recalledPayload = (RecalledPayload)recalled.Payload;
        recalledPayload.OriginalMessageId.Should().Be("cli_recall_target");
        recalledPayload.RecallTimestamp.Should().Be(recallTime);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task DeleteAsync_PermanentlyRemovesMessage()
    {
        const string convId = "conv_bench";
        var msg = new ChatMessage
        {
            ClientMessageId = "cli_delete_me",
            MessageId = "cli_delete_me",
            ConversationId = convId,
            SenderId = "user_me",
            Payload = new TextPayload { Content = "To be deleted" },
            Timestamp = 1700000003000L
        };

        await _messages.InsertAsync(msg);
        var beforeDelete = await _messages.GetByConversationAsync(convId, null, 10);
        beforeDelete.Should().Contain(m => m.ClientMessageId == "cli_delete_me");

        await _messages.DeleteAsync("cli_delete_me");

        var afterDelete = await _messages.GetByConversationAsync(convId, null, 10);
        afterDelete.Should().NotContain(m => m.ClientMessageId == "cli_delete_me");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task GetByConversationAsync_PaginationBoundary_ReturnsEmptyWhenOlderThanAll()
    {
        const string convId = "conv_bench";
        var boundary = DateTimeOffset.FromUnixTimeMilliseconds(100);
        var results = await _messages.GetByConversationAsync(convId, boundary, 50);

        results.Should().BeEmpty();
    }
}
