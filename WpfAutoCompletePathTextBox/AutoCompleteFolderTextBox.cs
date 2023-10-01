using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WpfAutoCompletePathTextBox;

[TemplatePart(Name = PartCandidatePopup, Type = typeof(Popup))]
[TemplatePart(Name = PartCandidateList, Type = typeof(ListBox))]
[TemplatePart(Name = PartRootGrid, Type = typeof(Grid))]
[TemplatePart(Name = PartHistoryExpander, Type = typeof(Expander))]
[TemplatePart(Name = PartHistoryPopup, Type = typeof(Popup))]
[TemplatePart(Name = PartHistoryList, Type = typeof(ListBox))]
public class AutoCompleteFolderTextBox : TextBox
{
    public const string PartCandidatePopup = "PART_CandidatePopup";
    public const string PartCandidateList = "PART_CandidateList";
    public const string PartRootGrid = "rootGrid";

    public const string PartHistoryExpander = "PART_HistoryExpander";
    public const string PartHistoryPopup = "PART_HistoryPopup";
    public const string PartHistoryList = "PART_HistoryList";

    private Popup? _candidatePopup;
    private ListBox? _candidateListBox;
    private Grid? _rootGrid;

    private Expander? _historyExpander;
    private Popup? _historyPopup;
    private ListBox? _historyListBox;

    private bool _templateLoaded;
    private string? _lastCandidateFolderPath;
    private readonly ObservableCollection<string> _histories = new();

    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.Register(nameof(Watermark), typeof(string), typeof(AutoCompleteFolderTextBox), new PropertyMetadata(""));
    public string? Watermark
    {
        get => (string?)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public static readonly DependencyProperty IsOpenHistoryPopupProperty =
        DependencyProperty.Register(nameof(IsOpenHistoryPopup), typeof(bool), typeof(AutoCompleteFolderTextBox),
            new FrameworkPropertyMetadata(false, (d, e) =>
            {
                if (d is AutoCompleteFolderTextBox self && e.NewValue is bool value)
                {
                    if (self._historyExpander is { } expander)
                        expander.IsExpanded = value;

                    if (self._historyPopup is { } popup)
                        popup.IsOpen = value;

                    // Close other popup
                    if (value && self._candidatePopup is { } otherPopup)
                        otherPopup.IsOpen = false;
                }
            }));
    public bool IsOpenHistoryPopup
    {
        get => (bool)GetValue(IsOpenHistoryPopupProperty);
        set => SetValue(IsOpenHistoryPopupProperty, value);
    }

    static AutoCompleteFolderTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoCompleteFolderTextBox), new FrameworkPropertyMetadata(typeof(AutoCompleteFolderTextBox)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetParentWindow(this) is not { } parentWindow)
            throw new NullReferenceException();

        bool prevIsOpen = false;

        // テキスト入力でPath候補をポップアップ表示
        {
            var popup = _candidatePopup = Template.FindName(PartCandidatePopup, this) as Popup;
            var listBox = _candidateListBox = Template.FindName(PartCandidateList, this) as ListBox;
            _rootGrid = Template.FindName(PartRootGrid, this) as Grid;

            if (popup is null || listBox is null)
                throw new NullReferenceException();

            KeyDown += AutoCompleteTextBox_KeyDown;
            PreviewKeyDown += AutoCompleteTextBox_PreviewKeyDown;

            listBox.PreviewMouseDown += ListBox_PreviewMouseDown;
            listBox.KeyDown += CandidateListBox_KeyDown;
            popup.CustomPopupPlacementCallback += Popup_Repositioning;

            // PopupはWindowのフォーカス状態と連動させます（Popup表示だけが残る対応）
            parentWindow.Deactivated += (_, _) => (prevIsOpen, popup.IsOpen) = (popup.IsOpen, false);
            parentWindow.Activated += (_, _) => popup.IsOpen = prevIsOpen;
        }

        // ExpanderでPath履歴をポップアップ表示
        {
            var expander = _historyExpander = Template.FindName(PartHistoryExpander, this) as Expander;
            var popup = _historyPopup = Template.FindName(PartHistoryPopup, this) as Popup;
            var listBox = _historyListBox = Template.FindName(PartHistoryList, this) as ListBox;

            if (expander is null || popup is null || listBox is null)
                throw new NullReferenceException();

            listBox.PreviewMouseDown += ListBox_PreviewMouseDown;
            listBox.KeyDown += HistoryListBox_KeyDown;
            popup.CustomPopupPlacementCallback += Popup_Repositioning;

            expander.IsEnabled = false;
            expander.Expanded += HistoryExpander_Expanded;      // Open
            expander.Collapsed += HistoryExpander_Expanded;     // Close
            _histories.CollectionChanged += Histories_CollectionChanged;

            // PopupはWindowのフォーカス状態と連動させます（Popup表示だけが残る対応）
            parentWindow.Deactivated += (_, _) => (prevIsOpen, popup.IsOpen) = (popup.IsOpen, false);
            parentWindow.Activated += (_, _) => popup.IsOpen = prevIsOpen;

            // for debug
            _histories.Insert(0, @"D:\tools\Viewer");
            _histories.Insert(0, @"D:\data\_temp");
            _histories.Insert(0, @"D:\_temp");
            _histories.Insert(0, @"D:\tools\Dev");
        }

