using Unpacker.Services;

namespace Unpacker.Tests.Services;

public sealed class UserLocalInstallerTests
{
    [Fact]
    public async Task InstallAsync_CreatesRequiredDirectories()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(sourceDir);

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                Path.Combine(sourceDir, "app"),
                "test-app",
                "Test App",
                null
            );

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        localDataDir,
                        "test-app"
                    )
                )
            );

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        userProfileDir,
                        ".local",
                        "bin"
                    )
                )
            );

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        userProfileDir,
                        ".local",
                        "share",
                        "applications"
                    )
                )
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_CopiesDirectoryContentsRecursively()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string nestedDir =
                Path.Combine(
                    sourceDir,
                    "assets"
                );

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(nestedDir);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sourceDir,
                    "app"
                ),
                "binary"
            );

            await File.WriteAllTextAsync(
                Path.Combine(
                    nestedDir,
                    "config.json"
                ),
                "{}"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                Path.Combine(sourceDir, "app"),
                "test-app",
                "Test App",
                null
            );

            string targetDir =
                Path.Combine(
                    localDataDir,
                    "test-app"
                );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        targetDir,
                        "app"
                    )
                )
            );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        targetDir,
                        "assets",
                        "config.json"
                    )
                )
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_SkipsTopLevelDotEntries()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(sourceDir);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sourceDir,
                    ".hidden"
                ),
                "hidden"
            );

            await File.WriteAllTextAsync(
                Path.Combine(
                    sourceDir,
                    "visible"
                ),
                "visible"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                Path.Combine(sourceDir, "visible"),
                "test-app",
                "Test App",
                null
            );

            string targetDir =
                Path.Combine(
                    localDataDir,
                    "test-app"
                );

            Assert.False(
                File.Exists(
                    Path.Combine(
                        targetDir,
                        ".hidden"
                    )
                )
            );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        targetDir,
                        "visible"
                    )
                )
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_PreservesNestedDotEntries()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string nestedDir =
                Path.Combine(
                    sourceDir,
                    "data"
                );

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(nestedDir);

            await File.WriteAllTextAsync(
                Path.Combine(
                    nestedDir,
                    ".config"
                ),
                "config"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                Path.Combine(sourceDir, "app"),
                "test-app",
                "Test App",
                null
            );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        localDataDir,
                        "test-app",
                        "data",
                        ".config"
                    )
                )
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_OverwritesExistingFiles()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(sourceDir);

            string sourceFile =
                Path.Combine(
                    sourceDir,
                    "app"
                );

            await File.WriteAllTextAsync(
                sourceFile,
                "version-one"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                sourceFile,
                "test-app",
                "Test App",
                null
            );

            await File.WriteAllTextAsync(
                sourceFile,
                "version-two"
            );

            await installer.InstallAsync(
                sourceDir,
                sourceFile,
                "test-app",
                "Test App",
                null
            );

            string installedFile =
                Path.Combine(
                    localDataDir,
                    "test-app",
                    "app"
                );

            Assert.Equal(
                "version-two",
                await File.ReadAllTextAsync(
                    installedFile
                )
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_PreservesFileSymlinks()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(tempDir, "source");

            string localDataDir =
                Path.Combine(tempDir, "local-data");

            string userProfileDir =
                Path.Combine(tempDir, "home");

            Directory.CreateDirectory(sourceDir);

            string targetFile =
                Path.Combine(
                    sourceDir,
                    "real-file"
                );

            await File.WriteAllTextAsync(
                targetFile,
                "content"
            );

            string linkPath =
                Path.Combine(
                    sourceDir,
                    "linked-file"
                );

            File.CreateSymbolicLink(
                linkPath,
                "real-file"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                targetFile,
                "test-app",
                "Test App",
                null
            );

            string installedLink =
                Path.Combine(
                    localDataDir,
                    "test-app",
                    "linked-file"
                );

            var linkInfo =
                new FileInfo(installedLink);

            Assert.Equal(
                "real-file",
                linkInfo.LinkTarget
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_CreatesLauncherSymbolicLink()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir = Path.Combine(tempDir, "source");
            string nestedDir = Path.Combine(sourceDir, "bin");
            string localDataDir = Path.Combine(tempDir, "local-data");
            string userProfileDir = Path.Combine(tempDir, "user-profile");

            Directory.CreateDirectory(nestedDir);

            string binaryPath = Path.Combine(nestedDir, "test-app");
            await File.WriteAllTextAsync(binaryPath, "binary");

            var installer = new UserLocalInstaller(localDataDir, userProfileDir);
            await installer.InstallAsync(
                sourceDir, binaryPath, "test-app", "Test App", null
            );

            string launcherPath = Path.Combine(userProfileDir, ".local", "bin", "test-app");
            var launcher = new FileInfo(launcherPath);

            Assert.Equal(
                Path.Combine(localDataDir, "test-app", "bin", "test-app"),
                launcher.LinkTarget
            );
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_ReplacesExistingLauncher()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string sourceDir =
                Path.Combine(
                    tempDir,
                    "source"
                );

            string localDataDir =
                Path.Combine(
                    tempDir,
                    "local-data"
                );

            string userProfileDir =
                Path.Combine(
                    tempDir,
                    "user-profile"
                );

            Directory.CreateDirectory(sourceDir);

            string binaryPath =
                Path.Combine(
                    sourceDir,
                    "test-app"
                );

            await File.WriteAllTextAsync(
                binaryPath,
                "binary"
            );

            string binDir =
                Path.Combine(
                    userProfileDir,
                    ".local",
                    "bin"
                );

            Directory.CreateDirectory(binDir);

            string launcherPath =
                Path.Combine(
                    binDir,
                    "test-app"
                );

            // Simulate an existing launcher from an earlier install.
            await File.WriteAllTextAsync(
                launcherPath,
                "old launcher"
            );

            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );

            await installer.InstallAsync(
                sourceDir,
                binaryPath,
                "test-app",
                "Test App",
                null
            );

            var launcher =
                new FileInfo(launcherPath);

            Assert.Equal(
                Path.Combine(
                    localDataDir,
                    "test-app",
                    "test-app"
                ),
                launcher.LinkTarget
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    [Fact]
    public async Task InstallAsync_CreatesDesktopFile()
    {
        string tempDir = CreateTempDirectory();
    
        try
        {
            string sourceDir = Path.Combine(tempDir, "source");
            string localDataDir = Path.Combine(tempDir, "local-data");
            string userProfileDir = Path.Combine(tempDir, "user-profile");
    
            Directory.CreateDirectory(sourceDir);
    
            string binaryPath =
                Path.Combine(sourceDir, "test-app");
    
            await File.WriteAllTextAsync(
                binaryPath,
                "binary"
            );
    
            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );
    
            await installer.InstallAsync(
                sourceDir,
                binaryPath,
                "test-app",
                "Test App",
                null
            );
    
            string desktopPath =
                Path.Combine(
                    userProfileDir,
                    ".local",
                    "share",
                    "applications",
                    "test-app.desktop"
                );
    
            string contents =
                await File.ReadAllTextAsync(
                    desktopPath
                );
    
            string launcherPath =
                Path.Combine(
                    userProfileDir,
                    ".local",
                    "bin",
                    "test-app"
                );
    
            Assert.Contains(
                "Name=Test App",
                contents
            );
    
            Assert.Contains(
                $"Exec={launcherPath}",
                contents
            );
    
            Assert.DoesNotContain(
                "Icon=",
                contents
            );
    
            Assert.Contains(
                "Type=Application",
                contents
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }
    
    [Fact]
    public async Task InstallAsync_CopiesIconAndReferencesItInDesktopFile()
    {
        string tempDir = CreateTempDirectory();
    
        try
        {
            string sourceDir = Path.Combine(tempDir, "source");
            string localDataDir = Path.Combine(tempDir, "local-data");
            string userProfileDir = Path.Combine(tempDir, "user-profile");
    
            Directory.CreateDirectory(sourceDir);
    
            string binaryPath =
                Path.Combine(sourceDir, "test-app");
    
            await File.WriteAllTextAsync(
                binaryPath,
                "binary"
            );
    
            string iconPath =
                Path.Combine(
                    tempDir,
                    "selected-icon.png"
                );
    
            await File.WriteAllTextAsync(
                iconPath,
                "fake icon"
            );
    
            var installer =
                new UserLocalInstaller(
                    localDataDir,
                    userProfileDir
                );
    
            await installer.InstallAsync(
                sourceDir,
                binaryPath,
                "test-app",
                "Test App",
                iconPath
            );
    
            string installedIconPath =
                Path.Combine(
                    localDataDir,
                    "test-app",
                    "icon.png"
                );
    
            Assert.True(
                File.Exists(installedIconPath)
            );
    
            string desktopPath =
                Path.Combine(
                    userProfileDir,
                    ".local",
                    "share",
                    "applications",
                    "test-app.desktop"
                );
    
            string contents =
                await File.ReadAllTextAsync(
                    desktopPath
                );
    
            Assert.Contains(
                $"Icon={installedIconPath}",
                contents
            );
        }
        finally
        {
            Directory.Delete(
                tempDir,
                recursive: true
            );
        }
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "UnpackerTests_" + Guid.NewGuid()
            );

        Directory.CreateDirectory(path);

        return path;
    }
}
