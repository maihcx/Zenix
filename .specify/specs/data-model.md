# Data Model Specification: Zalo Client Port to .NET 10 WinUI 3

**Feature Identifier**: `zalo-winui-port`  
**Phase**: Phase 1 Data Models & SQLite Schema Design  
**Date**: 2026-11-11  
**Status**: Approved (Production-Ready Architecture)  
**Spec Reference**: `.specify/specs/spec.md`  

---

## 1. Domain Entities & Immutable Record Hierarchy

All domain entities reside in `Zenix.Core.Zalo.Models`. They are 100% UI-agnostic, defined as C# 12 / .NET 10 immutable records with init-only properties, supporting value equality and memory efficiency.

### 1.1 User & Contact Profiles

```csharp
namespace Zenix.Core.Zalo.Models;

/// <summary>
/// Represents the authenticated user's profile.
/// </summary>
public record UserProfile
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? ZaloName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? AvatarUrl { get; init; }
    public string? CoverUrl { get; init; }
    public string? StatusText { get; init; }
    public string? Bio { get; init; }
    public int Gender { get; init; } // 0: Unknown, 1: Male, 2: Female
    public DateOnly? DateOfBirth { get; init; }
    public bool IsOnline { get; init; }
    public DateTimeOffset? LastActiveAt { get; init; }
    
    /// <summary>
    /// Gets two-letter initials used as an avatar fallback when AvatarUrl is null.
    /// </summary>
    public string Initials => ZaloStringHelper.ComputeInitials(DisplayName);
}

/// <summary>
/// Represents a contact entry in the user's address book.
/// </summary>
public record UserContact
{
    public required string ContactId { get; init; }
    public required string DisplayName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? AvatarUrl { get; init; }
    public string? AliasName { get; init; }
    public string SortKey { get; init; } = string.Empty;
    public string SectionHeader { get; init; } = "#";
    public bool IsFriend { get; init; }
    public bool IsOfficialAccount { get; init; }
    public bool IsBlocked { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public string EffectiveName => !string.IsNullOrWhiteSpace(AliasName) ? AliasName : DisplayName;
    public string Initials => ZaloStringHelper.ComputeInitials(EffectiveName);
}
```

### 1.2 Group Profile & Members

```csharp
namespace Zenix.Core.Zalo.Models;

public enum GroupMemberRole
{
    Member = 0,
    Admin = 1,
    Creator = 2
}

/// <summary>
/// Represents a multi-user group chat profile.
/// </summary>
public record GroupProfile
{
    public required string GroupId { get; init; } // e.g. "g123456789"
    public required string Name { get; init; }
    public string? AvatarUrl { get; init; }
    public required string CreatorId { get; init; }
    public int MemberCount { get; init; }
    public bool IsCommunity { get; init; }
    public bool IsMuted { get; init; }
    public string? PinnedMsgId { get; init; }
    public IReadOnlyList<GroupMember> Members { get; init; } = Array.Empty<GroupMember>();
    
    public string Initials => ZaloStringHelper.ComputeInitials(Name);
}

/// <summary>
/// Represents a member within a group chat.
/// </summary>
public record GroupMember
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public GroupMemberRole Role { get; init; } = GroupMemberRole.Member;
    public DateTimeOffset JoinedAt { get; init; }
    public string? InvitedBy { get; init; }
    
    public bool IsAdmin => Role is GroupMemberRole.Admin or GroupMemberRole.Creator;
    public bool IsCreator => Role is GroupMemberRole.Creator;
    public string Initials => ZaloStringHelper.ComputeInitials(DisplayName);
}
```

### 1.3 Conversations & Thread Metadata

```csharp
namespace Zenix.Core.Zalo.Models;

[Flags]
public enum ConversationActionFlags
{
    None = 0,
    Pinned = 1 << 0,          // 1
    Archived = 1 << 1,        // 2
    Hidden = 1 << 2,          // 4
    CannotPin = 1 << 3,       // 8
    CannotArchive = 1 << 4,   // 16
    CannotMute = 1 << 5,      // 32
    CannotDelete = 1 << 6,    // 64
    NoPinMenu = 1 << 7,       // 128
    CannotHide = 1 << 8,      // 256
    UnreadMark = 1 << 9       // 512
}

/// <summary>
/// Summary representation of a conversation thread in the master list.
/// </summary>
public record ConversationSummary
{
    public required string ConversationId { get; init; }
    public required string Title { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsGroup { get; init; }
    public bool IsProtected { get; init; }
    public bool IsE2EE { get; init; }
    public int UnreadCount { get; init; }
    public ConversationActionFlags Flags { get; init; }
    public ChatMessage? LastMessage { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public string DraftText { get; init; } = string.Empty;
    public ConversationExtras Extras { get; init; } = ConversationExtras.Default;
    
    public bool IsPinned => Flags.HasFlag(ConversationActionFlags.Pinned);
    public bool IsArchived => Flags.HasFlag(ConversationActionFlags.Archived);
    public string Initials => ZaloStringHelper.ComputeInitials(Title);
}

/// <summary>
/// Extended preferences and UI customizations for a conversation.
/// </summary>
public record ConversationExtras
{
    public static readonly ConversationExtras Default = new();
    
    public DateTimeOffset? MuteUntil { get; init; }
    public int NotificationLevel { get; init; } = 1; // 0: None, 1: All, 2: MentionsOnly
    public string? ThemeId { get; init; }
    public string? CustomBackgroundUrl { get; init; }
    public DateTimeOffset? DraftTimestamp { get; init; }
    public bool IsMuted => MuteUntil.HasValue && MuteUntil.Value > DateTimeOffset.UtcNow;
}
```

