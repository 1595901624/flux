# -*- coding: utf-8 -*-
"""扫描 XAML 中含中文文本但元素缺少 x:Uid 的位置，以及 .cs 中用户可见中文字符串。"""
import io, re, glob

XAML_FILES = glob.glob('Views/*.xaml') + ['MainWindow.xaml']
print('=== XAML: 含中文但元素无 x:Uid ===')
for path in sorted(XAML_FILES):
    with io.open(path, encoding='utf-8') as f:
        s = f.read()
    # 逐元素扫描：<Tag ...>...</Tag> 或自闭合，属性中含中文
    for m in re.finditer(r'<[\w:.]+[^>]*?>', s, re.S):
        tag = m.group(0)
        if not re.search(r'[一-龥]', tag):
            continue
        if 'x:Uid=' in tag:
            continue
        # 提取含中文的属性
        attrs = re.findall(r'([\w:]+)="([^"]*[一-龥][^"]*)"', tag)
        if not attrs:
            continue
        line = s[:m.start()].count('\n') + 1
        first = attrs[0]
        print(f'{path}:{line}  {first[0]}={first[1][:30]}')

print()
print('=== C#: 用户可见中文字符串（排除注释） ===')
CS_FILES = glob.glob('ViewModels/*.cs') + glob.glob('Views/*.cs') + glob.glob('Services/*.cs') + ['Utils/Format.cs', 'App.xaml.cs', 'Program.cs']
for path in sorted(CS_FILES):
    with io.open(path, encoding='utf-8') as f:
        for i, line in enumerate(f, 1):
            t = line.strip()
            if t.startswith('//') or t.startswith('///'):
                continue
            # 找字符串字面量中的中文（排除 L10n 行、Trace 行）
            if 'L10n' in line or 'Trace(' in line:
                continue
            for m in re.finditer(r'"([^"]*[一-龥][^"]*)"', line):
                print(f'{path}:{i}  "{m.group(1)[:44]}"')
                break
