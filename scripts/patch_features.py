# -*- coding: utf-8 -*-
"""Loopback 卡片 + 二维码菜单接线。"""
import io

# 1. SettingsPage: UWP Loopback 卡片
p = 'Views/SettingsPage.xaml'
s = io.open(p, encoding='utf-8').read()
old = '''                <sc:SettingsCard x:Uid="Settings_CardBackup" Header="备份与恢复"'''
new = '''                <sc:SettingsCard x:Uid="Settings_CardLoopback" Header="UWP Loopback 工具" Description="允许 UWP 应用通过本机代理联网（需要管理员授权）">
                    <Button x:Uid="Settings_OpenLoopback" Content="打开工具" Click="OpenLoopback_Click" />
                </sc:SettingsCard>
                <sc:SettingsCard x:Uid="Settings_CardBackup" Header="备份与恢复"'''
assert old in s, "settings card"
s = s.replace(old, new)
io.open(p, 'w', encoding='utf-8', newline='').write(s)
print("settings xaml ok")

# 2. SettingsPage.xaml.cs: 处理器
p2 = 'Views/SettingsPage.xaml.cs'
s2 = io.open(p2, encoding='utf-8').read()
old2 = '    private async void InstallService_Click(object sender, RoutedEventArgs e)'
assert old2 in s2, "handler anchor"
new2 = '''    private async void OpenLoopback_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UwpLoopbackDialog(XamlRoot);
        await dialog.ShowAsync();
    }

    private async void InstallService_Click(object sender, RoutedEventArgs e)'''
s2 = s2.replace(old2, new2, 1)
io.open(p2, 'w', encoding='utf-8', newline='').write(s2)
print("settings cs ok")

# 3. 设置键表：Loopback 卡片
p3 = 'scripts/i18n_settings_table.py'
s3 = io.open(p3, encoding='utf-8').read()
if 'Settings_CardLoopback' not in s3:
    add = '''    k("Settings_CardLoopback", "UWP Loopback 工具", "UWP Loopback tool", "UWP Loopback 工具", "UWP Loopbackツール", "UWP Loopback 도구", "UWP-Loopback-Tool", "Herramienta UWP Loopback", "Инструмент UWP Loopback", "UWP Loopback aracı", "Alat UWP Loopback", "ابزار UWP Loopback", "أداة UWP Loopback", "UWP Loopback коралы")
    k("Settings_CardLoopback.Desc", "允许 UWP 应用通过本机代理联网（需要管理员授权）", "Allow UWP apps to reach the proxy (needs admin consent)", "允許 UWP 應用透過本機代理連網（需要管理員授權）", "UWPアプリのプロキシ利用を許可（要管理者）", "UWP 앱이 프록시를 사용하도록 허용(관리자 필요)", "UWP-Apps über den Proxy erlauben (Admin nötig)", "Permite que apps UWP usen el proxy (requiere admin)", "Разрешить UWP-приложениям использовать прокси (нужен админ)", "UWP uygulamalarının proxy kullanımına izin ver (yönetici gerekir)", "Izinkan aplikasi UWP memakai proxy (perlu admin)", "اجازه عبور برنامه‌های UWP از پروکسی (نیاز به دسترسی مدیر)", "السماح لتطبيقات UWP عبر البروكسي (يحتاج مسؤول)", "UWP кушакларга прокси аша чыгу рөхсәте (админ кирәк)")
    k("Settings_OpenLoopback", "打开工具", "Open tool", "開啟工具", "ツールを開く", "도구 열기", "Tool öffnen", "Abrir herramienta", "Открыть инструмент", "Aracı aç", "Buka alat", "باز کردن ابزار", "افتح الأداة", "Коралны ачу")
'''
    s3 = s3.rstrip() + '\n' + add
    io.open(p3, 'w', encoding='utf-8', newline='').write(s3)
print("settings table ok")

# 4. ProfilesPage: 二维码菜单项 + 处理器
p4 = 'Views/ProfilesPage.xaml'
s4 = io.open(p4, encoding='utf-8').read()
if 'Profiles_MenuQr' not in s4:
    old4 = '                                <MenuFlyoutItem x:Uid="Profiles_MenuEdit" Text="编辑信息" Click="MenuEdit_Click" />'
    assert old4 in s4, "profiles menu"
    new4 = '''                                <MenuFlyoutItem x:Uid="Profiles_MenuQr" Text="二维码" Click="MenuQr_Click" />
''' + old4
    s4 = s4.replace(old4, new4)
    io.open(p4, 'w', encoding='utf-8', newline='').write(s4)
print("profiles xaml ok")

p5 = 'Views/ProfilesPage.xaml.cs'
s5 = io.open(p5, encoding='utf-8').read()
if 'MenuQr_Click' not in s5:
    old5 = '    private async void MenuEdit_Click(object sender, RoutedEventArgs e)'
    assert old5 in s5, "profiles handler"
    new5 = '''    private async void MenuQr_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is not { } vm) return;
        if (vm.Item.Type != "remote" || string.IsNullOrEmpty(vm.Item.Url))
        {
            var warn = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = L10n.T("Msg_QrTitle").Split('：')[0],
                Content = L10n.T("Msg_QrRemoteOnly"),
                CloseButtonText = L10n.T("Common_OK"),
            };
            await warn.ShowAsync();
            return;
        }
        var dialog = new QrDialog(vm.Item, XamlRoot);
        await dialog.ShowAsync();
    }

''' + old5
    s5 = s5.replace(old5, new5, 1)
    io.open(p5, 'w', encoding='utf-8', newline='').write(s5)
print("profiles cs ok")
