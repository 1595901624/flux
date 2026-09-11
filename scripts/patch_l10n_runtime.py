# -*- coding: utf-8 -*-
"""ViewModel/服务层运行时消息 → L10n.T/L10n.F 迁移补丁。幂等。"""
import io

def patch(path, pairs, extra_using=None):
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    changed = 0
    for old, new in pairs:
        if old in s:
            s = s.replace(old, new)
            changed += 1
    if extra_using and extra_using not in s and "using Flux.Services;" not in s:
        s = s.replace("namespace", extra_using + "\n\nnamespace", 1)
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"{path}: {changed} 处替换")

# ---------- ProfilesViewModel ----------
patch("ViewModels/ProfilesViewModel.cs", [
    ('string.IsNullOrWhiteSpace(Item.Name) ? "(未命名)" : Item.Name', 'string.IsNullOrWhiteSpace(Item.Name) ? L10n.T("VM_ProfileUnnamed") : Item.Name'),
    ('if (Item.Type == "local") return "本地文件";', 'if (Item.Type == "local") return L10n.T("VM_ProfileTypeLocalFile");'),
    ('Item.Type == "remote" ? "远程订阅" : "本地配置"', 'Item.Type == "remote" ? L10n.T("VM_ProfileTypeRemote") : L10n.T("VM_ProfileTypeLocalConfig")'),
    ('? "从未更新"', '? L10n.T("VM_NeverUpdated")'),
    (': "更新于 " + Item.Updated.ToString("MM-dd HH:mm");', ': L10n.F("VM_UpdatedAt", Item.Updated.ToString("MM-dd HH:mm"));'),
    ('return days >= 0 ? $"{dt:yyyy-MM-dd} 到期（剩 {days} 天）" : "已到期";', 'return days >= 0 ? L10n.F("VM_ExpireIn", dt.ToString("yyyy-MM-dd"), days) : L10n.T("VM_Expired");'),
    ('StatusText = "正在导入订阅…";', 'StatusText = L10n.T("VM_Importing");'),
    ('StatusText = $"导入成功: {item.Name}";', 'StatusText = L10n.F("VM_Imported", item.Name);'),
    ('StatusText = "导入失败: " + ex.Message;', 'StatusText = L10n.F("VM_ImportFailed", ex.Message);'),
    ('StatusText = "正在导入本地配置…";', 'StatusText = L10n.T("VM_ImportingLocal");'),
    ('LogService.App("本地导入失败: " + ex, "error");', 'LogService.App(L10n.F("VM_LocalImportFailedLog", ex.Message), "error");'),
    ('StatusText = $"已切换: {vm.DisplayName}";', 'StatusText = L10n.F("VM_Switched", vm.DisplayName);'),
    ('StatusText = "切换失败: " + ex.Message;', 'StatusText = L10n.F("VM_SwitchFailed", ex.Message);'),
    ('await AppServices.Subscription.CreateEmptyAsync(name ?? "新配置");', 'await AppServices.Subscription.CreateEmptyAsync(name ?? L10n.T("VM_DefaultNewName"));'),
    ('StatusText = $"已创建: {item.Name}";', 'StatusText = L10n.F("VM_Created", item.Name);'),
    ('StatusText = "创建失败: " + ex.Message;', 'StatusText = L10n.F("VM_CreateFailed", ex.Message);'),
    ('StatusText = "没有可更新的远程订阅";', 'StatusText = L10n.T("VM_NoRemoteToUpdate");'),
    ('StatusText = $"正在更新: {item.Name}…";', 'StatusText = L10n.F("VM_Updating", item.Name);'),
    ('LogService.App($"订阅更新失败: {item.Name}: {ex.Message}", "warn");', 'LogService.App(L10n.F("VM_ProfileUpdateFailedLog", item.Name, ex.Message), "warn");'),
    ('StatusText = $"全部更新完成：成功 {ok}/{targets.Count}";', 'StatusText = L10n.F("VM_UpdateAllDone", ok, targets.Count);'),
    ('StatusText = $"正在更新: {vm.DisplayName}…";', 'StatusText = L10n.F("VM_Updating", vm.DisplayName);'),
    ('StatusText = $"更新成功: {vm.DisplayName}";', 'StatusText = L10n.F("VM_UpdateSucceeded", vm.DisplayName);'),
    ('StatusText = "更新失败: " + ex.Message;', 'StatusText = L10n.F("VM_UpdateFailed", ex.Message);'),
    ('StatusText = "已删除";', 'StatusText = L10n.T("VM_Deleted");'),
    ('StatusText = "删除失败: " + ex.Message;', 'StatusText = L10n.F("VM_DeleteFailed", ex.Message);'),
])

