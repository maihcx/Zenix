// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Text.Json.Serialization;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages.Payloads;

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Abstract base record for all strongly-typed message payloads supporting polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextPayload), typeDiscriminator: "text")]
[JsonDerivedType(typeof(PhotoPayload), typeDiscriminator: "photo")]
[JsonDerivedType(typeof(VideoPayload), typeDiscriminator: "video")]
[JsonDerivedType(typeof(AudioVoicePayload), typeDiscriminator: "audio_voice")]
[JsonDerivedType(typeof(FileAttachmentPayload), typeDiscriminator: "file_attachment")]
[JsonDerivedType(typeof(StickerPayload), typeDiscriminator: "sticker")]
[JsonDerivedType(typeof(LocationPayload), typeDiscriminator: "location")]
[JsonDerivedType(typeof(ContactCardPayload), typeDiscriminator: "contact_card")]
[JsonDerivedType(typeof(PollPayload), typeDiscriminator: "poll")]
[JsonDerivedType(typeof(UndoDeletePayload), typeDiscriminator: "undo_delete")]
[JsonDerivedType(typeof(RecalledPayload), typeDiscriminator: "recalled")]
[JsonDerivedType(typeof(GroupSystemEventPayload), typeDiscriminator: "group_system_event")]
[JsonDerivedType(typeof(LinkPreviewPayload), typeDiscriminator: "link_preview")]
[JsonDerivedType(typeof(CallLogPayload), typeDiscriminator: "call_log")]
[JsonDerivedType(typeof(ECardPayload), typeDiscriminator: "ecard")]
[JsonDerivedType(typeof(UnsupportedPayload), typeDiscriminator: "unsupported")]
public abstract record ChatMessagePayload
{
    /// <summary>
    /// Gets the message kind corresponding to this payload instance.
    /// </summary>
    public abstract MessageKind Kind { get; }
}
