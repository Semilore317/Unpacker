using CommunityToolkit.Mvvm.ComponentModel;

namespace Unpacker.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "UNPACKER";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppName))]
    private string _filePath = "";

    [ObservableProperty]
    private bool _isSystemWide = true;

    public string AppName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return "Application Name";
            try
            {
                return System.IO.Path.GetFileNameWithoutExtension(FilePath);
            }
            catch
            {
                return "Application Name";
            }
        }
    }
    
    public string AppVersion => "1.0.0"; // Placeholder for now
}