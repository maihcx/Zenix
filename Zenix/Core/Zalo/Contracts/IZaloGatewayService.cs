// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Encapsulates binary frame payload received from the realtime WebSocket connection.
/// </summary>
/// <param name="Version">The framing protocol version byte (typically 0x01).</param>
/// <param name="Command">The 16-bit little-endian command identifier.</param>
/// <param name="SubCommand">The 8-bit subcommand identifier.</param>
/// <param name="Payload">The binary or UTF-8 JSON message payload.</param>
public record ZaloIncomingFrameEventArgs(
    byte Version,
    ushort Command,
    byte SubCommand,
    ReadOnlyMemory<byte> Payload
);

/// <summary>
/// Encapsulates connection lifecycle transition details for the WebSocket gateway.
/// </summary>
/// <param name="IsConnected">Indicates whether the gateway channel is actively open.</param>
/// <param name="Reason">Optional human-readable description or error code explaining the state transition.</param>
public record ZaloConnectionStateChangedEventArgs(
    bool IsConnected,
    string? Reason = null
);

/// <summary>
/// Defines the low-level realtime duplex WebSocket gateway transport communicating with Zalo message servers.
/// Handles binary packet framing <c>[version: 1B][cmd: 2B LE][subCmd: 1B]</c>, heartbeats, and reconnect loops.
/// </summary>
public interface IZaloGatewayService : IAsyncDisposable
{
    /// <summary>
    /// Occurs when an incoming binary packet frame has been validated and decoded from the wire.
    /// </summary>
    event EventHandler<ZaloIncomingFrameEventArgs>? FrameReceived;

    /// <summary>
    /// Occurs when the transport connection state changes between connected and disconnected.
    /// </summary>
    event EventHandler<ZaloConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Gets a value indicating whether the underlying WebSocket channel is currently open and healthy.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Establishes the authenticated duplex WebSocket connection to the assigned gateway host.
    /// Injects Chromium wire headers and required session cookies.
    /// </summary>
    /// <param name="sessionCookie">The authenticated session cookie (<c>zpw_sek</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous connection handshake.</returns>
    Task ConnectAsync(string sessionCookie, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully closes the active WebSocket connection and halts heartbeat timers.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous disconnection operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes and transmits a binary frame through the WebSocket connection using standard 4-byte framing.
    /// </summary>
    /// <param name="version">The framing protocol version (typically 0x01).</param>
    /// <param name="command">The 16-bit command identifier.</param>
    /// <param name="subCommand">The 8-bit subcommand identifier.</param>
    /// <param name="payload">The body payload to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous transmission operation.</returns>
    Task SendBinaryFrameAsync(
        byte version, 
        ushort command, 
        byte subCommand, 
        ReadOnlyMemory<byte> payload, 
        CancellationToken cancellationToken = default);
}
