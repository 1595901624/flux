# -*- coding: utf-8 -*-
"""第二遍：处理多行元素与文本型 Content 按钮；向前回溯到开标签行。"""
import io, re

EDITS = [
    ("Views/HomePage.xaml", 'Text="TUN 需要管理员权限运行"', "Home_TunAdminHint"),
    ("Views/ProxiesPage.xaml", 'PlaceholderText="筛选节点名称/类型"', "Proxies_FilterBox"),
    ("Views/ProfilesPage.xaml", 'PlaceholderText="输入订阅链接', "Profiles_UrlBox"),
    ("Views/ProfilesPage.xaml", 'Click="CreateEmpty_Click"', "Profiles_CreateEmpty"),
    ("Views/ProfilesPage.xaml", 'Click="UpdateAll_Click"', "Profiles_UpdateAll"),
    ("Views/ProfilesPage.xaml", 'Click="MenuGlobalEnhance_Click"', "Profiles_GlobalEnhance"),
    ("Views/ConnectionsPage.xaml", 'PlaceholderText="搜索主机 / 规则 / 进程"', "Connections_SearchBox"),
    ("Views/ConnectionsPage.xaml", 'x:Name="ClosedToggle"', "Connections_ClosedToggle"),
    ("Views/RulesPage.xaml", 'PlaceholderText="搜索规则内容"', "Rules_SearchBox"),
    ("Views/LogsPage.xaml", 'PlaceholderText="搜索日志内容"', "Logs_SearchBox"),
    ("Views/UnlockPage.xaml", 'x:Name="RunAllButton"', "Unlock_RunAll"),
]

def add_uid(path, needle, uid):
    with io.open(path, encoding="utf-8") as f:
        s = f.read()
    pos = s.find(needle)
    if pos < 0:
        print(f"MISS: {path}: {uid}")
        return
    # 向前找包含 '<Tag' 的行首
    pos2 = s.rfind("<", 0, pos)
    line_start = s.rfind("\n", 0, pos2) + 1
    line_end = s.find("\n", pos2)
    line = s[line_start:line_end]
    if "x:Uid" in line:
        print(f"SKIP: {path}: {uid}")
        return
    m = re.match(r"(\s*)<(\w+)", line)
    if not m:
        print(f"NORULE: {path}: {uid}")
        return
    new_line = line.replace(f"<{m.group(2)}", f'<{m.group(2)} x:Uid="{uid}"', 1)
    s = s[:line_start] + new_line + s[line_end:]
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(s)
    print(f"OK: {path}: {uid}")

for path, needle, uid in EDITS:
    add_uid(path, needle, uid)
