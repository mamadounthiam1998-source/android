using GhostAndroid.Core.PlatformTools;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class PlatformToolsLocatorTests
{
    [Fact]
    public void FileNames_MatchPlatform()
    {
        if (PlatformToolsLocator.IsWindows)
        {
            Assert.Equal("adb.exe", PlatformToolsLocator.AdbFileName);
            Assert.Equal("fastboot.exe", PlatformToolsLocator.FastbootFileName);
        }
        else
        {
            Assert.Equal("adb", PlatformToolsLocator.AdbFileName);
            Assert.Equal("fastboot", PlatformToolsLocator.FastbootFileName);
        }
    }

    [Fact]
    public void CandidateDirectories_IncludeExplicitFirst()
    {
        var locator = new PlatformToolsLocator("/opt/custom/platform-tools");
        var dirs = locator.CandidateDirectories().ToList();
        Assert.Equal("/opt/custom/platform-tools", dirs[0]);
    }

    [Fact]
    public void Find_ReturnsPath_WhenExecutablePresent()
    {
        // Crée un faux exécutable adb dans un dossier temporaire et vérifie qu'il est trouvé.
        var dir = Directory.CreateTempSubdirectory("ghost_tools_");
        try
        {
            var exe = Path.Combine(dir.FullName, PlatformToolsLocator.AdbFileName);
            File.WriteAllText(exe, "#!/bin/sh\necho test");

            var locator = new PlatformToolsLocator(dir.FullName);
            Assert.True(locator.AdbAvailable);
            Assert.Equal(exe, locator.FindAdb());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Find_ReturnsNull_WhenAbsent()
    {
        var locator = new PlatformToolsLocator("/nonexistent/path/ghost-xyz");
        // On ne peut garantir l'absence sur le PATH de la machine de test, donc on
        // vérifie seulement que le dossier explicite inexistant ne produit pas de faux positif.
        Assert.Null(locator.FindAdb() is { } p && p.StartsWith("/nonexistent") ? p : null);
    }
}