### 1.4 Chat Messages & Polymorphic Payloads

```csharp
namespace Zenix.Core.Zalo.Models;

public enum MessageDeliveryStatus
{
    Failed = -2,
    Draft = 0,
    Sent = 1,
    Delivered = 2,
    Seen = 3,
    SeenFull = 4,
    Sending = 10
}

public enum NoticeType
{
    General = 0,
    MemberJoined = 1,
    MemberLeft = 2,
    PinUpdated = 3,
    SecurityChanged = 4
}

public enum SystemEventCode
{
    Unknown = 0,
    GroupRenamed = 101,
    GroupAvatarChanged = 102,
    E2EESafetyNumberChanged = 201,
    CallMissed = 301
}

/// <summary>
/// Discriminated union of strongly-typed message payloads.
/// </summary>
public abstract record MessagePayload
{
    public record TextPayload(string Text) : MessagePayload;
    
    public record PhotoPayload(
        string PhotoUrl, 
        string? HighResUrl, 
        int Width, 
        int Height, 
        long FileSize, 
        string? ThumbnailBase64 = null
    ) : MessagePayload;
    
    public record VideoPayload(
        string VideoUrl, 
        string? PosterUrl, 
        TimeSpan Duration, 
        int Width, 
        int Height, 
        long FileSize
    ) : MessagePayload;
    
    public record VoicePayload(
        string AudioUrl, 
        TimeSpan Duration, 
        IReadOnlyList<byte>? Waveform, 
        long FileSize
    ) : MessagePayload;
    
    public record FilePayload(
        string FileName, 
        long FileSizeBytes, 
        string DownloadUrl, 
        string Extension, 
        string? Checksum = null
    ) : MessagePayload;
    
    public record StickerPayload(
        string CategoryId, 
        string StickerId, 
        string StaticUrl, 
        string? AnimationUrl, 
        string? SpriteUrl = null
    ) : MessagePayload;
    
    public record LocationPayload(
        double Latitude, 
        double Longitude, 
        string Title, 
        string Address, 
        string? ThumbnailUrl = null
    ) : MessagePayload;
    
    public record ContactCardPayload(
        string TargetUserId, 
        string TargetName, 
        string? Phone, 
        string? AvatarUrl
    ) : MessagePayload;
    
    public record ECardPayload(
        string Title, 
        string Description, 
        string ActionUrl, 
        string? ImageUrl, 
        IReadOnlyList<CardButton> Buttons
    ) : MessagePayload;
    
    public record PollPayload(
        string PollId, 
        string Question, 
        int TotalVotes, 
        bool IsClosed, 
        IReadOnlyList<PollOption> Options
    ) : MessagePayload;
    
    public record NoticePayload(
        string NoticeText, 
        NoticeType NoticeType
    ) : MessagePayload;
    
    public record SystemPayload(
        string SystemMessage, 
        SystemEventCode EventCode
    ) : MessagePayload;
    
    public record RecalledPayload(
        string OriginalSenderName, 
        DateTimeOffset RecalledAt
    ) : MessagePayload;
    
    public record UnsupportedPayload(
        int RawTypeCode, 
        string RawJson
    ) : MessagePayload;
}

public record CardButton(string Title, string ActionUrl, string Style = "Primary");

/// <summary>
/// Core message entity.
/// </summary>
public record ChatMessage
{
    public required string CliMsgId { get; init; }
    public string? ServerMsgId { get; init; }
    public required string ConversationId { get; init; }
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public string? SenderAvatarUrl { get; init; }
    public bool IsOutgoing { get; init; }
    public MessageDeliveryStatus Status { get; init; }
    public required MessagePayload Payload { get; init; }
    public QuotedMessage? Quote { get; init; }
    public IReadOnlyList<MentionSpan> Mentions { get; init; } = Array.Empty<MentionSpan>();
    public IReadOnlyList<MessageReaction> Reactions { get; init; } = Array.Empty<MessageReaction>();
    public DateTimeOffset Timestamp { get; init; }
    public int SequenceOffset { get; init; }
}

public record QuotedMessage(
    string QuotedMessageId,
    string SenderId,
    string SenderName,
    string PreviewText,
    string PayloadType
);

public record MentionSpan(
    int Position,
    int Length,
    string UserId,
    bool IsAll = false
);

public record MessageReaction(
    string Emoji,
    string ReactionType,
    int Count,
    bool IncludesMe,
    IReadOnlyList<string> ReactorNames
);
```

