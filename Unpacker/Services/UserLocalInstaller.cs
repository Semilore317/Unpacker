using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Unpacker.Services;

public sealed class UserLocalInstaller
{
    private readonly string _localApplicationDataDir;
    private readonly string _userProfileDir;

    public UserLocalInstaller(
        string? localApplicationDataDir = null,
        string? userProfileDir = null
    )
    {
        _localApplicationDataDir =
            localApplicationDataDir
            ?? Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

        _userProfileDir =
            userProfileDir
            ?? Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile
            );
    }

    public async Task InstallAsync(
        string sourceDir,
        string binaryPath,
        string appName,
        string displayName,
        string? iconPath
    )
    {
        string targetDir =
            Path.Combine(
                _localApplicationDataDir,
                appName
            );

        string binDir =
            Path.Combine(
                _userProfileDir,
                ".local",
                "bin"
            );

        string desktopDir =
            Path.Combine(
                _userProfileDir,
                ".local",
                "share",
                "applications"
            );

        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(desktopDir);

        CopyDirectoryContents(
            sourceDir,
            targetDir,
            skipDotEntries: true
        );

        string relativeBinaryPath =
            Path.GetRelativePath(
                sourceDir,
                binaryPath
            );

        string installedBinaryPath =
            Path.Combine(
                targetDir,
                relativeBinaryPath
            );

        string launcherPath =
            Path.Combine(
                binDir,
                appName
            );

        // Match the previous `ln -sf` behaviour on reinstall.
        DeleteExistingEntry(launcherPath);

        File.CreateSymbolicLink(
            launcherPath,
            installedBinaryPath
        );

        string iconName = "";

        if (
            !string.IsNullOrEmpty(iconPath) &&
            File.Exists(iconPath)
        )
        {
            string iconExtension =
                Path.GetExtension(iconPath);

            string installedIconPath =
                Path.Combine(
                    targetDir,
                    $"icon{iconExtension}"
                );

            File.Copy(
                iconPath,
                installedIconPath,
                overwrite: true
            );

            iconName = installedIconPath;
        }

        string desktopFilePath =
            Path.Combine(
                desktopDir,
                $"{appName}.desktop"
            );

        await CreateDesktopFileAsync(
            desktopFilePath,
            displayName,
            launcherPath,
            iconName
        );
    }

    private static void CopyDirectoryContents(
        string sourceDir,
        string destDir,
        bool skipDotEntries = false
    )
    {
        Directory.CreateDirectory(destDir);

        var directory =
            new DirectoryInfo(sourceDir);

        foreach (
            FileSystemInfo entry
            in directory.EnumerateFileSystemInfos()
        )
        {
            // The previous shell implementation copied sourceDir/*,
            // which excluded top-level dotfiles.
            if (
                skipDotEntries &&
                entry.Name.StartsWith(
                    ".",
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            string destPath =
                Path.Combine(
                    destDir,
                    entry.Name
                );

            // Preserve symlinks instead of following them while copying.
            if (entry.LinkTarget is not null)
            {
                DeleteExistingEntry(destPath);

                if (entry is DirectoryInfo)
                {
                    Directory.CreateSymbolicLink(
                        destPath,
                        entry.LinkTarget
                    );
                }
                else
                {
                    File.CreateSymbolicLink(
                        destPath,
                        entry.LinkTarget
                    );
                }

                continue;
            }

            if (entry is DirectoryInfo subDir)
            {
                CopyDirectoryContents(
                    subDir.FullName,
                    destPath
                );

                continue;
            }

            if (entry is FileInfo file)
            {
                file.CopyTo(
                    destPath,
                    overwrite: true
                );
            }
        }
    }

    private static void DeleteExistingEntry(
        string path
    )
    {
        var file =
            new FileInfo(path);

        if (
            file.Exists ||
            file.LinkTarget is not null
        )
        {
            file.Delete();
            return;
        }

        var directory =
            new DirectoryInfo(path);

        if (
            directory.Exists ||
            directory.LinkTarget is not null
        )
        {
            directory.Delete(
                recursive: true
            );
        }
    }

    private static Task CreateDesktopFileAsync(
        string path,
        string displayName,
        string execPath,
        string iconName = ""
    )
    {
        var lines =
            new List<string>
            {
                "[Desktop Entry]",
                $"Name={displayName}",
                $"Exec={execPath}"
            };

        if (!string.IsNullOrEmpty(iconName))
        {
            lines.Add(
                $"Icon={iconName}"
            );
        }

        lines.Add("Type=Application");
        lines.Add("Categories=Utility;");
        lines.Add("Terminal=false");

        string contents =
            string.Join(
                Environment.NewLine,
                lines
            ) + Environment.NewLine;

        return File.WriteAllTextAsync(
            path,
            contents
        );
    }
}