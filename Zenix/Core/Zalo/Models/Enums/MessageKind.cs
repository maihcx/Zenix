// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the type of content carried by a chat message on the wire.
/// </summary>
public enum MessageKind
{
    /// <summary>
    /// Plain-text message.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Photo or static graphic image.
    /// </summary>
    Photo = 2,

    /// <summary>
    /// Animated or static sticker.
    /// </summary>
    Sticker = 3,

    /// <summary>
    /// Animated GIF asset.
    /// </summary>
    Gif = 4,

    /// <summary>
    /// Video clip media.
    /// </summary>
    Video = 5,

    /// <summary>
    /// Voice memo or recorded audio clip.
    /// </summary>
    AudioVoice = 6,

    /// <summary>
    /// Generic binary or document file attachment.
    /// </summary>
    FileAttachment = 7,

    /// <summary>
    /// Rich web hyperlink preview card.
    /// </summary>
    LinkPreview = 8,

    /// <summary>
    /// Shared user contact business card.
    /// </summary>
    ContactCard = 9,

    /// <summary>
    /// Shared geographic coordinates and map pin.
    /// </summary>
    Location = 10,

    /// <summary>
    /// Voice or video call event log.
    /// </summary>
    CallLog = 11,

    /// <summary>
    /// Interactive greeting e-card.
    /// </summary>
    ECard = 12,

    /// <summary>
    /// Group voting poll.
    /// </summary>
    Poll = 14,

    /// <summary>
    /// Reverted local deletion event.
    /// </summary>
    UndoDelete = 15,

    /// <summary>
    /// Message revoked or recalled for all recipients.
    /// </summary>
    Recalled = 20,

    /// <summary>
    /// Group administrative system event.
    /// </summary>
    GroupSystemEvent = 21
}