### 1.5 Polls & Shared Media

```csharp
namespace Zenix.Core.Zalo.Models;

public record PollDetail
{
    public required string PollId { get; init; }
    public required string GroupId { get; init; }
    public required string CreatorId { get; init; }
    public required string Question { get; init; }
    public IReadOnlyList<PollOption> Options { get; init; } = Array.Empty<PollOption>();
    public int TotalVotes { get; init; }
    public bool IsMultiChoice { get; init; }
    public bool AllowAddOptions { get; init; }
    public bool IsClosed { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public record PollOption
{
    public required string OptionId { get; init; }
    public required string Text { get; init; }
    public int VoteCount { get; init; }
    public bool IsVotedByMe { get; init; }
    public IReadOnlyList<string> VoterUserIds { get; init; } = Array.Empty<string>();
}

public record SharedMediaItem
{
    public required string MediaId { get; init; }
    public required string ConversationId { get; init; }
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string MediaType { get; init; } // Photo, Video, Document, Link
    public required string Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public long FileSize { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

---

## 2. Entity Relationship Diagram (ERD)

```text
┌──────────────────────┐         1:N          ┌──────────────────────────┐
│     UserProfile      │─────────────────────<│       UserContact        │
└──────────────────────┘                      └──────────────────────────┘
           │
           │ 1:N (Memberships)
           ▼
┌──────────────────────┐         1:N          ┌──────────────────────────┐
│     GroupMember      │>────────────────────-│       GroupProfile       │
└──────────────────────┘                      └──────────────────────────┘
                                                           │
                                                           │ 1:1
                                                           ▼
┌──────────────────────┐         1:1          ┌──────────────────────────┐
│  ConversationExtras  │<─────────────────────│   ConversationSummary    │
└──────────────────────┘                      └──────────────────────────┘
                                                           │
                                                           │ 1:N
                                                           ▼
┌──────────────────────┐         1:N          ┌──────────────────────────┐
│   SharedMediaItem    │<─────────────────────│       ChatMessage        │
└──────────────────────┘                      └──────────────────────────┘
                                                           │
                      ┌───────────────────┬────────────────┴───────────────────┐
                      │ 1:N               │ 1:N                                │ 1:0..1
                      ▼                   ▼                                    ▼
           ┌──────────────────────┐ ┌───────────────┐               ┌────────────────────┐
           │   MessageReaction    │ │  MentionSpan  │               │   QuotedMessage    │
           └──────────────────────┘ └───────────────┘               └────────────────────┘
```

---

## 3. SQLite Database Schema (DAL 4.0 Layout)

Database file location: `%LocalAppData%\Zenix\sqlite\_production\<account_id>\chat_storage.db`.

```sql
-- Enforce DAL 4.0 Performance Pragmas
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA busy_timeout = 5000;
PRAGMA temp_store = MEMORY;
PRAGMA foreign_keys = ON;
PRAGMA user_version = 400;

-- 1. Conversations Table
CREATE TABLE IF NOT EXISTS conversations (
    conversation_id TEXT PRIMARY KEY NOT NULL,
    title TEXT NOT NULL,
    avatar_url TEXT,
    is_group INTEGER NOT NULL DEFAULT 0,
    is_protected INTEGER NOT NULL DEFAULT 0,
    is_e2ee INTEGER NOT NULL DEFAULT 0,
    unread_count INTEGER NOT NULL DEFAULT 0,
    flags INTEGER NOT NULL DEFAULT 0,
    last_activity_at INTEGER NOT NULL,
    draft_text TEXT,
    extras_json TEXT
);
CREATE INDEX IF NOT EXISTS idx_conversations_activity ON conversations(last_activity_at DESC);
CREATE INDEX IF NOT EXISTS idx_conversations_flags ON conversations(flags);

