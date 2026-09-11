# -*- coding: utf-8 -*-
"""剩余对话框 i18n 迁移 + UnlockTestService 状态码化。幂等。"""
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

# ---------- UnlockTestService：状态改为中性码 ----------
patch("Flux.Core/Unlock/UnlockTestService.cs", [
    ('return ("支持", region, "非自制剧解锁");', 'return ("supported", region, "original-only unlocked");'),
    ('if (status == 403) return ("不支持", null, "403");\n                if (status == 404) return ("supported", null, "originals only");' if False else 'if (status == 403) return ("不支持", null, "403");', 'if (status == 403) return ("unsupported", null, "403");'),
    ('if (status == 404) return ("支持", null, "仅自制剧");', 'if (status == 404) return ("supported", null, "originals only");'),
    ('if (status == 200 && (body.Contains("disneyplus", StringComparison.OrdinalIgnoreCase)))\n                {\n                    var region = Extract(body, @"""region"":\s*""([A-Za-z]{2})""");\n                    return ("支持", region, null);\n                }',
     'if (status == 200 && (body.Contains("disneyplus", StringComparison.OrdinalIgnoreCase)))\n                {\n                    var region = Extract(body, @"""region"":\s*""([A-Za-z]{2})""");\n                    return ("supported", region, null);\n                }'),
    ('if (status == 403 || status == 451) return ("不支持", null, $"HTTP {status}");', 'if (status == 403 || status == 451) return ("unsupported", null, $"HTTP {status}");'),
    ('if (status == 200) return ("支持", null, null);', 'if (status == 200) return ("supported", null, null);'),
    ('if (status == 403) return ("不支持", null, "403");', 'if (status == 403) return ("unsupported", null, "403");'),
    ('if (body.Contains("not available in your country", StringComparison.OrdinalIgnoreCase))\n                    return ("不支持", null, null);', 'if (body.Contains("not available in your country", StringComparison.OrdinalIgnoreCase))\n                    return ("unsupported", null, null);'),
    ('return ("支持", region, null);', 'return ("supported", region, null);'),
    ('return string.IsNullOrEmpty(region) ? ("未知", null, "无地区信息") : ("支持", region, null);', 'return string.IsNullOrEmpty(region) ? ("unknown", null, "no region") : ("supported", region, null);'),
    ('return ("支持", region, null);', 'return ("supported", region, null);'),
    ('if (status == 403) return ("不支持", null, "403");', 'if (status == 403) return ("unsupported", null, "403");'),
    ('return ("支持", "TW", null);', 'return ("supported", "TW", null);'),
    ('if (body.Contains("仅限港澳台", StringComparison.Ordinal) || body.Contains("area limit", StringComparison.OrdinalIgnoreCase))\n                    return ("不支持", null, null);', 'if (body.Contains("仅限港澳台", StringComparison.Ordinal) || body.Contains("area limit", StringComparison.OrdinalIgnoreCase))\n                    return ("unsupported", null, null);'),
    ('if (body.Contains("\\"code\\":0", StringComparison.Ordinal) || body.Contains(" dash", StringComparison.Ordinal))\n                    return ("支持", null, null);', 'if (body.Contains("\\"code\\":0", StringComparison.Ordinal) || body.Contains(" dash", StringComparison.Ordinal))\n                    return ("supported", null, null);'),
    ('return ("支持", region, null);', 'return ("supported", region, null);'),
    ('return ("未知", null, $"HTTP {status}");', 'return ("unknown", null, $"HTTP {status}");'),
    ('return ("未知", null, null);', 'return ("unknown", null, null);'),
    ('return ("未知", null, body.Length > 60 ? body[..60] : body);', 'return ("unknown", null, body.Length > 60 ? body[..60] : body);'),
    ('return ("未知", null, "no region info");', 'return ("unknown", null, "no region info");'),
    ('(_, _) => ("未知", null, null);', '(_, _) => ("unknown", null, null);'),
])

