using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Screenshots;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

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

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // TODO: Install logic
    }
}