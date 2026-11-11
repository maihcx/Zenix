// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Specifies the user selection result from a confirmation dialog.
/// </summary>
public enum ConfirmationResult
{
    /// <summary>The primary action was selected (e.g. Confirm, Yes, Delete).</summary>
    Primary = 0,

    /// <summary>The secondary action was selected (e.g. No, Discard).</summary>
    Secondary = 1,

    /// <summary>The dialog was cancelled, dismissed, or timed out.</summary>
    Cancel = 2
}

/// <summary>
/// Specifies the severity level for non-modal InfoBar toast notifications.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Informational message.</summary>
    Informational = 0,

    /// <summary>Success notification.</summary>
    Success = 1,

    /// <summary>Warning notification.</summary>
    Warning = 2,

    /// <summary>Critical error notification.</summary>
    Error = 3
}

/// <summary>
/// Defines an abstracted dialog and notification service.
/// Decouples ViewModels from WinUI 3 visual elements (ContentDialog, XamlRoot, InfoBar)
/// to strictly satisfy Clean MVVM and enable automated UI testing.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays an asynchronous alert dialog with a single confirmation button.
    /// </summary>
    /// <param name="title">The localized dialog title.</param>
    /// <param name="message">The localized message body.</param>
    /// <param name="closeButtonText">The text for the close button, or null for default.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous dialog dismissal.</returns>
    Task ShowAlertAsync(
        string title, 
        string message, 
        string? closeButtonText = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays an asynchronous confirmation dialog with Primary and Cancel buttons.
    /// </summary>
    /// <param name="title">The localized dialog title.</param>
    /// <param name="message">The localized prompt body.</param>
    /// <param name="primaryButtonText">The action button text (e.g. "Delete", "Confirm").</param>
    /// <param name="cancelButtonText">The cancel button text.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task resolving to the user's confirmation choice.</returns>
    Task<ConfirmationResult> ShowConfirmationAsync(
        string title, 
        string message, 
        string primaryButtonText, 
        string cancelButtonText, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a non-blocking toast or banner notification via the application's root InfoBar host.
    /// </summary>
    /// <param name="title">The brief notification summary.</param>
    /// <param name="message">Detailed notification description.</param>
    /// <param name="severity">The notification severity level.</param>
    /// <param name="durationSeconds">Auto-dismiss duration in seconds (0 for persistent).</param>
    void ShowNotification(
        string title, 
        string message, 
        NotificationSeverity severity = NotificationSeverity.Informational, 
        int durationSeconds = 5);
}
