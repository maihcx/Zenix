// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Text.Json.Serialization;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents a generic standardized response envelope returned by Zalo REST APIs.
/// </summary>
/// <typeparam name="T">The type of the inner response data.</typeparam>
public sealed record ZaloApiResponse<T>
{
    /// <summary>
    /// Gets the API error code (0 indicates success).
    /// </summary>
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; init; }

    /// <summary>
    /// Gets the error description or status message returned by the server.
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the structured payload data, or default if none was returned.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>
    /// Gets a value indicating whether the API response denotes success (ErrorCode == 0).
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => ErrorCode == 0;
}

/// <summary>
/// Represents a non-generic standardized response envelope returned by Zalo REST APIs.
/// </summary>
public sealed record ZaloApiResponse
{
    /// <summary>
    /// Gets the API error code (0 indicates success).
    /// </summary>
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; init; }

    /// <summary>
    /// Gets the error description or status message returned by the server.
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the API response denotes success (ErrorCode == 0).
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => ErrorCode == 0;
}
