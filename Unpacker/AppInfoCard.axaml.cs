using Avalonia;
using Avalonia.Controls;

namespace Unpacker;

public partial class AppInfoCard : UserControl
{
    public AppInfoCard()
    {
        InitializeComponent();
    }

    // Allow parent to bind: <local:AppInfoCard AppName="{Binding Name}" />
    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<AppInfoCard, string>(nameof(AppName), "Application Name");

    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<AppInfoCard, string>(nameof(AppVersion), "1.0.0");

    public string AppVersion
    {
        get => GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }
    
    // Events
    
    // event handler for updating archive path
    //public void UpdateNamePath
}