# ---------- RulesProvidersDialog ----------
patch("Views/RulesProvidersDialog.cs", [
    ('Title = "规则 Provider";', 'Title = L10n.T("Msg_ProviderTitleRules");'),
    ('CloseButtonText = "关闭";', 'CloseButtonText = L10n.T("Common_Close");'),
    ('var refreshAll = new Button { Content = "全部更新" };', 'var refreshAll = new Button { Content = L10n.T("Profiles_UpdateAll") };'),
    ('var reload = new Button { Content = "刷新列表" };', 'var reload = new Button { Content = L10n.T("Msg_RefreshList") };'),
    ('_status.Text = "正在加载…";', '_status.Text = L10n.T("Msg_Loading");'),
    ('_status.Text = "当前订阅没有规则 Provider";', '_status.Text = L10n.T("Msg_NoRuleProviders");'),
    ('_status.Text = $"共 {_providers.Count} 个 Provider";', '_status.Text = L10n.F("Msg_ProviderCount", _providers.Count);'),
    ('Text = $"{provider.Type} · {provider.Behavior} · {provider.RuleCount} 条",', 'Text = L10n.F("Msg_RulesMeta", provider.Type, provider.Behavior, provider.RuleCount),'),
    ('var update = new Button { Content = "更新" };', 'var update = new Button { Content = L10n.T("Msg_Update") };'),
    ('_status.Text = $"正在更新 {provider.Name}…";', '_status.Text = L10n.F("Msg_Updating", provider.Name);'),
    ('_status.Text = $"已更新 {provider.Name}";', '_status.Text = L10n.F("Msg_UpdatedOne", provider.Name);'),
    ('_status.Text = $"更新失败 {provider.Name}: {ex.Message}";', '_status.Text = L10n.F("Msg_UpdateFailedNamed", provider.Name, ex.Message);'),
    ('_status.Text = "正在全部更新…";', '_status.Text = L10n.T("Msg_UpdatingAll");'),
    ('_status.Text = $"全部更新完成：{ok}/{_providers.Count}";', '_status.Text = L10n.F("Msg_UpdateAllDoneCount", ok, _providers.Count);'),
    ('if (string.IsNullOrEmpty(iso)) return "未更新";\n        if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");\n        return "未更新";',
     'if (string.IsNullOrEmpty(iso)) return L10n.T("Msg_NeverUpdatedShort");\n        if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");\n        return L10n.T("Msg_NeverUpdatedShort");'),
])

# ---------- ProxiesProvidersDialog ----------
patch("Views/ProxiesProvidersDialog.cs", [
    ('Title = "代理 Provider";', 'Title = L10n.T("Msg_ProviderTitleProxy");'),
    ('CloseButtonText = "关闭";', 'CloseButtonText = L10n.T("Common_Close");'),
    ('var updateAll = new Button { Content = "全部更新" };', 'var updateAll = new Button { Content = L10n.T("Profiles_UpdateAll") };'),
    ('var healthCheck = new Button { Content = "健康检查" };', 'var healthCheck = new Button { Content = L10n.T("Msg_HealthCheck") };'),
    ('var reload = new Button { Content = "刷新列表" };', 'var reload = new Button { Content = L10n.T("Msg_RefreshList") };'),
    ('_status.Text = "正在加载…";', '_status.Text = L10n.T("Msg_Loading");'),
    ('_status.Text = "当前订阅没有代理 Provider";', '_status.Text = L10n.T("Msg_NoProxyProviders");'),
    ('_status.Text = $"共 {_providers.Count} 个 Provider";', '_status.Text = L10n.F("Msg_ProviderCount", _providers.Count);'),
    ('Text = $"{provider.ProxyCount} 个节点",', 'Text = L10n.F("Msg_ProxyNodeCount", provider.ProxyCount),'),
    ('var update = new Button { Content = "更新" };', 'var update = new Button { Content = L10n.T("Msg_Update") };'),
    ('_status.Text = $"正在更新 {provider.Name}…";', '_status.Text = L10n.F("Msg_Updating", provider.Name);'),
    ('_status.Text = $"已更新 {provider.Name}";', '_status.Text = L10n.F("Msg_UpdatedOne", provider.Name);'),
    ('_status.Text = $"更新失败 {provider.Name}: {ex.Message}";', '_status.Text = L10n.F("Msg_UpdateFailedNamed", provider.Name, ex.Message);'),
    ('_status.Text = "正在全部更新…";', '_status.Text = L10n.T("Msg_UpdatingAll");'),
    ('_status.Text = $"全部更新完成：{ok}/{_providers.Count}";', '_status.Text = L10n.F("Msg_UpdateAllDoneCount", ok, _providers.Count);'),
    ('_status.Text = "正在健康检查…";', '_status.Text = L10n.T("Msg_HealthChecking");'),
    ('_status.Text = "健康检查完成";', '_status.Text = L10n.T("Msg_HealthCheckDone");'),
    ('if (string.IsNullOrEmpty(iso)) return "未更新";\n        if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");\n        return "未更新";',
     'if (string.IsNullOrEmpty(iso)) return L10n.T("Msg_NeverUpdatedShort");\n        if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");\n        return L10n.T("Msg_NeverUpdatedShort");'),
])

