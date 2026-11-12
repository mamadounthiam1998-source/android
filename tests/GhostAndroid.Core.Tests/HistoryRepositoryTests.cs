using GhostAndroid.Core.Models;
using GhostAndroid.Core.Storage;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class HistoryRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly HistoryRepository _repo;

    public HistoryRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ghost_test_{Guid.NewGuid():N}.db");
        _repo = new HistoryRepository(_dbPath);
    }

    [Fact]
    public async Task AddAndGet_Backups()
    {
        await _repo.InitializeAsync();

        var id = await _repo.AddBackupAsync(new BackupRecord
        {
            FilePath = "/backups/test.ghostbak",
            DeviceSerial = "SER1",
            DeviceModel = "Pixel 7",
            TotalBytes = 123456,
            FileCount = 10,
            Encrypted = true,
            Notes = "sauvegarde sélective",
        });

        Assert.True(id > 0);

        var all = await _repo.GetBackupsAsync();
        Assert.Single(all);
        Assert.Equal("Pixel 7", all[0].DeviceModel);
        Assert.Equal(123456, all[0].TotalBytes);
        Assert.True(all[0].Encrypted);
    }

    [Fact]
    public async Task Delete_RemovesBackup()
    {
        await _repo.InitializeAsync();
        var id = await _repo.AddBackupAsync(new BackupRecord { FilePath = "x.ghostbak", TotalBytes = 1, FileCount = 1, Encrypted = true });
        await _repo.DeleteBackupAsync(id);
        Assert.Empty(await _repo.GetBackupsAsync());
    }

    [Fact]
    public async Task Settings_UpsertAndRead()
    {
        await _repo.InitializeAsync();

        Assert.Null(await _repo.GetSettingAsync("theme"));

        await _repo.SetSettingAsync("theme", "dark");
        Assert.Equal("dark", await _repo.GetSettingAsync("theme"));

        await _repo.SetSettingAsync("theme", "cyan"); // upsert
        Assert.Equal("cyan", await _repo.GetSettingAsync("theme"));
    }

    [Fact]
    public async Task Backups_OrderedByDateDescending()
    {
        await _repo.InitializeAsync();
        await _repo.AddBackupAsync(new BackupRecord { FilePath = "old", CreatedAt = DateTimeOffset.Now.AddDays(-2), TotalBytes = 1, FileCount = 1, Encrypted = true });
        await _repo.AddBackupAsync(new BackupRecord { FilePath = "new", CreatedAt = DateTimeOffset.Now, TotalBytes = 1, FileCount = 1, Encrypted = true });

        var all = await _repo.GetBackupsAsync();
        Assert.Equal("new", all[0].FilePath);
        Assert.Equal("old", all[1].FilePath);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
