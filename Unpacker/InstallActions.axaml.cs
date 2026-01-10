using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
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

    // Installation Logs
    public static readonly StyledProperty<string> InstallationLogsProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(InstallationLogs), "");

    public string InstallationLogs
    {
        get => GetValue(InstallationLogsProperty);
        set => SetValue(InstallationLogsProperty, value);
    }

    private void Log(string message)
    {
        var msg = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Console.WriteLine(msg);
        
        // Dispatch to UI thread just in case, though we are mostly on UI thread or async
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            var current = InstallationLogs ?? "";
            if (!string.IsNullOrEmpty(current)) current += "\n";
            InstallationLogs = current + msg;
        });
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
        if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath))
        {
            Log("Error: SourcePath is empty or file does not exist.");
            return;
        }

        // Visual feedback
        var originalText = InstallButtonText;
        InstallButtonText = "Installing...";
        InstallationLogs = ""; // Clear logs
        Log($"Starting installation for: {SourcePath}");

        string tempDir = Path.Combine(Path.GetTempPath(), "Unpacker_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        Log($"Created temporary directory: {tempDir}");

        try
        {
            // 1. Extract
            Log("Extracting archive...");
            await ExtractArchive(SourcePath, tempDir);
            Log("Extraction complete.");

            // 2. Detect Executable
            Log("Detecting executable...");
            string? exePath = DetectExecutable(tempDir);
            if (exePath == null)
            {
                Log("Error: No executable found in the archive.");
                InstallButtonText = "Error: No Exe Found";
                await Task.Delay(2000);
                InstallButtonText = originalText;
                return;
            }

            string appName = SanitizeAppName(Path.GetFileNameWithoutExtension(SourcePath));
            Log($"Detected executable: {Path.GetFileName(exePath)}");
            Log($"Inferred App Name: {appName}");

            // 3. Install
            if (IsSystemWide)
            {
                Log("Installing System-Wide (FPM)...");
                await InstallSystemWideWithFpm(tempDir, exePath, appName);
            }
            else
            {
                Log("Installing User-Local...");
                await InstallUserLocal(tempDir, exePath, appName);
            }

            Log("Installation completed successfully!");
            InstallButtonText = "Done!";
        }
        catch (Exception ex)
        {
            Log($"FATAL ERROR: {ex.Message}");
            if (ex.StackTrace != null) Log(ex.StackTrace); // Optional: log stack trace
            InstallButtonText = "Error";
        }
        finally
        {
            // Cleanup
            Log("Cleaning up temporary files...");
            try { Directory.Delete(tempDir, true); } catch (Exception cleanupEx) { Log($"Warning: Failed to clean up temp dir: {cleanupEx.Message}"); }
            
            await Task.Delay(2000);
            InstallButtonText = originalText; // Reset button
        }
    }

    private async Task InstallSystemWideWithFpm(string sourceDir, string exePath, string appName)
    {
        Log("Checking prerequisites...");
        if (!IsCommandAvailable("fpm"))
        {
            throw new Exception("FPM is not installed. Please install 'ruby-dev' and 'gem install fpm'.");
        }

        string? packageType = GetSystemPackageType();
        if (packageType == null)
        {
            throw new Exception("Unsupported system package manager (could not detect apt or rpm/dnf).");
        }
        Log($"Detected Package Manager Type: {packageType}");

        // Prepare Staging Directory
        string stagingDir = Path.Combine(sourceDir, "_staging");
        Log($"Preparing staging directory: {stagingDir}");
        
        string installPrefix = $"/opt/{appName}";
        string stagingInstallDir = Path.Combine(stagingDir, "opt", appName);
        string stagingBinDir = Path.Combine(stagingDir, "usr", "bin");
        string stagingDesktopDir = Path.Combine(stagingDir, "usr", "share", "applications");

        Directory.CreateDirectory(stagingInstallDir);
        Directory.CreateDirectory(stagingBinDir);
        Directory.CreateDirectory(stagingDesktopDir);

        // Copy all extracted files to staging/opt/AppName
        Log($"Copying files to {stagingInstallDir}...");
        CopyDirectory(sourceDir, stagingInstallDir, exclude: "_staging");

        // Create Symlink for /usr/bin/AppName -> /opt/AppName/path/to/exe
        string relExePath = Path.GetRelativePath(sourceDir, exePath);
        string targetPath = Path.Combine(installPrefix, relExePath);
        string symlinkPath = Path.Combine(stagingBinDir, appName);
        
        Log($"Creating symlink: {symlinkPath} -> {targetPath}");
        File.CreateSymbolicLink(symlinkPath, targetPath);

        // Create Desktop File
        Log("Generating .desktop file...");
        await CreateDesktopFile(Path.Combine(stagingDesktopDir, $"{appName}.desktop"), appName, $"/usr/bin/{appName}");

        // Build Package with FPM
        string outputDir = sourceDir;
        string version = "1.0"; // TODO: Detect version?
        
        Log($"Building {packageType} package with FPM...");
        
        // FPM arguments
        var fpmArgs = $"-s dir -t {packageType} -n \"{appName}\" -v {version} -C \"{stagingDir}\" -p \"{outputDir}\" .";
        
        await RunCommand("fpm", fpmArgs);

        // Find the generated package
        var packageFile = Directory.GetFiles(outputDir, $"*.{packageType}").OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
        if (packageFile == null)
        {
            throw new Exception($"FPM failed to generate a .{packageType} file.");
        }

        Log($"Package created successfully: {packageFile}");

        // Install the package
        await InstallPackage(packageFile, packageType);
    }

    private async Task InstallPackage(string packageFile, string packageType)
    {
        string installCmd = "pkexec";
        string installArgs = "";

        if (packageType == "deb")
        {
            // apt-get install ./package.deb
            // Using a relative path with apt often requires ./
            // But better to use absolute path for safety
            installArgs = $"apt-get install -y \"{packageFile}\"";
        }
        else
        {
            string pkgMgr = IsCommandAvailable("dnf") ? "dnf" : "rpm";
            if (pkgMgr == "rpm") installArgs = $"-i \"{packageFile}\" "; 
            else installArgs = $"install -y \"{packageFile}\" "; 
        }

        Log($"Requesting sudo permissions to install package...");
        Log($"Command: {installCmd} {installArgs}");
        
        await RunCommand(installCmd, installArgs);
        Log("Package installed successfully via system package manager.");
    }

    private async Task InstallUserLocal(string sourceDir, string exePath, string appName)
    {
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName); 
        string binDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        string desktopDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");

        Log($"Installing to user local directory: {targetDir}");
        Log($"Bin directory: {binDir}");

        string relExePath = Path.GetRelativePath(sourceDir, exePath);
        
        // Generate script
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        
        sb.AppendLine($"mkdir -p \"{targetDir}\"");
        sb.AppendLine($"mkdir -p \"{binDir}\"");
        sb.AppendLine($"mkdir -p \"{desktopDir}\"");
        
        // Copy files
        sb.AppendLine($"cp -r \"{sourceDir}/\"* \"{targetDir}/\" 2>/dev/null || true");
        
        // Symlink
        sb.AppendLine($"ln -sf \"{targetDir}/{relExePath}\" \"{binDir}/{appName}");
        
        // Desktop File
        sb.AppendLine($"cat > \"{desktopDir}/{appName}.desktop\" <<EOL");
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine($"Name={appName}");
        sb.AppendLine($"Exec={binDir}/{appName}");
        sb.AppendLine("Type=Application");
        sb.AppendLine("Categories=Utility;");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("EOL");

        // Run script
        string scriptPath = Path.Combine(sourceDir, "install_user.sh");
        await File.WriteAllTextAsync(scriptPath, sb.ToString());
        File.SetUnixFileMode(scriptPath, File.GetUnixFileMode(scriptPath) | UnixFileMode.UserExecute);

        await RunCommand("bash", $"\"{scriptPath}\"");
    }

    private string SanitizeAppName(string input)
    {
        if (input.EndsWith(".tar")) input = Path.GetFileNameWithoutExtension(input);
        var safe = new string(input.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return string.IsNullOrEmpty(safe) ? "unpacked-app" : safe.ToLowerInvariant();
    }

    private string? GetSystemPackageType()
    {
        if (IsCommandAvailable("apt-get")) return "deb";
        if (IsCommandAvailable("dnf") || IsCommandAvailable("rpm")) return "rpm";
        return null;
    }

    private bool IsCommandAvailable(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "which",
            Arguments = command,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        try
        {
            var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunCommand(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var p = Process.Start(psi);
        if (p == null) throw new Exception($"Failed to start {fileName}");

        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            throw new Exception($"Command '{fileName} {args}' failed with code {p.ExitCode}.\nStdOut: {stdout}\nStdErr: {stderr}");
        }
    }

    private void CopyDirectory(string sourceDir, string destDir, string exclude = null)
    {
        var dir = new DirectoryInfo(sourceDir);
        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destDir, file.Name), true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (exclude != null && subDir.Name == exclude) continue;
            
            string nextDest = Path.Combine(destDir, subDir.Name);
            Directory.CreateDirectory(nextDest);
            CopyDirectory(subDir.FullName, nextDest, exclude); 
        }
    }

    private Task CreateDesktopFile(string path, string appName, string execPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine($"Name={appName}");
        sb.AppendLine($"Exec={execPath}");
        sb.AppendLine("Type=Application");
        sb.AppendLine("Categories=Utility;");
        sb.AppendLine("Terminal=false");
        
        return File.WriteAllTextAsync(path, sb.ToString());
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
                if (p?.ExitCode != 0) throw new Exception("Tar extraction failed.");
            }
        });
    }

    private string? DetectExecutable(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        var elfFiles = new List<string>();
        foreach (var file in files)
        {
            if (IsElf(file)) elfFiles.Add(file);
        }

        if (elfFiles.Count == 0) return null;
        if (elfFiles.Count == 1) return elfFiles[0];

        return elfFiles.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }

    private bool IsElf(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[4];
            if (fs.Read(buffer, 0, 4) < 4) return false;
            return buffer[0] == 0x7F && buffer[1] == 0x45 && buffer[2] == 0x4C && buffer[3] == 0x46;
        }
        catch
        {
            return false;
        }
    }
}
