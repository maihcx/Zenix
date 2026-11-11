// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Helpers;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Tests.Zalo.Models;

/// <summary>
/// Unit tests verifying profile models, group metadata, and Zalo string/collation helpers.
/// </summary>
public sealed class ProfileModelTests
{
    /// <summary>
    /// Verifies UserProfile properties and immutability via record with expressions.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void UserProfile_InstantiationAndImmutability_MaintainsState()
    {
        var dob = new DateTimeOffset(1995, 8, 15, 0, 0, 0, TimeSpan.Zero);
        var profile = new UserProfile
        {
            UserId = "123456789",
            DisplayName = "Nguyen Van A",
            PhoneNumber = "+84901234567",
            AvatarUrl = "https://avatar.zalo.me/123.jpg",
            CoverUrl = "https://cover.zalo.me/123.jpg",
            Gender = ZaloGender.Male,
            DateOfBirth = dob,
            StatusMessage = "Hello Zalo!",
            IsBusiness = false,
            LastUpdatedTimestamp = 1700000000000
        };

        profile.UserId.Should().Be("123456789");
        profile.DisplayName.Should().Be("Nguyen Van A");
        profile.PhoneNumber.Should().Be("+84901234567");
        profile.AvatarUrl.Should().Be("https://avatar.zalo.me/123.jpg");
        profile.CoverUrl.Should().Be("https://cover.zalo.me/123.jpg");
        profile.Gender.Should().Be(ZaloGender.Male);
        profile.DateOfBirth.Should().Be(dob);
        profile.StatusMessage.Should().Be("Hello Zalo!");
        profile.IsBusiness.Should().BeFalse();
        profile.LastUpdatedTimestamp.Should().Be(1700000000000);

        var updated = profile with { StatusMessage = "New status", IsBusiness = true };
        updated.StatusMessage.Should().Be("New status");
        updated.IsBusiness.Should().BeTrue();
        profile.StatusMessage.Should().Be("Hello Zalo!");
    }

    /// <summary>
    /// Verifies UserContact EffectiveName prefers LocalCustomName when provided.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void UserContact_EffectiveName_PrefersLocalCustomName()
    {
        var contact1 = new UserContact
        {
            UserId = "contact1",
            DisplayName = "Nguyen Van B",
            LocalCustomName = "Anh Ba"
        };
        contact1.EffectiveName.Should().Be("Anh Ba");

        var contact2 = new UserContact
        {
            UserId = "contact2",
            DisplayName = "Tran Thi C",
            LocalCustomName = null
        };
        contact2.EffectiveName.Should().Be("Tran Thi C");

        var contact3 = new UserContact
        {
            UserId = "contact3",
            DisplayName = "Tran Thi C",
            LocalCustomName = "   "
        };
        contact3.EffectiveName.Should().Be("Tran Thi C");
    }

    /// <summary>
    /// Verifies UserContact SortKey and SectionHeader are computed automatically from EffectiveName.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void UserContact_SortKeyAndSectionHeader_ComputedCorrectly()
    {
        var contact = new UserContact
        {
            UserId = "c1",
            DisplayName = "Nguyễn Văn An"
        };

        contact.SortKey.Should().Be("AN NGUYEN VAN");
        contact.SectionHeader.Should().Be("A");

        var contactWithAlias = contact with { LocalCustomName = "Bình An" };
        contactWithAlias.EffectiveName.Should().Be("Bình An");
        contactWithAlias.SortKey.Should().Be("AN BINH");
        contactWithAlias.SectionHeader.Should().Be("A");
    }

    /// <summary>
    /// Verifies GroupProfile properties and disappearing message parameters.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void GroupProfile_PropertiesAndImmutability_MaintainsState()
    {
        var group = new GroupProfile
        {
            GroupId = "grp_1001",
            Name = "Team Engineering",
            AvatarUrl = "https://group.zalo.me/avatar.png",
            CreatorId = "user_001",
            MemberCount = 42,
            IsE2ee = true,
            IsMuted = false,
            IsDisappearing = true,
            DisappearingDurationSeconds = 86400,
            CreatedTimestamp = 1690000000000,
            Description = "Core team discussion"
        };

        group.GroupId.Should().Be("grp_1001");
        group.Name.Should().Be("Team Engineering");
        group.CreatorId.Should().Be("user_001");
        group.MemberCount.Should().Be(42);
        group.IsE2ee.Should().BeTrue();
        group.IsDisappearing.Should().BeTrue();
        group.DisappearingDurationSeconds.Should().Be(86400);

        var renamed = group with { Name = "Zenix Core Team", MemberCount = 43 };
        renamed.Name.Should().Be("Zenix Core Team");
        renamed.MemberCount.Should().Be(43);
        group.Name.Should().Be("Team Engineering");
    }

