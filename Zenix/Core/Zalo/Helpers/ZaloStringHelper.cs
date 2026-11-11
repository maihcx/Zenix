// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Zenix.Core.Zalo.Helpers;

/// <summary>
/// Provides string manipulation and collation utilities for Zalo contact names, initials, and diacritic folding.
/// </summary>
public static class ZaloStringHelper
{
    /// <summary>
    /// Computes display initials (1 to 2 uppercase characters) from a user or group name.
    /// </summary>
    /// <param name="name">The full name or display title.</param>
    /// <returns>A string containing 1 or 2 uppercase characters, or "?" if name is null, empty, or symbol-only.</returns>
    public static string ComputeInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        if (words.Length == 1)
        {
            var firstChar = FindFirstLetterOrDigit(words[0]);
            return firstChar.HasValue ? char.ToUpperInvariant(firstChar.Value).ToString() : "?";
        }

        var first = FindFirstLetterOrDigit(words[0]);
        var last = FindFirstLetterOrDigit(words[^1]);

        if (first.HasValue && last.HasValue)
        {
            return $"{char.ToUpperInvariant(first.Value)}{char.ToUpperInvariant(last.Value)}";
        }

        if (first.HasValue)
        {
            return char.ToUpperInvariant(first.Value).ToString();
        }

        if (last.HasValue)
        {
            return char.ToUpperInvariant(last.Value).ToString();
        }

        return "?";
    }

    /// <summary>
    /// Strips Vietnamese and Latin diacritical marks from the input string, replacing them with ASCII equivalents.
    /// </summary>
    /// <param name="text">The original text with potential diacritics.</param>
    /// <returns>A normalized string stripped of diacritical marks, or empty string if input is null or whitespace.</returns>
    public static string FoldDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var step1 = text.Replace('đ', 'd').Replace('Đ', 'D');
        var normalized = step1.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Computes an alphabetical sort key for contact ordering (collating given name first for Vietnamese names).
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <returns>An uppercase normalized sort key string.</returns>
    public static string ComputeSortKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var folded = FoldDiacritics(name).Trim();
        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length <= 1)
        {
            return folded.ToUpperInvariant();
        }

        // Place given name (last word) first for proper Vietnamese alphabetical indexing
        var givenName = words[^1];
        var remaining = string.Join(" ", words.Take(words.Length - 1));
        return $"{givenName} {remaining}".ToUpperInvariant();
    }

    /// <summary>
    /// Computes the single-character grouping header (e.g., 'A'..'Z' or '#') for list sectioning.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <returns>A single-character string representing the section ('A'-'Z' or '#').</returns>
    public static string GetSectionHeader(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "#";
        }

        var folded = FoldDiacritics(name).Trim();
        if (string.IsNullOrEmpty(folded))
        {
            return "#";
        }

        // If the string starts with a non-letter (e.g. digit, symbol, emoji), group under '#'
        if (!char.IsLetter(folded[0]))
        {
            return "#";
        }

        // In Vietnamese, grouping is typically by given name (last word)
        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var targetWord = words.Length > 0 ? words[^1] : folded;

        if (targetWord.Length > 0)
        {
            var firstChar = targetWord[0];
            var upper = char.ToUpperInvariant(firstChar);
            if (upper >= 'A' && upper <= 'Z')
            {
                return upper.ToString();
            }
        }

        return "#";
    }

    private static char? FindFirstLetterOrDigit(string word)
    {
        foreach (var c in word)
        {
            if (char.IsLetterOrDigit(c))
            {
                return c;
            }
        }

        return null;
    }
}
