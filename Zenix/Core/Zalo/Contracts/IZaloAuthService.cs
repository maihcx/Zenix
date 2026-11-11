// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Represents the step state during the desktop QR code login lifecycle.
/// </summary>
public enum QrLoginStep
{
    /// <summary>Authentication has not been initiated.</summary>
    NotStarted = 0,

    /// <summary>Primary QR code is displayed awaiting user scan on mobile.</summary>
    WaitingScan = 1,

    /// <summary>User has scanned QR code and must confirm on phone.</summary>
    WaitingConfirm = 2,

    /// <summary>Second QR code scan is required for device linking confirmation (error code -20).</summary>
    SecondQrRequired = 3,

    /// <summary>QR code lifetime expired without scan.</summary>
    Expired = 4,

    /// <summary>Authentication succeeded and session token is established.</summary>
    Success = 5,

    /// <summary>Authentication failed due to rejection, kickout, or network fault.</summary>
    Failed = 6
}

/// <summary>
/// Encapsulates the active QR code state for UI presentation.
/// </summary>
/// <param name="Token">The unique QR transaction token returned by the auth server.</param>
/// <param name="QrDataUrl">The base64-encoded SVG or PNG image data URL to render.</param>
/// <param name="IsSecondQr">Indicates whether this is the secondary device-linking QR code.</param>
/// <param name="ExpiresAt">The timestamp when the current QR code token expires.</param>
public record QrCodeState(
    string Token,
    string QrDataUrl,
    bool IsSecondQr,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// Encapsulates scanned user preview metadata during the confirmation phase.
/// </summary>
/// <param name="DisplayName">The user display name returned by the mobile scanner.</param>
/// <param name="AvatarUrl">The user avatar image URL returned by the mobile scanner.</param>
public record QrConfirmedUser(
    string DisplayName,
    string? AvatarUrl
);

/// <summary>
/// Encapsulates the result of each polling tick in the QR login state machine.
/// </summary>
/// <param name="Step">The current QR login progression step.</param>
/// <param name="ConfirmedUser">Scanned user metadata if awaiting phone confirmation.</param>
/// <param name="SessionCookie">The authenticated session cookie string on successful completion.</param>
/// <param name="ErrorMessage">The error description if the operation failed.</param>
public record QrLoginStepResult(
    QrLoginStep Step,
    QrConfirmedUser? ConfirmedUser = null,
    string? SessionCookie = null,
    string? ErrorMessage = null
);

/// <summary>
/// Defines the authentication service managing QR generation, long-polling, device linking, and session restoration.
/// </summary>
public interface IZaloAuthService
{
    /// <summary>
    /// Occurs when the server terminates the active session because another desktop client logged into the account.
    /// </summary>
    event EventHandler<string>? SessionKickedOut;

    /// <summary>
    /// Gets or creates the persistent unique client IMEI (<c>z_uuid</c>) adhering to the format <c>&lt;uuid&gt;-&lt;md5(UA)&gt;</c>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted or newly generated client IMEI string.</returns>
    ValueTask<string> GetOrCreateClientImeiAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a new QR code token and rendered SVG/PNG image data from the auth endpoint.
    /// </summary>
    /// <param name="isSecondQr">Specifies whether this request initiates a secondary device-linking QR code.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that resolves to the new QR code state.</returns>
    Task<QrCodeState> RequestQrCodeAsync(bool isSecondQr = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a continuous asynchronous stream of status ticks polling the QR scan confirmation endpoint.
    /// </summary>
    /// <param name="token">The QR code token returned by <see cref="RequestQrCodeAsync"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream emitting each step transition until completion or failure.</returns>
    IAsyncEnumerable<QrLoginStepResult> PollQrScanStateAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to restore an existing authenticated session from encrypted local storage without showing QR prompt.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that resolves to <c>true</c> if a valid session was restored; otherwise, <c>false</c>.</returns>
    Task<bool> TryRestoreSavedSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the current authenticated session, clears stored tokens, and returns the client to an unauthenticated state.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous logout operation.</returns>
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
