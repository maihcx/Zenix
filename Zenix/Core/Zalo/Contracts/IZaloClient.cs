// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines the top-level orchestrator and client coordinator for all Zalo operations in Zenix.
/// Aggregates authentication, real-time gateway events, messaging, contact directory, and local storage services.
/// </summary>
public interface IZaloClient : IAsyncDisposable
{
    /// <summary>
    /// Gets the authentication and session management service.
    /// </summary>
    IZaloAuthService Auth { get; }

    /// <summary>
    /// Gets the real-time binary WebSocket gateway transport service.
    /// </summary>
    IZaloGatewayService Gateway { get; }

    /// <summary>
    /// Gets the messaging service managing message send, receive, recall, and reactions.
    /// </summary>
    IZaloMessageService Messages { get; }

    /// <summary>
    /// Gets the media service managing uploads, downloads, thumbnails, and de-noising.
    /// </summary>
    IZaloMediaService Media { get; }

    /// <summary>
    /// Gets the contact directory and group management service.
    /// </summary>
    IZaloContactService Contacts { get; }

    /// <summary>
    /// Gets the cryptographic engine executing AES, HKDF, and Signal Protocol operations.
    /// </summary>
    IZaloCryptoService Crypto { get; }

    /// <summary>
    /// Gets the SQLite DAL 4.0 storage service managing local repositories.
    /// </summary>
    IZaloStorageService Storage { get; }

    /// <summary>
    /// Gets the currently authenticated user profile, or <c>null</c> if not logged in.
    /// </summary>
    UserProfile? CurrentUser { get; }

    /// <summary>
    /// Gets a value indicating whether the client currently holds an active, authenticated session.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Initializes the client, restoring any persisted session tokens and initializing local SQLite storage.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects network channels and flushes cached state without discarding saved login credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous disconnect operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
