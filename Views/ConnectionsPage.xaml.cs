using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Models;
using Flux.Services;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class ConnectionsPage : Page
{
    public ConnectionsViewModel Vm { get; } = new();
    private readonly List<ConnectionVm> _filtered = new();

    /// <summary>列定义：id → 资源键 / 绑定路径 / 宽度。</summary>
    private static readonly (string Id, string ResourceKey, string BindingPath, int Width)[] AllColumns =
    [
        ("host", "Connections_Host", "Host", 0),            // 0 = 弹性宽度
        ("network", "Connections_Network", "Network", 70),
        ("download", "Connections_Download", "DownloadText", 90),
        ("upload", "Connections_Upload", "UploadText", 90),
        ("rule", "Connections_Rule", "Rule", 150),
        ("chains", "Connections_Chains", "Chains", 140),
        ("process", "Connections_Process", "Process", 130),
        ("time", "Connections_Time", "StartText", 85),
    ];

    /// <summary>列表布局固定显示的紧凑列。</summary>
    private static readonly string[] ListColumns = ["host", "rule", "download", "time"];

    private bool _rebuilding;

    public ConnectionsPage()
    {
        InitializeComponent();
        ConnList.ItemsSource = _filtered;
        RebuildColumns();
        Vm.Active.CollectionChanged += (_, _) => ApplyFilter();
        Vm.Closed.CollectionChanged += (_, _) => ApplyFilter();
        Loaded += (_, _) => Vm.Start();
        Unloaded += (_, _) => Vm.Stop();
    }

    /// <summary>按用户配置（列顺序/可见性 + 布局）重建表头与条目模板。</summary>
    private void RebuildColumns()
    {
        if (_rebuilding) return;
        _rebuilding = true;
        try
        {
            var verge = AppServices.Config.Verge;
            var layout = verge.ConnectionsLayout == "list" ? "list" : "table";

            var ordered = verge.ConnectionsColumns.Count > 0
                ? verge.ConnectionsColumns.Where(id => AllColumns.Any(c => c.Id == id)).ToList()
                : AllColumns.Select(c => c.Id).ToList();
            foreach (var c in AllColumns)
                if (!ordered.Contains(c.Id)) ordered.Add(c.Id);

            var visibleIds = layout == "list"
                ? ListColumns.ToList()
                : ordered;

            var columns = visibleIds
                .Select(id => AllColumns.First(c => c.Id == id))
                .ToList();

            BuildHeader(columns);
            ConnList.ItemTemplate = BuildItemTemplate(columns);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void BuildHeader(List<(string Id, string ResourceKey, string BindingPath, int Width)> columns)
    {
        HeaderGrid.Children.Clear();
        HeaderGrid.ColumnDefinitions.Clear();
        foreach (var (id, key, _, width) in columns)
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = width == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width),
            });
        }
        var index = 0;
        foreach (var (id, key, _, width) in columns)
        {
            var text = new TextBlock
            {
                Text = L10n.T(key),
                FontSize = 12,
                Opacity = 0.6,
            };
            Grid.SetColumn(text, index);
            HeaderGrid.Children.Add(text);
            index++;
        }
    }

    /// <summary>生成条目模板（经典 Binding，XamlReader 解析；支持用户配置的列顺序）。</summary>
    private DataTemplate BuildItemTemplate(List<(string Id, string ResourceKey, string BindingPath, int Width)> columns)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""
            <DataTemplate
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid Padding="12,6,12,6" ColumnSpacing="10" HorizontalAlignment="Stretch">
                    <Grid.ColumnDefinitions>
            """);
        foreach (var (id, key, path, width) in columns)
            sb.Append(width == 0
                ? "<ColumnDefinition Width=\"*\" />"
                : $"<ColumnDefinition Width=\"{width}\" />");
        sb.Append("</Grid.ColumnDefinitions>");
        var index = 0;
        foreach (var (id, key, path, width) in columns)
        {
            var dynamic = path.EndsWith("Text");
            var mode = dynamic ? ", Mode=OneWay" : "";
            sb.Append("            <TextBlock Text=\"{Binding ").Append(path).Append(mode)
              .Append("}\" Grid.Column=\"").Append(index)
              .Append("\" FontSize=\"11\" Opacity=\"0.6\" VerticalAlignment=\"Center\" TextTrimming=\"CharacterEllipsis\" />\n");
            index++;
        }
        sb.Append("</Grid></DataTemplate>");
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(sb.ToString());
    }

    private void ApplyFilter()
    {
        var source = (IEnumerable<ConnectionVm>?)(ClosedToggle.IsChecked == true ? Vm.Closed : Vm.Active);
        _filtered.Clear();
        if (source is not null)
        {
            var q = Vm.SearchText;
            foreach (var c in source)
            {
                if (string.IsNullOrEmpty(q) || Vm.Matches(c)) _filtered.Add(c);
            }
        }
        ConnList.ItemsSource = null;
        ConnList.ItemsSource = _filtered;
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortBox.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            Vm.SortMode = mode;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.SearchText = SearchBox.Text;
        ApplyFilter();
    }

    private void ClosedToggle_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private async void CloseAll_Click(object sender, RoutedEventArgs e)
    {
        await Vm.CloseAllAsync();
    }

    /// <summary>列设置对话框：可见性勾选 + 上移/下移排序。</summary>
    private async void ColumnSettings_Click(object sender, RoutedEventArgs e)
    {
        var verge = AppServices.Config.Verge;
        var ordered = verge.ConnectionsColumns.Count > 0
            ? verge.ConnectionsColumns.Where(id => AllColumns.Any(c => c.Id == id)).ToList()
            : AllColumns.Select(c => c.Id).ToList();
        foreach (var c in AllColumns)
            if (!ordered.Contains(c.Id)) ordered.Add(c.Id);

        var panel = new StackPanel { Spacing = 6, MinWidth = 360 };
        var checkboxes = new Dictionary<string, CheckBox>();
        foreach (var id in ordered)
        {
            var def = AllColumns.First(c => c.Id == id);
            var box = new CheckBox
            {
                Content = new TextBlock { Text = L10n.T(def.ResourceKey), FontSize = 13 },
                IsChecked = verge.ConnectionsColumns.Count == 0 || verge.ConnectionsColumns.Contains(id),
                Tag = id,
            };
            checkboxes[id] = box;

            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(box, 0);
            var up = new Button { Content = "↑", Padding = new Thickness(6, 2, 6, 2) };
            var down = new Button { Content = "↓", Padding = new Thickness(6, 2, 6, 2) };
            var rowGrid = new Grid { Tag = (panel, ordered) };
            Grid.SetColumn(up, 1);
            Grid.SetColumn(down, 2);
            row.Children.Add(box);
            row.Children.Add(up);
            row.Children.Add(down);
            panel.Children.Add(row);

            up.Click += (_, _) => MoveRow(panel, row, -1);
            down.Click += (_, _) => MoveRow(panel, row, +1);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L10n.T("Msg_ColumnSettings"),
            Content = new ScrollViewer { MaxHeight = 420, Content = panel },
            PrimaryButtonText = L10n.T("Common_Save"),
            CloseButtonText = L10n.T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        // 从 UI 顺序收集勾选列
        var result = new List<string>();
        foreach (var child in panel.Children.OfType<Grid>())
        {
            var box = child.Children.OfType<CheckBox>().First();
            if (box.IsChecked == true && box.Tag is string id)
                result.Add(id);
        }
        if (result.Count == 0)
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = L10n.T("Msg_ColumnSettings"),
                Content = L10n.T("Msg_NoVisibleColumns"),
                CloseButtonText = L10n.T("Common_OK"),
            }.ShowAsync();
            return;
        }

        verge.ConnectionsColumns = result;
        AppServices.Config.SaveVerge();
        RebuildColumns();
        ApplyFilter();
    }

    private static void MoveRow(StackPanel panel, Grid row, int offset)
    {
        var index = panel.Children.IndexOf(row);
        var target = index + offset;
        if (target < 0 || target >= panel.Children.Count) return;
        panel.Children.Remove(row);
        panel.Children.Insert(target, row);
    }

    private void LayoutToggle_Click(object sender, RoutedEventArgs e)
    {
        var verge = AppServices.Config.Verge;
        verge.ConnectionsLayout = verge.ConnectionsLayout == "list" ? "table" : "list";
        AppServices.Config.SaveVerge();
        LayoutButton.Content = verge.ConnectionsLayout == "list" ? L10n.T("Msg_LayoutList") : L10n.T("Msg_LayoutTable");
        RebuildColumns();
        ApplyFilter();
    }

    private async void ConnList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ClosedToggle.IsChecked == true) return;
        if (e.ClickedItem is ConnectionVm vm)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = vm.Host,
                Content = BuildDetail(vm),
                PrimaryButtonText = L10n.T("Msg_CloseThisConn"),
                CloseButtonText = L10n.T("Msg_Back"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await Vm.CloseAsync(vm);
        }
    }

    private object BuildDetail(ConnectionVm vm)
    {
        var lines = new List<string>
        {
            L10n.F("Msg_DetailNetwork", vm.Network, vm.Type),
            L10n.F("Msg_DetailHost", vm.Host),
            L10n.F("Msg_DetailDestination", vm.Destination),
            L10n.F("Msg_DetailSource", vm.Source),
            L10n.F("Msg_DetailRule", vm.Rule),
            L10n.F("Msg_DetailChains", vm.Chains),
            L10n.F("Msg_DetailProcess", vm.Process),
            L10n.F("Msg_DetailTraffic", vm.DownloadText, vm.UploadText),
            L10n.F("Msg_DetailTime", vm.StartText),
        };
        var panel = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            panel.Children.Add(new TextBlock { Text = line, FontSize = 13, IsTextSelectionEnabled = true });
        }
        return panel;
    }
}
