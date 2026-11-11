// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines an abstracted, platform-independent file and folder picker service.
/// Decouples ViewModels from WinRT / Win32 picker APIs (FileOpenPicker, FileSavePicker, folder pickers)
/// to strictly uphold Clean MVVM and enable automated unit testing.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Displays a single-file open dialog filtered by the specified file extensions.
    /// </summary>
    /// <param name="title">The localized dialog title displayed to the user.</param>
    /// <param name="filter">A collection of allowed file extensions (e.g. [".png", ".jpg", ".pdf"]).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The absolute path of the chosen file, or <c>null</c> if the user canceled.</returns>
    Task<string?> PickSingleFileAsync(
        string title, 
        IReadOnlyList<string> filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a multiple-file open dialog filtered by the specified file extensions.
    /// </summary>
    /// <param name="title">The localized dialog title displayed to the user.</param>
    /// <param name="filter">A collection of allowed file extensions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of absolute paths of selected files, or an empty list if canceled.</returns>
    Task<IReadOnlyList<string>> PickMultipleFilesAsync(
        string title, 
        IReadOnlyList<string> filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a save file dialog to select a destination path for an exported or downloaded file.
    /// </summary>
    /// <param name="title">The localized dialog title displayed to the user.</param>
    /// <param name="defaultFileName">The proposed default file name.</param>
    /// <param name="defaultExtension">The default file extension (e.g. ".pdf").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The chosen destination file path, or <c>null</c> if the user canceled.</returns>
    Task<string?> PickSaveFileAsync(
        string title, 
        string defaultFileName, 
        string defaultExtension, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a folder selection dialog to select a target download directory.
    /// </summary>
    /// <param name="title">The localized dialog title displayed to the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The chosen directory path, or <c>null</c> if the user canceled.</returns>
    Task<string?> PickFolderAsync(
        string title, 
        CancellationToken cancellationToken = default);
}
