// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IZaloMediaService.cs" company="Zenix">
//   Copyright (c) Zenix. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Encapsulates transfer progress metrics during media upload or download.
/// </summary>
/// <param name="BytesTransferred">The total number of bytes transferred so far.</param>
/// <param name="TotalBytes">The total size of the file in bytes, or null if unknown.</param>
/// <param name="ProgressPercentage">The completion percentage between 0.0 and 100.0.</param>
public record MediaTransferProgress(
    long BytesTransferred,
    long? TotalBytes,
    double ProgressPercentage
);

/// <summary>
/// Defines media operations including file upload/download, video thumbnail extraction,
/// viewport LIFO download throttling, and media de-noising.
/// </summary>
public interface IZaloMediaService
{
    /// <summary>
    /// Uploads an image or photo attachment, returning the resulting hosted URLs and dimensions.
    /// Emulates Chromium WebKitFormBoundary multipart framing.
    /// </summary>
    /// <param name="filePath">The local path to the image file.</param>
    /// <param name="progress">An optional progress callback.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A photo payload containing the remote image URLs.</returns>
    Task<MessagePayload.PhotoPayload> UploadPhotoAsync(
        string filePath, 
        IProgress<MediaTransferProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a generic file or document attachment with progress reporting.
    /// </summary>
    /// <param name="filePath">The local path to the file.</param>
    /// <param name="progress">An optional progress callback.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A file payload containing metadata and the remote download URL.</returns>
    Task<MessagePayload.FilePayload> UploadFileAsync(
        string filePath, 
        IProgress<MediaTransferProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a remote media file from Zalo CDN, applies automatic media de-noising if protected,
    /// and saves the output to a specified destination directory.
    /// </summary>
    /// <param name="downloadUrl">The remote CDN download URL.</param>
    /// <param name="destinationPath">The local filesystem path to write the downloaded file.</param>
    /// <param name="progress">An optional progress callback.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    Task DownloadMediaAsync(
        string downloadUrl, 
        string destinationPath, 
        IProgress<MediaTransferProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the poster thumbnail from a local or streamed video file using ISO-BMFF parser
    /// without invoking full video decoders.
    /// </summary>
    /// <param name="videoStream">A seekable stream pointing to MP4/QuickTime video data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A byte array containing the extracted JPEG/PNG poster thumbnail image.</returns>
    Task<byte[]> ExtractVideoPosterAsync(
        Stream videoStream, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues an image thumbnail download using a viewport-aware LIFO scheduler, prioritizing
    /// items currently visible in the active chat viewport over off-screen items.
    /// </summary>
    /// <param name="thumbnailUrl">The remote thumbnail URL to fetch.</param>
    /// <param name="isHighPriority">Indicates whether the item is immediately visible in the active viewport.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A stream containing the cached or freshly downloaded thumbnail image.</returns>
    Task<Stream> GetThumbnailStreamAsync(
        string thumbnailUrl, 
        bool isHighPriority = true, 
        CancellationToken cancellationToken = default);
}
