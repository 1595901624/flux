# -*- coding: utf-8 -*-
"""为 SettingsPage.xaml 元素添加 x:Uid（卡片=Header/Description 二合一，按钮=Text）。幂等。"""
import io, re

# (needle, uid)
EDITS = [
    ('TextBlock Text="设置" FontSize="26"', "Settings_Title"),
    ('TextBlock Text="系统设置"', "Settings_SectionSystem"),
    ('TextBlock Text="Clash 设置"', "Settings_SectionClash"),
    ('TextBlock Text="应用" FontSize="16"', "Settings_SectionApp"),
    ('Header="开机自启动"', "Settings_CardAutoLaunch"),
    ('Header="静默启动"', "Settings_CardSilentStart"),
    ('Header="系统代理"', "Settings_CardSysProxy"),
    ('Header="PAC 模式"', "Settings_CardPac"),
    ('Header="代理守护"', "Settings_CardProxyGuard"),
    ('Header="代理绕过列表"', "Settings_CardBypass"),
    ('Header="混合代理端口"', "Settings_CardMixedPort"),
    ('Header="允许局域网连接"', "Settings_CardAllowLan"),
    ('Header="IPv6"', "Settings_CardIpv6"),
    ('Header="日志级别"', "Settings_CardLogLevel"),
    ('Header="应用日志"', "Settings_CardFileLog"),
    ('Header="TUN 模式"', "Settings_CardTun"),
    ('Header="TUN 高级设置"', "Settings_CardTunAdvanced"),
    ('Header="DNS 设置"', "Settings_CardDns"),
    ('Header="外部控制器"', "Settings_CardController"),
    ('Header="断开连接于模式切换"', "Settings_CardAutoCloseConn"),
    ('Header="语言 / Language"', "Settings_CardLanguage"),
    ('Header="主题模式"', "Settings_CardTheme"),
    ('Content="跟随系统"', "Settings_ThemeSystem"),
    ('Content="浅色"', "Settings_ThemeLight"),
    ('Content="深色"', "Settings_ThemeDark"),
    ('Content="重启"', "Settings_RestartCoreButton2"),
    ('Header="重启内核"', "Settings_CardRestartCore"),
    ('Header="Flux 服务"', "Settings_CardService"),
    ('Content="安装 / 修复服务"', "Settings_InstallService"),
    ('Header="轻量模式"', "Settings_CardLightweight"),
    ('PlaceholderText="分钟"', "Settings_LightweightMinutes"),
    ('Header="全局热键 · 显示 / 隐藏窗口"', "Settings_CardHotkeyWindow"),
    ('Header="全局热键 · 切换系统代理"', "Settings_CardHotkeySysproxy"),
    ('Header="全局热键 · 切换 TUN"', "Settings_CardHotkeyTun"),
    ('Header="全局热键 · 重新激活订阅"', "Settings_CardHotkeyReactivate"),
    ('Header="全局热键" Description=', "Settings_CardHotkeySave"),
    ('Content="保存热键"', "Settings_SaveHotkeys"),
    ('Header="复制环境变量"', "Settings_CardCopyEnv"),
    ('Header="备份与恢复"', "Settings_CardBackup"),
    ('Content="创建备份"', "Settings_CreateBackup"),
    ('Content="恢复备份"', "Settings_RestoreBackup"),
    ('Content="备份目录"', "Settings_BackupDir"),
    ('Header="WebDAV 备份"', "Settings_CardWebDav"),
    ('Content="设置服务器"', "Settings_WebDavConfig"),
    ('Content="上传"', "Settings_WebDavUpload"),
    ('Header="WebDAV 备份" Description="云端', "Settings_WebDavRestore_DUP"),
    ('Content="导出诊断包"', "Settings_ExportDiagnostics"),
    ('Content="检查更新"', "Settings_CheckUpdate"),
    ('Header="打开目录"', "Settings_CardOpenDirs"),
    ('Content="数据目录"', "Settings_OpenData"),
    ('Content="日志目录"', "Settings_OpenLogs"),
    ('Content="打开 GitHub"', "Settings_OpenGitHub"),
    ('Header="关于 Flux"', "Settings_CardAbout"),
    ('Content="退出应用"', "Settings_ExitApp"),
]

# 特殊：WebDAV 恢复按钮（Content="恢复"）与其 Header 分离处理
SPECIAL = [
    # 找 Content="恢复" 且非"恢复备份"的按钮
    ('Content="恢复"', "Settings_WebDavRestore"),
    ('Content="应用"', "Settings_ApplyTunAdvanced"),  # 第一个 应用 按钮（TUN 高级）
]

def add_uid(path, needle, uid, occurrence=0):
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    # 找第 occurrence+1 次出现
    pos = -1
    for _ in range(occurrence + 1):
        pos = s.find(needle, pos + 1)
        if pos < 0:
            print(f"MISS: {uid}")
            return
    pos2 = s.rfind("<", 0, pos)
    line_start = s.rfind("\n", 0, pos2) + 1
    line_end = s.find("\n", pos2)
    line = s[line_start:line_end]
    if "x:Uid" in line:
        print(f"SKIP: {uid}")
        return
    m = re.match(r"(\s*)<(\w+)", line)
    if not m:
        print(f"NORULE: {uid}")
        return
    new_line = line.replace(f"<{m.group(2)}", f'<{m.group(2)} x:Uid="{uid}"', 1)
    s = s[:line_start] + new_line + s[line_end:]
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"OK: {uid}")

for needle, uid in EDITS:
    add_uid("Views/SettingsPage.xaml", needle, uid)
add_uid("Views/SettingsPage.xaml", 'Content="恢复"', "Settings_WebDavRestore", occurrence=1)
add_uid("Views/SettingsPage.xaml", 'Content="应用"', "Common_Apply", occurrence=0)
add_uid("Views/SettingsPage.xaml", 'Content="应用"', "Common_Apply", occurrence=1)
add_uid("Views/SettingsPage.xaml", 'Content="应用"', "Common_Apply", occurrence=2)
