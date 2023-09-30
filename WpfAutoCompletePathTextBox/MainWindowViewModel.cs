﻿using System.Collections.ObjectModel;

namespace WpfAutoCompletePathTextBox;

public sealed class MainWindowViewModel : MyBindableBase
{
    public ObservableCollection<string> PathHistory { get; } = new();

    public string DirectoryPathText
    {
        get => _directoryPathText;
        set
        {
            if (SetProperty(ref _directoryPathText, value))
            {
                PathHistory.Add(value);
            }
        }
    }
    private string _directoryPathText = "";
}