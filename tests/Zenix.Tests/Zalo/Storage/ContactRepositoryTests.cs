// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Profiles;
using Zenix.Core.Zalo.Storage;
using Zenix.Core.Zalo.Storage.Repositories;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="ContactRepository"/> and <see cref="GroupRepository"/> verifying
/// contact upsert, Vietnamese collation, section headers, search queries, and group member role administration.
/// </summary>
public sealed class ContactRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly ZaloSqliteDalService _dal;
    private readonly ZaloSqliteMigrator _migrator;
    private readonly ContactRepository _contacts;
    private readonly GroupRepository _groups;

    public ContactRepositoryTests()
    {
        _dal = new ZaloSqliteDalService(":memory:");
        _migrator = new ZaloSqliteMigrator();
        _contacts = new ContactRepository(_dal);
        _groups = new GroupRepository(_dal);
    }

    public async Task InitializeAsync()
    {
        await using var connection = await _dal.CreateOpenConnectionAsync();
        await _migrator.MigrateAsync(connection);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _migrator.Dispose();
        _dal.Dispose();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task UpsertContactAsync_AndGetByIdAsync_RoundTripsProperly()
    {
        var contact = new UserContact
        {
            UserId = "user_1001",
            DisplayName = "Lê Hoàng Long",
            PhoneNumber = "0901234567",
            Gender = ZaloGender.Male,
            IsFriend = true
        };

        await _contacts.UpsertContactAsync(contact);

        var retrieved = await _contacts.GetByIdAsync("user_1001");
        retrieved.Should().NotBeNull();
        retrieved!.DisplayName.Should().Be("Lê Hoàng Long");
        retrieved.PhoneNumber.Should().Be("0901234567");
        retrieved.Gender.Should().Be(ZaloGender.Male);
        retrieved.IsFriend.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task GetAllContactsAsync_VietnameseCollation_GroupsProperlyUnderSections()
    {
        var list = new List<UserContact>
        {
            new() { UserId = "u1", DisplayName = "999 Fast Food" },
            new() { UserId = "u2", DisplayName = "Trần Văn An" },
            new() { UserId = "u3", DisplayName = "Nguyễn Thị Ánh Tuyết" },
            new() { UserId = "u4", DisplayName = "Bùi Minh Đức" }
        };

        await _contacts.UpsertBatchAsync(list);

        var all = await _contacts.GetAllContactsAsync();
        all.Should().HaveCount(4);

        // Verify section headers
        var fastFood = all.FirstOrDefault(c => c.UserId == "u1");
        fastFood!.SectionHeader.Should().Be("#");

        var an = all.FirstOrDefault(c => c.UserId == "u2");
        an!.SectionHeader.Should().Be("A");
        an.SortKey.Should().StartWith("AN");

        var tuyet = all.FirstOrDefault(c => c.UserId == "u3");
        tuyet!.SectionHeader.Should().Be("T");
        tuyet.SortKey.Should().StartWith("TUYET");

        var duc = all.FirstOrDefault(c => c.UserId == "u4");
        duc!.SectionHeader.Should().Be("D");
        duc.SortKey.Should().StartWith("DUC");

        // Verify overall alphabetical sorting: '#' comes before Latin letters in ASCII collation
        all[0].SectionHeader.Should().Be("#");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task SearchAsync_MatchesDisplayNameAndCustomAlias()
    {
        var contacts = new List<UserContact>
        {
            new() { UserId = "s1", DisplayName = "Nguyễn Văn Bình", LocalCustomName = "Bình IT" },
            new() { UserId = "s2", DisplayName = "Phạm Quỳnh Anh", PhoneNumber = "0987654321" },
            new() { UserId = "s3", DisplayName = "Võ Thành Nam" }
        };

        await _contacts.UpsertBatchAsync(contacts);

        // Search by alias
        var aliasResults = await _contacts.SearchAsync("Bình IT");
        aliasResults.Should().ContainSingle(c => c.UserId == "s1");

        // Search by phone number
        var phoneResults = await _contacts.SearchAsync("098765");
        phoneResults.Should().ContainSingle(c => c.UserId == "s2");

        // Search by unaccented name
        var foldedResults = await _contacts.SearchAsync("Quynh Anh");
        foldedResults.Should().ContainSingle(c => c.UserId == "s2");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public async Task GroupRepository_ManagesProfileAndMemberRoles()
    {
        var group = new GroupProfile
        {
            GroupId = "group_team_alpha",
            Name = "Team Alpha Engineering",
            CreatorId = "user_boss",
            MemberCount = 3,
            IsE2ee = true,
            CreatedTimestamp = 1700000000000L
        };

        await _groups.UpsertGroupAsync(group);

        var members = new List<GroupMember>
        {
            new() { GroupId = "group_team_alpha", UserId = "user_boss", DisplayName = "Boss", Role = GroupMemberRole.Creator },
            new() { GroupId = "group_team_alpha", UserId = "user_mod", DisplayName = "Moderator", Role = GroupMemberRole.Admin },
            new() { GroupId = "group_team_alpha", UserId = "user_dev", DisplayName = "Developer", Role = GroupMemberRole.Member }
        };

        await _groups.UpsertMembersBatchAsync("group_team_alpha", members);

        var retrievedGroup = await _groups.GetGroupByIdAsync("group_team_alpha");
        retrievedGroup.Should().NotBeNull();
        retrievedGroup!.Name.Should().Be("Team Alpha Engineering");
        retrievedGroup.IsE2ee.Should().BeTrue();

        var retrievedMembers = await _groups.GetMembersAsync("group_team_alpha");
        retrievedMembers.Should().HaveCount(3);
        // Ordered by role descending: Creator (2) -> Admin (1) -> Member (0)
        retrievedMembers[0].Role.Should().Be(GroupMemberRole.Creator);
        retrievedMembers[1].Role.Should().Be(GroupMemberRole.Admin);
        retrievedMembers[2].Role.Should().Be(GroupMemberRole.Member);

        // Remove member
        await _groups.RemoveMemberAsync("group_team_alpha", "user_dev");
        var afterRemove = await _groups.GetMembersAsync("group_team_alpha");
        afterRemove.Should().HaveCount(2);
        afterRemove.Should().NotContain(m => m.UserId == "user_dev");
    }
}
