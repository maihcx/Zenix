// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConversationListViewModel.cs" company="Zenix">
//   Copyright (c) Zenix. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Zenix.Core.Zalo.Models;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines the UI-agnostic ViewModel contract for the conversation master list (sidebar).
/// Strictly decoupled from UI visual elements (zero references to UIElement, FrameworkElement, or Window).
/// All properties and commands are bound via WinUI 3 compiled bindings ({x:Bind}).
/// </summary>
public interface IConversationListViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the observable collection of conversation summaries displayed in the sidebar list.
    /// </summary>
    ObservableCollection<ConversationSummary> Conversations { get; }

    /// <summary>
    /// Gets or sets the currently selected conversation summary.
    /// </summary>
    ConversationSummary? SelectedConversation { get; set; }

    /// <summary>
    /// Gets or sets the text filter query applied to the conversation list.
    /// </summary>
    string SearchQuery { get; set; }

    /// <summary>
    /// Gets a value indicating whether conversation data is actively being fetched or synchronized.
    /// </summary>
    bool IsRefreshing { get; }

    /// <summary>
    /// Gets the total unread badge count aggregated across all active conversations.
    /// </summary>
    int TotalUnreadCount { get; }

    /// <summary>
    /// Gets the command that refreshes the conversation list from the server and local storage.
    /// </summary>
    ICommand RefreshCommand { get; }

    /// <summary>
    /// Gets the command that pins or unpins a conversation to the top of the list.
    /// </summary>
    ICommand TogglePinCommand { get; }

    /// <summary>
    /// Gets the command that toggles the mute state of notifications for a conversation.
    /// </summary>
    ICommand ToggleMuteCommand { get; }

    /// <summary>
    /// Gets the command that marks a conversation as read or unread.
    /// </summary>
    ICommand MarkAsReadCommand { get; }

    /// <summary>
    /// Gets the command that archives or unarchives a conversation thread.
    /// </summary>
    ICommand ToggleArchiveCommand { get; }

    /// <summary>
    /// Gets the command that deletes a conversation thread and purges cached messages.
    /// </summary>
    ICommand DeleteConversationCommand { get; }
}
