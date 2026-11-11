// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Zenix.Strings;

namespace Zenix.Tests.Zalo;

/// <summary>
/// Unit tests verifying localization resource bundle integrity, key parity, BOM cleanliness, and LocaleKeys consistency.
/// </summary>
public sealed class LocalizationVerificationTests
{
    private static readonly string StringsDirectory = FindStringsDirectory();

    private static readonly string[] ResxFiles =
    [
        "String.resx",
        "String.en.resx",
        "String.vi.resx"
    ];

    private static string FindStringsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Zenix.sln")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate repository root containing Zenix.sln.");
        }

        return Path.Combine(dir.FullName, "Zenix", "Strings");
    }

    /// <summary>
    /// Verifies that all expected resx resource files exist in Zenix/Strings.
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void ResxFiles_AllExist()
    {
        Directory.Exists(StringsDirectory).Should().BeTrue($"Strings directory '{StringsDirectory}' must exist.");

        foreach (var file in ResxFiles)
        {
            var filePath = Path.Combine(StringsDirectory, file);
            File.Exists(filePath).Should().BeTrue($"Resource file '{filePath}' must exist.");
        }
    }

    /// <summary>
    /// Verifies that no resx file contains a UTF-8 Byte Order Mark (BOM: 0xEF, 0xBB, 0xBF).
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void ResxFiles_ContainNoUtf8Bom()
    {
        foreach (var file in ResxFiles)
        {
            var filePath = Path.Combine(StringsDirectory, file);
            var bytes = File.ReadAllBytes(filePath);

            bytes.Length.Should().BeGreaterThan(3);
            var hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            hasBom.Should().BeFalse($"File '{file}' must be encoded in UTF-8 without BOM.");
        }
    }

    /// <summary>
    /// Verifies that all resx files strictly use Windows CRLF (\r\n) line endings without bare LF characters.
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void ResxFiles_UseWindowsCrlfLineEndings()
    {
        foreach (var file in ResxFiles)
        {
            var filePath = Path.Combine(StringsDirectory, file);
            var bytes = File.ReadAllBytes(filePath);

            var crCount = bytes.Count(b => b == (byte)'\r');
            var lfCount = bytes.Count(b => b == (byte)'\n');

            crCount.Should().BeGreaterThan(0, $"File '{file}' must contain newline characters.");
            crCount.Should().Be(lfCount, $"File '{file}' must have matching CR and LF counts (pure CRLF).");
        }
    }

    /// <summary>
    /// Verifies 100% key parity across neutral (String.resx), English (String.en.resx), and Vietnamese (String.vi.resx).
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void ResxFiles_Have100PercentKeyParity()
    {
        var neutralKeys = LoadKeysFromResx("String.resx");
        var enKeys = LoadKeysFromResx("String.en.resx");
        var viKeys = LoadKeysFromResx("String.vi.resx");

        neutralKeys.Should().NotBeEmpty("String.resx must contain localization keys.");
        enKeys.Should().BeEquivalentTo(neutralKeys, "String.en.resx keys must exactly match String.resx");
        viKeys.Should().BeEquivalentTo(neutralKeys, "String.vi.resx keys must exactly match String.resx");
    }

    /// <summary>
    /// Verifies that no localization resource entry has an empty or whitespace-only value.
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void ResxFiles_ContainNoEmptyValues()
    {
        foreach (var file in ResxFiles)
        {
            var entries = LoadEntriesFromResx(file);
            entries.Should().NotBeEmpty();

            foreach (var (key, value) in entries)
            {
                string.IsNullOrWhiteSpace(value).Should().BeFalse(
                    $"Key '{key}' in '{file}' must have a non-empty string value."
                );
            }
        }
    }

    /// <summary>
    /// Verifies that all public constants on LocaleKeys match keys in the resx resource files.
    /// </summary>
    [Fact]
    [Trait("Category", "Localization")]
    public void LocaleKeys_MatchResxEntries()
    {
        var fields = typeof(LocaleKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToList();

        fields.Should().NotBeEmpty("LocaleKeys class must contain string constants.");

        var resxKeys = LoadKeysFromResx("String.resx");

        foreach (var field in fields)
        {
            var constantValue = (string)field.GetValue(null)!;
            constantValue.Should().Be(field.Name, $"Field '{field.Name}' value should match constant identifier.");
            resxKeys.Should().Contain(constantValue, $"Key '{constantValue}' defined in LocaleKeys must exist in String.resx.");
        }

        resxKeys.Count.Should().Be(fields.Count, "All resx keys should be represented in LocaleKeys constants.");
    }

    private static HashSet<string> LoadKeysFromResx(string fileName)
    {
        var filePath = Path.Combine(StringsDirectory, fileName);
        var doc = XDocument.Load(filePath);
        return doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet();
    }

    private static Dictionary<string, string> LoadEntriesFromResx(string fileName)
    {
        var filePath = Path.Combine(StringsDirectory, fileName);
        var doc = XDocument.Load(filePath);
        return doc.Root!.Elements("data")
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? string.Empty
            );
    }
}