# ---------- UnlockPage：状态码 → 本地化显示 ----------
patch("Views/UnlockPage.xaml.cs", [
    ('private string _statusText = "未测试";', 'private string _statusText = "untested";'),
    ('''        StatusText = result.Status;
        Detail = string.IsNullOrEmpty(result.Region)
            ? result.Detail ?? ""
            : $"{result.Detail} · {result.Region}".Trim(' ', '·');
        StatusBrush = result.Status switch
        {
            "支持" => Green(),
            "不支持" => Red(),
            "失败" => Orange(),
            _ => Gray(),
        };''',
     '''        StatusText = result.Status switch
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
        };'''),
    ('vm.Update(new UnlockResult(check.Id, check.Name, "未测试"));', 'vm.Update(new UnlockResult(check.Id, check.Name, "untested"));'),
    ('vm.Update(new UnlockResult(check.Id, check.Name, "测试中"));', 'vm.Update(new UnlockResult(check.Id, check.Name, "testing"));'),
])

# ---------- HomePage ----------
patch("Views/HomePage.xaml.cs", [
    ('await ShowErrorAsync("系统代理切换失败", ex.Message);', 'await ShowErrorAsync(L10n.T("Msg_SysProxyToggleFailedTitle"), ex.Message);'),
    ('await ShowErrorAsync("TUN 切换失败", ex.Message);', 'await ShowErrorAsync(L10n.T("Msg_TunToggleFailedTitle"), ex.Message);'),
    ('await ShowErrorAsync("内核重启失败", ex.Message);', 'await ShowErrorAsync(L10n.T("Msg_CoreRestartFailedTitle"), ex.Message);'),
])

# ---------- ConnectionsPage ----------
patch("Views/ConnectionsPage.xaml.cs", [
    ('PrimaryButtonText = "关闭此连接",', 'PrimaryButtonText = L10n.T("Msg_CloseThisConn"),'),
    ('CloseButtonText = "返回",', 'CloseButtonText = L10n.T("Msg_Back"),'),
    ('CloseButtonText = "确定",', 'CloseButtonText = L10n.T("Common_OK"),'),
    ('$"网络: {vm.Network} ({vm.Type})",', 'L10n.F("Msg_DetailNetwork", vm.Network, vm.Type),'),
    ('$"主机: {vm.Host}",', 'L10n.F("Msg_DetailHost", vm.Host),'),
    ('$"目标: {vm.Destination}",', 'L10n.F("Msg_DetailDestination", vm.Destination),'),
    ('$"来源: {vm.Source}",', 'L10n.F("Msg_DetailSource", vm.Source),'),
    ('$"规则: {vm.Rule}",', 'L10n.F("Msg_DetailRule", vm.Rule),'),
    ('$"链路: {vm.Chains}",', 'L10n.F("Msg_DetailChains", vm.Chains),'),
    ('$"进程: {vm.Process}",', 'L10n.F("Msg_DetailProcess", vm.Process),'),
    ('$"下载: {vm.DownloadText}   上传: {vm.UploadText}",', 'L10n.F("Msg_DetailTraffic", vm.DownloadText, vm.UploadText),'),
    ('$"时间: {vm.StartText}",', 'L10n.F("Msg_DetailTime", vm.StartText),'),
])