        _templateLoaded = true;
    }

    #region Common

    private static Window? GetParentWindow(DependencyObject d)
    {
        while (d is not Window)
            d = LogicalTreeHelper.GetParent(d);

        return d as Window;
    }

    private CustomPopupPlacement[] Popup_Repositioning(Size popupSize, Size targetSize, Point offset)
    {
        if (_rootGrid is not { } rootGrid)
            throw new NullReferenceException();

        return new CustomPopupPlacement[]
        {
            new(new Point(0.01 - offset.X, rootGrid.ActualHeight - offset.Y), PopupPrimaryAxis.None)
        };
    }

    private void AutoCompleteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_candidateListBox is not { } listBox)
            throw new NullReferenceException();

        if (listBox.Items.Count > 0 && e.OriginalSource is not ListBoxItem)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Prior:
                case Key.Next:
                    listBox.Focus();

                    if (listBox.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem lbi)
                        lbi.Focus();

                    e.Handled = true;
                    break;
            }
        }
    }

    private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_candidatePopup is not { } popup)
            throw new NullReferenceException();

        if (e.Key is Key.Enter)
        {
            popup.IsOpen = false;

            var current = Text;
            var newText = IsDriveLetterOnly(current) ? current : current.TrimEnd('\\');
            SetTextAndMoveLastChar(this, newText);
            NotifyTextProperty();
        }
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        if (!_templateLoaded)
            return;

        if (_candidatePopup is not { } popup)
            throw new NullReferenceException();

        if (_candidateListBox?.Items is not { } listItems)
            throw new NullReferenceException();

        try
        {
            listItems.Clear();

            var text = Text;
            _lastCandidateFolderPath = Path.GetDirectoryName(text);

            foreach (string path in lookup(text))
            {
                if (text != path)
                    listItems.Add(path);
            }
        }
        finally
        {
            popup.IsOpen = listItems.Count > 0;
        }

        static IEnumerable<string> lookup(string path)
        {
            try
            {
                var dirName = Path.GetDirectoryName(path);
                if (dirName is null)
                {
                    if (IsDriveLetterOnly(path))
                        dirName = path;
                }

                if (Directory.Exists(dirName))
                {
                    return new DirectoryInfo(dirName)
                        .EnumerateDirectories()
                        .Where(x => x.FullName.StartsWith(path, StringComparison.InvariantCultureIgnoreCase))
                        .Select(x => x.FullName);
                }
            }
            catch (Exception) { }

            return Enumerable.Empty<string>();
        }
    }

    private void NotifyTextProperty()
    {
        var pathText = Text;

        //Debug.WriteLine(@$"View:Nofity->""{pathText}""");
        GetBindingExpression(TextBox.TextProperty).UpdateSource();

        if (DirectoryExistsRule.IsValidPath(pathText))
        {
            if (_histories.Contains(pathText))
                _histories.Remove(pathText);

            // 新規要素を先頭に追加します
            _histories.Insert(0, pathText);
        }
    }

    private static void SetTextAndMoveLastChar(TextBox textBox, string text)
    {
        textBox.Text = text;
        textBox.Select(text.Length, 0);   // Select last char
    }

    private static bool IsDriveLetterOnly(string path) => path.Length == 3 && path[^2..] == ":\\";

    private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton is not MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is not TextBlock textBlock)
            return;

        Text = textBlock.Text;
        NotifyTextProperty();

        if (_candidatePopup is { } popup)
            popup.IsOpen = false;

        IsOpenHistoryPopup = false;

        e.Handled = true;
    }

    #endregion

    #region Candidate
    private void CandidateListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ListBoxItem listItem)
            return;

        if (_candidatePopup is not { } popup)
            throw new NullReferenceException();

        string? text = e.Key switch
        {
            Key.Enter => listItem.Content as string,
            Key.Oem5 => listItem.Content is string ? (listItem.Content as string) + '\\' : null,      // BackslashKey
            Key.Escape => _lastCandidateFolderPath is not null ? _lastCandidateFolderPath.TrimEnd('\\') + '\\' : null,
            _ => null
        };

        if (text is null)
            return;

        // 以下だとポップアップのEnterキー選択でPathを確定しません
        //if (e.Key is Key.Enter)
        //    UpdateSource();

        SetTextAndMoveLastChar(this, text);

        // 以下だとポップアップのEnterキー選択でPathを確定します
        if (e.Key is Key.Enter)
            NotifyTextProperty();

        // 基本的には 選択が完了しているのでPopupをクローズしますが、
        // Backslashキー かつ 子ディレクトリが存在する場合は継続したPath選択のためクローズしません。
        popup.IsOpen = e.Key is Key.Oem5 && new DirectoryInfo(Text).EnumerateDirectories().Any();

        Keyboard.Focus(this);
        e.Handled = true;
    }

    #endregion

    #region History

    private void Histories_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not IEnumerable<string> sourceItems)
            throw new NotSupportedException();

        if (_historyListBox?.Items is not { } listItems)
            throw new NullReferenceException();

        // 元コレクションの順で追加します(最新要素が上に登録されます)
        listItems.Clear();
        foreach (var item in sourceItems)
            listItems.Add(item);

        if (_historyExpander is { } expander)
            expander.IsEnabled = listItems.Count > 0;
    }

    private void HistoryExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander)
            return;

        if (_historyListBox is not { } listBox)
            throw new NullReferenceException();

        Debug.WriteLine("Expanded");

        var isShowHistroy = listBox.Items.Count <= 0 ? false : expander.IsExpanded;
        IsOpenHistoryPopup = isShowHistroy;

        if (IsOpenHistoryPopup && listBox.Items.Count > 0)
            listBox.Focus();

        e.Handled = true;
    }

    private void HistoryListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
            return;

        if (e.OriginalSource is not ListBoxItem listItem)
            return;

        if (listItem.Content is not string selectedPath)
            return;

        SetTextAndMoveLastChar(this, selectedPath);
        NotifyTextProperty();

        IsOpenHistoryPopup = false;

        Keyboard.Focus(this);
        e.Handled = true;
    }

    #endregion

}
