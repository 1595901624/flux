# -*- coding: utf-8 -*-
"""核心服务层 LogService/异常消息 → L10n.T/L10n.F 迁移补丁。幂等。"""
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

# ---------- CoreProcessService ----------
patch("Services/CoreProcessService.cs", [
    ('LogService.App("mihomo 内核已由服务以特权模式启动");', 'LogService.App(L10n.T("Core_StartedService"));'),
    ('LogService.App("服务模式启动失败，回退应用内模式: " + result.Error?.Message, "warn");', 'LogService.App(L10n.F("Core_ServiceStartFailedFallback", result.Error?.Message ?? ""), "warn");'),
    ('LogService.App("mihomo 内核已启动");', 'LogService.App(L10n.T("Core_Started"));'),
    ('LogService.App("服务停止内核失败: " + result.Error?.Message, "warn");', 'LogService.App(L10n.F("Core_StopViaServiceFailed", result.Error?.Message ?? ""), "warn");'),
    ('throw new InvalidOperationException("mihomo 进程启动失败");', 'throw new InvalidOperationException(L10n.T("Core_ProcessStartFailed"));'),
    ('LogService.App($"已清理残留内核进程 PID {leftover.Id}", "warn");', 'LogService.App(L10n.F("Core_KillLeftover", leftover.Id), "warn");'),
    ('LogService.App("配置应用被拒绝: " + ex.Message, "error");', 'LogService.App(L10n.F("Core_ApplyRejected", ex.Message), "error");'),
    ('LogService.App("运行时配置已热重载");', 'LogService.App(L10n.T("Core_HotReloaded"));'),
    ('LogService.App("热重载失败，重启内核: " + ex.Message, "warn");', 'LogService.App(L10n.F("Core_HotReloadFailedRestart", ex.Message), "warn");'),
    ('LogService.App($"已恢复节点选择: {item.Name} → {item.Now}");', 'LogService.App(L10n.F("Core_RestoredSelection", item.Name, item.Now));'),
    ('LogService.App($"有 {pending.Count} 个已保存节点在当前配置中不存在，已跳过恢复", "warn");', 'LogService.App(L10n.F("Core_SelectionMissing", pending.Count), "warn");'),
    ('LogService.App("恢复节点选择失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Core_RestoreSelectionsFailed", ex.Message), "warn");'),
    ('LogService.App("Job Object 关联失败，主进程异常退出时内核可能残留", "warn");', 'LogService.App(L10n.T("Core_JobAssignFailed"), "warn");'),
    ('throw new InvalidOperationException("配置校验失败: " + output.Trim());', 'throw new InvalidOperationException(L10n.F("Core_ValidateFailed", output.Trim()));'),
    ('throw new InvalidOperationException("mihomo 进程异常退出，请查看日志");', 'throw new InvalidOperationException(L10n.T("Core_ProcessExited"));'),
    ('throw new TimeoutException("等待 External Controller 就绪超时");', 'throw new TimeoutException(L10n.T("Core_ControllerTimeout"));'),
    ('LogService.App($"mihomo PID: {_process.Id}");', 'LogService.App(L10n.F("Core_Pid", _process.Id));'),
])

# ---------- SysProxyService ----------
patch("Services/SysProxyService.cs", [
    ('LogService.App("系统代理已恢复");', 'LogService.App(L10n.T("Proxy_Restored"));'),
    ('LogService.App($"系统代理已开启: {server}");', 'LogService.App(L10n.F("Proxy_Enabled", server));'),
    ('LogService.App($"系统代理已开启（PAC 模式）: {pacUrl}");', 'LogService.App(L10n.F("Proxy_EnabledPac", pacUrl));'),
    ('LogService.App("检测到上次未恢复的系统代理，已恢复原始设置", "warn");', 'LogService.App(L10n.T("Proxy_StaleRestored"), "warn");'),
    ('LogService.App("检测到旧版本遗留的系统代理，已自动关闭", "warn");', 'LogService.App(L10n.T("Proxy_LegacyDisabled"), "warn");'),
    ('LogService.App("恢复遗留系统代理失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Proxy_StaleRestoreFailed", ex.Message), "warn");'),
    ('LogService.App("系统代理已被其他程序修改，Flux 不再覆盖该设置", "warn");', 'LogService.App(L10n.T("Proxy_ChangedByOther"), "warn");'),
    ('LogService.App("系统代理快照读取失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Proxy_SnapshotReadFailed", ex.Message), "warn");'),
    ('LogService.App("检测到系统代理被修改，正在恢复", "warn");', 'LogService.App(L10n.T("Proxy_ChangedRestoring"), "warn");'),
    ('LogService.App("代理守护异常: " + ex.Message, "warn");', 'LogService.App(L10n.F("Proxy_GuardError", ex.Message), "warn");'),
])

