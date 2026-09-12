using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Flux.Utils;

namespace Flux.ViewModels;

/// <summary>代理组（ListView 分组头）。</summary>
public class ProxiesGroupHeader : ObservableObject
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";

    private string _now = "";
    public string Now
    {
        get => _now;
        set
        {
            if (SetProperty(ref _now, value))
                OnPropertyChanged(nameof(NowDisplay));
        }
    }

    public string NowDisplay => string.IsNullOrEmpty(Now) ? "" : Flux.Services.L10n.F("Proxies_CurrentNow", Now);
    public string NodeCountText => Flux.Services.L10n.F("Proxies_NodeCount", Nodes.Count);

    public ObservableCollection<ProxiesNodeVm> Nodes { get; } = new();

    public IAsyncRelayCommand TestDelayCommand { get; init; } = null!;

    public ProxiesGroupHeader()
    {
        Nodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(NodeCountText));
    }
}

/// <summary>代理节点（列表项）。</summary>
public class ProxiesNodeVm : ObservableObject
{
    public string Name { get; init; } = "";
    public string GroupName { get; init; } = "";
    public string Type { get; set; } = "";
    public bool Udp { get; set; }

    private int _delay = -1;
    public int Delay
    {
        get => _delay;
        set
        {
            if (SetProperty(ref _delay, value))
            {
                OnPropertyChanged(nameof(DelayText));
                OnPropertyChanged(nameof(DelayBrush));
                OnPropertyChanged(nameof(HasDelay));
                OnPropertyChanged(nameof(HasDelayVisibility));
                OnPropertyChanged(nameof(IsTesting));
                OnPropertyChanged(nameof(TestingVisibility));
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectedVisibility));
                OnPropertyChanged(nameof(UnselectedVisibility));
            }
        }
    }

    public bool HasDelay => Delay >= 0;
    public string HasDelayVisibility => Delay >= 0 ? "Visible" : "Collapsed";
    public bool IsTesting => Delay == -2;
    public string TestingVisibility => IsTesting ? "Visible" : "Collapsed";
    public string DelayText => Format.DelayText(Delay);
    public SolidColorBrush DelayBrush => Format.DelayBrush(Delay);
    public string TypeDisplay => Udp ? $"{Type} · UDP" : Type;
    public string SelectedVisibility => IsSelected ? "Visible" : "Collapsed";
    public string UnselectedVisibility => IsSelected ? "Collapsed" : "Visible";
}
