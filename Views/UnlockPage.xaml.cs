using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Flux.Core.Unlock;
using Flux.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace Flux.Views;

/// <summary>解锁测试单项显示模型（可通知刷新）。</summary>
public sealed class UnlockItemVm : INotifyPropertyChanged
{
    private string _name = "";
    private string _statusText = "untested";
    private string _detail = "";
    private SolidColorBrush _statusBrush = Gray();

    public string Name { get => _name; private set => Set(ref _name, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string Detail { get => _detail; private set => Set(ref _detail, value); }
    public SolidColorBrush StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Update(UnlockResult result)
    {
        Name = result.Name;
        StatusText = result.Status switch
        {
            "supported" => L10n.T("Msg_UnlockSupported"),
            "unsupported" => L10n.T("Msg_UnlockUnsupported"),
            "unknown" => L10n.T("Msg_UnlockUnknown"),
            "failed" => L10n.T("Msg_UnlockFailed"),
            "testing" => L10n.T("Msg_UnlockTesting"),
            _ => L10n.T("Msg_UnlockUntested"),
        };
        Detail = string.IsNullOrEmpty(result.Region)
            ? result.Detail ?? ""
            : $"{result.Detail} · {result.Region}".Trim(' ', '·');
        StatusBrush = result.Status switch
        {
            "supported" => Green(),
            "unsupported" => Red(),
            "failed" => Orange(),
            _ => Gray(),
        };
    }

    private static SolidColorBrush Gray() => new(Color.FromArgb(255, 128, 128, 128));
    private static SolidColorBrush Green() => new(Color.FromArgb(255, 46, 160, 67));
    private static SolidColorBrush Red() => new(Color.FromArgb(255, 205, 80, 80));
    private static SolidColorBrush Orange() => new(Color.FromArgb(255, 220, 150, 40));
}

public sealed partial class UnlockPage : Page
{
    private readonly UnlockTestService _service;
    private readonly Dictionary<string, UnlockItemVm> _items = [];
    private bool _running;

    public UnlockPage()
    {
        InitializeComponent();
        _service = new UnlockTestService(AppServices.Config.MixedPort);
        foreach (var check in _service.Checks)
        {
            var vm = new UnlockItemVm();
            vm.Update(new UnlockResult(check.Id, check.Name, "untested"));
            _items[check.Id] = vm;
            ResultList.Items.Add(vm);
        }
    }

    private async void RunAll_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        RunAllButton.IsEnabled = false;
        StatusPanel.Visibility = Visibility.Visible;
        try
        {
            var results = await _service.RunAllAsync(timeoutMs: 8000);
            foreach (var result in results)
            {
                if (_items.TryGetValue(result.Id, out var vm))
                    vm.Update(result);
            }
        }
        finally
        {
            _running = false;
            RunAllButton.IsEnabled = true;
            StatusPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void Retest_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        if (sender is not FrameworkElement { DataContext: UnlockItemVm vm }) return;
        var check = _service.Checks.FirstOrDefault(c => c.Name == vm.Name);
        if (check is null) return;
        vm.Update(new UnlockResult(check.Id, check.Name, "testing"));
        var result = await _service.RunAsync(check, timeoutMs: 8000);
        vm.Update(result);
    }
}
