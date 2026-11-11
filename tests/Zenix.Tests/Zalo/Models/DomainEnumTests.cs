// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Tests.Zalo.Models;

/// <summary>
/// Unit tests verifying domain enum numeric values and bitmask operations.
/// </summary>
public sealed class DomainEnumTests
{
    /// <summary>
    /// Verifies MessageDeliveryStatus integer mappings match data model and protocol specification.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void MessageDeliveryStatus_ValuesMatchSpecification()
    {
        ((int)MessageDeliveryStatus.Failed).Should().Be(-2);
        ((int)MessageDeliveryStatus.Draft).Should().Be(0);
        ((int)MessageDeliveryStatus.Sent).Should().Be(1);
        ((int)MessageDeliveryStatus.Delivered).Should().Be(2);
        ((int)MessageDeliveryStatus.Seen).Should().Be(3);
        ((int)MessageDeliveryStatus.SeenAll).Should().Be(4);
        ((int)MessageDeliveryStatus.SeenFull).Should().Be(4);
        ((int)MessageDeliveryStatus.Sending).Should().Be(10);
    }

    /// <summary>
    /// Verifies MessageKind integer mappings match wire payload protocol codes.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void MessageKind_ValuesMatchSpecification()
    {
        ((int)MessageKind.Text).Should().Be(1);
        ((int)MessageKind.Photo).Should().Be(2);
        ((int)MessageKind.Sticker).Should().Be(3);
        ((int)MessageKind.Gif).Should().Be(4);
        ((int)MessageKind.Video).Should().Be(5);
        ((int)MessageKind.AudioVoice).Should().Be(6);
        ((int)MessageKind.FileAttachment).Should().Be(7);
        ((int)MessageKind.LinkPreview).Should().Be(8);
        ((int)MessageKind.ContactCard).Should().Be(9);
        ((int)MessageKind.Location).Should().Be(10);
        ((int)MessageKind.CallLog).Should().Be(11);
        ((int)MessageKind.ECard).Should().Be(12);
        ((int)MessageKind.Poll).Should().Be(14);
        ((int)MessageKind.UndoDelete).Should().Be(15);
        ((int)MessageKind.Recalled).Should().Be(20);
        ((int)MessageKind.GroupSystemEvent).Should().Be(21);
    }

    /// <summary>
    /// Verifies ChatConversationType values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ChatConversationType_ValuesMatchSpecification()
    {
        ((int)ChatConversationType.Direct).Should().Be(0);
        ((int)ChatConversationType.Group).Should().Be(1);
    }

    /// <summary>
    /// Verifies GroupMemberRole values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void GroupMemberRole_ValuesMatchSpecification()
    {
        ((int)GroupMemberRole.Member).Should().Be(0);
        ((int)GroupMemberRole.Admin).Should().Be(1);
        ((int)GroupMemberRole.Creator).Should().Be(2);
    }

    /// <summary>
    /// Verifies ZaloGender values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ZaloGender_ValuesMatchSpecification()
    {
        ((int)ZaloGender.Unknown).Should().Be(0);
        ((int)ZaloGender.Male).Should().Be(1);
        ((int)ZaloGender.Female).Should().Be(2);
    }

    /// <summary>
    /// Verifies QrScanState values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void QrScanState_ValuesMatchSpecification()
    {
        ((int)QrScanState.ErrorNegative20NeedsQr2).Should().Be(-20);
        ((int)QrScanState.Initial).Should().Be(0);
        ((int)QrScanState.ScannedWaitingConfirm).Should().Be(1);
        ((int)QrScanState.Approved).Should().Be(2);
        ((int)QrScanState.Expired).Should().Be(3);
    }

    /// <summary>
    /// Verifies NetworkConnectionState values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void NetworkConnectionState_ValuesMatchSpecification()
    {
        ((int)NetworkConnectionState.Disconnected).Should().Be(0);
        ((int)NetworkConnectionState.Connecting).Should().Be(1);
        ((int)NetworkConnectionState.Connected).Should().Be(2);
        ((int)NetworkConnectionState.Reconnecting).Should().Be(3);
    }

    /// <summary>
    /// Verifies NoticeType and SystemEventCode values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void NoticeAndSystemEvent_ValuesMatchSpecification()
    {
        ((int)NoticeType.General).Should().Be(0);
        ((int)NoticeType.MemberJoined).Should().Be(1);
        ((int)NoticeType.MemberLeft).Should().Be(2);
        ((int)NoticeType.PinUpdated).Should().Be(3);
        ((int)NoticeType.SecurityChanged).Should().Be(4);

        ((int)SystemEventCode.Unknown).Should().Be(0);
        ((int)SystemEventCode.GroupRenamed).Should().Be(101);
        ((int)SystemEventCode.GroupAvatarChanged).Should().Be(102);
        ((int)SystemEventCode.E2EESafetyNumberChanged).Should().Be(201);
        ((int)SystemEventCode.CallMissed).Should().Be(301);
    }

    /// <summary>
    /// Verifies ConversationActionFlags bitwise logic.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ConversationActionFlags_BitwiseOperations()
    {
        var flags = ConversationActionFlags.Pinned | ConversationActionFlags.UnreadMark;
        flags.HasFlag(ConversationActionFlags.Pinned).Should().BeTrue();
        flags.HasFlag(ConversationActionFlags.UnreadMark).Should().BeTrue();
        flags.HasFlag(ConversationActionFlags.Archived).Should().BeFalse();
    }
}
