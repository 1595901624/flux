# -*- coding: utf-8 -*-
"""第二轮 i18n 补漏：XAML uid + 代码 L10n 替换。幂等。"""
import io, re

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

# ---------- XAML uid 补漏 ----------
def add_uid(path, needle, uid):
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    pos = s.find(needle)
    if pos < 0:
        print(f"MISS: {uid}")
        return
    pos2 = s.rfind("<", 0, pos)
    line_start = s.rfind("\n", 0, pos2) + 1
    line = s[line_start:s.find("\n", pos2)]
    if "x:Uid" in line:
        print(f"SKIP: {uid}")
        return
    m = re.match(r"(\s*)<([\w:.]+)", line)
    if not m:
        print(f"NORULE: {uid}")
        return
    new_line = line.replace(f"<{m.group(2)}", f'<{m.group(2)} x:Uid="{uid}"', 1)
    s = s[:line_start] + new_line + s[line_end_idx(s, pos2)]
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"OK: {uid}")

def line_end_idx(s, pos):
    return s.find("\n", pos)

HX = 'Views/HomePage.xaml'
PX = 'Views/ProxiesPage.xaml'
PFX = 'Views/ProfilesPage.xaml'
SX = 'Views/SettingsPage.xaml'

add_uid(HX, 'Header="系统代理"', "Home_ToggleSysProxy")
add_uid(HX, 'Header="TUN 模式"', "Home_ToggleTun")
add_uid(HX, 'RadioButton Content="规则"', "Fmt_ModeRule")
add_uid(HX, 'RadioButton Content="全局"', "Fmt_ModeGlobal")
add_uid(HX, 'RadioButton Content="直连"', "Fmt_ModeDirect")
add_uid(HX, 'Run Text="混合端口: "', "Home_MixedPortLabel")
add_uid(HX, 'Run Text="运行时间: "', "Home_UptimeLabel")
add_uid(HX, 'Run Text="规则数量: "', "Home_RulesCountLabel")
add_uid(HX, 'Button Content="重启内核"', "Home_RestartCore")
add_uid(HX, 'Button Content="打开日志目录"', "Home_OpenLogs")
add_uid(PX, 'SegmentedItem Tag="rule"', "Fmt_ModeRule")
add_uid(PX, 'SegmentedItem Tag="global"', "Fmt_ModeGlobal")
add_uid(PX, 'SegmentedItem Tag="direct"', "Fmt_ModeDirect")
add_uid(PX, 'ToolTip="整组测速"', "Proxies_TipGroupTest")
add_uid(PFX, 'TextBlock Text="当前"', "Profiles_CurrentBadge")
add_uid(PFX, 'ToolTipService.ToolTip="更新订阅"', "Profiles_TipUpdate")
add_uid(PFX, 'ToolTipService.ToolTip="删除订阅"', "Profiles_TipDelete")
add_uid(SX, 'ComboBoxItem Content="跟随系统" Tag="system"', "Settings_ThemeSystem")

# 注意：语言框的"跟随系统"不需要翻译（保持母语显示自身）

