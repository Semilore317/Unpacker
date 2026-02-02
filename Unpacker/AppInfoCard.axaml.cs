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

    // Icon Bitmap
    public static readonly StyledProperty<Avalonia.Media.Imaging.Bitmap?> IconBitmapProperty =
        AvaloniaProperty.Register<AppInfoCard, Avalonia.Media.Imaging.Bitmap?>(nameof(IconBitmap));

    public Avalonia.Media.Imaging.Bitmap? IconBitmap
    {
        get => GetValue(IconBitmapProperty);
        set => SetValue(IconBitmapProperty, value);
    }

    // Select Icon Command
    public static readonly StyledProperty<System.Windows.Input.ICommand?> SelectIconCommandProperty =
        AvaloniaProperty.Register<AppInfoCard, System.Windows.Input.ICommand?>(nameof(SelectIconCommand));

    public System.Windows.Input.ICommand? SelectIconCommand
    {
        get => GetValue(SelectIconCommandProperty);
        set => SetValue(SelectIconCommandProperty, value);
    }
}