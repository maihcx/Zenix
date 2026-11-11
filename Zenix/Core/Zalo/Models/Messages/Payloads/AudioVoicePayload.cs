// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a recorded voice memo or audio clip.
/// </summary>
public sealed record AudioVoicePayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.AudioVoice;

    /// <summary>
    /// Gets the audio file download URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the duration of the audio clip in seconds.
    /// </summary>
    public int DurationSeconds { get; init; }

    /// <summary>
    /// Gets the file size in bytes of the audio recording.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the optional waveform amplitude sample points for UI rendering.
    /// </summary>
    public IReadOnlyList<int>? Waveform { get; init; }
}
