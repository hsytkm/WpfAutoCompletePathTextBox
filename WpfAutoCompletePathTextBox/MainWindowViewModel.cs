using System.Collections.ObjectModel;

namespace WpfAutoCompletePathTextBox;

public sealed class MainWindowViewModel : MyBindableBase
{
    public ObservableCollection<string> PathHistories { get; } = new();

    public string DirectoryPathText
    {
        get => _directoryPathText;
        set
        {
            if (SetProperty(ref _directoryPathText, value))
            {
                //Debug.WriteLine(@$"ViewModel:SetProperty->""{value}""");
                PathHistories.Add(value);
            }
        }
    }
    private string _directoryPathText = "";

    public bool IsEnableText
    {
        get => _isEnableText;
        set => SetProperty(ref _isEnableText, value);
    }
    private bool _isEnableText = true;
    
}
