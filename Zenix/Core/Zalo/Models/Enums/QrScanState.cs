// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the polling status of a QR login flow.
/// </summary>
public enum QrScanState
{
    /// <summary>
    /// QR code requires migration to QR v2 protocol (-20).
    /// </summary>
    ErrorNegative20NeedsQr2 = -20,

    /// <summary>
    /// Initial awaiting-scan state.
    /// </summary>
    Initial = 0,

    /// <summary>
    /// Scanned by mobile device and awaiting user confirmation.
    /// </summary>
    ScannedWaitingConfirm = 1,

    /// <summary>
    /// Login approved on mobile device.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// QR code expired without approval.
    /// </summary>
    Expired = 3
}
