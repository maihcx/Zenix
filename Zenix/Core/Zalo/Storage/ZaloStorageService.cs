// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Storage.Repositories;

namespace Zenix.Core.Zalo.Storage;

/// <summary>
/// Master SQLite DAL 4.0 storage facade coordinating per-account partitioned databases,
/// migrations, repositories, DPAPI credentials, and safe account data wiping.
/// </summary>
public sealed class ZaloStorageService : IZaloStorageService, IDisposable
{
    private readonly bool _forceInMemory;
    private readonly string? _customRootDirectory;
    private readonly ZaloCredentialStorage _credentials;
    private readonly FailedDraftStore _drafts;
    private readonly ZaloSqliteMigrator _migrator;

    private ZaloSqliteDalService? _dal;
    private ConversationRepository? _conversations;
    private MessageRepository? _messages;
    private ContactRepository? _contacts;
    private GroupRepository? _groups;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZaloStorageService"/> class.
    /// </summary>
    /// <param name="forceInMemory">If <c>true</c>, forces in-memory SQLite storage for unit testing.</param>
    /// <param name="customRootDirectory">Optional custom directory override for databases and credentials.</param>
    public ZaloStorageService(bool forceInMemory = false, string? customRootDirectory = null)
    {
        _forceInMemory = forceInMemory;
        _customRootDirectory = customRootDirectory;
        _credentials = new ZaloCredentialStorage(
            customRootDirectory != null ? Path.Combine(customRootDirectory, "credentials") : null);
        _drafts = new FailedDraftStore();
        _migrator = new ZaloSqliteMigrator();
    }

    /// <summary>
    /// Gets the credentials storage manager protected by Windows DPAPI.
    /// </summary>
    public ZaloCredentialStorage Credentials => _credentials;

    /// <summary>
    /// Gets the in-memory/disk composer draft manager.
    /// </summary>
    public FailedDraftStore Drafts => _drafts;

    /// <summary>
    /// Gets the active account identifier, or <c>null</c> if not initialized.
    /// </summary>
    public string? ActiveAccountId => _dal?.AccountId;

    /// <inheritdoc/>
    public IConversationRepository Conversations
    {
        get
        {
            EnsureInitialized();
            return _conversations!;
        }
    }

    /// <inheritdoc/>
    public IMessageRepository Messages
    {
        get
        {
            EnsureInitialized();
            return _messages!;
        }
    }

    /// <inheritdoc/>
    public IContactRepository Contacts
    {
        get
        {
            EnsureInitialized();
            return _contacts!;
        }
    }

    /// <inheritdoc/>
    public IGroupRepository Groups
    {
        get
        {
            EnsureInitialized();
            return _groups!;
        }
    }

    /// <inheritdoc/>
    public async Task InitializeAccountDatabaseAsync(string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        EnsureNotDisposed();

        if (_dal != null)
        {
            await CloseDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }

        var dal = new ZaloSqliteDalService(accountId, _forceInMemory, _customRootDirectory);
        await using (var connection = await dal.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        {
            await _migrator.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        _dal = dal;
        _conversations = new ConversationRepository(dal);
        _messages = new MessageRepository(dal);
        _contacts = new ContactRepository(dal);
        _groups = new GroupRepository(dal);
    }

    /// <inheritdoc/>
    public async Task CloseDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_dal != null)
        {
            await _dal.CloseAsync(cancellationToken).ConfigureAwait(false);
            await _dal.DisposeAsync().ConfigureAwait(false);
            _dal = null;
            _conversations = null;
            _messages = null;
            _contacts = null;
            _groups = null;
        }
    }

    /// <inheritdoc/>
    public async Task WipeAccountDataAsync(string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        EnsureNotDisposed();

        if (string.Equals(_dal?.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
        {
            await CloseDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }

        _credentials.ClearCredentials(accountId);

        if (_forceInMemory || accountId == ":memory:")
        {
            return;
        }

        var root = _customRootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zenix",
            "sqlite",
            "_production");

        var accountDir = Path.Combine(root, SanitizePath(accountId));
        if (!Directory.Exists(accountDir))
        {
            return;
        }

        var primaryDb = Path.Combine(accountDir, "chat_storage.db");
        var walFile = Path.Combine(accountDir, "chat_storage.db-wal");
        var shmFile = Path.Combine(accountDir, "chat_storage.db-shm");

        TryDeleteFile(primaryDb);
        TryDeleteFile(walFile);
        TryDeleteFile(shmFile);

        try
        {
            if (Directory.GetFileSystemEntries(accountDir).Length == 0)
            {
                Directory.Delete(accountDir, recursive: false);
            }
        }
        catch (IOException)
        {
            // Directory busy or contains other items
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await CloseDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
        _migrator.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _dal?.Dispose();
        _dal = null;
        _migrator.Dispose();
        GC.SuppressFinalize(this);
    }

    private void EnsureInitialized()
    {
        EnsureNotDisposed();
        if (_dal == null)
        {
            throw new InvalidOperationException("Database has not been initialized for an account.");
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort file deletion
        }
    }

    private static string SanitizePath(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
