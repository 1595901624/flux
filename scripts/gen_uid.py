# -*- coding: utf-8 -*-
"""为 XAML 元素添加 x:Uid（i18n 资源引用）。幂等：已有 x:Uid 的行跳过。"""
import io, re

# (file, unique existing substring, x:Uid to add)
EDITS = [
    ("MainWindow.xaml", 'NavigationViewItem Tag="home"', "Nav_Home"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="proxies"', "Nav_Proxies"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="profiles"', "Nav_Profiles"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="connections"', "Nav_Connections"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="rules"', "Nav_Rules"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="logs"', "Nav_Logs"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="unlock"', "Nav_Unlock"),
    ("MainWindow.xaml", 'NavigationViewItem Tag="settings"', "Nav_Settings"),

    ("Views/HomePage.xaml", 'TextBlock Text="首页" FontSize="26"', "Home_Title"),
    ("Views/HomePage.xaml", 'Text="进入代理设置"', "Home_OpenProxySettings"),
    ("Views/HomePage.xaml", 'Text="网络控制"', "Home_CardNetwork"),
    ("Views/HomePage.xaml", 'Text="TUN 需要管理员权限运行"', "Home_TunAdminHint"),
    ("Views/HomePage.xaml", 'Text="当前订阅"', "Home_CardProfile"),
    ("Views/HomePage.xaml", 'Text="当前节点"', "Home_CardNodes"),
    ("Views/HomePage.xaml", 'Text="流量统计"', "Home_CardTraffic"),
    ("Views/HomePage.xaml", 'Text="运行模式"', "Home_CardMode"),
    ("Views/HomePage.xaml", 'Text="内核信息"', "Home_CardCore"),

    ("Views/ProxiesPage.xaml", 'TextBlock Text="代理" FontSize="26"', "Proxies_Title"),
    ("Views/ProxiesPage.xaml", 'PlaceholderText="筛选节点名称/类型"', "Proxies_FilterBox"),
    ("Views/ProxiesPage.xaml", 'Text="暂无代理组"', "Proxies_Empty"),
    ("Views/Views/ProxiesPage.xaml", 'Text="请先在「订阅」页导入订阅配置"', "Proxies_EmptyHint"),

    ("Views/ProfilesPage.xaml", 'TextBlock Text="订阅" FontSize="26"', "Profiles_Title"),
    ("Views/ProfilesPage.xaml", 'PlaceholderText="输入订阅链接', "Profiles_UrlBox"),
    ("Views/ProfilesPage.xaml", 'TextBlock Text="本地文件"', "Profiles_ImportLocal"),
    ("Views/ProfilesPage.xaml", 'Content="新建空配置"', "Profiles_CreateEmpty"),
    ("Views/ProfilesPage.xaml", 'Content="全部更新"', "Profiles_UpdateAll"),
    ("Views/ProfilesPage.xaml", 'Content="全局增强"', "Profiles_GlobalEnhance"),
    ("Views/ProfilesPage.xaml", 'Text="暂无订阅"', "Profiles_Empty"),
    ("Views/ProfilesPage.xaml", 'Text="粘贴订阅链接后点击', "Profiles_EmptyHint"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="选择此订阅"', "Profiles_MenuSelect"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="更新"', "Profiles_MenuUpdate"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="编辑信息"', "Profiles_MenuEdit"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="编辑增强配置"', "Profiles_MenuEnhance"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="上移"', "Profiles_MenuUp"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="下移"', "Profiles_MenuDown"),
    ("Views/ProfilesPage.xaml", 'MenuFlyoutItem Text="删除"', "Profiles_MenuDelete"),

    ("Views/ConnectionsPage.xaml", 'TextBlock Text="连接" FontSize="26"', "Connections_Title"),
    ("Views/ConnectionsPage.xaml", 'PlaceholderText="搜索主机 / 规则 / 进程"', "Connections_SearchBox"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="关闭全部"', "Connections_CloseAll"),
    ("Views/ConnectionsPage.xaml", 'x:Name="ClosedToggle"', "Connections_ClosedToggle"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="主机"', "Connections_Host"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="网络"', "Connections_Network"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="下载"', "Connections_Download"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="上传"', "Connections_Upload"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="规则"', "Connections_Rule"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="链路"', "Connections_Chains"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="进程"', "Connections_Process"),
    ("Views/ConnectionsPage.xaml", 'TextBlock Text="时间"', "Connections_Time"),
    ("Views/ConnectionsPage.xaml", 'ComboBoxItem Tag="default"', "Connections_SortDefault"),
    ("Views/ConnectionsPage.xaml", 'ComboBoxItem Tag="upload"', "Connections_SortUpload"),
    ("Views/ConnectionsPage.xaml", 'ComboBoxItem Tag="download"', "Connections_SortDownload"),

    ("Views/RulesPage.xaml", 'TextBlock Text="规则" FontSize="26"', "Rules_Title"),
    ("Views/RulesPage.xaml", 'PlaceholderText="搜索规则内容"', "Rules_SearchBox"),
    ("Views/RulesPage.xaml", 'TextBlock Text="规则 Provider"', "Rules_Providers"),
    ("Views/RulesPage.xaml", 'TextBlock Text="刷新"', "Rules_Refresh"),
    ("Views/RulesPage.xaml", 'Text="暂无规则"', "Rules_Empty"),

    ("Views/LogsPage.xaml", 'TextBlock Text="日志" FontSize="26"', "Logs_Title"),
    ("Views/LogsPage.xaml", 'ComboBoxItem Tag="all"', "Logs_LevelAll"),
    ("Views/LogsPage.xaml", 'ComboBoxItem Tag="info"', "Logs_LevelInfo"),
    ("Views/LogsPage.xaml", 'ComboBoxItem Tag="warning"', "Logs_LevelWarning"),
    ("Views/LogsPage.xaml", 'ComboBoxItem Tag="error"', "Logs_LevelError"),
    ("Views/LogsPage.xaml", 'ComboBoxItem Tag="debug"', "Logs_LevelDebug"),
    ("Views/LogsPage.xaml", 'PlaceholderText="搜索日志内容"', "Logs_SearchBox"),
    ("Views/LogsPage.xaml", 'TextBlock Text="暂停"', "Logs_Pause"),
    ("Views/LogsPage.xaml", 'TextBlock Text="清空"', "Logs_Clear"),
    ("Views/LogsPage.xaml", 'TextBlock Text="倒序"', "Logs_NewestFirst"),
    ("Views/LogsPage.xaml", 'Text="暂无日志"', "Logs_Empty"),

    ("Views/UnlockPage.xaml", 'TextBlock Text="解锁测试"', "Unlock_Title"),
    ("Views/UnlockPage.xaml", 'Text="检测当前节点对流媒体与 AI 服务的解锁状态', "Unlock_Subtitle"),
    ("Views/UnlockPage.xaml", 'Content="全部测试"', "Unlock_RunAll"),
    ("Views/UnlockPage.xaml", 'Content="重测"', "Unlock_Retest"),
]

for path, needle, uid in EDITS:
    if path == "Views/Views/ProxiesPage.xaml":
        path = "Views/ProxiesPage.xaml"
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    line_idx = s.find(needle)
    if line_idx < 0:
        print(f"MISS: {path}: {needle}")
        continue
    # 找到 needle 所在行的行首 '<'
    line_start = s.rfind("\n", 0, line_idx) + 1
    line_end = s.find("\n", line_idx)
    line = s[line_start:line_end]
    if "x:Uid" in line:
        print(f"SKIP (has uid): {path}: {uid}")
        continue
    # 在 '<Element' 后插入 x:Uid
    m = re.match(r"(\s*)<(\w+)", line)
    if not m:
        print(f"NORULE: {path}: {needle}")
        continue
    indent, tag = m.group(1), m.group(2)
    new_line = line.replace(f"<{tag}", f'<{tag} x:Uid="{uid}"', 1)
    s = s[:line_start] + new_line + s[line_end:]
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"OK: {path}: {uid}")
