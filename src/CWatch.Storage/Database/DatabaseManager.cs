using Microsoft.Data.Sqlite;
using CWatch.Core.Interfaces;

namespace CWatch.Storage.Database;

/// <summary>
/// Owns the SQLite database file, pragmas, and schema versioning.
///
/// Schema history:
///   v1 - snapshots + clean_history tables (the original schema).
///
/// To change the schema: bump <see cref="CurrentSchemaVersion"/>, add a
/// MigrateToVersionNAsync step, and call it from the migration chain below.
/// Every step must tolerate an empty or partially-created database.
/// </summary>
public sealed class DatabaseManager
{
    /// <summary>Latest schema version this application understands.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly ILoggerService? _logger;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public DatabaseManager(string? customDbPath = null, ILoggerService? logger = null)
    {
        _logger = logger;
        _dbPath = customDbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CWatch", "cwatch.db");

        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 10,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        _connectionString = builder.ToString();
    }

    public string DatabasePath => _dbPath;

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeSchemaAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            await ExecuteAsync(conn, "PRAGMA journal_mode = WAL;");
            await ExecuteAsync(conn, "PRAGMA synchronous = NORMAL;");
            await ExecuteAsync(conn, "PRAGMA busy_timeout = 5000;");

            int userVersion = await GetUserVersionAsync(conn);

            if (userVersion > CurrentSchemaVersion)
            {
                // Never mutate a database written by a newer schema.
                throw new InvalidOperationException(
                    $"Database schema version {userVersion} is newer than this application supports (v{CurrentSchemaVersion}). " +
                    "Update C:Watch before opening this database.");
            }

            // Migration chain: each step runs only when the stored version is older.
            if (userVersion < 1) await MigrateToVersion1Async(conn);
            // if (userVersion < 2) await MigrateToVersion2Async(conn);

            if (userVersion != CurrentSchemaVersion)
            {
                await SetUserVersionAsync(conn, CurrentSchemaVersion);
                _logger?.LogInfo($"Database schema migrated from v{userVersion} to v{CurrentSchemaVersion}.");
            }
            else
            {
                _logger?.LogInfo($"SQLite database initialized (schema v{CurrentSchemaVersion}).");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ---------------- Migration steps ----------------

    private static async Task MigrateToVersion1Async(SqliteConnection conn)
    {
        // Idempotent base schema. Also adopts legacy databases that were
        // created before user_version existed (they report version 0).
        await ExecuteAsync(conn, @"
                CREATE TABLE IF NOT EXISTS snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    drive_letter TEXT NOT NULL,
                    total_bytes INTEGER NOT NULL,
                    free_bytes INTEGER NOT NULL,
                    categories_json TEXT NOT NULL,
                    top_items_json TEXT NOT NULL,
                    notes TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_snapshots_drive_time ON snapshots (drive_letter, timestamp_utc);

                CREATE TABLE IF NOT EXISTS clean_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    cleaned_utc TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    target_path TEXT NOT NULL,
                    bytes_cleaned INTEGER NOT NULL,
                    category_name TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_clean_history_time ON clean_history (cleaned_utc);
            ");
    }

    // private static async Task MigrateToVersion2Async(SqliteConnection conn) { ... }

    // ---------------- Helpers ----------------

    private static async Task<int> GetUserVersionAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync();
        return result is long v ? (int)v : 0;
    }

    private static async Task SetUserVersionAsync(SqliteConnection conn, int version)
    {
        // PRAGMA values cannot be parameterized; 'version' is an internal int constant.
        await ExecuteAsync(conn, $"PRAGMA user_version = {version};");
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string commandText)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        await cmd.ExecuteNonQueryAsync();
    }
}
