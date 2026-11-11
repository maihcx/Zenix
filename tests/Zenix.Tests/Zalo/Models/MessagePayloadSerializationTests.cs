// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Messages.Payloads;

namespace Zenix.Tests.Zalo.Models;

/// <summary>
/// Unit tests verifying System.Text.Json polymorphic serialization for ChatMessage and all 16 payload types.
/// </summary>
public sealed class MessagePayloadSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for TextPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_TextPayload_PolymorphicRoundTrip()
    {
        var msg = CreateSampleMessage(
            new TextPayload { Content = "Xin chao Zenix WinUI 3!" },
            MessageKind.Text);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"text\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.Payload.Should().BeOfType<TextPayload>();
        ((TextPayload)deserialized.Payload).Content.Should().Be("Xin chao Zenix WinUI 3!");
        deserialized.Payload.Kind.Should().Be(MessageKind.Text);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for PhotoPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_PhotoPayload_PolymorphicRoundTrip()
    {
        var payload = new PhotoPayload
        {
            Url = "https://photos.zalo.me/pic1.jpg",
            ThumbnailUrl = "https://photos.zalo.me/thumb1.jpg",
            Width = 1920,
            Height = 1080,
            FileSizeBytes = 2048576,
            Caption = "Sunset photo",
            Checksum = "abcdef123456"
        };
        var msg = CreateSampleMessage(payload, MessageKind.Photo);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"photo\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.Payload.Should().BeOfType<PhotoPayload>();
        var photo = (PhotoPayload)deserialized.Payload;
        photo.Url.Should().Be("https://photos.zalo.me/pic1.jpg");
        photo.Width.Should().Be(1920);
        photo.Height.Should().Be(1080);
        photo.FileSizeBytes.Should().Be(2048576);
        photo.Caption.Should().Be("Sunset photo");
        photo.Kind.Should().Be(MessageKind.Photo);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for VideoPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_VideoPayload_PolymorphicRoundTrip()
    {
        var payload = new VideoPayload
        {
            Url = "https://video.zalo.me/sample.mp4",
            ThumbnailUrl = "https://video.zalo.me/thumb.jpg",
            DurationSeconds = 120,
            Width = 1280,
            Height = 720,
            FileSizeBytes = 15000000,
            Caption = "Project demo"
        };
        var msg = CreateSampleMessage(payload, MessageKind.Video);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"video\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var video = (VideoPayload)deserialized!.Payload;
        video.DurationSeconds.Should().Be(120);
        video.Width.Should().Be(1280);
        video.Caption.Should().Be("Project demo");
        video.Kind.Should().Be(MessageKind.Video);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for AudioVoicePayload with waveform.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_AudioVoicePayload_PolymorphicRoundTrip()
    {
        var payload = new AudioVoicePayload
        {
            Url = "https://voice.zalo.me/clip.aac",
            DurationSeconds = 15,
            FileSizeBytes = 64000,
            Waveform = new List<int> { 10, 25, 80, 45, 12 }
        };
        var msg = CreateSampleMessage(payload, MessageKind.AudioVoice);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"audio_voice\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var audio = (AudioVoicePayload)deserialized!.Payload;
        audio.DurationSeconds.Should().Be(15);
        audio.Waveform.Should().HaveCount(5);
        audio.Kind.Should().Be(MessageKind.AudioVoice);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for FileAttachmentPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_FileAttachmentPayload_PolymorphicRoundTrip()
    {
        var payload = new FileAttachmentPayload
        {
            FileName = "ArchitectureSpec.pdf",
            Url = "https://files.zalo.me/doc123.pdf",
            FileSizeBytes = 1048576,
            FileExtension = ".pdf",
            Checksum = "sha256_abcdef"
        };
        var msg = CreateSampleMessage(payload, MessageKind.FileAttachment);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"file_attachment\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var file = (FileAttachmentPayload)deserialized!.Payload;
        file.FileName.Should().Be("ArchitectureSpec.pdf");
        file.FileExtension.Should().Be(".pdf");
        file.Kind.Should().Be(MessageKind.FileAttachment);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for StickerPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_StickerPayload_PolymorphicRoundTrip()
    {
        var payload = new StickerPayload
        {
            StickerId = 5501,
            CategoryId = 102,
            Url = "https://stickers.zalo.me/5501.webp",
            SpriteUrl = "https://stickers.zalo.me/sprite_5501.png",
            Width = 256,
            Height = 256
        };
        var msg = CreateSampleMessage(payload, MessageKind.Sticker);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"sticker\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var sticker = (StickerPayload)deserialized!.Payload;
        sticker.StickerId.Should().Be(5501);
        sticker.CategoryId.Should().Be(102);
        sticker.Kind.Should().Be(MessageKind.Sticker);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for LocationPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_LocationPayload_PolymorphicRoundTrip()
    {
        var payload = new LocationPayload
        {
            Latitude = 10.7769,
            Longitude = 106.7009,
            Address = "Ben Thanh Market, District 1, Ho Chi Minh City",
            PlaceName = "Ben Thanh",
            ThumbnailUrl = "https://map.zalo.me/thumb.jpg"
        };
        var msg = CreateSampleMessage(payload, MessageKind.Location);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"location\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var loc = (LocationPayload)deserialized!.Payload;
        loc.Latitude.Should().BeApproximately(10.7769, 0.0001);
        loc.Longitude.Should().BeApproximately(106.7009, 0.0001);
        loc.PlaceName.Should().Be("Ben Thanh");
        loc.Kind.Should().Be(MessageKind.Location);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for ContactCardPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_ContactCardPayload_PolymorphicRoundTrip()
    {
        var payload = new ContactCardPayload
        {
            ContactUserId = "user_contact_99",
            ContactName = "Nguyen Van VIP",
            ContactPhoneNumber = "+84988776655",
            ContactAvatarUrl = "https://avatar.zalo.me/vip.png"
        };
        var msg = CreateSampleMessage(payload, MessageKind.ContactCard);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"contact_card\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var contact = (ContactCardPayload)deserialized!.Payload;
        contact.ContactUserId.Should().Be("user_contact_99");
        contact.ContactName.Should().Be("Nguyen Van VIP");
        contact.Kind.Should().Be(MessageKind.ContactCard);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for PollPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_PollPayload_PolymorphicRoundTrip()
    {
        var payload = new PollPayload
        {
            PollId = "poll_001",
            Question = "What framework for modern Windows desktop?",
            AllowMultipleChoices = false,
            AllowAddOptions = true,
            IsClosed = false,
            CreatorId = "user_admin",
            ExpirationTimestamp = 1750000000000,
            Options = new List<PollOption>
            {
                new() { OptionId = "opt_1", Content = "WinUI 3 (.NET 10)", VoteCount = 42, VoterUserIds = new[] { "u1", "u2" } },
                new() { OptionId = "opt_2", Content = "WPF", VoteCount = 5, VoterUserIds = new[] { "u3" } }
            }
        };
        var msg = CreateSampleMessage(payload, MessageKind.Poll);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"poll\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var poll = (PollPayload)deserialized!.Payload;
        poll.PollId.Should().Be("poll_001");
        poll.Options.Should().HaveCount(2);
        poll.Options[0].VoteCount.Should().Be(42);
        poll.Kind.Should().Be(MessageKind.Poll);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for UndoDeletePayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_UndoDeletePayload_PolymorphicRoundTrip()
    {
        var payload = new UndoDeletePayload
        {
            TargetMessageId = "msg_deleted_123",
            DeleteTimestamp = 1720000000000
        };
        var msg = CreateSampleMessage(payload, MessageKind.UndoDelete);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"undo_delete\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var undo = (UndoDeletePayload)deserialized!.Payload;
        undo.TargetMessageId.Should().Be("msg_deleted_123");
        undo.Kind.Should().Be(MessageKind.UndoDelete);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for RecalledPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_RecalledPayload_PolymorphicRoundTrip()
    {
        var payload = new RecalledPayload
        {
            OriginalMessageId = "msg_orig_456",
            RecallNotice = "Tin nhắn đã được thu hồi",
            RecallTimestamp = 1720000050000
        };
        var msg = CreateSampleMessage(payload, MessageKind.Recalled);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"recalled\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var recalled = (RecalledPayload)deserialized!.Payload;
        recalled.OriginalMessageId.Should().Be("msg_orig_456");
        recalled.RecallNotice.Should().Be("Tin nhắn đã được thu hồi");
        recalled.Kind.Should().Be(MessageKind.Recalled);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for GroupSystemEventPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_GroupSystemEventPayload_PolymorphicRoundTrip()
    {
        var payload = new GroupSystemEventPayload
        {
            EventCode = SystemEventCode.GroupRenamed,
            ActorUserId = "admin_1",
            ActorName = "Admin User",
            TargetUserId = null,
            TargetName = null,
            EventDescription = "Admin User renamed group to Zenix Elite",
            OldValue = "Old Group Name",
            NewValue = "Zenix Elite"
        };
        var msg = CreateSampleMessage(payload, MessageKind.GroupSystemEvent);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"group_system_event\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var sysEvent = (GroupSystemEventPayload)deserialized!.Payload;
        sysEvent.EventCode.Should().Be(SystemEventCode.GroupRenamed);
        sysEvent.NewValue.Should().Be("Zenix Elite");
        sysEvent.Kind.Should().Be(MessageKind.GroupSystemEvent);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for LinkPreviewPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_LinkPreviewPayload_PolymorphicRoundTrip()
    {
        var payload = new LinkPreviewPayload
        {
            Url = "https://github.com/microsoft/microsoft-ui-xaml",
            Title = "WinUI 3 - Fluent Design on Windows",
            Description = "Windows UI Library documentation and source repository.",
            ThumbnailUrl = "https://github.com/banner.png",
            Domain = "github.com"
        };
        var msg = CreateSampleMessage(payload, MessageKind.LinkPreview);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"link_preview\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var link = (LinkPreviewPayload)deserialized!.Payload;
        link.Domain.Should().Be("github.com");
        link.Title.Should().Be("WinUI 3 - Fluent Design on Windows");
        link.Kind.Should().Be(MessageKind.LinkPreview);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for CallLogPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_CallLogPayload_PolymorphicRoundTrip()
    {
        var payload = new CallLogPayload
        {
            CallId = "call_session_789",
            CallDurationSeconds = 480,
            IsVideo = true,
            IsMissed = false,
            CallerId = "caller_007"
        };
        var msg = CreateSampleMessage(payload, MessageKind.CallLog);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"call_log\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var call = (CallLogPayload)deserialized!.Payload;
        call.CallDurationSeconds.Should().Be(480);
        call.IsVideo.Should().BeTrue();
        call.IsMissed.Should().BeFalse();
        call.Kind.Should().Be(MessageKind.CallLog);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for ECardPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_ECardPayload_PolymorphicRoundTrip()
    {
        var payload = new ECardPayload
        {
            TemplateId = "tet_greeting_2026",
            Title = "Happy New Year!",
            Message = "Wishing you prosperity and happiness.",
            ImageUrl = "https://ecard.zalo.me/tet2026.png",
            Buttons = new List<CardButton>
            {
                new() { ButtonId = "btn_1", Label = "Send Wishes", ActionUrl = "https://zalo.me/send", ActionType = "open_url" }
            }
        };
        var msg = CreateSampleMessage(payload, MessageKind.ECard);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"ecard\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var ecard = (ECardPayload)deserialized!.Payload;
        ecard.TemplateId.Should().Be("tet_greeting_2026");
        ecard.Buttons.Should().HaveCount(1);
        ecard.Buttons![0].Label.Should().Be("Send Wishes");
        ecard.Kind.Should().Be(MessageKind.ECard);
    }

    /// <summary>
    /// Verifies polymorphic serialization and deserialization for UnsupportedPayload.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_UnsupportedPayload_PolymorphicRoundTrip()
    {
        var payload = new UnsupportedPayload
        {
            RawKind = 9999,
            RawJson = "{\"unknown_field\":\"unsupported_data\"}"
        };
        var msg = CreateSampleMessage(payload, MessageKind.Text);

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        json.Should().Contain("\"$type\":\"unsupported\"");

        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        var unsupported = (UnsupportedPayload)deserialized!.Payload;
        unsupported.RawKind.Should().Be(9999);
        unsupported.RawJson.Should().Contain("unknown_field");
    }

    /// <summary>
    /// Verifies ChatMessage full structure with quotes, mentions, and reactions.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatMessage_FullMetadata_SerializesCompletely()
    {
        var msg = new ChatMessage
        {
            MessageId = "msg_full_1",
            ClientMessageId = "client_uuid_1",
            ConversationId = "conv_100",
            ConversationType = ChatConversationType.Group,
            SenderId = "sender_1",
            SenderName = "Sender Person",
            SenderAvatarUrl = "https://avatar.zalo.me/s.png",
            IsSelf = false,
            Kind = MessageKind.Text,
            Payload = new TextPayload { Content = "Hello @User2 please review this reply!" },
            DeliveryStatus = MessageDeliveryStatus.SeenAll,
            Timestamp = 1715000000000,
            QuoteMessageId = "msg_prev_0",
            QuotedMessage = new QuotedMessageSummary
            {
                MessageId = "msg_prev_0",
                SenderId = "user_2",
                SenderName = "User 2",
                Snippet = "What do you think?",
                Kind = MessageKind.Text
            },
            Mentions = new List<MentionSpan>
            {
                new() { UserId = "user_2", Offset = 6, Length = 6, DisplayText = "@User2" }
            },
            Reactions = new List<MessageReaction>
            {
                new() { UserId = "user_3", Icon = "👍", Timestamp = 1715000010000 },
                new() { UserId = "user_4", Icon = "❤️", Timestamp = 1715000012000 }
            },
            IsEdited = true,
            IsRecalled = false,
            IsPinned = true
        };

        var json = JsonSerializer.Serialize(msg, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.MessageId.Should().Be("msg_full_1");
        deserialized.QuotedMessage.Should().NotBeNull();
        deserialized.QuotedMessage!.Snippet.Should().Be("What do you think?");
        deserialized.Mentions.Should().HaveCount(1);
        deserialized.Mentions[0].DisplayText.Should().Be("@User2");
        deserialized.Reactions.Should().HaveCount(2);
        deserialized.IsEdited.Should().BeTrue();
        deserialized.IsPinned.Should().BeTrue();
    }

    private static ChatMessage CreateSampleMessage(ChatMessagePayload payload, MessageKind kind)
    {
        return new ChatMessage
        {
            MessageId = "test_msg_id",
            ClientMessageId = "test_client_id",
            ConversationId = "test_conv_id",
            ConversationType = ChatConversationType.Direct,
            SenderId = "test_sender",
            IsSelf = true,
            Kind = kind,
            Payload = payload,
            DeliveryStatus = MessageDeliveryStatus.Sent,
            Timestamp = 1700000000000
        };
    }
}