-- 2. Messages Table
CREATE TABLE IF NOT EXISTS messages (
    cli_msg_id TEXT PRIMARY KEY NOT NULL,
    server_msg_id TEXT,
    conversation_id TEXT NOT NULL,
    sender_id TEXT NOT NULL,
    sender_name TEXT NOT NULL,
    sender_avatar_url TEXT,
    is_outgoing INTEGER NOT NULL DEFAULT 0,
    status INTEGER NOT NULL DEFAULT 1,
    payload_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    quote_json TEXT,
    mentions_json TEXT,
    reactions_json TEXT,
    timestamp INTEGER NOT NULL,
    sequence_offset INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(conversation_id) REFERENCES conversations(conversation_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_messages_conv_time 
    ON messages(conversation_id, timestamp DESC, sequence_offset DESC);
CREATE UNIQUE INDEX IF NOT EXISTS idx_messages_server_id 
    ON messages(server_msg_id) WHERE server_msg_id IS NOT NULL;

-- 3. Contacts Table
CREATE TABLE IF NOT EXISTS contacts (
    contact_id TEXT PRIMARY KEY NOT NULL,
    display_name TEXT NOT NULL,
    phone_number TEXT,
    avatar_url TEXT,
    alias_name TEXT,
    sort_key TEXT NOT NULL,
    section_header TEXT NOT NULL,
    is_friend INTEGER NOT NULL DEFAULT 1,
    is_oa INTEGER NOT NULL DEFAULT 0,
    is_blocked INTEGER NOT NULL DEFAULT 0,
    updated_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_contacts_sort ON contacts(section_header, sort_key);

-- 4. Group Profiles & Members Tables
CREATE TABLE IF NOT EXISTS group_profiles (
    group_id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    avatar_url TEXT,
    creator_id TEXT NOT NULL,
    member_count INTEGER NOT NULL DEFAULT 0,
    is_community INTEGER NOT NULL DEFAULT 0,
    is_muted INTEGER NOT NULL DEFAULT 0,
    pinned_msg_id TEXT,
    updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS group_members (
    group_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    avatar_url TEXT,
    role INTEGER NOT NULL DEFAULT 0,
    joined_at INTEGER NOT NULL,
    invited_by TEXT,
    PRIMARY KEY(group_id, user_id),
    FOREIGN KEY(group_id) REFERENCES group_profiles(group_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_group_members_user ON group_members(user_id);

-- 5. Shared Media Cache Table
CREATE TABLE IF NOT EXISTS shared_media (
    media_id TEXT PRIMARY KEY NOT NULL,
    conversation_id TEXT NOT NULL,
    sender_id TEXT NOT NULL,
    sender_name TEXT NOT NULL,
    media_type TEXT NOT NULL,
    url TEXT NOT NULL,
    thumbnail_url TEXT,
    file_size INTEGER NOT NULL,
    timestamp INTEGER NOT NULL,
    FOREIGN KEY(conversation_id) REFERENCES conversations(conversation_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_shared_media_conv_type ON shared_media(conversation_id, media_type, timestamp DESC);

-- 6. Polls & Poll Options Tables
CREATE TABLE IF NOT EXISTS polls (
    poll_id TEXT PRIMARY KEY NOT NULL,
    group_id TEXT NOT NULL,
    creator_id TEXT NOT NULL,
    question TEXT NOT NULL,
    total_votes INTEGER NOT NULL DEFAULT 0,
    is_multi_choice INTEGER NOT NULL DEFAULT 0,
    allow_add_options INTEGER NOT NULL DEFAULT 0,
    is_closed INTEGER NOT NULL DEFAULT 0,
    expires_at INTEGER,
    FOREIGN KEY(group_id) REFERENCES group_profiles(group_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS poll_options (
    option_id TEXT NOT NULL,
    poll_id TEXT NOT NULL,
    text TEXT NOT NULL,
    vote_count INTEGER NOT NULL DEFAULT 0,
    is_voted_by_me INTEGER NOT NULL DEFAULT 0,
    voters_json TEXT,
    PRIMARY KEY(poll_id, option_id),
    FOREIGN KEY(poll_id) REFERENCES polls(poll_id) ON DELETE CASCADE
);

-- 7. E2EE Session & Ratchet Key Store
CREATE TABLE IF NOT EXISTS e2ee_sessions (
    contact_id TEXT PRIMARY KEY NOT NULL,
    session_record_blob BLOB NOT NULL,
    ratchet_state_blob BLOB NOT NULL,
    updated_at INTEGER NOT NULL
);

-- Database Triggers for Operational Integrity
CREATE TRIGGER IF NOT EXISTS trg_update_conversation_on_new_message
AFTER INSERT ON messages
BEGIN
    UPDATE conversations 
    SET last_activity_at = NEW.timestamp,
        unread_count = CASE 
            WHEN NEW.is_outgoing = 0 THEN unread_count + 1 
            ELSE unread_count 
        END
    WHERE conversation_id = NEW.conversation_id;
END;
```
