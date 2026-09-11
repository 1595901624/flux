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
    public string DisplayName => string.IsNullOrWhiteSpace(Item.Name) ? L10n.T("VM_ProfileUnnamed") : Item.Name;
    public string Desc => Item.Desc;
    public string Host
    {
        get
        {
            if (Item.Type == "local") return L10n.T("VM_ProfileTypeLocalFile");
            if (string.IsNullOrEmpty(Item.Url)) return "";
            try
            {
                return new Uri(Item.Url).Host;
            }
            catch { return ""; }
        }
    }
    public string TypeText => Item.Type == "remote" ? L10n.T("VM_ProfileTypeRemote") : L10n.T("VM_ProfileTypeLocalConfig");
    public string UpdatedText => Item.Updated == default
        ? L10n.T("VM_NeverUpdated")
        : L10n.F("VM_UpdatedAt", Item.Updated.ToString("MM-dd HH:mm"));

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
            return days >= 0 ? L10n.F("VM_ExpireIn", dt.ToString("yyyy-MM-dd"), days) : L10n.T("VM_Expired");
        }
    }

    public string CurrentVisibility => IsCurrent ? "Visible" : "Collapsed";
    public double CurrentOpacity => IsCurrent ? 1.0 : 0.7;
}

public partial class ProfilesViewModel : ObservableObject
{
    public ObservableCollection<ProfileItemVm> Items { get; } = new();
    private bool _subscribed;

    [ObservableProperty]
    public partial string ImportUrl { get; set; } = "";

    [ObservableProperty]
    public partial bool Busy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(StatusVisibility));
    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(StatusVisibility));

    public string StatusVisibility => (Busy || !string.IsNullOrEmpty(StatusText)) ? "Visible" : "Collapsed";

    public ProfilesViewModel()
    {
        Load();
    }

    public void Start()
    {
        if (_subscribed) return;
        AppServices.Subscription.ProfilesChanged += OnProfilesChanged;
        _subscribed = true;
    }

    public void Stop()
    {
        if (!_subscribed) return;
        AppServices.Subscription.ProfilesChanged -= OnProfilesChanged;
        _subscribed = false;
    }

    private void OnProfilesChanged() => App.UiDispatcher.TryEnqueue(Load);

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
        StatusText = L10n.T("VM_Importing");
        try
        {
            var item = await AppServices.Subscription.ImportAsync(url);
            ImportUrl = "";
            StatusText = L10n.F("VM_Imported", item.Name);
        }
        catch (Exception ex)
        {
            StatusText = L10n.F("VM_ImportFailed", ex.Message);
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
            StatusText = L10n.T("VM_ImportingLocal");
            var item = await AppServices.Subscription.ImportLocalAsync(file.Path);
            StatusText = L10n.F("VM_Imported", item.Name);
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("VM_LocalImportFailedLog", ex.Message), "error");
            StatusText = L10n.F("VM_ImportFailed", ex.Message);
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
            StatusText = L10n.F("VM_Switched", vm.DisplayName);
        }
        catch (Exception ex)
        {
            StatusText = L10n.F("VM_SwitchFailed", ex.Message);
        }
    }

    public async Task CreateEmptyAsync(string? name = null)
    {
        try
        {
            var item = await AppServices.Subscription.CreateEmptyAsync(name ?? L10n.T("VM_DefaultNewName"));
            Load();
            StatusText = L10n.F("VM_Created", item.Name);
        }
        catch (Exception ex)
        {
            StatusText = L10n.F("VM_CreateFailed", ex.Message);
        }
    }

    public async Task UpdateAllAsync()
    {
        var targets = AppServices.Config.Profiles.Items.Where(i => i.Type == "remote").ToList();
        if (targets.Count == 0)
        {
            StatusText = L10n.T("VM_NoRemoteToUpdate");
            return;
        }
        Busy = true;
        var ok = 0;
        try
        {
            foreach (var item in targets)
            {
                StatusText = L10n.F("VM_Updating", item.Name);
                try
                {
                    await AppServices.Subscription.UpdateAsync(item);
                    ok++;
                }
                catch (Exception ex)
                {
                    LogService.App(L10n.F("VM_ProfileUpdateFailedLog", item.Name, ex.Message), "warn");
                }
            }
            StatusText = L10n.F("VM_UpdateAllDone", ok, targets.Count);
        }
        finally
        {
            Busy = false;
            Load();
        }
    }

    public async Task UpdateAsync(ProfileItemVm vm)
    {
        Busy = true;
        StatusText = L10n.F("VM_Updating", vm.DisplayName);
        try
        {
            await AppServices.Subscription.UpdateAsync(vm.Item);
            StatusText = L10n.F("VM_UpdateSucceeded", vm.DisplayName);
        }
        catch (Exception ex)
        {
            StatusText = L10n.F("VM_UpdateFailed", ex.Message);
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
            StatusText = L10n.T("VM_Deleted");
        }
        catch (Exception ex)
        {
            StatusText = L10n.F("VM_DeleteFailed", ex.Message);
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
