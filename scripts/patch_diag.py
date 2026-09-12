# -*- coding: utf-8 -*-
"""DiagnosticsService 输出报告本地化。幂等。"""
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

patch("Services/DiagnosticsService.cs", [
    ('sb.AppendLine("=== Flux 诊断包 ===");', 'sb.AppendLine(L10n.T("Diag_Title"));'),
    ('sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");', 'sb.AppendLine(L10n.F("Diag_ExportedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));'),
    ('sb.AppendLine("--- 系统信息 ---");', 'sb.AppendLine(L10n.T("Diag_SectionSystem"));'),
    ('sb.AppendLine($"OS 版本: {Environment.OSVersion.VersionString}");', 'sb.AppendLine(L10n.F("Diag_OsVersion", Environment.OSVersion.VersionString));'),
    ('sb.AppendLine($"架构: {RuntimeInformation.ProcessArchitecture}");', 'sb.AppendLine(L10n.F("Diag_Arch", RuntimeInformation.ProcessArchitecture.ToString()));'),
    ('sb.AppendLine($"处理器数: {Environment.ProcessorCount}");', 'sb.AppendLine(L10n.F("Diag_Processors", Environment.ProcessorCount));'),
    ('sb.AppendLine($"Flux 版本: {typeof(DiagnosticsService).Assembly.GetName().Version}");', 'sb.AppendLine(L10n.F("Diag_FluxVersion", typeof(DiagnosticsService).Assembly.GetName().Version?.ToString() ?? ""));'),
    ('sb.AppendLine($"运行模式: {(PackageIdentity.IsPackaged ? "MSIX" : "便携")}");', 'sb.AppendLine(L10n.F("Diag_RunMode", PackageIdentity.IsPackaged ? "MSIX" : "Portable"));'),
    ('sb.AppendLine($"管理员: {TrayService.IsElevated()}");', 'sb.AppendLine(L10n.F("Diag_Admin", TrayService.IsElevated()));'),
    ('sb.AppendLine("--- 内核 ---");', 'sb.AppendLine(L10n.T("Diag_SectionCore"));'),
    ('sb.AppendLine($"mihomo 版本: {coreVersion ?? "未运行"}");', 'sb.AppendLine(L10n.F("Diag_CoreVersion", coreVersion ?? L10n.T("VM_NotRunning")));'),
    ('sb.AppendLine($"mihomo 版本: 获取失败 ({SensitiveMasker.Mask(ex.Message)})");', 'sb.AppendLine(L10n.F("Diag_CoreVersionFailed", SensitiveMasker.Mask(ex.Message)));'),
    ('sb.AppendLine($"内核模式: {AppServices.Core.Mode}");', 'sb.AppendLine(L10n.F("Diag_CoreMode", AppServices.Core.Mode.ToString()));'),
    ('sb.AppendLine($"服务状态: {AppServices.Privilege.GetServiceState()}");', 'sb.AppendLine(L10n.F("Diag_ServiceState", AppServices.Privilege.GetServiceState().ToString()));'),
    ('sb.AppendLine("--- 配置结构（敏感字段已遮蔽） ---");', 'sb.AppendLine(L10n.T("Diag_SectionConfig"));'),
    ('sb.AppendLine($"verge.yaml 键: {string.Join(", ", ReadTopLevelKeys(Paths.VergeConfigFile))}");', 'sb.AppendLine("verge.yaml: " + string.Join(", ", ReadTopLevelKeys(Paths.VergeConfigFile)));'),
    ('sb.AppendLine($"profiles.yaml 键: {string.Join(", ", ReadTopLevelKeys(Paths.ProfilesConfigFile))}");', 'sb.AppendLine("profiles.yaml: " + string.Join(", ", ReadTopLevelKeys(Paths.ProfilesConfigFile)));'),
    ('sb.AppendLine($"当前订阅: {SensitiveMasker.Mask(AppServices.Config.Profiles.GetCurrent()?.Name)}");', 'sb.AppendLine(L10n.F("Diag_CurrentProfile", SensitiveMasker.Mask(AppServices.Config.Profiles.GetCurrent()?.Name)));'),
    ('sb.AppendLine("--- 运行设置摘要 ---");', 'sb.AppendLine(L10n.T("Diag_SectionRuntime"));'),
    ('sb.AppendLine($"系统代理: {verge.EnableSystemProxy} (PAC: {verge.EnablePacMode})");', 'sb.AppendLine(L10n.F("Diag_SystemProxyLine", verge.EnableSystemProxy, verge.EnablePacMode));'),
    ('sb.AppendLine($"模式: {AppServices.Config.Mode}");', 'sb.AppendLine(L10n.F("Diag_CoreMode", AppServices.Config.Mode));'),
    ('sb.AppendLine($"内核: {AppServices.Core.Mode}");', 'sb.AppendLine(L10n.F("Diag_RunMode", AppServices.Core.Mode));'),
    ('sb.AppendLine("--- 最近应用日志（200 条，敏感信息已遮蔽） ---");', 'sb.AppendLine(L10n.T("Diag_SectionLogs"));'),
    ('sb.AppendLine("(无应用日志文件)");', 'sb.AppendLine(L10n.T("Diag_NoLogFile"));'),
    ('sb.AppendLine($"(日志读取失败: {SensitiveMasker.Mask(ex.Message)})");', 'sb.AppendLine(L10n.F("Diag_LogReadFailed", SensitiveMasker.Mask(ex.Message)));'),
    ('sb.AppendLine("  (无法解析)");', 'sb.AppendLine("  " + L10n.T("Diag_MaskedValue"));'),
    ('sb.AppendLine($"  (读取失败: {SensitiveMasker.Mask(ex.Message)})");', 'sb.AppendLine("  " + L10n.F("Diag_LogReadFailed", SensitiveMasker.Mask(ex.Message)));'),
])
