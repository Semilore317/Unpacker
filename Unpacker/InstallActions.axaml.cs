using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Unpacker;

public partial class InstallActions : UserControl
{
    public InstallActions()
    {
        InitializeComponent();
        DataContext = this;
    }
    
    // Source Path
    public static readonly StyledProperty<string> SourcePathProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(SourcePath), "");

    public string SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    // system wide toggle ( install to opt)
    public static readonly StyledProperty<bool> IsSystemWideProperty =
        AvaloniaProperty.Register<InstallActions, bool>(nameof(IsSystemWide), true);

    public bool IsSystemWide
    {
        get => GetValue(IsSystemWideProperty);
        set 
        { 
            SetValue(IsSystemWideProperty, value);
            
            // FIX: Manually update the other properties when this changes
            InstallButtonText = value ? "Install (Admin)" : "Install";
            ShieldOpacity = value ? 1.0 : 0.0;
        }
    }
    
    // install button text
    public static readonly StyledProperty<string> InstallButtonTextProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(InstallButtonText), "Install (Admin)");

    public string InstallButtonText
    {
        get => GetValue(InstallButtonTextProperty);
        set => SetValue(InstallButtonTextProperty, value);
    }

    // Shield Opacity
    public static readonly StyledProperty<double> ShieldOpacityProperty =
        AvaloniaProperty.Register<InstallActions, double>(nameof(ShieldOpacity), 1.0);

    public double ShieldOpacity
    {
        get => GetValue(ShieldOpacityProperty);
        set => SetValue(ShieldOpacityProperty, value);
    }

    // EVENTS
    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        // abstraction for x11/wayland topLevel handling
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Archive to Install",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Archive Files")
                {
                    Patterns = new[] { "*.tar.gz", "*.zip", "*.tar.xz", "*.tar", "*.7z", "*.rar" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            SourcePath = files[0].Path.LocalPath;
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath)) return;

        // Visual feedback could be added here (disable button, show spinner)

        string tempDir = Path.Combine(Path.GetTempPath(), "Unpacker_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Extract
            await ExtractArchive(SourcePath, tempDir);

            // 2. Detect Executable
            string? exePath = DetectExecutable(tempDir);
            if (exePath == null)
            {
                // Show detection error
                // For CLI prototype we just log, in real app show dialog
                Debug.WriteLine("No executable found!"); 
                return;
            }

            string appName = Path.GetFileNameWithoutExtension(SourcePath);
            // Handle .tar.gz double extension
            if (appName.EndsWith(".tar")) appName = Path.GetFileNameWithoutExtension(appName);
            // Sanitize
            appName = new string(appName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (string.IsNullOrEmpty(appName)) appName = "unpacked-app";
            
            // 3. Install
            string installScript = GenerateInstallScript(tempDir, exePath, appName, IsSystemWide);
            
            string scriptPath = Path.Combine(tempDir, "install.sh");
            await File.WriteAllTextAsync(scriptPath, installScript);
            
            // chmod +x
            File.SetUnixFileMode(scriptPath, File.GetUnixFileMode(scriptPath) | UnixFileMode.UserExecute);

            if (IsSystemWide)
            {
                Process.Start("pkexec", $"bash \"{scriptPath}\" ");
            }
            else
            {
                Process.Start("bash", $"\"{scriptPath}\" ");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}");
        }
    }

    private Task ExtractArchive(string archivePath, string destDir)
    {
        return Task.Run(() =>
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destDir);
            }
            else
            {
                // Use system tar for gz, xz, bz2, etc.
                var psi = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xf \"{archivePath}\" -C \"{destDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p?.WaitForExit();
            }
        });
    }

    private string? DetectExecutable(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        
        // Strategy: Find ELF files
        var elfFiles = new List<string>();
        foreach (var file in files)
        {
            if (IsElf(file)) elfFiles.Add(file);
        }

        if (elfFiles.Count == 0) return null;
        if (elfFiles.Count == 1) return elfFiles[0];

        // Heuristics if multiple ELF files
        
        // 1. Prefer files named after the folder or known variations
        var dirName = Path.GetFileName(dir);
        // If dir is temp, this heuristic is weak. We extracted to 'Unpacker_GUID'.
        // But maybe there is a top-level folder inside.
        
        // 2. Look for largest executable (often the main binary vs helpers)
        return elfFiles.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }

    private bool IsElf(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[4];
            if (fs.Read(buffer, 0, 4) < 4) return false;
            // 0x7F E L F
            return buffer[0] == 0x7F && buffer[1] == 0x45 && buffer[2] == 0x4C && buffer[3] == 0x46;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateInstallScript(string sourceDir, string exePath, string appName, bool systemWide)
    {
        // sourceDir is likely the temp folder. 
        // We want to copy the CONTENTS of the extracted archive.
        // If the archive had a single root folder, we might want to copy that folder.
        // But simplifying: copy everything from sourceDir to targetDir
        
        string targetDir = systemWide ? $"/opt/{appName}" : $"$HOME/.local/share/{appName}";
        string binDir = systemWide ? "/usr/bin" : "$HOME/.local/bin";
        string desktopDir = systemWide ? "/usr/share/applications" : "$HOME/.local/share/applications";
        
        var relExePath = Path.GetRelativePath(sourceDir, exePath);

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e"); // Exit on error
        
        sb.AppendLine($"mkdir -p \"{targetDir}\"");
        sb.AppendLine($"mkdir -p \"{binDir}\"");
        sb.AppendLine($"mkdir -p \"{desktopDir}\"");
        
        // Copy files
        // We use rsync or cp -r. cp -r is standard.
        // We need to be careful not to copy the install.sh itself if it's in sourceDir
        sb.AppendLine($"cp -r \"{sourceDir}/\"* \"{targetDir}/\" 2>/dev/null || true");
        
        // Create Symlink
        sb.AppendLine($"ln -sf \"{targetDir}/{relExePath}\" \"{binDir}/{appName}");
        
        // Generate Desktop File
        sb.AppendLine($"cat > \"{desktopDir}/{appName}.desktop\" <<EOL");
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine($"Name={appName}");
        sb.AppendLine($"Exec={binDir}/{appName}");
        sb.AppendLine("Type=Application");
        sb.AppendLine("Categories=Utility;");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("EOL");
        
        // Notify
        sb.AppendLine($"echo \"Installed {appName} to {targetDir}\"");

        return sb.ToString();
    }
}
