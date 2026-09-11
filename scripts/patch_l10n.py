# -*- coding: utf-8 -*-
"""将 SettingsPage/ProfilesPage/EnhanceEditorDialog/ProfileEditDialog 的
硬编码组合文本替换为 L10n.T / L10n.F 资源引用。幂等：替换后标记不再匹配。"""
import io

def patch(path, pairs):
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    changed = 0
    for old, new in pairs:
        if old in s:
            s = s.replace(old, new)
            changed += 1
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"{path}: {changed} 处替换")

# ---------- SettingsPage.xaml.cs ----------
sp = [
    ('Title = "端口已被占用",\n                Content = $"端口 {port} 已被其他进程或本应用监听，请更换端口。",',
     'Title = L10n.T("Msg_PortOccupiedTitle"),\n                Content = L10n.F("Msg_PortOccupiedBody", port),'),
    ('Title = "TUN 需要特权",', 'Title = L10n.T("Msg_TunPrivilegeTitle"),'),
    ('Content = "TUN 模式需要特权运行内核。可以选择：\\n\\n" +\n                          "1. 以管理员身份运行 Flux；\\n" +\n                          "2. 安装 Flux 服务（推荐，普通用户即可使用 TUN）。",',
     'Content = L10n.T("Msg_TunPrivilegeBody"),'),
    ('PrimaryButtonText = "安装服务",', 'PrimaryButtonText = L10n.T("Settings_InstallService"),'),
    ('Content = "Flux 服务已安装并启动，普通用户模式下即可开启 TUN。"',
     'Content = L10n.T("Msg_ServiceInstalledBody")'),
    ('Title = result.Success ? "服务已安装" : "服务安装失败",',
     'Title = result.Success ? L10n.T("Msg_ServiceInstalledTitle") : L10n.T("Msg_ServiceFailedTitle"),'),
    ('await ShowInfoAsync("热键保存被拒绝", string.Join(Environment.NewLine, conflicts));',
     'await ShowInfoAsync(L10n.T("Msg_HotkeyRejected"), string.Join(Environment.NewLine, conflicts.Select(L10n.T)));'),
    ('await ShowInfoAsync("热键已保存", "全局热键已注册生效。");',
     'await ShowInfoAsync(L10n.T("Msg_HotkeySaved"), L10n.T("Msg_HotkeySavedBody"));'),
    ('Title = "语言已更改",', 'Title = L10n.T("Msg_LangChanged"),'),
    ('Content = "请重启应用以完整应用新语言设置。",', 'Content = L10n.T("Msg_LangChangedBody"),'),
    ('PrimaryButtonText = "立即重启",', 'PrimaryButtonText = L10n.T("Msg_RestartNow"),'),
    ('CloseButtonText = "稍后",', 'CloseButtonText = L10n.T("Msg_RestartLater"),'),
    ('await ShowInfoAsync("已应用", "TUN 高级设置已重载。");',
     'await ShowInfoAsync(L10n.T("Msg_Applied"), L10n.T("Msg_TunPrivilegeTitle"));'),
    ('await ShowInfoAsync("已应用", "DNS 设置已重载。");',
     'await ShowInfoAsync(L10n.T("Msg_Applied"), L10n.T("Msg_Applied"));'),
    ('await ShowInfoAsync("地址无效", "外部控制器地址不能为空。");',
     'await ShowInfoAsync(L10n.T("Msg_WebDavInvalidTitle"), L10n.T("Msg_WebDavInvalidBody"));'),
    ('await ShowInfoAsync("已应用", "外部控制器已更新并重启内核。");',
     'await ShowInfoAsync(L10n.T("Msg_Applied"), L10n.T("Msg_RestartDoneCore"));'),
    ('await ShowInfoAsync("应用失败", ex.Message + "（已回滚）");',
     'await ShowInfoAsync(L10n.T("Msg_ApplyFailedTitle"), ex.Message + L10n.T("Msg_RolledBack"));'),
    ('Content = $"{(format == "powershell" ? "PowerShell" : "CMD")} 环境变量已复制到剪贴板。",',
     'Content = L10n.F("Msg_CopiedBody", format == "powershell" ? "PowerShell" : "CMD"),'),
    ('Title = "已复制",', 'Title = L10n.T("Msg_Copied"),'),
    ('Title = "备份完成",', 'Title = L10n.T("Msg_BackupDone"),'),
    ('Content = $"已创建备份 {name}",', 'Content = L10n.F("Msg_BackupDoneBody", name),'),
    ('await ShowInfoAsync("备份失败", ex.Message);', 'await ShowInfoAsync(L10n.T("Msg_BackupFailed"), ex.Message);'),
    ('await ShowInfoAsync("读取备份失败", ex.Message); return; }', 'await ShowInfoAsync(L10n.T("Msg_ListFailed"), ex.Message); return; }'),
    ('await ShowInfoAsync("没有备份", "尚未创建任何备份。");',
     'await ShowInfoAsync(L10n.T("Msg_NoBackups"), L10n.T("Msg_NoBackupsBody"));'),
    ('await ShowInfoAsync("恢复完成", $"已从 {selectedName} 恢复并重启内核。");',
     'await ShowInfoAsync(L10n.T("Msg_RestoreDone"), L10n.F("Msg_RestoreDoneBody", selectedName));'),
    ('await ShowInfoAsync("恢复失败", ex.Message);', 'await ShowInfoAsync(L10n.T("Msg_RestoreFailed"), ex.Message);'),
    ('Title = "WebDAV 服务器设置",', 'Title = L10n.T("Msg_WebDavTitle"),'),
    ('await ShowInfoAsync("地址无效", "WebDAV 地址必须是有效的 HTTP/HTTPS URL。");',
     'await ShowInfoAsync(L10n.T("Msg_WebDavInvalidTitle"), L10n.T("Msg_WebDavInvalidBody"));'),
    ('await ShowInfoAsync("已保存", "WebDAV 配置已保存（密码经 DPAPI 加密，明文不落盘）。");',
     'await ShowInfoAsync(L10n.T("Msg_WebDavSaved"), L10n.T("Msg_WebDavSavedBody"));'),
    ('await ShowInfoAsync("未配置", "请先设置 WebDAV 服务器。");',
     'await ShowInfoAsync(L10n.T("Msg_NotConfigured"), L10n.T("Msg_NotConfiguredBody"));'),
    ('await ShowInfoAsync("上传完成", name + " 已上传到 WebDAV。");',
     'await ShowInfoAsync(L10n.T("Msg_UploadDone"), L10n.F("Msg_UploadDoneBody", name));'),
    ('await ShowInfoAsync("上传失败", ex.Message);', 'await ShowInfoAsync(L10n.T("Msg_UploadFailed"), ex.Message);'),
    ('await ShowInfoAsync("无备份", "WebDAV 上没有备份文件。");',
     'await ShowInfoAsync(L10n.T("Msg_WebDavNoBackups"), L10n.T("Msg_WebDavNoBackupsBody"));'),
    ('Title = "从 WebDAV 恢复",', 'Title = L10n.T("Msg_WebDavRestoreTitle"),'),
    ('PrimaryButtonText = "下载并恢复",', 'PrimaryButtonText = L10n.T("Msg_DownloadRestore"),'),
    ('await ShowInfoAsync("恢复完成", "已从 " + selected + " 恢复并重启内核。");',
     'await ShowInfoAsync(L10n.T("Msg_RestoreDone"), L10n.F("Msg_RestoreDoneBody", selected));'),
    ('await ShowInfoAsync("导出失败", ex.Message);', 'await ShowInfoAsync(L10n.T("Msg_ExportFailed"), ex.Message);'),
    ('await ShowInfoAsync("检查更新", "当前已是最新版本（" + current + "）。");',
     'await ShowInfoAsync(L10n.T("Msg_UpdateCheckTitle"), L10n.F("Msg_UpToDate", current));'),
    ('Title = "发现新版本 " + result.LatestVersion,', 'Title = L10n.F("Msg_NewVersion", result.LatestVersion),'),
    ('PrimaryButtonText = "打开发布页",', 'PrimaryButtonText = L10n.T("Msg_OpenReleasePage"),'),
    ('await ShowInfoAsync("检查更新失败", ex.Message);', 'await ShowInfoAsync(L10n.T("Msg_UpdateCheckFailed"), ex.Message);'),
    ('Content = path + "（内容仅保存在本地，可自行决定是否分享）",',
     'Content = path + " " + L10n.T("Msg_DiagLocalOnly"),'),
    ('var dialog = new ContentDialog\n            {\n                XamlRoot = XamlRoot,\n                Title = "恢复备份",',
     'var dialog = new ContentDialog\n            {\n                XamlRoot = XamlRoot,\n                Title = L10n.T("Settings_RestoreBackup"),'),
]
patch("Views/SettingsPage.xaml.cs", sp)

