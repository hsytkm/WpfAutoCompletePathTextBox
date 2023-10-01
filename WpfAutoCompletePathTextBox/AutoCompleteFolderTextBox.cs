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
    // Max count combobox items
    private const int MaxHistoryCount = 5;

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

    // 読み取り専用の依存関係プロパティ
    private static readonly DependencyPropertyKey IsOpenCandidatePopupPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsOpenCandidatePopup), typeof(bool), typeof(AutoCompleteFolderTextBox),
            new FrameworkPropertyMetadata(false, (d, e) =>
            {
                if (d is AutoCompleteFolderTextBox self && e.NewValue is bool value)
                {
                    if (self._candidatePopup is { } popup)
                        popup.IsOpen = value;

                    // Close other popup
                    if (value)
                        self.IsOpenHistoryPopup = false;
                }
            }));
    public static readonly DependencyProperty IsOpenCandidatePopupProperty = IsOpenCandidatePopupPropertyKey.DependencyProperty;
    public bool IsOpenCandidatePopup
    {
        get => (bool)GetValue(IsOpenCandidatePopupProperty);
        private set => SetValue(IsOpenCandidatePopupPropertyKey, value);
    }

    // 読み取り専用の依存関係プロパティ
    private static readonly DependencyPropertyKey IsOpenHistoryPopupPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsOpenHistoryPopup), typeof(bool), typeof(AutoCompleteFolderTextBox),
            new FrameworkPropertyMetadata(false, (d, e) =>
            {
                if (d is AutoCompleteFolderTextBox self && e.NewValue is bool value)
                {
                    if (self._historyExpander is { } expander)
                        expander.IsExpanded = value;

                    if (self._historyPopup is { } popup)
                        popup.IsOpen = value;

                    // Close other popup
                    if (value)
                        self.IsOpenCandidatePopup = false;
                }
            }));
    public static readonly DependencyProperty IsOpenHistoryPopupProperty = IsOpenHistoryPopupPropertyKey.DependencyProperty;
    public bool IsOpenHistoryPopup
    {
        get => (bool)GetValue(IsOpenHistoryPopupProperty);
        private set => SetValue(IsOpenHistoryPopupPropertyKey, value);
    }

    static AutoCompleteFolderTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoCompleteFolderTextBox), new FrameworkPropertyMetadata(typeof(AutoCompleteFolderTextBox)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 共通
        KeyDown += AutoCompleteTextBox_KeyDown;

        if (GetParentWindow(this) is not { } parentWindow)
            throw new NullReferenceException();

        // Path候補のポップアップ表示
        {
            var popup = _candidatePopup = Template.FindName(PartCandidatePopup, this) as Popup;
            var listBox = _candidateListBox = Template.FindName(PartCandidateList, this) as ListBox;
            _rootGrid = Template.FindName(PartRootGrid, this) as Grid;

            if (popup is null || listBox is null)
                throw new NullReferenceException();

            PreviewKeyDown += CandidateParentTextBox_KeyDown;
            listBox.KeyDown += CandidateListBox_KeyDown;
            listBox.PreviewMouseDown += ListBox_PreviewMouseDown;
            popup.CustomPopupPlacementCallback += Popup_Repositioning;

            // PopupはWindowのフォーカス状態と連動させます（Popup表示だけが残る対応）
            bool prevIsOpen = false;
            parentWindow.Deactivated += (_, _) => (prevIsOpen, IsOpenCandidatePopup) = (popup.IsOpen, false);
            parentWindow.Activated += (_, _) => IsOpenCandidatePopup = prevIsOpen;
        }

        // Path履歴のポップアップ表示
        {
            var expander = _historyExpander = Template.FindName(PartHistoryExpander, this) as Expander;
            var popup = _historyPopup = Template.FindName(PartHistoryPopup, this) as Popup;
            var listBox = _historyListBox = Template.FindName(PartHistoryList, this) as ListBox;

            if (expander is null || popup is null || listBox is null)
                throw new NullReferenceException();

            PreviewKeyDown += HistoryParentTextBox_KeyDown;
            listBox.KeyDown += HistoryListBox_KeyDown;
            listBox.PreviewMouseDown += ListBox_PreviewMouseDown;
            popup.CustomPopupPlacementCallback += Popup_Repositioning;

            expander.IsEnabled = false;
            expander.Expanded += HistoryExpander_Expanded;      // Open
            expander.Collapsed += HistoryExpander_Expanded;     // Close
            _histories.CollectionChanged += Histories_CollectionChanged;

            // PopupはWindowのフォーカス状態と連動させます（Popup表示だけが残る対応）
            bool prevIsOpen = false;
            parentWindow.Deactivated += (_, _) => (prevIsOpen, IsOpenHistoryPopup) = (popup.IsOpen, false);
            parentWindow.Activated += (_, _) => IsOpenHistoryPopup = prevIsOpen;

#if DEBUG
            _histories.Insert(0, @"D:\tools\Viewer");
            _histories.Insert(0, @"D:\data\_temp");
            _histories.Insert(0, @"D:\_temp");
            _histories.Insert(0, @"D:\tools\Dev");
#endif
        }

        _templateLoaded = true;
    }

    #region Self

    private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
            return;

        IsOpenCandidatePopup = false;
        var current = Text;
        var newText = IsDriveLetterOnly(current) ? current : current.TrimEnd('\\');
        SetTextAndMoveLastChar(this, newText);
        NotifyTextProperty();
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        if (!_templateLoaded)
            return;

        if (_candidateListBox?.Items is not { } listItems)
            throw new NullReferenceException();

        try
        {
            var text = Text;
            _lastCandidateFolderPath = Path.GetDirectoryName(text);

            listItems.Clear();
            foreach (string path in lookup(text))
            {
                if (text != path)
                    listItems.Add(path);
            }
        }
        finally
        {
            IsOpenCandidatePopup = listItems.Count > 0;
        }

        static IEnumerable<string> lookup(string path)
        {
            try
            {
                var dirName = Path.GetDirectoryName(path);
                if (dirName is null && IsDriveLetterOnly(path))
                {
                    dirName = path;
                }

                if (Directory.Exists(dirName))
                    return new DirectoryInfo(dirName)
                        .EnumerateDirectories()
                        .Where(x => x.FullName.StartsWith(path, StringComparison.InvariantCultureIgnoreCase))
                        .Select(x => x.FullName);
            }
            catch (Exception) { }

            return Enumerable.Empty<string>();
        }
    }

    private void NotifyTextProperty()
    {
        var pathText = Text;
        GetBindingExpression(TextBox.TextProperty).UpdateSource();

        if (DirectoryExistsRule.IsValidPath(pathText))
        {
            var items = _histories;

            // delete duplicate item
            if (items.Contains(pathText))
                items.Remove(pathText);

            while (items.Count >= MaxHistoryCount)
                items.RemoveAt(items.Count - 1);

            items.Insert(0, pathText);  // add to head
        }
    }

    private static void SetTextAndMoveLastChar(TextBox textBox, string text)
    {
        textBox.Text = text;
        textBox.Select(text.Length, 0);   // select last char
    }

    private static bool IsDriveLetterOnly(string path) => path.Length == 3 && path[^2..] == ":\\";

    #endregion

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

        double x = 0.01 - offset.X;
        double y = rootGrid.ActualHeight - offset.Y;
        return new CustomPopupPlacement[] { new(new Point(x, y), PopupPrimaryAxis.None) };
    }

    private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton is not MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is not TextBlock textBlock)
            return;

        Text = textBlock.Text;
        NotifyTextProperty();

        IsOpenCandidatePopup = false;
        IsOpenHistoryPopup = false;

        e.Handled = true;
    }

    #endregion

    #region Candidate

    private void CandidateListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ListBoxItem listItem)
            return;

        string? text = e.Key switch
        {
            Key.Enter => listItem.Content as string,
            Key.Oem5 => listItem.Content is string ? (listItem.Content as string) + '\\' : null,    // BackslashKey
            Key.Escape => _lastCandidateFolderPath is not null ? _lastCandidateFolderPath.TrimEnd('\\') + '\\' : null,
            _ => null
        };

        if (text is null)
            return;

        SetTextAndMoveLastChar(this, text);

        // ポップアップ中のEnterキー選択でPathを確定します
        if (e.Key is Key.Enter)
            NotifyTextProperty();

        // 基本的には 選択が完了しているのでPopupをクローズしますが、
        // Backslashキー かつ 子ディレクトリが存在する場合は継続したPath選択のためクローズしません。
        IsOpenCandidatePopup = e.Key is Key.Oem5 && new DirectoryInfo(Text).EnumerateDirectories().Any();
        Keyboard.Focus(this);

        e.Handled = true;
    }

    private void CandidateParentTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpenCandidatePopup)
            return;

        if (_candidateListBox is not { } listBox)
            throw new NullReferenceException();

        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Prior: // PageUp
            case Key.Next:  // PageDown
                if (listBox.Items.Count > 0 && e.OriginalSource is not ListBoxItem)
                {
                    listBox.Focus();

                    if (listBox.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem lbi)
                        lbi.Focus();

                    e.Handled = true;
                }
                break;
        }
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

    private void HistoryParentTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsOpenCandidatePopup || IsOpenHistoryPopup)
            return;

        if (_historyListBox is not { } listBox)
            throw new NullReferenceException();

        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Prior: // PageUp
            case Key.Next:  // PageDown
                if (listBox.Items.Count > 0)
                {
                    IsOpenHistoryPopup = true;
                    listBox.Focus();
                }
                break;
        }
    }

    #endregion

}