# ---------- HomeViewModel ----------
patch("ViewModels/HomeViewModel.cs", [
    ('public partial string ProfileName { get; set; } = "（未启用）";', 'public partial string ProfileName { get; set; } = "";'),
    ('public partial string ModeText { get; set; } = "规则";', 'public partial string ModeText { get; set; } = "";'),
    ('public partial string SubscriptionStatusText { get; set; } = "点击卡片管理订阅";', 'public partial string SubscriptionStatusText { get; set; } = "";'),
    ('public partial string CoreStatusText { get; set; } = "检查中…";', 'public partial string CoreStatusText { get; set; } = "";'),
    ('? $"{(int)up.TotalHours} 小时 {up.Minutes} 分"', '? L10n.F("VM_UptimeHours", (int)up.TotalHours, up.Minutes)'),
    (': $"{up.Minutes} 分 {up.Seconds} 秒";', ': L10n.F("VM_UptimeMinutes", up.Minutes, up.Seconds);'),
    ('UptimeText = "未运行";', 'UptimeText = L10n.T("VM_NotRunning");'),
    ('RuleCountText = count > 0 ? $"{count} 条" : "";', 'RuleCountText = count > 0 ? L10n.F("VM_RulesCount", count) : "";'),
    ('ProfileName = current?.Name ?? "（未启用订阅）";', 'ProfileName = current?.Name ?? L10n.T("VM_ProfileNoSubscription");'),
    ('? "尚未导入订阅，点击前往"', '? L10n.T("VM_SubNoProfile")'),
    (': current.Type == "remote" ? "远程订阅 · 点击卡片管理" : "本地配置 · 点击卡片管理";', ': current.Type == "remote" ? L10n.T("VM_SubRemoteHint") : L10n.T("VM_SubLocalHint");'),
    ('CoreStatusText = "内核运行中";', 'CoreStatusText = L10n.T("VM_CoreRunning");'),
    ('CoreStatusText = "内核未运行";', 'CoreStatusText = L10n.T("VM_CoreNotRunning");'),
    ('throw new InvalidOperationException("TUN 模式需要以管理员身份运行应用");', 'throw new InvalidOperationException(L10n.T("VM_TunNeedAdmin"));'),
    ('throw new InvalidOperationException("内核未运行或拒绝了 TUN 配置");', 'throw new InvalidOperationException(L10n.T("VM_TunRejected"));'),
])

# ---------- ConnectionsViewModel ----------
patch("ViewModels/ConnectionsViewModel.cs", [
    ('CountText = $"活跃 {Active.Count} · 已关闭 {Closed.Count}";', 'CountText = L10n.F("VM_ConnCount", Active.Count, Closed.Count);'),
    ('LogService.App("关闭连接失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("VM_ConnCloseFailed", ex.Message), "warn");'),
    ('LogService.App("关闭全部连接失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("VM_ConnCloseAllFailed", ex.Message), "warn");'),
])

# ---------- LogsViewModel ----------
patch("ViewModels/LogsViewModel.cs", [
    ('CountText = $"{Items.Count} 条";', 'CountText = L10n.F("VM_LogsCount", Items.Count);'),
])

# ---------- SubscriptionService ----------
patch("Services/SubscriptionService.cs", [
    ('throw new InvalidOperationException("文件不是有效的 YAML 配置");', 'throw new InvalidOperationException(L10n.T("SVC_InvalidYaml"));'),
    ('throw new InvalidOperationException("仅远程订阅可更新");', 'throw new InvalidOperationException(L10n.T("SVC_OnlyRemoteUpdatable"));'),
    ('LogService.App($"订阅未变化（304）: {item.Name}");', 'LogService.App(L10n.F("SVC_NotModifiedLog", item.Name));'),
    ('throw new InvalidOperationException("订阅更新失败: " + ex.Message, ex);', 'throw new InvalidOperationException(L10n.F("SVC_UpdateFailed", ex.Message), ex);'),
    ('Name = string.IsNullOrWhiteSpace(name) ? "新配置" : name,', 'Name = string.IsNullOrWhiteSpace(name) ? L10n.T("VM_DefaultNewName") : name,'),
    ('throw new InvalidOperationException("下载失败: " + last?.Message, last);', 'throw new InvalidOperationException(L10n.F("SVC_DownloadFailed", last?.Message ?? ""), last);'),
    ('throw new InvalidOperationException("订阅地址必须是有效的 HTTP/HTTPS URL");', 'throw new InvalidOperationException(L10n.T("SVC_InvalidUrl"));'),
    ('throw new InvalidOperationException("订阅内容超过 20 MiB 限制");', 'throw new InvalidOperationException(L10n.T("SVC_TooLarge"));'),
    ('LogService.App($"导入后应用订阅失败: {item.Name}", "warn");', 'LogService.App(L10n.F("SVC_ApplyAfterImportFailed", item.Name), "warn");'),
    ('LogService.App($"订阅自动更新成功: {item.Name}");', 'LogService.App(L10n.F("SVC_AutoUpdateOk", item.Name));'),
    ('LogService.App($"订阅自动更新失败: {item.Name}: {ex.Message}", "warn");', 'LogService.App(L10n.F("SVC_AutoUpdateFailed", item.Name, ex.Message), "warn");'),
])

