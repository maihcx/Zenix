// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Zenix.Core.Zalo.Storage;

/// <summary>
/// Manages schema initialization and version handshakes for Zalo SQLite storage,
/// ensuring all normalized tables, indexes, and triggers target schema version 400.
/// </summary>
public sealed class ZaloSqliteMigrator : IDisposable
{
    /// <summary>
    /// Gets the required target schema version (400).
    /// </summary>
    public const int TargetVersion = 400;

    private readonly SemaphoreSlim _migrationLock = new(1, 1);
    private string? _cachedScript;
    private bool _isDisposed;

    /// <summary>
    /// Retrieves the current <c>PRAGMA user_version</c> from the given SQLite connection.
    /// </summary>
    /// <param name="connection">The open SQLite connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The current integer user_version value.</returns>
    public async Task<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null && result is not DBNull ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Inspects the database version and executes the master schema DDL if <c>user_version &lt; 400</c>.
    /// </summary>
    /// <param name="connection">The open SQLite connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if schema migration was performed; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Controlled DDL scripts from embedded resources.")]
    public async Task<bool> MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var currentVersion = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (currentVersion >= TargetVersion)
        {
            return false;
        }

        await _migrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check version under lock to ensure idempotency across concurrent callers
            currentVersion = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
            if (currentVersion >= TargetVersion)
            {
                return false;
            }

            var script = await LoadSchemaScriptAsync(cancellationToken).ConfigureAwait(false);

            await using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = script;
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await using (var versionCmd = connection.CreateCommand())
                    {
                        versionCmd.Transaction = transaction;
                        versionCmd.CommandText = $"PRAGMA user_version = {TargetVersion};";
                        await versionCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }

            return true;
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    /// <summary>
    /// Loads the master DDL script from embedded assembly resources or disk.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The SQL script content string.</returns>
    public async Task<string> LoadSchemaScriptAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedScript != null)
        {
            return _cachedScript;
        }

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Zenix.Core.Zalo.Storage.Scripts.Schema_v400.sql";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            _cachedScript = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return _cachedScript;
        }

        // Fallback: look for script on disk relative to app base
        var diskPath = Path.Combine(AppContext.BaseDirectory, "Core", "Zalo", "Storage", "Scripts", "Schema_v400.sql");
        if (File.Exists(diskPath))
        {
            _cachedScript = await File.ReadAllTextAsync(diskPath, cancellationToken).ConfigureAwait(false);
            return _cachedScript;
        }

        // Secondary fallback: look relative to current working directory
        var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "Zenix", "Core", "Zalo", "Storage", "Scripts", "Schema_v400.sql");
        if (File.Exists(cwdPath))
        {
            _cachedScript = await File.ReadAllTextAsync(cwdPath, cancellationToken).ConfigureAwait(false);
            return _cachedScript;
        }

        throw new FileNotFoundException($"Unable to locate required schema resource '{resourceName}' or disk script '{diskPath}'.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _migrationLock.Dispose();
    }
}
