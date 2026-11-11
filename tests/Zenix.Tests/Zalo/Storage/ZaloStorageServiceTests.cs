// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Messages.Payloads;
using Zenix.Core.Zalo.Models.Profiles;
using Zenix.Core.Zalo.Storage;
using Zenix.Core.Zalo.Storage.Repositories;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="ZaloStorageService"/> facade verifying lifecycle coordination,
/// uninitialized guards, repository synchronization, <see cref="FailedDraftStore"/> logic, and account data wipe.
/// </summary>
public sealed class ZaloStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public ZaloStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"zenix_facade_test_{Guid.NewGuid():N}");
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
    public void Repositories_AccessedBeforeInitialization_ThrowsInvalidOperationException()
    {
        using var service = new ZaloStorageService(forceInMemory: true);

        var actConv = () => _ = service.Conversations;
        actConv.Should().Throw<InvalidOperationException>().WithMessage("*not been initialized*");

        var actMsg = () => _ = service.Messages;
        actMsg.Should().Throw<InvalidOperationException>().WithMessage("*not been initialized*");

        var actContact = () => _ = service.Contacts;
        actContact.Should().Throw<InvalidOperationException>().WithMessage("*not been initialized*");

        var actGroup = () => _ = service.Groups;
        actGroup.Should().Throw<InvalidOperationException>().WithMessage("*not been initialized*");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task InitializeAccountDatabaseAsync_EnablesAllRepositoriesAndE2ESync()
    {
        await using var service = new ZaloStorageService(forceInMemory: false, _tempDirectory);
        await service.InitializeAccountDatabaseAsync("acc_main_user");

        service.ActiveAccountId.Should().Be("acc_main_user");

        // 1. Insert conversation
        await service.Conversations.UpsertAsync(new ConversationSummary
        {
            ConversationId = "conv_e2e_1",
            Title = "E2E Test Thread",
            LastActivityTimestamp = 1700000000000L,
            UnreadCount = 0
        });

        // 2. Insert message (trigger will update conversation's last_activity_at and unread_count)
        await service.Messages.InsertAsync(new ChatMessage
        {
            ClientMessageId = "cli_e2e_1",
            MessageId = "srv_e2e_1",
            ConversationId = "conv_e2e_1",
            SenderId = "friend_1",
            SenderName = "Bob",
            IsSelf = false,
            Payload = new TextPayload { Content = "Hello from Bob" },
            Timestamp = 1700000010000L
        });

        // 3. Verify conversation state updated by trigger
        var conv = await service.Conversations.GetByIdAsync("conv_e2e_1");
        conv.Should().NotBeNull();
        conv!.LastActivityTimestamp.Should().Be(1700000010000L);
        conv.UnreadCount.Should().Be(1);

        // 4. Update unread count manually to 0
        await service.Conversations.UpdateUnreadCountAsync("conv_e2e_1", 0);
        var convRead = await service.Conversations.GetByIdAsync("conv_e2e_1");
        convRead!.UnreadCount.Should().Be(0);

        // 5. Query message list
        var messages = await service.Messages.GetByConversationAsync("conv_e2e_1");
        messages.Should().HaveCount(1);
        messages[0].ClientMessageId.Should().Be("cli_e2e_1");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void FailedDraftStore_AppendingShiftsMentionsAndMergesFailures()
    {
        var draftA = new ComposerDraft
        {
            Text = "hi @An",
            Mentions = new[] { new MentionSpan { UserId = "1", Offset = 3, Length = 3, DisplayText = "@An" } }
        };

        var draftB = new ComposerDraft
        {
            Text = "hello @Bo",
            Mentions = new[] { new MentionSpan { UserId = "2", Offset = 6, Length = 3, DisplayText = "@Bo" } }
        };

        var merged = draftA.Appending(draftB);
        merged.Text.Should().Be("hi @An\nhello @Bo");

        // Shift is draftA.Text.Length + 1 = 6 + 1 = 7
        merged.Mentions.Should().HaveCount(2);
        merged.Mentions[0].Offset.Should().Be(3);
        merged.Mentions[1].Offset.Should().Be(6 + 7);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void FailedDraftStore_AppendingToEmpty_TakesOther()
    {
        var draft = new ComposerDraft
        {
            Text = "NonEmpty",
            Mentions = new[] { new MentionSpan { UserId = "x", Offset = 0, Length = 8, DisplayText = "NonEmpty" } }
        };

        var empty = new ComposerDraft { Text = string.Empty };

        empty.Appending(draft).Should().BeEquivalentTo(draft);
        draft.Appending(empty).Should().BeEquivalentTo(draft);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void FailedDraftStore_KeepsPerConversationAndTracksNotice()
    {
        var store = new FailedDraftStore();

        store.Stash(new ComposerDraft { Text = "one" }, "c1", isSilent: true);
        store.Stash(new ComposerDraft { Text = "two" }, "c1", isSilent: false);
        store.Stash(new ComposerDraft { Text = "other" }, "c2", isSilent: true);
        store.Stash(new ComposerDraft { Text = "" }, "c3");

        store.Revision.Should().Be(3);
        store.Has("c1").Should().BeTrue();
        store.Has("c2").Should().BeTrue();
        store.Has("c3").Should().BeFalse();

        // c1 had a silent stash merged with a non-silent one: notice must be true
        var c1Result = store.TakeWithNotice("c1");
        c1Result.Should().NotBeNull();
        c1Result!.Value.Draft.Text.Should().Be("one\ntwo");
        c1Result.Value.Notice.Should().BeTrue();

        // After taking, c1 is gone
        store.Take("c1").Should().BeNull();

        // c2 was stashed silent: notice must be false
        var c2Result = store.TakeWithNotice("c2");
        c2Result.Should().NotBeNull();
        c2Result!.Value.Draft.Text.Should().Be("other");
        c2Result.Value.Notice.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void FailedDraftStore_Drain_EmptiesTheStore()
    {
        var store = new FailedDraftStore();
        store.Stash(new ComposerDraft { Text = "msg A" }, "c1");
        store.Stash(new ComposerDraft { Text = "msg B" }, "c2");

        var drained = store.Drain();
        drained.Should().HaveCount(2);

        store.Has("c1").Should().BeFalse();
        store.Has("c2").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task WipeAccountDataAsync_DeletesDatabaseFilesAndSidecars()
    {
        await using var service = new ZaloStorageService(forceInMemory: false, _tempDirectory);
        await service.InitializeAccountDatabaseAsync("wipe_account_test");

        // Save some data
        await service.Conversations.UpsertAsync(new ConversationSummary
        {
            ConversationId = "conv_wipe",
            Title = "Wipe Target",
            LastActivityTimestamp = 1000
        });

        // Save secret
        service.Credentials.SaveSecret("wipe_account_test", "token", "super_secret");

        var accountDbDir = Path.Combine(_tempDirectory, "wipe_account_test");
        var dbFile = Path.Combine(accountDbDir, "chat_storage.db");
        File.Exists(dbFile).Should().BeTrue();

        // Wipe account
        await service.WipeAccountDataAsync("wipe_account_test");

        File.Exists(dbFile).Should().BeFalse();
        service.Credentials.GetSecret("wipe_account_test", "token").Should().BeNull();
    }
}