# ---------- TrayService ----------
patch("Services/TrayService.cs", [
    ('LogService.App("托盘初始化失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Tray_InitFailed", ex.Message), "warn");'),
    ('new PopupMenuItem("显示主窗口", (_, _) =>', 'new PopupMenuItem(L10n.T("Tray_ShowWindow"), (_, _) =>'),
    ('CreateModeItem("规则模式", "rule", mode),', 'CreateModeItem(L10n.T("Tray_ModeRule"), "rule", mode),'),
    ('CreateModeItem("全局模式", "global", mode),', 'CreateModeItem(L10n.T("Tray_ModeGlobal"), "global", mode),'),
    ('CreateModeItem("直连模式", "direct", mode),', 'CreateModeItem(L10n.T("Tray_ModeDirect"), "direct", mode),'),
    ('new PopupMenuItem("系统代理", (_, _) =>', 'new PopupMenuItem(L10n.T("Tray_SystemProxy"), (_, _) =>'),
    ('new PopupMenuItem("TUN 模式", (_, _) =>', 'new PopupMenuItem(L10n.T("Tray_TunMode"), (_, _) =>'),
    ('new PopupMenuItem("轻量模式", (_, _) =>', 'new PopupMenuItem(L10n.T("Tray_Lightweight"), (_, _) =>'),
    ('new PopupMenuItem("重启内核", (_, _) =>', 'new PopupMenuItem(L10n.T("Tray_RestartCore"), (_, _) =>'),
    ('new PopupMenuItem("退出", (_, _) => _dispatcher?.TryEnqueue(ExitApp)),', 'new PopupMenuItem(L10n.T("Tray_Exit"), (_, _) => _dispatcher?.TryEnqueue(ExitApp)),'),
    ('LogService.App("托盘菜单刷新失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Tray_MenuRefreshFailed", ex.Message), "warn");'),
    ('var sub = new PopupSubMenu("代理组");', 'var sub = new PopupSubMenu(L10n.T("Tray_ProxyGroups"));'),
    ('sub.Items.Add(new PopupMenuItem("(内核未运行)", (_, _) => { }) { Enabled = false });', 'sub.Items.Add(new PopupMenuItem(L10n.T("Tray_CoreNotRunning"), (_, _) => { }) { Enabled = false });'),
    ('LogService.App("托盘切换节点失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Tray_ToggleNodeFailed", ex.Message), "warn");'),
    ('var sub = new PopupSubMenu("订阅");', 'var sub = new PopupSubMenu(L10n.T("Tray_Profiles"));'),
    ('sub.Items.Add(new PopupMenuItem("(暂无订阅)", (_, _) => { }) { Enabled = false });', 'sub.Items.Add(new PopupMenuItem(L10n.T("Tray_NoProfiles"), (_, _) => { }) { Enabled = false });'),
    ('LogService.App("订阅切换失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Tray_ProfileSwitchFailed", ex.Message), "warn");'),
    ('LogService.App("模式切换失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Tray_ModeSwitchFailed", ex.Message), "warn");'),
    ('LogService.App("系统代理切换失败: " + ex.Message, "error");', 'LogService.App(L10n.F("Tray_SysProxyToggleFailed", ex.Message), "error");'),
    ('ShowNotification("系统代理切换失败: " + ex.Message);', 'ShowNotification(L10n.F("Tray_SysProxyToggleFailed", ex.Message));'),
    ('ShowNotification("TUN 模式需要以管理员身份运行应用");', 'ShowNotification(L10n.T("VM_TunNeedAdmin"));'),
    ('ShowNotification("内核未运行或拒绝了 TUN 配置");', 'ShowNotification(L10n.T("VM_TunRejected"));'),
    ('catch (Exception ex) { LogService.App("退出清理未完成: " + ex.Message, "warn"); }', 'catch (Exception ex) { LogService.App(L10n.F("Tray_ExitCleanupIncomplete", ex.Message), "warn"); }'),
])

# ---------- Format.ModeText ----------
patch("Utils/Format.cs", [
    ('"rule" => "规则",', '"rule" => Flux.Services.L10n.T("Fmt_ModeRule"),'),
    ('"global" => "全局",', '"global" => Flux.Services.L10n.T("Fmt_ModeGlobal"),'),
    ('"direct" => "直连",', '"direct" => Flux.Services.L10n.T("Fmt_ModeDirect"),'),
])
