using GhostAndroid.Core.Models;
using Microsoft.Data.Sqlite;

namespace GhostAndroid.Core.Storage;

public interface IHistoryRepository
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<long> AddBackupAsync(BackupRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<BackupRecord>> GetBackupsAsync(CancellationToken ct = default);
    Task DeleteBackupAsync(long id, CancellationToken ct = default);
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);
}

/// <summary>
/// Persistance locale SQLite (aucune donnée envoyée ailleurs). Stocke l'historique
/// des sauvegardes et les préférences. Les données utilisateur sensibles ne sont
/// jamais stockées ici — uniquement des métadonnées.
/// </summary>
public sealed class HistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;

    public HistoryRepository(string databasePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return conn;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS backups (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                file_path    TEXT NOT NULL,
                device_serial TEXT,
                device_model TEXT,
                created_at   TEXT NOT NULL,
                total_bytes  INTEGER NOT NULL,
                file_count   INTEGER NOT NULL,
                encrypted    INTEGER NOT NULL,
                notes        TEXT
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<long> AddBackupAsync(BackupRecord record, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO backups (file_path, device_serial, device_model, created_at, total_bytes, file_count, encrypted, notes)
            VALUES ($path, $serial, $model, $created, $bytes, $count, $enc, $notes);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$path", record.FilePath);
        cmd.Parameters.AddWithValue("$serial", (object?)record.DeviceSerial ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$model", (object?)record.DeviceModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", record.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$bytes", record.TotalBytes);
        cmd.Parameters.AddWithValue("$count", record.FileCount);
        cmd.Parameters.AddWithValue("$enc", record.Encrypted ? 1 : 0);
        cmd.Parameters.AddWithValue("$notes", (object?)record.Notes ?? DBNull.Value);
        var id = (long)(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        return id;
    }

    public async Task<IReadOnlyList<BackupRecord>> GetBackupsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, device_serial, device_model, created_at, total_bytes, file_count, encrypted, notes FROM backups ORDER BY created_at DESC;";
        var list = new List<BackupRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new BackupRecord
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                DeviceSerial = reader.IsDBNull(2) ? null : reader.GetString(2),
                DeviceModel = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(4)),
                TotalBytes = reader.GetInt64(5),
                FileCount = reader.GetInt32(6),
                Encrypted = reader.GetInt32(7) == 1,
                Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
            });
        }
        return list;
    }

    public async Task DeleteBackupAsync(long id, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM backups WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
