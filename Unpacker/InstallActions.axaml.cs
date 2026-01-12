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

// Simple RelayCommand implementation if not available
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
    public void Execute(object parameter) => _execute();
    public event EventHandler CanExecuteChanged;
}

public partial class InstallActions : UserControl
{
    public InstallActions()
    {
        InitializeComponent();
        DataContext = this;
        ResetViewCommand = new RelayCommand(ResetView);
        SelectIconCommand = new RelayCommand(SelectIcon);
    }
    
    // COMMANDS
    public System.Windows.Input.ICommand ResetViewCommand { get; }

    private void ResetView()
    {
        IsInstalling = false;
        IsInstallationDone = false;
        InstallationLogs = "";
    }
    
    // Source Path
    public static readonly StyledProperty<string> SourcePathProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(SourcePath), "");

    public string SourcePath
    {
        get => GetValue(SourcePathProperty);
        set 
        {
            SetValue(SourcePathProperty, value);
            if (!string.IsNullOrEmpty(value) && File.Exists(value))
            {
                // Fire and forget - analyze/extract in background
                _ = ProcessSourceArchive(value);
            }
        }
    }

    private string? _currentExtractedPath;
    private string? _selectedIconPath;

    // App Icon Bitmap
    public static readonly StyledProperty<Avalonia.Media.Imaging.Bitmap?> AppIconBitmapProperty =
        AvaloniaProperty.Register<InstallActions, Avalonia.Media.Imaging.Bitmap?>(nameof(AppIconBitmap));

    public Avalonia.Media.Imaging.Bitmap? AppIconBitmap
    {
        get => GetValue(AppIconBitmapProperty);
        set => SetValue(AppIconBitmapProperty, value);
    }

    // Is Extracting (Status)
    public static readonly StyledProperty<bool> IsExtractingProperty =
        AvaloniaProperty.Register<InstallActions, bool>(nameof(IsExtracting), false);

    public bool IsExtracting
    {
        get => GetValue(IsExtractingProperty);
        set => SetValue(IsExtractingProperty, value);
    }

    // Select Icon Command
    public System.Windows.Input.ICommand SelectIconCommand { get; }

    private async Task ProcessSourceArchive(string path)
    {
        if (IsExtracting) return; // Prevent double trigger if logic allows (though setter is fine)
        IsExtracting = true;
        
        try 
        {
            Log($"Analyzing archive: {Path.GetFileName(path)}");
            
            // 1. Metadata - use StripArchiveExtensions for proper name extraction
            string baseName = StripArchiveExtensions(Path.GetFileName(path));
            AppName = SanitizeAppName(baseName);
            AppVersion = DetectVersionFromPath(baseName);
            AppIconBitmap = null; // Reset icon
            _selectedIconPath = null;
            _currentExtractedPath = null; // Reset path

            // 2. Pre-Extract
            string tempDir = Path.Combine(Path.GetTempPath(), "Unpacker_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            
            Log("Extracting archive for inspection (background)...");
            await ExtractArchive(path, tempDir);
            
            _currentExtractedPath = tempDir;
            Log($"Archive extracted to: {_currentExtractedPath}");
        }
        catch (Exception ex)
        {
            Log($"Error analyzing archive: {ex.Message}");
        }
        finally
        {
            IsExtracting = false;
        }
    }

    private async void SelectIcon()
    {
        if (IsExtracting)
        {
            Log("[WAIT] Please wait, archive is still being extracted...");
            return;
        }

        if (string.IsNullOrEmpty(_currentExtractedPath) || !Directory.Exists(_currentExtractedPath))
        {
            Log("[WARNING] Extraction failed or not found. Opening picker at default location.");
            // We allow proceeding so at least the picker opens
        }
        else
        {
             Log("[ACTION REQUIRED] Please select the icon file (png, jpg, svg) from the opened window.");
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        IStorageFolder? startLocation = null;
        if (!string.IsNullOrEmpty(_currentExtractedPath) && Directory.Exists(_currentExtractedPath))
        {
            try 
            {
                // Uri constructor handles absolute paths correctly (adds file:// automatically)
                // e.g. /tmp/foo -> file:///tmp/foo
                startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(_currentExtractedPath));
            }
            catch (Exception ex)
            {
                 Log($"Warning: Could not set start location (URI error): {ex.Message}");
                 // Fallback: Try passing path string directly if the overload exists (it often handles it better)
                 try { startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(_currentExtractedPath); } catch {}
            }
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select App Icon",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.svg", "*.ico" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            var p = files[0].Path.LocalPath;
            Log($"Icon selected: {p}");
            _selectedIconPath = p;

            try
            {
                using var stream = File.OpenRead(p);
                AppIconBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            }
            catch (Exception ex)
            {
                Log($"Failed to load icon image: {ex.Message}");
            }
        }
    }

    // App Name
    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(AppName), "Application");

    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    // App Version
    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(AppVersion), "1.0.0");

    public string AppVersion
    {
        get => GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
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

    // IsInstalling (View State)
    public static readonly StyledProperty<bool> IsInstallingProperty =
        AvaloniaProperty.Register<InstallActions, bool>(nameof(IsInstalling), false);

    public bool IsInstalling
    {
        get => GetValue(IsInstallingProperty);
        set => SetValue(IsInstallingProperty, value);
    }

    // IsInstallationDone (View State)
    public static readonly StyledProperty<bool> IsInstallationDoneProperty =
        AvaloniaProperty.Register<InstallActions, bool>(nameof(IsInstallationDone), false);

    public bool IsInstallationDone
    {
        get => GetValue(IsInstallationDoneProperty);
        set => SetValue(IsInstallationDoneProperty, value);
    }

    // Log Caret Index (Auto-scroll placeholder)
    public static readonly StyledProperty<int> LogCaretIndexProperty =
        AvaloniaProperty.Register<InstallActions, int>(nameof(LogCaretIndex), 0);

    public int LogCaretIndex
    {
        get => GetValue(LogCaretIndexProperty);
        set => SetValue(LogCaretIndexProperty, value);
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
        
        // Switch View
        IsInstalling = true;
        IsInstallationDone = false;
        InstallationLogs = ""; // Clear logs
        
        Log($"Starting installation for: {SourcePath}");

        try
        {
            string tempDir = _currentExtractedPath;

            // Ensure extracted
            if (string.IsNullOrEmpty(tempDir) || !Directory.Exists(tempDir))
            {
                 Log("Re-extracting archive...");
                 tempDir = Path.Combine(Path.GetTempPath(), "Unpacker_" + Guid.NewGuid());
                 Directory.CreateDirectory(tempDir);
                 await ExtractArchive(SourcePath, tempDir);
                 _currentExtractedPath = tempDir;
            }
            else
            {
                Log("Using pre-extracted files.");
            }

            // 2. Detect Binary
            Log("Detecting binary...");
            string? binaryPath = DetectExecutable(tempDir);
            if (binaryPath == null)
            {
                Log("Error: No binary (ELF) found in the archive.");
                InstallButtonText = "Error: No Binary Found";
                IsInstallationDone = true; // Allow user to go back
                return;
            }

            string appName = SanitizeAppName(AppName); // Sanitize the user-edited name just in case
            Log($"Detected binary: {Path.GetFileName(binaryPath)}");
            Log($"App Name: {appName}");
            Log($"Version: {AppVersion}");

            // 3. Install
            if (IsSystemWide)
            {
                Log("Installing System-Wide (FPM)...");
                await InstallSystemWideWithFpm(tempDir, binaryPath, appName, AppVersion, _selectedIconPath);
            }
            else
            {
                Log("Installing User-Local...");
                await InstallUserLocal(tempDir, binaryPath, appName, _selectedIconPath);
            }

            Log("Installation completed successfully!");
            InstallButtonText = "Done!";
        }
        catch (Exception ex)
        {
            Log($"FATAL ERROR: {ex.Message}");
            if (ex.StackTrace != null) Log(ex.StackTrace); 
            InstallButtonText = "Error";
        }
        finally
        {
            // Cleanup
            try { /* Keep temp dir for now if we want to reuse it? Or delete it always? */ 
                  /* For now, delete it to be clean, but this means next install needs re-extract. */
                  if (_currentExtractedPath != null) Directory.Delete(_currentExtractedPath, true); 
                  _currentExtractedPath = null;
            } catch (Exception cleanupEx) { Log($"Warning: Failed to clean up temp dir: {cleanupEx.Message}"); }
            
            InstallButtonText = originalText; // Reset button text for next time
            IsInstallationDone = true; // Enable the "Done" button to go back
        }
    }

    private async Task InstallSystemWideWithFpm(string sourceDir, string binaryPath, string appName, string version, string? iconPath)
    {
        Log("Checking prerequisites...");
        if (!IsCommandAvailable("fpm"))
        {
            throw new Exception("FPM is not installed. Please install 'ruby-dev' and 'gem install fpm'.");
        }

        string? packageType = GetSystemPackageType();
        if (packageType == null)
        {
            throw new Exception("Unsupported system package manager (could not detect apt, dnf, rpm, or pacman).");
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

        // Create Symlink for /usr/bin/AppName -> /opt/AppName/path/to/binary
        string relBinaryPath = Path.GetRelativePath(sourceDir, binaryPath);
        string targetPath = Path.Combine(installPrefix, relBinaryPath);
        string symlinkPath = Path.Combine(stagingBinDir, appName);
        
        Log($"Creating symlink: {symlinkPath} -> {targetPath}");
        File.CreateSymbolicLink(symlinkPath, targetPath);

        // Create Desktop File
        Log("Generating .desktop file...");
        string finalIconPath = "";

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
             // Copy icon to /opt/AppName/icon.png (Installation Directory)
             // Staging path: stagingDir/opt/AppName/icon.png
             
             // Ensure stagingInstallDir exists (it should be created above)
             string iconExt = Path.GetExtension(iconPath);
             string iconDestFile = Path.Combine(stagingInstallDir, $"icon{iconExt}");
             
             Log($"Copying icon to app directory: {iconDestFile}");
             File.Copy(iconPath, iconDestFile, true);
             
             // Final absolute path on target system
             finalIconPath = Path.Combine(installPrefix, $"icon{iconExt}");
        }

        await CreateDesktopFile(Path.Combine(stagingDesktopDir, $"{appName}.desktop"), appName, $"/usr/bin/{appName}", finalIconPath);

        // Build Package with FPM
        string outputDir = sourceDir;
        // Version is passed in now
        Log($"Using version: {version}");
        
        Log($"Building {packageType} package with FPM...");
        
        // FPM arguments
        var fpmArgs = $"-s dir -t {packageType} -n \"{appName}\" -v {version} -C \"{stagingDir}\" -p \"{outputDir}\" .";
        
        await RunCommand("fpm", fpmArgs);

        // Find the generated package
        string searchPattern = packageType switch
        {
            "pacman" => "*.pkg.tar.*",
            "deb" => "*.deb",
            "rpm" => "*.rpm",
            _ => "*.*"
        };
        var packageFile = Directory.GetFiles(outputDir, searchPattern).OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
        if (packageFile == null)
        {
            throw new Exception($"FPM failed to generate a package file (searched for {searchPattern}).");
        }

        Log($"Package created successfully: {packageFile}");

        // Install the package
        await InstallPackage(packageFile, packageType);
    }

    private string DetectVersionFromPath(string nameOrPath)
    {
        // Try to match standard version patterns like 1.2.3, v1.2, etc.
        // Note: Caller should pass already-stripped base name (without extensions)
        // Regex for x.y.z versioning
        var match = System.Text.RegularExpressions.Regex.Match(nameOrPath, @"(\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9]+)?)");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "1.0.0"; // Fallback
    }

    private async Task InstallPackage(string packageFile, string packageType)
    {
        // Ensure we work with absolute paths
        packageFile = Path.GetFullPath(packageFile);

        // Build argument list - using ArgumentList avoids shell quoting issues
        var args = new List<string>();
        
        if (packageType == "deb")
        {
            // pkexec sh -c "apt-get install -y /path/to/file.deb"
            args.Add("sh");
            args.Add("-c");
            args.Add($"apt-get install -y '{packageFile}'");
        }
        else if (packageType == "pacman")
        {
            // pkexec sh -c "pacman -U --noconfirm /path/to/file.pkg.tar.zst"
            args.Add("sh");
            args.Add("-c");
            args.Add($"pacman -U --noconfirm '{packageFile}'");
        }
        else
        {
            string pkgMgr = IsCommandAvailable("dnf") ? "dnf" : "rpm";
            args.Add("sh");
            args.Add("-c");
            if (pkgMgr == "rpm") 
                args.Add($"rpm -Uvh --force '{packageFile}'");
            else 
                args.Add($"dnf install -y '{packageFile}'");
        }

        Log($"Requesting sudo permissions to install package...");
        Log($"Command: pkexec {string.Join(" ", args.Select(a => $"\"{a}\""))}");
        
        await RunCommandWithArgs("pkexec", args);
        Log("Package installed successfully via system package manager.");
    }

    private async Task InstallUserLocal(string sourceDir, string binaryPath, string appName, string? iconPath)
    {
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName); 
        string binDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        string desktopDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");

        Log($"Installing to user local directory: {targetDir}");
        Log($"Bin directory: {binDir}");

        string relBinaryPath = Path.GetRelativePath(sourceDir, binaryPath);
        
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
        sb.AppendLine($"ln -sf \"{targetDir}/{relBinaryPath}\" \"{binDir}/{appName}\"");
        
        // Icon Logic
        string iconLine = "";
        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
             string iconExt = Path.GetExtension(iconPath);
             sb.AppendLine($"cp \"{iconPath}\" \"{targetDir}/icon{iconExt}\"");
             iconLine = $"Icon={targetDir}/icon{iconExt}";
             Log($"Including icon in user installation.");
        }

        // Desktop File
        sb.AppendLine($"cat > \"{desktopDir}/{appName}.desktop\" <<EOL");
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine($"Name={appName}");
        sb.AppendLine($"Exec={binDir}/{appName}");
        if (!string.IsNullOrEmpty(iconLine)) sb.AppendLine(iconLine);
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
        // Input should already be stripped of extensions by StripArchiveExtensions
        var safe = new string(input.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return string.IsNullOrEmpty(safe) ? "unpacked-app" : safe.ToLowerInvariant();
    }

    private string StripArchiveExtensions(string fileName)
    {
        // Handle multi-part extensions like .tar.gz, .tar.xz
        string[] extensions = { ".tar.gz", ".tar.xz", ".tar.bz2", ".tgz", ".tar", ".zip", ".7z", ".rar" };
        foreach (var ext in extensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return fileName[..^ext.Length];
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private string? GetSystemPackageType()
    {
        if (IsCommandAvailable("pacman")) return "pacman";
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

        // Read stdout/stderr BEFORE WaitForExitAsync to prevent deadlock
        // when output buffers fill up
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        
        await p.WaitForExitAsync();
        
        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (p.ExitCode != 0)
        {
            throw new Exception($"Command '{fileName} {args}' failed with code {p.ExitCode}.\nStdOut: {stdout}\nStdErr: {stderr}");
        }
    }

    private async Task RunCommandWithArgs(string fileName, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        // Add each argument as a discrete item - this preserves arguments with spaces
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var p = Process.Start(psi);
        if (p == null) throw new Exception($"Failed to start {fileName}");

        // Read stdout/stderr BEFORE WaitForExitAsync to prevent deadlock
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        
        await p.WaitForExitAsync();
        
        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (p.ExitCode != 0)
        {
            throw new Exception($"Command '{fileName}' failed with code {p.ExitCode}.\nStdOut: {stdout}\nStdErr: {stderr}");
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

    private Task CreateDesktopFile(string path, string appName, string execPath, string iconName = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine($"Name={appName}");
        sb.AppendLine($"Exec={execPath}");
        if (!string.IsNullOrEmpty(iconName))
        {
            sb.AppendLine($"Icon={iconName}");
        }
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
        Log($"Scanning {files.Length} files for executables...");

        var candidates = new List<(string Path, bool IsElf, bool IsScript)>();

        foreach (var file in files)
        {
            if (new FileInfo(file).Length == 0) continue;
            
            bool isElf = IsElf(file);
            bool isScript = !isElf && IsShellScript(file);

            if (isElf || isScript)
            {
                candidates.Add((file, isElf, isScript));
            }
        }

        if (candidates.Count == 0)
        {
            Log("Debug info: No candidates found. Listing all files:");
            foreach (var f in files.Take(20)) Log($" - {Path.GetFileName(f)}");
            if (files.Length > 20) Log($" ... and {files.Length - 20} more.");
            return null;
        }

        // Strategy for tarball installations:
        // 1. Prefer ELF binary matching the app name
        // 2. Fallback to largest ELF (usually the main application binary)
        // 3. Only use scripts as last resort

        // Use StripArchiveExtensions to properly handle .tar.gz, .tar.xz, etc.
        var appName = SanitizeAppName(StripArchiveExtensions(Path.GetFileName(SourcePath)));
        
        // Debug: Show candidate breakdown
        int elfCount = candidates.Count(c => c.IsElf);
        int scriptCount = candidates.Count(c => c.IsScript);
        Log($"Found {elfCount} ELF binaries and {scriptCount} scripts");
        
        if (elfCount > 0)
        {
            Log($"ELF candidates: {string.Join(", ", candidates.Where(c => c.IsElf).Take(5).Select(c => Path.GetFileName(c.Path)))}");
        }
        
        Log($"Filtering candidates for app name: {appName}");

        // 2. Name Match - ONLY for ELF binaries (scripts like bump.sh should not match)
        // Strip non-letters for fuzzy comparison
        var simpleAppName = new string(appName.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        
        var nameMatch = candidates
            .Where(c => c.IsElf) // Only consider ELF binaries for name matching
            .Where(c => 
            {
                var simpleName = new string(Path.GetFileNameWithoutExtension(c.Path).Where(char.IsLetter).ToArray()).ToLowerInvariant();
                return simpleName.Contains(simpleAppName) || simpleAppName.Contains(simpleName);
            })
            .OrderBy(c => Path.GetFileName(c.Path).Length) // Prefer shorter names (e.g. 'beekeeper-studio' over 'beekeeper-studio-bin')
            .FirstOrDefault();
        
        if (nameMatch.Path != null) return nameMatch.Path;

        // 3. Fallback: Largest ELF (usually the main binary for Electron apps, etc.)
        var largestElf = candidates
            .Where(c => c.IsElf)
            .OrderByDescending(c => new FileInfo(c.Path).Length)
            .FirstOrDefault();
        
        if (largestElf.Path != null) return largestElf.Path;

        // 4. Last resort: Any script (prefer shorter names like 'run' or 'start')
        var script = candidates
            .Where(c => c.IsScript)
            .OrderBy(c => Path.GetFileName(c.Path).Length)
            .FirstOrDefault();
            
        return script.Path;
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
        catch { return false; }
    }

    private bool IsShellScript(string path)
    {
        try
        {
            // Check for Shebang #!
            using var fs = File.OpenRead(path);
            var buffer = new byte[2];
            if (fs.Read(buffer, 0, 2) < 2) return false;
            return buffer[0] == '#' && buffer[1] == '!';
        }
        catch { return false; }
    }
}
