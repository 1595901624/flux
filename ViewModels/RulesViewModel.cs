using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Flux.Services;
using Windows.UI;

namespace Flux.ViewModels;

/// <summary>规则显示模型。</summary>
public class RuleItemVm
{
    public int Index { get; init; }
    public string Type { get; init; } = "";
    public string Payload { get; init; } = "";
    public string Proxy { get; init; } = "";

    public string IndexText => Index.ToString();
    public string TypeText => Type;

    public SolidColorBrush ProxyBrush
    {
        get
        {
            if (Proxy is "DIRECT") return new SolidColorBrush(Colors.ForestGreen);
            if (Proxy is "REJECT" or "REJECT-DROP") return new SolidColorBrush(Colors.IndianRed);
            return new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
        }
    }
}

public partial class RulesViewModel : ObservableObject
{
    public ObservableCollection<RuleItemVm> Items { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    [ObservableProperty]
    public partial bool Loading { get; set; }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private List<RuleItemVm> _all = new();

    public RulesViewModel()
    {
    }

    public async Task LoadAsync()
    {
        Loading = true;
        try
        {
            var json = await AppServices.Api.GetRulesAsync();
            var list = new List<RuleItemVm>();
            if (json.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
            {
                var i = 1;
                foreach (var r in rules.EnumerateArray())
                {
                    list.Add(new RuleItemVm
                    {
                        Index = i++,
                        Type = r.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                        Payload = r.TryGetProperty("payload", out var p) ? p.GetString() ?? "" : "",
                        Proxy = r.TryGetProperty("proxy", out var px) ? px.GetString() ?? "" : "",
                    });
                }
            }
            _all = list;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            CountText = Flux.Services.L10n.F("RulesVM_LoadFailed", ex.Message) + ex.Message;
        }
        finally
        {
            Loading = false;
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(r =>
                r.Payload.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Type.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Proxy.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        Items.Clear();
        foreach (var r in filtered.Take(5000))
            Items.Add(r);
        CountText = Flux.Services.L10n.F("RulesVM_Count", _all.Count);
    }
}
