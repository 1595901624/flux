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
    public string Id { get; private set; } = "";
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
        Id = result.Id;
        Name = result.Name;
        StatusText = result.Status switch
        {
            UnlockStatus.Supported => L10n.T("Msg_UnlockSupported"),
            UnlockStatus.Unsupported => L10n.T("Msg_UnlockUnsupported"),
            UnlockStatus.Unknown => L10n.T("Msg_UnlockUnknown"),
            UnlockStatus.Failed => L10n.T("Msg_UnlockFailed"),
            UnlockStatus.Testing => L10n.T("Msg_UnlockTesting"),
            _ => L10n.T("Msg_UnlockUntested"),
        };
        Detail = string.IsNullOrEmpty(result.Region)
            ? result.Detail ?? ""
            : $"{result.Detail} · {result.Region}".Trim(' ', '·');
        StatusBrush = result.Status switch
        {
            UnlockStatus.Supported => Green(),
            UnlockStatus.Unsupported => Red(),
            UnlockStatus.Failed => Orange(),
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
    private UnlockTestService _service;
    private readonly Dictionary<string, UnlockItemVm> _items = [];
    private bool _running;
    private CancellationTokenSource? _runCts;

    public UnlockPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => _runCts?.Cancel();
        _service = new UnlockTestService(AppServices.Config.MixedPort);
        foreach (var check in _service.Checks)
        {
            var vm = new UnlockItemVm();
            vm.Update(new UnlockResult(check.Id, check.Name, UnlockStatus.Untested));
            _items[check.Id] = vm;
            ResultList.Items.Add(vm);
        }
    }

    private async void RunAll_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        _service = new UnlockTestService(AppServices.Config.MixedPort);
        using var cts = new CancellationTokenSource();
        _runCts = cts;
        RunAllButton.IsEnabled = false;
        StatusPanel.Visibility = Visibility.Visible;
        RunProgress.Visibility = Visibility.Visible;
        RunProgress.Maximum = _service.Checks.Count;
        RunProgress.Value = 0;
        StatusMessage.Text = L10n.F("Msg_UnlockProgress", 0, _service.Checks.Count);
        foreach (var check in _service.Checks)
            _items[check.Id].Update(new UnlockResult(check.Id, check.Name, UnlockStatus.Testing));
        try
        {
            var completed = 0;
            var progress = new Progress<UnlockResult>(result =>
            {
                if (!_running || cts.IsCancellationRequested || !ReferenceEquals(_runCts, cts)) return;
                if (_items.TryGetValue(result.Id, out var vm)) vm.Update(result);
                RunProgress.Value = ++completed;
                StatusMessage.Text = L10n.F("Msg_UnlockProgress", completed, _service.Checks.Count);
            });
            var results = await _service.RunAllAsync(timeoutMs: 8000, ct: cts.Token, progress: progress);
            foreach (var result in results)
            {
                if (_items.TryGetValue(result.Id, out var vm))
                    vm.Update(result);
            }
            RunProgress.Value = results.Count;
            StatusMessage.Text = L10n.F("Msg_UnlockCompleted", results.Count);
        }
        catch (OperationCanceledException) { StatusPanel.Visibility = Visibility.Collapsed; }
        catch (Exception ex)
        {
            StatusMessage.Text = L10n.F("Msg_UnlockError", ex.Message);
            LogService.App(StatusMessage.Text, "error");
        }
        finally
        {
            _running = false;
            _runCts = null;
            RunAllButton.IsEnabled = true;
            RunProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void Retest_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        if (sender is not FrameworkElement { DataContext: UnlockItemVm vm }) return;
        _service = new UnlockTestService(AppServices.Config.MixedPort);
        var check = _service.Checks.FirstOrDefault(c => c.Id == vm.Id);
        if (check is null) return;
        _running = true;
        RunAllButton.IsEnabled = false;
        using var cts = new CancellationTokenSource();
        _runCts = cts;
        vm.Update(new UnlockResult(check.Id, check.Name, UnlockStatus.Testing));
        StatusPanel.Visibility = Visibility.Visible;
        RunProgress.Visibility = Visibility.Visible;
        RunProgress.Maximum = 1;
        RunProgress.Value = 0;
        StatusMessage.Text = L10n.F("Msg_UnlockProgress", 0, 1);
        try
        {
            vm.Update(await _service.RunAsync(check, timeoutMs: 8000, ct: cts.Token));
            RunProgress.Value = 1;
            StatusMessage.Text = L10n.F("Msg_UnlockCompleted", 1);
        }
        catch (OperationCanceledException) { StatusPanel.Visibility = Visibility.Collapsed; }
        finally
        {
            _running = false;
            _runCts = null;
            RunAllButton.IsEnabled = true;
            RunProgress.Visibility = Visibility.Collapsed;
        }
    }
}
