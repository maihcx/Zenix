// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Zenix.Core.Zalo.Storage;

/// <summary>
/// Data Access Layer (DAL) service for Zalo local storage, managing SQLite connections,
/// WAL mode pragmas, safe UNIQUE constraint conflict tolerance, and write serialization.
/// </summary>
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Internal DAL executes parameterized statements passed by repositories.")]
public sealed class ZaloSqliteDalService : IAsyncDisposable, IDisposable
{
    private const int SqliteConstraintErrorCode = 19;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _inMemoryInitLock = new(1, 1);
    private readonly string _connectionString;
    private SqliteConnection? _inMemoryMasterConnection;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZaloSqliteDalService"/> class targeting either
    /// a physical database file partitioned by account ID or an in-memory test database.
    /// </summary>
    /// <param name="accountId">The account identifier, or <c>":memory:"</c> for in-memory mode.</param>
    /// <param name="isInMemory">Explicitly requests an in-memory database.</param>
    /// <param name="customRootDirectory">Optional custom root directory override for testing.</param>
    public ZaloSqliteDalService(string accountId, bool isInMemory = false, string? customRootDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        AccountId = accountId;
        IsInMemory = isInMemory || accountId == ":memory:";

        if (IsInMemory)
        {
            var memoryDbName = accountId == ":memory:" ? $"zenix_mem_{Guid.NewGuid():N}" : accountId;
            DatabasePath = $":memory:{memoryDbName}";
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = memoryDbName,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString();
        }
        else
        {
            var root = customRootDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Zenix",
                "sqlite",
                "_production");

            var sanitizedAccount = SanitizePath(accountId);
            var accountDir = Path.Combine(root, sanitizedAccount);
            Directory.CreateDirectory(accountDir);

            DatabasePath = Path.Combine(accountDir, "chat_storage.db");
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString();
        }
    }

    /// <summary>
    /// Gets the account identifier associated with this storage instance.
    /// </summary>
    public string AccountId { get; }

    /// <summary>
    /// Gets the full path to the SQLite database file, or memory pseudo-path.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets a value indicating whether this DAL instance operates in in-memory mode.
    /// </summary>
    public bool IsInMemory { get; }

    /// <summary>
    /// Gets the SQLite connection string used by this service.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Creates and opens a new SQLite connection configured with WAL and performance pragmas.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An open, configured <see cref="SqliteConnection"/>.</returns>
    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (IsInMemory && _inMemoryMasterConnection == null)
        {
            await _inMemoryInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_inMemoryMasterConnection == null)
                {
                    var master = new SqliteConnection(_connectionString);
                    await master.OpenAsync(cancellationToken).ConfigureAwait(false);
                    await ApplyPragmasAsync(master, cancellationToken).ConfigureAwait(false);
                    _inMemoryMasterConnection = master;
                }
            }
            finally
            {
                _inMemoryInitLock.Release();
            }
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ApplyPragmasAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Executes a non-query command with write serialization and safe UNIQUE conflict suppression.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">Optional dictionary of parameter names and values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An execution result containing rows affected and last inserted row ID.</returns>
    public async Task<SqliteExecutionResult> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var connection = await CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command, parameters);

            try
            {
                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                long lastId = 0;
                await using var idCmd = connection.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                var idObj = await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (idObj is long l)
                {
                    lastId = l;
                }
                else if (idObj is int i)
                {
                    lastId = i;
                }

                return new SqliteExecutionResult
                {
                    LastInsertedId = lastId,
                    RowsAffected = rowsAffected
                };
            }
            catch (SqliteException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Safe UNIQUE constraint handling: matches official Zalo SQLiteHost behavior
                return new SqliteExecutionResult
                {
                    LastInsertedId = 0,
                    RowsAffected = 0
                };
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Executes a non-blocking read query, streaming results to a mapped collection.
    /// Multiple reads can execute concurrently without blocking or being blocked by WAL readers.
    /// </summary>
    /// <typeparam name="T">The mapped entity type.</typeparam>
    /// <param name="sql">The SELECT query statement.</param>
    /// <param name="mapper">A delegate mapping each <see cref="SqliteDataReader"/> row to <typeparamref name="T"/>.</param>
    /// <param name="parameters">Optional dictionary of parameter names and values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of mapped results.</returns>
    public async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(
        string sql,
        Func<SqliteDataReader, T> mapper,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        await using var connection = await CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command, parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(mapper(reader));
        }

        return results;
    }

    /// <summary>
    /// Executes a query returning a single scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar return type.</typeparam>
    /// <param name="sql">The SELECT query statement.</param>
    /// <param name="parameters">Optional dictionary of parameter names and values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The scalar value, or <c>default</c> if null.</returns>
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        await using var connection = await CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command, parameters);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result == null || result is DBNull)
        {
            return default;
        }

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Executes an action within a serialized write transaction, automatically committing or rolling back.
    /// </summary>
    /// <param name="action">The transaction delegate to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous transaction operation.</returns>
    public async Task ExecuteInTransactionAsync(
        Func<SqliteConnection, SqliteTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureNotDisposed();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await action(connection, transaction).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Closes all active connection handles and flushes pending writes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inMemoryMasterConnection != null)
            {
                await _inMemoryMasterConnection.CloseAsync().ConfigureAwait(false);
                await _inMemoryMasterConnection.DisposeAsync().ConfigureAwait(false);
                _inMemoryMasterConnection = null;
            }
        }
        finally
        {
            _writeLock.Release();
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
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _writeLock.Dispose();
        _inMemoryInitLock.Dispose();
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
        _inMemoryMasterConnection?.Dispose();
        _inMemoryMasterConnection = null;
        _writeLock.Dispose();
        _inMemoryInitLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "PRAGMA journal_mode = WAL; " +
            "PRAGMA synchronous = NORMAL; " +
            "PRAGMA busy_timeout = 5000; " +
            "PRAGMA cache_size = -8000; " +
            "PRAGMA foreign_keys = ON;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void BindParameters(SqliteCommand command, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters == null)
        {
            return;
        }

        foreach (var (key, value) in parameters)
        {
            var paramName = key.StartsWith('@') ? key : "@" + key;
            command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }

    private static bool IsUniqueConstraintViolation(SqliteException ex)
    {
        return ex.SqliteErrorCode == SqliteConstraintErrorCode ||
               ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
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

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
