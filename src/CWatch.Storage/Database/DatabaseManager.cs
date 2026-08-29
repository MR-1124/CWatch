using Microsoft.Data.Sqlite;
using CWatch.Core.Interfaces;

namespace CWatch.Storage.Database;

public sealed class DatabaseManager
{
    private readonly string _connectionString;
    private readonly ILoggerService? _logger;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public DatabaseManager(string? customDbPath = null, ILoggerService? logger = null)
    {
        _logger = logger;
        string dbPath = customDbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CWatch", "cwatch.db");

        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 10,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        _connectionString = builder.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeSchemaAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;

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
            ";

            await cmd.ExecuteNonQueryAsync();
            _logger?.LogInfo("SQLite database initialized successfully.");
        }
        finally
        {
            _initLock.Release();
        }
    }
}
