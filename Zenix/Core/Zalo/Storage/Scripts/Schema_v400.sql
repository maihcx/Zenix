-- Copyright (c) Zenix. All rights reserved.
-- Licensed under the proprietary and confidential license.
-- Unauthorized copying, distribution, modification, or disclosure of this file,
-- via any medium, is strictly prohibited without prior written permission.

PRAGMA foreign_keys = ON;

-- 1. Conversations Table
CREATE TABLE IF NOT EXISTS conversations (
    conversation_id TEXT PRIMARY KEY NOT NULL,
    type INTEGER NOT NULL DEFAULT 0,
    title TEXT NOT NULL,
    avatar_url TEXT,
    snippet TEXT,
    last_sender_name TEXT,
    last_activity_at INTEGER NOT NULL DEFAULT 0,
    unread_count INTEGER NOT NULL DEFAULT 0,
    flags INTEGER NOT NULL DEFAULT 0,
    last_delivery_status INTEGER NOT NULL DEFAULT 1,
    is_muted INTEGER NOT NULL DEFAULT 0,
    pinned_msg_id TEXT,
    draft_text TEXT,
    extras_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_conv_updated ON conversations(last_activity_at DESC);
CREATE INDEX IF NOT EXISTS idx_conversations_flags ON conversations(flags);

-- 2. Messages Table
CREATE TABLE IF NOT EXISTS messages (
    cli_msg_id TEXT PRIMARY KEY NOT NULL,
    server_msg_id TEXT,
    conversation_id TEXT NOT NULL,
    conversation_type INTEGER NOT NULL DEFAULT 0,
    sender_id TEXT NOT NULL,
    sender_name TEXT,
    sender_avatar_url TEXT,
    is_self INTEGER NOT NULL DEFAULT 0,
    status INTEGER NOT NULL DEFAULT 1,
    kind INTEGER NOT NULL DEFAULT 0,
    payload_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    quote_msg_id TEXT,
    quote_json TEXT,
    mentions_json TEXT,
    reactions_json TEXT,
    timestamp INTEGER NOT NULL,
    sequence_offset INTEGER NOT NULL DEFAULT 0,
    is_edited INTEGER NOT NULL DEFAULT 0,
    is_recalled INTEGER NOT NULL DEFAULT 0,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(conversation_id) REFERENCES conversations(conversation_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_messages_conv_ts ON messages(conversation_id, timestamp DESC, sequence_offset DESC);
CREATE INDEX IF NOT EXISTS idx_messages_cli_id ON messages(cli_msg_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_messages_server_id ON messages(server_msg_id) WHERE server_msg_id IS NOT NULL;

-- 3. Contacts Table
CREATE TABLE IF NOT EXISTS contacts (
    contact_id TEXT PRIMARY KEY NOT NULL,
    display_name TEXT NOT NULL,
    local_custom_name TEXT,
    phone_number TEXT,
    avatar_url TEXT,
    gender INTEGER NOT NULL DEFAULT 0,
    is_friend INTEGER NOT NULL DEFAULT 1,
    is_blocked INTEGER NOT NULL DEFAULT 0,
    is_muted INTEGER NOT NULL DEFAULT 0,
    sort_key TEXT NOT NULL,
    section_header TEXT NOT NULL,
    unaccented_name TEXT,
    last_seen_timestamp INTEGER NOT NULL DEFAULT 0,
    updated_at INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_contacts_sort ON contacts(sort_key);
CREATE INDEX IF NOT EXISTS idx_contacts_section_sort ON contacts(section_header, sort_key);

-- 4. Group Profiles & View & Members Tables
CREATE TABLE IF NOT EXISTS group_profiles (
    group_id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    avatar_url TEXT,
    creator_id TEXT NOT NULL,
    member_count INTEGER NOT NULL DEFAULT 0,
    is_e2ee INTEGER NOT NULL DEFAULT 0,
    is_muted INTEGER NOT NULL DEFAULT 0,
    is_disappearing INTEGER NOT NULL DEFAULT 0,
    disappearing_duration_seconds INTEGER NOT NULL DEFAULT 0,
    created_timestamp INTEGER NOT NULL DEFAULT 0,
    description TEXT,
    pinned_msg_id TEXT,
    updated_at INTEGER NOT NULL DEFAULT 0
);

CREATE VIEW IF NOT EXISTS groups AS SELECT * FROM group_profiles;

CREATE TABLE IF NOT EXISTS group_members (
    group_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    avatar_url TEXT,
    role INTEGER NOT NULL DEFAULT 0,
    joined_at INTEGER NOT NULL DEFAULT 0,
    is_muted INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(group_id, user_id),
    FOREIGN KEY(group_id) REFERENCES group_profiles(group_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_group_members_user ON group_members(user_id);

-- 5. Drafts Table
CREATE TABLE IF NOT EXISTS drafts (
    conversation_id TEXT PRIMARY KEY NOT NULL,
    draft_text TEXT NOT NULL,
    mentions_json TEXT,
    reply_to_json TEXT,
    updated_at INTEGER NOT NULL DEFAULT 0
);

-- 6. Keychain Keys Table
CREATE TABLE IF NOT EXISTS keychain_keys (
    key_id TEXT PRIMARY KEY NOT NULL,
    key_type TEXT NOT NULL,
    encrypted_blob BLOB NOT NULL,
    created_at INTEGER NOT NULL DEFAULT 0,
    updated_at INTEGER NOT NULL DEFAULT 0
);

-- 7. Sync Checkpoints Table
CREATE TABLE IF NOT EXISTS sync_checkpoints (
    checkpoint_key TEXT PRIMARY KEY NOT NULL,
    checkpoint_value TEXT NOT NULL,
    updated_at INTEGER NOT NULL DEFAULT 0
);

-- 8. Trigger for Operational Integrity
CREATE TRIGGER IF NOT EXISTS trg_update_conversation_on_new_message
AFTER INSERT ON messages
BEGIN
    UPDATE conversations
    SET last_activity_at = NEW.timestamp,
        unread_count = CASE
            WHEN NEW.is_self = 0 THEN unread_count + 1
            ELSE unread_count
        END,
        last_delivery_status = NEW.status,
        last_sender_name = NEW.sender_name
    WHERE conversation_id = NEW.conversation_id;
END;