# ---------- ProfilesPage.xaml.cs ----------
pp = [
    ('Title = "删除订阅",', 'Title = L10n.T("Msg_DeleteProfileTitle"),'),
    ('Content = $"确定删除「{vm.DisplayName}」吗？",', 'Content = L10n.F("Msg_DeleteProfileBody", vm.DisplayName),'),
    ('PrimaryButtonText = "删除",', 'PrimaryButtonText = L10n.T("Common_Delete"),'),
    ('CloseButtonText = "取消",', 'CloseButtonText = L10n.T("Common_Cancel"),'),
    ('Title = "保存失败",', 'Title = L10n.T("Msg_SaveFailed"),'),
    ('CloseButtonText = "确定",', 'CloseButtonText = L10n.T("Common_OK"),'),
]
patch("Views/ProfilesPage.xaml.cs", pp)

# ---------- EnhanceEditorDialog.cs ----------
ee = [
    ('Title = item is null ? "编辑全局增强配置" : $"编辑增强配置：{item.Name}";',
     'Title = item is null ? L10n.T("Msg_EnhanceGlobalTitle") : L10n.F("Msg_EnhanceProfileTitle", item.Name);'),
    ('Text = "应用顺序：全局 Merge → 全局 Script → 订阅 Merge → 订阅 Script → Rules → Proxies → Groups",',
     'Text = L10n.T("Msg_EnhanceOrder"),'),
    ('_status.Text = "✓ 已保存";', '_status.Text = L10n.T("Msg_EnhanceSaved");'),
]
patch("Views/EnhanceEditorDialog.cs", ee)

# ---------- ProfileEditDialog.cs ----------
pe = [
    ('Title = $"编辑订阅：{item.Name}";', 'Title = L10n.F("Msg_EditProfileTitle", item.Name);'),
    ('PrimaryButtonText = "保存";', 'PrimaryButtonText = L10n.T("Common_Save");'),
    ('CloseButtonText = "取消";', 'CloseButtonText = L10n.T("Common_Cancel");'),
    ('if (name.Length == 0) return "名称不能为空";', 'if (name.Length == 0) return L10n.T("Msg_NameRequired");'),
    ('return "下载超时必须是 5-300 秒";', 'return L10n.T("Msg_TimeoutInvalid");'),
    ('return "更新间隔必须是非负整数";', 'return L10n.T("Msg_IntervalInvalid");'),
    ('return "请选择更新通道";', 'return L10n.T("Msg_ChooseChannel");'),
]
patch("Views/ProfileEditDialog.cs", pe)
