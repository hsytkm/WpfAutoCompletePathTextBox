using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Controls.Primitives;

namespace WpfAutoCompletePathTextBox;

public partial class SelectFolderTextBox : TextBox
{
    private Popup? _popup;
    private ListBox? _itemListBox;
    private Grid? _rootGrid;

    private bool _templateLoaded;
    private string? _lastDirectoryPath;

    public SelectFolderTextBox()
    {
        InitializeComponent();
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
                    itemListBox.SelectedIndex = 0;

                    var selectedIndex = itemListBox.SelectedIndex;
                    var lbi = itemListBox.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as ListBoxItem;
                    lbi?.Focus();

                    e.Handled = true;
                    break;
            }
        }
    }

    private void ItemList_KeyDown(object sender, KeyEventArgs e)
    {
        if (_popup is not { } popup)
            throw new NullReferenceException();

        if (e.OriginalSource is not ListBoxItem tbi)
            return;

        string? text = e.Key switch
        {
            Key.Enter => tbi.Content as string,
            Key.Escape => _lastDirectoryPath is null ? null : _lastDirectoryPath.TrimEnd('\\') + '\\',
            _ => null
        };

        if (text is null)
        {
            e.Handled = false;
            return;
        }

        // 以下だとポップアップのEnterキー選択でPathを確定しません
        //if (e.Key is Key.Enter)
        //    UpdateSource();

        Text = text;
        Select(Text.Length, 0);  // Select last char

        // 以下だとポップアップのEnterキー選択でPathを確定します
        if (e.Key is Key.Enter)
            UpdateSource();

        Keyboard.Focus(this);
        popup.IsOpen = false;
        e.Handled = true;
    }

    private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_popup is not { } popup)
            throw new NullReferenceException();

        if (e.Key is Key.Enter)
        {
            popup.IsOpen = false;
            UpdateSource();
        }
    }

    private void UpdateSource()
    {
        GetBindingExpression(TextBox.TextProperty).UpdateSource();
    }

    private void ItemList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_popup is not { } popup)
            throw new NullReferenceException();

        if (e.LeftButton is not MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is not TextBlock tb)
            return;

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
            _lastDirectoryPath = Path.GetDirectoryName(text);

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
                    if (path.Length == 3 && path[^2..] == ":\\")
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
}
