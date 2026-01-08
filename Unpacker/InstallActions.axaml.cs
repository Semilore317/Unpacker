using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Unpacker;

public partial class InstallActions : UserControl
{
    public InstallActions()
    {
        InitializeComponent();
        DataContext = this;
    }

    // --- 1. Source Path ---
    public static readonly StyledProperty<string> SourcePathProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(SourcePath), "");

    public string SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    // --- 2. System Wide Toggle ---
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

    // --- 3. Install Button Text (Now a Real Property) ---
    public static readonly StyledProperty<string> InstallButtonTextProperty =
        AvaloniaProperty.Register<InstallActions, string>(nameof(InstallButtonText), "Install (Admin)");

    public string InstallButtonText
    {
        get => GetValue(InstallButtonTextProperty);
        set => SetValue(InstallButtonTextProperty, value);
    }

    // --- 4. Shield Opacity (Now a Real Property) ---
    public static readonly StyledProperty<double> ShieldOpacityProperty =
        AvaloniaProperty.Register<InstallActions, double>(nameof(ShieldOpacity), 1.0);

    public double ShieldOpacity
    {
        get => GetValue(ShieldOpacityProperty);
        set => SetValue(ShieldOpacityProperty, value);
    }

    // --- Events ---
    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        // TODO: File picker logic
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // TODO: Install logic
    }
}