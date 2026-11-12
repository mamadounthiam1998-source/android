using System.Text;
using GhostAndroid.Core.Backup;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.Security;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class BackupRoundTripTests
{
    private static BackupService NewService() =>
        new(new FakeAdbEngine(), new GcmFileCrypto(iterations: 10_000));

    private static RestoreService NewRestore() =>
        new(new GcmFileCrypto(iterations: 10_000));

    private static string CreateSampleTree(out string[] relativePaths)
    {
        var root = Directory.CreateTempSubdirectory("ghost_src_").FullName;
        Directory.CreateDirectory(Path.Combine(root, "Photos"));
        Directory.CreateDirectory(Path.Combine(root, "Documents", "sub"));
        File.WriteAllText(Path.Combine(root, "Photos", "a.txt"), "photo A contenu");
        File.WriteAllBytes(Path.Combine(root, "Photos", "b.bin"), new byte[] { 1, 2, 3, 4, 5 });
        File.WriteAllText(Path.Combine(root, "Documents", "sub", "c.txt"), "document C");
        relativePaths = new[] { "Photos/a.txt", "Photos/b.bin", "Documents/sub/c.txt" };
        return root;
    }

    [Fact]
    public async Task Archive_Then_Extract_RoundTrips_WithIntegrity()
    {
        var src = CreateSampleTree(out var rels);
        var archive = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + BackupService.FileExtension);
        var dest = Directory.CreateTempSubdirectory("ghost_dst_").FullName;
        const string password = "coffre-fort-123";

        try
        {
            var manifest = new BackupManifest { DeviceModel = "Test Device", DeviceSerial = "SER123" };
            await NewService().ArchiveFolderAsync(src, archive, password, manifest);

            Assert.True(File.Exists(archive));
            Assert.Equal(3, manifest.FileCount);
            Assert.All(manifest.Entries, e => Assert.False(string.IsNullOrEmpty(e.Sha256)));

            var result = await NewRestore().ExtractArchiveAsync(archive, dest, password);

            Assert.True(result.Success, result.Error);
            Assert.Equal(3, result.FilesExtracted);
            Assert.True(result.IntegrityVerified);
            Assert.NotNull(result.Manifest);
            Assert.Equal("SER123", result.Manifest!.DeviceSerial);

            foreach (var rel in rels)
                Assert.True(File.Exists(Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar))), rel);

            Assert.Equal("photo A contenu",
                File.ReadAllText(Path.Combine(dest, "Photos", "a.txt")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dest, true);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    [Fact]
    public async Task Manifest_RecordsHonestLimitations()
    {
        var src = CreateSampleTree(out _);
        var archive = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + BackupService.FileExtension);
        try
        {
            var manifest = new BackupManifest
            {
                NotIncludedNotes = BackupSources.KnownLimitations.ToList()
            };
            await NewService().ArchiveFolderAsync(src, archive, "pw", manifest);

            var read = await NewRestore().ReadManifestAsync(archive, "pw");
            Assert.NotNull(read);
            Assert.NotEmpty(read!.NotIncludedNotes);
            Assert.Contains(read.NotIncludedNotes, n => n.Contains("SMS"));
            Assert.Equal("AES-256-GCM", read.EncryptionAlgorithm);
        }
        finally
        {
            Directory.Delete(src, true);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    [Fact]
    public async Task Extract_WithWrongPassword_Fails()
    {
        var src = CreateSampleTree(out _);
        var archive = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + BackupService.FileExtension);
        var dest = Directory.CreateTempSubdirectory("ghost_dst_").FullName;
        try
        {
            await NewService().ArchiveFolderAsync(src, archive, "bonMotDePasse", new BackupManifest());
            var result = await NewRestore().ExtractArchiveAsync(archive, dest, "mauvais");
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dest, true);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    [Fact]
    public async Task VerifyIntegrity_DetectsCorruptedFile()
    {
        var src = CreateSampleTree(out _);
        var archive = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + BackupService.FileExtension);
        var dest = Directory.CreateTempSubdirectory("ghost_dst_").FullName;
        try
        {
            var manifest = new BackupManifest();
            await NewService().ArchiveFolderAsync(src, archive, "pw", manifest);
            var result = await NewRestore().ExtractArchiveAsync(archive, dest, "pw");
            Assert.True(result.IntegrityVerified);

            // Corrompt un fichier extrait puis revérifie par rapport au manifeste.
            File.WriteAllText(Path.Combine(dest, "Photos", "a.txt"), "contenu modifié");
            var issues = await RestoreService.VerifyIntegrityAsync(dest, result.Manifest!);
            Assert.Contains(issues, i => i.RelativePath == "Photos/a.txt");
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dest, true);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }
}