    /// <summary>
    /// Verifies GroupMember roles, joined timestamps, and muted states.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void GroupMember_RolesAndMute_MaintainsState()
    {
        var member = new GroupMember
        {
            GroupId = "grp_1",
            UserId = "usr_1",
            DisplayName = "Dao Duy Tuan",
            Role = GroupMemberRole.Admin,
            JoinedTimestamp = 1680000000000,
            IsMuted = false
        };

        member.Role.Should().Be(GroupMemberRole.Admin);
        member.JoinedTimestamp.Should().Be(1680000000000);
        member.IsMuted.Should().BeFalse();

        var mutedCreator = member with { Role = GroupMemberRole.Creator, IsMuted = true };
        mutedCreator.Role.Should().Be(GroupMemberRole.Creator);
        mutedCreator.IsMuted.Should().BeTrue();
    }

    /// <summary>
    /// Verifies ConversationSummary derived flags reflect bitmask values.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ConversationSummary_ComputedFlags_ReflectBitmask()
    {
        var summary = new ConversationSummary
        {
            ConversationId = "conv_1",
            ConversationType = ChatConversationType.Direct,
            Title = "Doan Thi Diem",
            Flags = ConversationActionFlags.Pinned | ConversationActionFlags.UnreadMark,
            LastDeliveryStatus = MessageDeliveryStatus.Delivered,
            UnreadCount = 3,
            LastActivityTimestamp = 1710000000000
        };

        summary.IsPinned.Should().BeTrue();
        summary.IsArchived.Should().BeFalse();
        summary.IsHidden.Should().BeFalse();
        summary.UnreadCount.Should().Be(3);

        var archived = summary with { Flags = ConversationActionFlags.Archived | ConversationActionFlags.Hidden };
        archived.IsPinned.Should().BeFalse();
        archived.IsArchived.Should().BeTrue();
        archived.IsHidden.Should().BeTrue();
    }

    /// <summary>
    /// Verifies ZaloStringHelper.ComputeInitials for various single, compound, and edge-case names.
    /// </summary>
    [Theory]
    [Trait("Category", "Models")]
    [InlineData("Nguyễn Văn An", "NA")]
    [InlineData("Lê Bình", "LB")]
    [InlineData("An", "A")]
    [InlineData("John Doe", "JD")]
    [InlineData("Z", "Z")]
    [InlineData("   ", "?")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    [InlineData("---", "?")]
    public void ZaloStringHelper_ComputeInitials_HandlesVariousNames(string? name, string expected)
    {
        ZaloStringHelper.ComputeInitials(name).Should().Be(expected);
    }

    /// <summary>
    /// Verifies ZaloStringHelper.FoldDiacritics accurately strips Vietnamese accents.
    /// </summary>
    [Theory]
    [Trait("Category", "Models")]
    [InlineData("Nguyễn Văn An", "Nguyen Van An")]
    [InlineData("Đặng Thùy Trâm", "Dang Thuy Tram")]
    [InlineData("Hồ Chí Minh", "Ho Chi Minh")]
    [InlineData("Phạm Tuân", "Pham Tuan")]
    [InlineData("VIỆT NAM", "VIET NAM")]
    [InlineData("Đỗ Đạt", "Do Dat")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void ZaloStringHelper_FoldDiacritics_StripsAccentsAccurately(string? text, string expected)
    {
        ZaloStringHelper.FoldDiacritics(text).Should().Be(expected);
    }

    /// <summary>
    /// Verifies ZaloStringHelper.ComputeSortKey places the Vietnamese given name first in uppercase.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ZaloStringHelper_ComputeSortKey_VietnameseCollation()
    {
        ZaloStringHelper.ComputeSortKey("Nguyễn Văn An").Should().Be("AN NGUYEN VAN");
        ZaloStringHelper.ComputeSortKey("Trần Văn Bình").Should().Be("BINH TRAN VAN");
        ZaloStringHelper.ComputeSortKey("Bình").Should().Be("BINH");
        ZaloStringHelper.ComputeSortKey("").Should().BeEmpty();
        ZaloStringHelper.ComputeSortKey(null).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies ZaloStringHelper.GetSectionHeader groups names into 'A'..'Z' or '#'.
    /// </summary>
    [Theory]
    [Trait("Category", "Models")]
    [InlineData("Nguyễn Văn An", "A")]
    [InlineData("Đặng Thùy Trâm", "T")]
    [InlineData("Lê Bình", "B")]
    [InlineData("Ánh Tuyết", "T")]
    [InlineData("123 Numbers", "#")]
    [InlineData("!Special", "#")]
    [InlineData("", "#")]
    [InlineData(null, "#")]
    public void ZaloStringHelper_GetSectionHeader_GroupsAlphabetically(string? name, string expected)
    {
        ZaloStringHelper.GetSectionHeader(name).Should().Be(expected);
    }
}