# ---------- 代码替换 ----------
patch("ViewModels/ProxiesModels.cs", [
    ('? $"当前: {Now}"', '? Flux.Services.L10n.F("Proxies_CurrentNow", Now)'),
    ('NodeCountText => $"{Nodes.Count} 个";', 'NodeCountText => Flux.Services.L10n.F("Proxies_NodeCount", Nodes.Count);'),
])
patch("ViewModels/ProxiesViewModel.cs", [
    ('LogService.App("模式切换失败: " + ex.Message, "warn");', 'LogService.App(Flux.Services.L10n.F("ProxiesVM_ModeSwitchFailed", ex.Message), "warn");'),
    ('LogService.App($"切换节点后已关闭 {closed} 个旧连接");', 'LogService.App(Flux.Services.L10n.F("ProxiesVM_ClosedOldConnections", closed));'),
    ('LogService.App($"切换节点失败 [{group} → {name}]: " + ex.Message, "warn");', 'LogService.App(Flux.Services.L10n.F("ProxiesVM_NodeSwitchFailed", group, name, ex.Message), "warn");'),
])
patch("ViewModels/RulesViewModel.cs", [
    ('"加载失败: "', 'Flux.Services.L10n.F("RulesVM_LoadFailed", ex.Message)'),
    ('$"共 {_all.Count} 条"', 'Flux.Services.L10n.F("RulesVM_Count", _all.Count)'),
])
patch("Views/LogsPage.xaml.cs", [
    ('text.Text = paused ? "继续" : "暂停";', 'text.Text = paused ? Flux.Services.L10n.T("Logs_Continue") : Flux.Services.L10n.T("Logs_Pause");'),
])
patch("Views/HomePage.xaml.cs", [
    ('CloseButtonText = "确定",', 'CloseButtonText = L10n.T("Common_OK"),'),
])
patch("Views/SettingsPage.xaml.cs", [
    ('? "Flux 服务已安装并启动，普通用户模式下即可开启 TUN。"', '? L10n.T("Msg_ServiceInstalledBody")'),
    ('result.Error?.ToString() ?? "未知错误",', 'result.Error?.ToString() ?? L10n.T("Priv_UnknownError"),'),
])
patch("Views/EnhanceEditorDialog.cs", [
    ('PrimaryButtonText = "保存并应用";', 'PrimaryButtonText = L10n.T("Enhace_SaveAndApply");'),
    ('CloseButtonText = "关闭";', 'CloseButtonText = L10n.T("Common_Close");'),
    ('(ChainType.Merge, "全局 Merge", true),', '(ChainType.Merge, L10n.T("Enhance_NameGlobalMerge"), true),'),
    ('(ChainType.Script, "全局 Script", true),', '(ChainType.Script, L10n.T("Enhance_NameGlobalScript"), true),'),
    ('(ChainType.Merge, "订阅 Merge", false),', '(ChainType.Merge, L10n.T("Enhance_NameProfileMerge"), false),'),
    ('(ChainType.Script, "订阅 Script", false),', '(ChainType.Script, L10n.T("Enhance_NameProfileScript"), false),'),
    ('(ChainType.Rules, "订阅 Rules", false),', '(ChainType.Rules, L10n.T("Enhance_NameProfileRules"), false),'),
    ('(ChainType.Proxies, "订阅 Proxies", false),', '(ChainType.Proxies, L10n.T("Enhance_NameProfileProxies"), false),'),
    ('(ChainType.Groups, "订阅 Groups", false),', '(ChainType.Groups, L10n.T("Enhance_NameProfileGroups"), false),'),
])
patch("Views/ProfileEditDialog.cs", [
    ('PlaceholderText = "订阅名称"', 'PlaceholderText = L10n.T("ProfileEdit_NamePlaceholder")'),
    ('PlaceholderText = "描述（可选）"', 'PlaceholderText = L10n.T("ProfileEdit_DescPlaceholder")'),
    ('PlaceholderText = "默认 clash-verge/v2.5.2"', 'PlaceholderText = L10n.T("ProfileEdit_UaPlaceholder")'),
    ('Content = "接受无效 TLS 证书（危险）"', 'Content = L10n.T("ProfileEdit_InvalidCertLabel")'),
    ('Content = "参与自动更新"', 'Content = L10n.T("ProfileEdit_AutoUpdateLabel")'),
    ('("自动回退（直连→内核→系统）", "auto"),', '("自动回退（直连→内核→系统）", "auto"),'),  # 保留（下方统一替换）
    ('form.Children.Add(Field("名称", _nameBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldName"), _nameBox));'),
    ('form.Children.Add(Field("描述", _descBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldDesc"), _descBox));'),
    ('form.Children.Add(Field("订阅 URL", _urlBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldUrl"), _urlBox));'),
    ('form.Children.Add(Field("User-Agent", _uaBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldUa"), _uaBox));'),
    ('form.Children.Add(Field("更新通道", _channelBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldChannel"), _channelBox));'),
    ('form.Children.Add(Field("更新间隔（分钟，0=不自动）", _intervalBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldInterval"), _intervalBox));'),
    ('form.Children.Add(Field("下载超时（秒）", _timeoutBox));', 'form.Children.Add(Field(L10n.T("ProfileEdit_FieldTimeout"), _timeoutBox));'),
    ('var name = _nameBox.Text.Trim();\n        if (name.Length == 0) return L10n.T("Msg_NameRequired");', 'var name = _nameBox.Text.Trim();\n        if (name.Length == 0) return L10n.T("Msg_NameRequired");'),
])
patch("Services/HotkeyService.cs", [
    ('failures.Add($"{action}: 无法识别的组合键「{comboText}」");', 'failures.Add(L10n.F("Hotkey_UnknownCombo", action, comboText));'),
    ('failures.Add($"{action}: 与「{duplicate.Action}」的热键冲突（{comboText}）");', 'failures.Add(L10n.F("Hotkey_Duplicate", action, duplicate.Action, comboText));'),
    ('failures.Add($"{action}: 组合键「{combo.Display}」已被其他程序占用");', 'failures.Add(L10n.F("Hotkey_InUse", action, combo.Display));'),
])
patch("Services/PrivilegeBroker.cs", [
    ('"通过服务启动内核",', 'L10n.T("Priv_ActionStartCore"),'),
    ('"通过服务停止内核",', 'L10n.T("Priv_ActionStopCore"),'),
    ('return OperationResult<bool>.Fail("service_unavailable", "无法连接 Flux 服务", action);', 'return OperationResult<bool>.Fail("service_unavailable", L10n.T("Priv_ServiceUnavailable"), action);'),
    ('"服务协议版本不匹配（服务 v{response.Version}，应用 v{ServiceProtocol.Version}），请重启服务", action);', 'L10n.F("Priv_VersionMismatch", response.Version, ServiceProtocol.Version), action);'),
    ('response.Error ?? "未知服务错误");', 'response.Error ?? L10n.T("Priv_UnknownError"));'),
    ('"未找到服务安装器，请重新安装应用", "服务安装");', 'L10n.T("Priv_InstallerMissing"), L10n.T("Priv_InstallStep"));'),
    ('return OperationResult<bool>.Fail("installer_failed", "无法启动安装器", "服务安装");', 'return OperationResult<bool>.Fail("installer_failed", L10n.T("Priv_InstallerLaunchFailed"), L10n.T("Priv_InstallStep"));'),
    ('$"安装器退出码 {process.ExitCode}（用户取消或安装失败）", "服务安装");', 'L10n.F("Priv_InstallerExitCode", process.ExitCode), L10n.T("Priv_InstallStep"));'),
    ('return OperationResult<bool>.Fail("service_not_ready", "服务已安装但未就绪", "服务安装");', 'return OperationResult<bool>.Fail("service_not_ready", L10n.T("Priv_NotReady"), L10n.T("Priv_InstallStep"));'),
    ('return OperationResult<bool>.Fail("uac_cancelled", "用户取消了 UAC 授权", "服务安装");', 'return OperationResult<bool>.Fail("uac_cancelled", L10n.T("Msg_UacCancelled"), L10n.T("Priv_InstallStep"));'),
    ('result.Error?.ToString() ?? "未知错误"', 'result.Error?.ToString() ?? L10n.T("Priv_UnknownError")'),
])
patch("Services/ProfileContentValidator.cs", [
    ('throw new InvalidOperationException("内容不是有效的 YAML 映射");', 'throw new InvalidOperationException(Flux.Services.L10n.T("Validator_InvalidYamlMapping"));'),
    ('throw new InvalidOperationException("内容不是有效的 YAML");', 'throw new InvalidOperationException(Flux.Services.L10n.T("Validator_InvalidYaml"));'),
    ('"配置中缺少 proxies / proxy-providers，不是有效的 Clash 配置"', 'Flux.Services.L10n.T("Validator_NotClash")'),
])
patch("Services/ProfileEnhanceService.cs", [
    ('throw new InvalidOperationException("该类型不支持全局增强");', 'throw new InvalidOperationException(L10n.T("Enhance_GlobalNotSupported"));'),
    ('new ChainItem(ChainType.Merge, GlobalUid, "全局 Merge", GlobalMergeFile, true), "全局 Merge"));', 'new ChainItem(ChainType.Merge, GlobalUid, L10n.T("Enhance_NameGlobalMerge"), GlobalMergeFile, true), "全局 Merge"));'),
    ('new ChainItem(ChainType.Script, GlobalUid, "全局 Script", GlobalScriptFile, true), "全局 Script"));', 'new ChainItem(ChainType.Script, GlobalUid, L10n.T("Enhance_NameGlobalScript"), GlobalScriptFile, true), "全局 Script"));'),
    ('new ChainItem(type, item.Uid, $"{type} 增强", Path.GetFileName(fileName), false), content);', 'new ChainItem(type, item.Uid, L10n.F("Enhance_TypedName", type), Path.GetFileName(fileName), false), content);'),
    ('throw new InvalidOperationException("Script 文件必须定义 main(config, profileName) 函数");', 'throw new InvalidOperationException(L10n.T("Enhance_ScriptNeedsMain"));'),
    ('throw new InvalidOperationException($"YAML 语法错误: {fileName}（必须是键值映射）");', 'throw new InvalidOperationException(L10n.F("Enhance_YamlSyntaxError", fileName));'),
])
patch("Services/WinInetProxySettings.cs", [
    ('throw CreateWin32Exception("无法通知 Windows 系统代理设置已更改");', 'throw CreateWin32Exception(Flux.Services.L10n.T("WinInet_NotifyFailed"));'),
    ('throw CreateWin32Exception("无法刷新 Windows 系统代理设置");', 'throw CreateWin32Exception(Flux.Services.L10n.T("WinInet_RefreshFailed"));'),
    ('throw CreateWin32Exception("无法读取 Windows 系统代理设置");', 'throw CreateWin32Exception(Flux.Services.L10n.T("WinInet_ReadFailed"));'),
    ('throw CreateWin32Exception("无法修改 Windows 系统代理设置");', 'throw CreateWin32Exception(Flux.Services.L10n.T("WinInet_WriteFailed"));'),
])
patch("Services/YamlHelper.cs", [
    ('LogService.App("YAML 解析失败: " + ex.Message, "error");', 'LogService.App(Flux.Services.L10n.F("Yaml_ParseFailed", ex.Message), "error");'),
])
patch("Utils/Format.cs", [
    ('0 => "超时",', '0 => Flux.Services.L10n.T("Format_Timeout"),'),
])
patch("Services/Paths.cs", [
    ('throw new FileNotFoundException("未找到内置 mihomo 内核（core\\\\mihomo.exe）", CoreSourcePath);', 'throw new FileNotFoundException(Flux.Services.L10n.T("Paths_CoreMissing"), CoreSourcePath);'),
])
patch("App.xaml.cs", [
    ('Services.LogService.App("UI 未处理异常: " + e.Message + "\\n" + e.Exception, "error");', 'Services.LogService.App(Services.L10n.F("App_UnhandledUi", e.Message + "\\n" + e.Exception), "error");'),
    ('Services.LogService.App("进程未处理异常: " + e.Exception, "error");', 'Services.LogService.App(Services.L10n.F("App_UnhandledDomain", e.Exception), "error");'),
    ('Services.LogService.App("未观察任务异常: " + e.Exception, "warn");', 'Services.LogService.App(Services.L10n.F("App_UnobservedTask", e.Exception), "warn");'),
])
patch("Services/AppBootstrapper.cs", [
    ('LogService.App($"热键切换模式: {mode}");', 'LogService.App(L10n.F("Boot_HotkeyModeSwitch", mode));'),
])
