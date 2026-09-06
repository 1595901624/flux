using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Flux.Models;
using Flux.Services;
using Flux.Utils;

namespace Flux.ViewModels;

/// <summary>订阅卡片显示模型。</summary>
public class ProfileItemVm : ObservableObject
{
    public ProfileItem Item { get; }

    public ProfileItemVm(ProfileItem item, bool isCurrent)
    {
        Item = item;
        _isCurrent = isCurrent;
    }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (SetProperty(ref _isCurrent, value))
            {
                OnPropertyChanged(nameof(CurrentVisibility));
                OnPropertyChanged(nameof(CurrentOpacity));
            }
        }
    }

    public string Uid => Item.Uid;
    public string DisplayName => string.IsNullOrWhiteSpace(Item.Name) ? "(未命名)" : Item.Name;
    public string Desc => Item.Desc;
    public string Host
    {
        get
        {
            if (Item.Type == "local") return "本地文件";
            if (string.IsNullOrEmpty(Item.Url)) return "";
            try
            {
                return new Uri(Item.Url).Host;
            }
            catch { return ""; }
        }
    }
    public string TypeText => Item.Type == "remote" ? "远程订阅" : "本地配置";
    public string UpdatedText => Item.Updated == default
        ? "从未更新"
        : "更新于 " + Item.Updated.ToString("MM-dd HH:mm");

    public string UsageText
    {
        get
        {
            var e = Item.Extra;
            if (e is null || e.Total <= 0) return "";
            var used = e.Upload + e.Download;
            return $"{Format.Bytes(used)} / {Format.Bytes(e.Total)}";
        }
    }

    public double UsageRatio
    {
        get
        {
            var e = Item.Extra;
            if (e is null || e.Total <= 0) return 0;
            return Math.Clamp((e.Upload + e.Download) / (double)e.Total, 0, 1);
        }
    }

    public string UsageVisibility => Item.Extra is { Total: > 0 } ? "Visible" : "Collapsed";

    public string ExpireText
    {
        get
        {
            var e = Item.Extra;
            if (e is null || e.Expire <= 0) return "";
            var dt = DateTimeOffset.FromUnixTimeSeconds(e.Expire).LocalDateTime;
            var days = (dt - DateTime.Now).Days;
            return days >= 0 ? $"{dt:yyyy-MM-dd} 到期（剩 {days} 天）" : "已到期";
        }
    }

    public string CurrentVisibility => IsCurrent ? "Visible" : "Collapsed";
    public double CurrentOpacity => IsCurrent ? 1.0 : 0.7;
}

public partial class ProfilesViewModel : ObservableObject
{
    public ObservableCollection<ProfileItemVm> Items { get; } = new();

    [ObservableProperty]
    private string _importUrl = "";

    [ObservableProperty]
    private bool _busy;

    [ObservableProperty]
    private string _statusText = "";

    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(StatusVisibility));
    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(StatusVisibility));

    public string StatusVisibility => (Busy || !string.IsNullOrEmpty(StatusText)) ? "Visible" : "Collapsed";

    public ProfilesViewModel()
    {
        AppServices.Subscription.ProfilesChanged += () =>
            App.UiDispatcher.TryEnqueue(Load);
        Load();
    }

    public void Load()
    {
        var current = AppServices.Config.Profiles.Current;
        Items.Clear();
        foreach (var item in AppServices.Config.Profiles.Items)
        {
            Items.Add(new ProfileItemVm(item, item.Uid == current));
        }
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        var url = ImportUrl?.Trim();
        if (string.IsNullOrEmpty(url)) return;
        Busy = true;
        StatusText = "正在导入订阅…";
        try
        {
            var item = await AppServices.Subscription.ImportAsync(url);
            ImportUrl = "";
            StatusText = $"导入成功: {item.Name}";
            await AppServices.Core.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            StatusText = "导入失败: " + ex.Message;
        }
        finally
        {
            Busy = false;
            Load();
        }
    }

    public async Task ImportLocalAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add(".yaml");
            picker.FileTypeFilter.Add(".yml");
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            Busy = true;
            StatusText = "正在导入本地配置…";
            var item = await AppServices.Subscription.ImportLocalAsync(file.Path);
            StatusText = $"导入成功: {item.Name}";
            await AppServices.Core.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            LogService.App("本地导入失败: " + ex, "error");
            StatusText = "导入失败: " + ex.Message;
        }
        finally
        {
            Busy = false;
            Load();
        }
    }

    public async Task SelectAsync(ProfileItemVm vm)
    {
        try
        {
            await AppServices.Subscription.SelectAsync(vm.Uid);
            Load();
            StatusText = $"已切换: {vm.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = "切换失败: " + ex.Message;
        }
    }

    public async Task UpdateAsync(ProfileItemVm vm)
    {
        Busy = true;
        StatusText = $"正在更新: {vm.DisplayName}…";
        try
        {
            await AppServices.Subscription.UpdateAsync(vm.Item);
            StatusText = $"更新成功: {vm.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = "更新失败: " + ex.Message;
        }
        finally
        {
            Busy = false;
            Load();
        }
    }

    public async Task DeleteAsync(ProfileItemVm vm)
    {
        Busy = true;
        try
        {
            await AppServices.Subscription.DeleteAsync(vm.Item);
            StatusText = "已删除";
        }
        catch (Exception ex)
        {
            StatusText = "删除失败: " + ex.Message;
        }
        finally
        {
            Busy = false;
            Load();
        }
    }

    public async Task MoveAsync(ProfileItemVm vm, int offset)
    {
        var items = AppServices.Config.Profiles.Items;
        var idx = items.FindIndex(i => i.Uid == vm.Uid);
        var target = idx + offset;
        if (idx < 0 || target < 0 || target >= items.Count) return;
        (items[idx], items[target]) = (items[target], items[idx]);
        AppServices.Config.SaveProfiles();
        Load();
        await Task.CompletedTask;
    }
}