# ---------- ConfigService ----------
patch("Services/ConfigService.cs", [
    ('catch (Exception ex) { LogService.App("verge.yaml 加载失败: " + ex.Message, "error"); }', 'catch (Exception ex) { LogService.App(L10n.F("Config_VergeLoadFailed", ex.Message), "error"); }'),
    ('LogService.App("verge.yaml 迁移备份失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Config_VergeMigrateBackupFailed", ex.Message), "warn");'),
    ('LogService.App($"verge.yaml 已迁移至 schema v1，备份: {backup}", "info");', 'LogService.App(L10n.F("Config_VergeMigrated", 1, backup), "info");'),
    ('catch (Exception ex) { LogService.App("config.yaml 加载失败: " + ex.Message, "error"); }', 'catch (Exception ex) { LogService.App(L10n.F("Config_ClashLoadFailed", ex.Message), "error"); }'),
    ('catch (Exception ex) { LogService.App("profiles.yaml 加载失败: " + ex.Message, "error"); }', 'catch (Exception ex) { LogService.App(L10n.F("Config_ProfilesLoadFailed", ex.Message), "error"); }'),
])

# ---------- DeepLinkService ----------
patch("Services/DeepLinkService.cs", [
    ('LogService.App("协议注册失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("DeepLink_RegisterFailed", ex.Message), "warn");'),
    ('LogService.App("深链导入失败: " + ex.Message, "error");', 'LogService.App(L10n.F("DeepLink_ImportFailed", ex.Message), "error");'),
])

# ---------- AutoStartService ----------
patch("Services/AutoStartService.cs", [
    ('StartupTaskState.DisabledByUser => "开机自启动已被用户禁用，请在任务管理器的“启动应用”中重新启用 Flux Proxy",', 'StartupTaskState.DisabledByUser => L10n.T("AutoStart_DisabledByUser"),'),
    ('StartupTaskState.DisabledByPolicy => "开机自启动已被系统策略禁用",', 'StartupTaskState.DisabledByPolicy => L10n.T("AutoStart_DisabledByPolicy"),'),
    ('_ => "用户未允许开机自启动",', '_ => L10n.T("AutoStart_NotAllowed"),'),
    ('?? throw new InvalidOperationException("无法打开 Run 注册表键");', '?? throw new InvalidOperationException(L10n.T("AutoStart_RunKeyFailed"));'),
])

# ---------- AppBootstrapper ----------
patch("Services/AppBootstrapper.cs", [
    ('catch (Exception ex) { LogService.App("语言设置失败: " + ex.Message, "warn"); }', 'catch (Exception ex) { LogService.App(L10n.F("Boot_LanguageFailed", ex.Message), "warn"); }'),
    ('LogService.App("内核未就绪，已跳过系统代理以避免网络中断", "warn");', 'LogService.App(L10n.T("Boot_SkipProxyNoCore"), "warn");'),
    ('LogService.App("当前进程没有管理员权限且服务不可用，已安全关闭 TUN 模式", "warn");', 'LogService.App(L10n.T("Boot_TunDisabledNoPrivilege"), "warn");'),
    ('LogService.App("热键注册失败: " + failure, "warn");', 'LogService.App(L10n.F("Boot_HotkeyRegisterFailed", failure), "warn");'),
    ('LogService.App("热键初始化失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Boot_HotkeyInitFailed", ex.Message), "warn");'),
    ('LogService.App($"热键执行失败 ({action}): {ex.Message}", "warn");', 'LogService.App(L10n.F("Boot_HotkeyExecFailed", action, ex.Message), "warn");'),
    ('LogService.App($"热键动作未实现: {action}", "warn");', 'LogService.App(L10n.F("Boot_HotkeyNotImplemented", action), "warn");'),
])

# ---------- LightweightManager ----------
patch("Services/LightweightManager.cs", [
    ('LogService.App($"窗口已关闭 {minutes} 分钟，自动进入轻量模式");', 'LogService.App(L10n.F("Boot_LightweightAuto", minutes));'),
    ('LogService.App("已进入轻量模式（主窗口已释放，内核保持运行）");', 'LogService.App(L10n.T("Lightweight_Entered"));'),
    ('LogService.App("已退出轻量模式");', 'LogService.App(L10n.T("Lightweight_Exited"));'),
    ('LogService.App("自动轻量模式失败: " + ex.Message, "warn");', 'LogService.App(L10n.F("Boot_LightweightAutoFailed", ex.Message), "warn");'),
])
