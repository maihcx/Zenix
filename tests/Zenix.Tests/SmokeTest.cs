// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using FluentAssertions;
using Xunit;

namespace Zenix.Tests;

/// <summary>
/// Basic test harness verification test.
/// </summary>
public sealed class SmokeTest
{
    /// <summary>
    /// Verifies test harness execution.
    /// </summary>
    [Fact]
    public void TestRunner_ExecutesSuccessfully()
    {
        true.Should().BeTrue();
    }
}
