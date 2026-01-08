using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Unpacker; // CHANGED from Unpackr.Controls to Unpacker

public partial class FileCard : UserControl
{
    public FileCard()
    {
        InitializeComponent();
    }
    
    // Define the Title property
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FileCard, string>(nameof(Title), "Default Title");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}