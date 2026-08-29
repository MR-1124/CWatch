using System.Globalization;
using Microsoft.Data.Sqlite;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Storage.Database;

namespace CWatch.Storage.Repositories;

public sealed class SnapshotRepository : ISnapshotRepository
{
    private readonly DatabaseManager _dbManager;
    private readonly ILoggerService? _logger;

    public SnapshotRepository(DatabaseManager dbManager, ILoggerService? logger = null)
    {
        _dbManager = dbManager;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _dbManager.InitializeSchemaAsync();
    }

    public async Task<long> SaveSnapshotAsync(StorageSnapshot snapshot)
    {
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO snapshots (timestamp_utc, drive_letter, total_bytes, free_bytes, categories_json, top_items_json, notes)
                VALUES ($time, $drive, $total, $free, $cats, $items, $notes);
                SELECT last_insert_rowid();
            ";

            cmd.Parameters.AddWithValue("$time", snapshot.TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$drive", snapshot.DriveLetter);
            cmd.Parameters.AddWithValue("$total", snapshot.TotalBytes);
            cmd.Parameters.AddWithValue("$free", snapshot.FreeBytes);
            cmd.Parameters.AddWithValue("$cats", snapshot.CategoriesJson ?? "[]");
            cmd.Parameters.AddWithValue("$items", snapshot.TopItemsJson ?? "[]");
            cmd.Parameters.AddWithValue("$notes", (object?)snapshot.Notes ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            long newId = result != null ? (long)result : 0;
            snapshot.Id = newId;
            _logger?.LogInfo($"Snapshot #{newId} saved for drive {snapshot.DriveLetter}. Free: {snapshot.FormattedFree}");
            return newId;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to save snapshot.", ex);
            return 0;
        }
    }

    public async Task<StorageSnapshot?> GetLatestSnapshotAsync(string driveLetter = "C:")
    {
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, timestamp_utc, drive_letter, total_bytes, free_bytes, categories_json, top_items_json, notes
                FROM snapshots
                WHERE drive_letter = $drive
                ORDER BY timestamp_utc DESC
                LIMIT 1;
            ";
            cmd.Parameters.AddWithValue("$drive", driveLetter);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapSnapshot(reader);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to read latest snapshot.", ex);
        }

        return null;
    }

    public async Task<List<StorageSnapshot>> GetSnapshotsAsync(string driveLetter, DateTime fromUtc, DateTime toUtc)
    {
        var list = new List<StorageSnapshot>();
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, timestamp_utc, drive_letter, total_bytes, free_bytes, categories_json, top_items_json, notes
                FROM snapshots
                WHERE drive_letter = $drive
                  AND timestamp_utc >= $from
                  AND timestamp_utc <= $to
                ORDER BY timestamp_utc ASC;
            ";
            cmd.Parameters.AddWithValue("$drive", driveLetter);
            cmd.Parameters.AddWithValue("$from", fromUtc.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$to", toUtc.ToString("o", CultureInfo.InvariantCulture));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapSnapshot(reader));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to query snapshots in range.", ex);
        }

        return list;
    }

    public async Task<List<StorageSnapshot>> GetAllSnapshotsAsync(string driveLetter, int limit = 300)
    {
        var list = new List<StorageSnapshot>();
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, timestamp_utc, drive_letter, total_bytes, free_bytes, categories_json, top_items_json, notes
                FROM snapshots
                WHERE drive_letter = $drive
                ORDER BY timestamp_utc DESC
                LIMIT $limit;
            ";
            cmd.Parameters.AddWithValue("$drive", driveLetter);
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapSnapshot(reader));
            }

            // Return in chronological order
            list.Reverse();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to query all snapshots.", ex);
        }

        return list;
    }

    public async Task RecordCleanHistoryAsync(CleanHistoryItem historyItem)
    {
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO clean_history (cleaned_utc, provider_id, target_path, bytes_cleaned, category_name)
                VALUES ($time, $prov, $path, $bytes, $cat);
            ";
            cmd.Parameters.AddWithValue("$time", historyItem.CleanedUtc.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$prov", historyItem.ProviderId);
            cmd.Parameters.AddWithValue("$path", historyItem.TargetPath);
            cmd.Parameters.AddWithValue("$bytes", historyItem.BytesCleaned);
            cmd.Parameters.AddWithValue("$cat", historyItem.CategoryName);

            await cmd.ExecuteNonQueryAsync();
            _logger?.LogInfo($"Clean history recorded for {historyItem.TargetPath} ({ByteSizeFormatter.Format(historyItem.BytesCleaned)})");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to record clean history.", ex);
        }
    }

    public async Task<List<CleanHistoryItem>> GetCleanHistoryAsync(int limit = 100)
    {
        var list = new List<CleanHistoryItem>();
        try
        {
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, cleaned_utc, provider_id, target_path, bytes_cleaned, category_name
                FROM clean_history
                ORDER BY cleaned_utc DESC
                LIMIT $limit;
            ";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CleanHistoryItem
                {
                    Id = reader.GetInt64(0),
                    CleanedUtc = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                    ProviderId = reader.GetString(2),
                    TargetPath = reader.GetString(3),
                    BytesCleaned = reader.GetInt64(4),
                    CategoryName = reader.GetString(5)
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to query clean history.", ex);
        }

        return list;
    }

    public async Task PruneOldSnapshotsAsync(int retentionDays)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            using var conn = _dbManager.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM snapshots WHERE timestamp_utc < $cutoff;
                DELETE FROM clean_history WHERE cleaned_utc < $cutoff;
            ";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("o", CultureInfo.InvariantCulture));

            int deleted = await cmd.ExecuteNonQueryAsync();
            _logger?.LogInfo($"Pruned {deleted} old records older than {cutoff:yyyy-MM-dd}.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to prune old snapshots.", ex);
        }
    }

    private static StorageSnapshot MapSnapshot(SqliteDataReader reader)
    {
        return new StorageSnapshot
        {
            Id = reader.GetInt64(0),
            TimestampUtc = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            DriveLetter = reader.GetString(2),
            TotalBytes = reader.GetInt64(3),
            FreeBytes = reader.GetInt64(4),
            CategoriesJson = reader.IsDBNull(5) ? "[]" : reader.GetString(5),
            TopItemsJson = reader.IsDBNull(6) ? "[]" : reader.GetString(6),
            Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
