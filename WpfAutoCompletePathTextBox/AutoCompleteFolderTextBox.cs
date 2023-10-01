using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Controls.Primitives;

namespace WpfAutoCompletePathTextBox;

public class AutoCompleteFolderTextBox : TextBox
{
    private Popup? _popup;
    private ListBox? _itemListBox;
    private Grid? _rootGrid;

    private bool _templateLoaded;
    private string? _lastFolderPath;

    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.Register(nameof(Watermark), typeof(string), typeof(AutoCompleteFolderTextBox), new PropertyMetadata(""));

    public string? Watermark
    {
        get => (string?)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    static AutoCompleteFolderTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoCompleteFolderTextBox), new FrameworkPropertyMetadata(typeof(AutoCompleteFolderTextBox)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var popup = _popup = Template.FindName("PART_Popup", this) as Popup;
        var itemList = _itemListBox = Template.FindName("PART_ItemList", this) as ListBox;
        _rootGrid = Template.FindName("root", this) as Grid;

        if (popup is null || itemList is null)
            throw new NullReferenceException();

        KeyDown += AutoCompleteTextBox_KeyDown;
        PreviewKeyDown += AutoCompleteTextBox_PreviewKeyDown;
        itemList.PreviewMouseDown += ItemList_PreviewMouseDown;
        itemList.KeyDown += ItemList_KeyDown;
        popup.CustomPopupPlacementCallback += Popup_Repositioning;

        // PopupはWindowのフォーカス状態と連動させます（Popup表示だけが残る対応）
        if (GetParentWindow(this) is { } parentWindow)
        {
            bool prevIsOpen = false;
            parentWindow.Deactivated += (_, _) => (prevIsOpen, popup.IsOpen) = (popup.IsOpen, false);
            parentWindow.Activated += (_, _) => popup.IsOpen = prevIsOpen;
        }

        _templateLoaded = true;
    }

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
        if (_itemListBox is not { } itemListBox)
            throw new NullReferenceException();

        if (itemListBox.Items.Count > 0 && e.OriginalSource is not ListBoxItem)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Prior:
                case Key.Next:
                    itemListBox.Focus();

                    if (itemListBox.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem lbi)
                        lbi.Focus();

                    e.Handled = true;
                    break;
            }
        }
    }

    private void ItemList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ListBoxItem tbi)
            return;

        if (_popup is not { } popup)
            throw new NullReferenceException();

        string? text = e.Key switch
        {
            Key.Enter => tbi.Content as string,
            Key.Oem5 => tbi.Content is string ? (tbi.Content as string) + '\\' : null,      // BackslashKey
            Key.Escape => _lastFolderPath is not null ? _lastFolderPath.TrimEnd('\\') + '\\' : null,
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
            UpdateSource();

        Keyboard.Focus(this);

        // 基本的には 選択が完了しているのでPopupをクローズしますが、
        // Backslashキー かつ 子ディレクトリが存在する場合は継続したPath選択のためクローズしません。
        popup.IsOpen = e.Key is Key.Oem5 && new DirectoryInfo(Text).EnumerateDirectories().Any();

        e.Handled = true;
    }

    private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_popup is not { } popup)
            throw new NullReferenceException();

        if (e.Key is Key.Enter)
        {
            popup.IsOpen = false;

            var current = Text;
            var newText = IsDriveLetterOnly(current) ? current : current.TrimEnd('\\');
            SetTextAndMoveLastChar(this, newText);
            UpdateSource();
        }
    }

    private void UpdateSource()
    {
        GetBindingExpression(TextBox.TextProperty).UpdateSource();
    }

    private void ItemList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton is not MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is not TextBlock tb)
            return;

        if (_popup is not { } popup)
            throw new NullReferenceException();

        Text = tb.Text;
        UpdateSource();
        popup.IsOpen = false;
        e.Handled = true;
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        if (!_templateLoaded)
            return;

        if (_popup is not { } popup)
            throw new NullReferenceException();

        if (_itemListBox?.Items is not { } items)
            throw new NullReferenceException();

        try
        {
            items.Clear();

            var text = Text;
            _lastFolderPath = Path.GetDirectoryName(text);

            foreach (string path in lookup(text))
            {
                if (text != path)
                    items.Add(path);
            }
        }
        finally
        {
            popup.IsOpen = items.Count > 0;
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

    private static void SetTextAndMoveLastChar(TextBox textBlock, string text)
    {
        textBlock.Text = text;
        textBlock.Select(text.Length, 0);   // Select last char
    }

    private static bool IsDriveLetterOnly(string path) => path.Length == 3 && path[^2..] == ":\\";
}
