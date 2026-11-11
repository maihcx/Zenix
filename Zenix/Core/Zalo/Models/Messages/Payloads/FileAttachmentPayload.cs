// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for an arbitrary attached document or file.
/// </summary>
public sealed record FileAttachmentPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.FileAttachment;

    /// <summary>
    /// Gets the original name of the attached file.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the download URL for the file.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the size in bytes of the attached file.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the file extension suffix (e.g. ".pdf", ".zip").
    /// </summary>
    public string? FileExtension { get; init; }

    /// <summary>
    /// Gets the cryptographic hash or checksum of the file.
    /// </summary>
    public string? Checksum { get; init; }
}
