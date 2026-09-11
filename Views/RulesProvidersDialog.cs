using Flux.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Flux.Views;

/// <summary>规则 Provider 对话框：展示类型/行为/规则数/更新时间，支持单个与全部更新。</summary>
public sealed class RulesProvidersDialog : ContentDialog
{
    private readonly StackPanel _list = new() { Spacing = 6 };
    private readonly TextBlock _status = new() { Opacity = 0.75, FontSize = 12 };
    private List<RuleProviderInfo> _providers = [];

    public RulesProvidersDialog(XamlRoot root)
    {
        XamlRoot = root;
        Title = L10n.T("Msg_ProviderTitleRules");
        CloseButtonText = L10n.T("Common_Close");

        var header = new StackPanel { Spacing = 10, MinWidth = 480 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refreshAll = new Button { Content = L10n.T("Profiles_UpdateAll") };
        refreshAll.Click += async (_, _) => await UpdateAllAsync();
        var reload = new Button { Content = L10n.T("Msg_RefreshList") };
        reload.Click += async (_, _) => await LoadAsync();
        toolbar.Children.Add(refreshAll);
        toolbar.Children.Add(reload);
        header.Children.Add(toolbar);
        header.Children.Add(_status);
        header.Children.Add(_list);

        Content = new ScrollViewer { MaxHeight = 480, Content = header };
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _status.Text = L10n.T("Msg_Loading");
        _list.Children.Clear();
        _providers = await AppServices.Api.GetRuleProviderInfoAsync();
        if (_providers.Count == 0)
        {
            _status.Text = L10n.T("Msg_NoRuleProviders");
            return;
        }
        _status.Text = L10n.F("Msg_ProviderCount", _providers.Count);
        foreach (var provider in _providers)
            _list.Children.Add(BuildRow(provider));
    }

    private Grid BuildRow(RuleProviderInfo provider)
    {
        var grid = new Grid { ColumnSpacing = 10, Padding = new Thickness(8, 6, 8, 6) };
        grid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.Transparent);
        for (var i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        var name = new TextBlock { Text = provider.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var meta = new TextBlock
        {
            Text = L10n.F("Msg_RulesMeta", provider.Type, provider.Behavior, provider.RuleCount),
            FontSize = 11,
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var updated = new TextBlock
        {
            Text = FormatTime(provider.UpdatedAt),
            FontSize = 11,
            Opacity = 0.55,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var update = new Button { Content = L10n.T("Msg_Update") };
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            _status.Text = L10n.F("Msg_Updating", provider.Name);
            try
            {
                await AppServices.Api.UpdateRuleProviderAsync(provider.Name);
                _status.Text = L10n.F("Msg_UpdatedOne", provider.Name);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _status.Text = L10n.F("Msg_UpdateFailedNamed", provider.Name, ex.Message);
                update.IsEnabled = true;
            }
        };

        Grid.SetColumn(name, 0);
        Grid.SetColumn(meta, 1);
        Grid.SetColumn(updated, 2);
        Grid.SetColumn(update, 3);
        grid.Children.Add(name);
        grid.Children.Add(meta);
        grid.Children.Add(updated);
        grid.Children.Add(update);
        return grid;
    }

    private async Task UpdateAllAsync()
    {
        _status.Text = L10n.T("Msg_UpdatingAll");
        var ok = 0;
        foreach (var provider in _providers)
        {
            try
            {
                await AppServices.Api.UpdateRuleProviderAsync(provider.Name);
                ok++;
            }
            catch { }
        }
        _status.Text = L10n.F("Msg_UpdateAllDoneCount", ok, _providers.Count);
        await LoadAsync();
    }

    private static string FormatTime(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return L10n.T("Msg_NeverUpdatedShort");
        if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");
        return L10n.T("Msg_NeverUpdatedShort");
    }
}
