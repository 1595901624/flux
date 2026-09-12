# -*- coding: utf-8 -*-
"""校验每个 XAML x:Uid 元素在 resw 中是否含有该元素类型支持的属性。

元素 → 必需属性映射（缺一即 XamlParseException 崩溃）：
  TextBlock/Run            → .Text
  Button/ToggleButton/     → .Content
  ComboBoxItem/RadioButton/
  NavigationViewItem
  MenuFlyoutItem           → .Text
  TextBox                  → .PlaceholderText
  sc:SettingsCard          → .Header
  PasswordBox              → 无（占位在代码中）
"""
import io, re, glob, os
import xml.etree.ElementTree as ET

XAML_NS = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X_NS = '{http://schemas.microsoft.com/winfx/2006/xaml}'

PROP_BY_TAG = {
    'TextBlock': ['Text'],
    'SegmentedItem': ['Content'],
    'ToggleSwitch': ['Header'],
    'Run': ['Text'],
    'Button': ['Content'],
    'ToggleButton': ['Content'],
    'ComboBoxItem': ['Content'],
    'RadioButton': ['Content'],
    'NavigationViewItem': ['Content'],
    'MenuFlyoutItem': ['Text'],
    'TextBox': ['PlaceholderText'],
    'SettingsCard': ['Header', 'Description'],
}

def load_resw_names(lang='zh-CN'):
    path = os.path.join('Strings', lang, 'Resources.resw')
    names = set()
    root = ET.parse(path).getroot()
    for data in root.iter('data'):
        names.add(data.get('name'))
    return names

resw = load_resw_names()
problems = 0
for path in sorted(glob.glob('Views/*.xaml') + ['MainWindow.xaml']):
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as e:
        print(f'XML 解析失败 {path}: {e}')
        problems += 1
        continue
    for elem in root.iter():
        uid = elem.get(f'{X_NS}Uid')
        if not uid:
            continue
        tag = elem.tag.split('}')[-1]
        props = PROP_BY_TAG.get(tag)
        if props is None:
            print(f'未知元素类型 {tag}（{path}，uid={uid}）——请补充映射')
            problems += 1
            continue
        if any(f'{uid}.{p}' in resw for p in props):
            continue
        # 附加属性（如 ToolTipService.ToolTip）适用于任何元素
        if any(name.startswith(uid + '.') and '.ToolTipService.' in name for name in resw):
            continue
        print(f'{path}: uid="{uid}" 元素 {tag} 缺少 {"/".join(props)} 条目')
        problems += 1

print(f'\n问题数: {problems}')
