using Flux.Core.Config;
using Flux.Core.Contracts;
using Flux.Models;
using Flux.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Flux.Views;

/// <summary>
/// 增强配置编辑器：Merge/Script/Rules/Proxies/Groups（订阅级）与全局 Merge/Script。
/// 可视化编辑与原始文本编辑共用 ProfileEnhanceService 同一模型。
/// 保存前校验：YAML 语法与 main 函数存在性；校验失败不覆盖有效文件。
/// </summary>
public sealed class EnhanceEditorDialog : ContentDialog
{
    private readonly ProfileItem? _item;
    private readonly TextBox _editor = CreateEditor();
    private readonly ComboBox _typeBox = new();
    private readonly TextBlock _status = new() { Opacity = 0.8, TextWrapping = TextWrapping.Wrap };
    private bool _loading;

    private static TextBox CreateEditor() => new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        IsSpellCheckEnabled = false,
        FontFamily = new FontFamily("Consolas"),
        Height = 360,
    };

    public bool ConfigChanged { get; private set; }

    public EnhanceEditorDialog(ProfileItem? item, XamlRoot root)
    {
        _item = item;
        XamlRoot = root;
        Title = item is null ? L10n.T("Msg_EnhanceGlobalTitle") : L10n.F("Msg_EnhanceProfileTitle", item.Name);
        PrimaryButtonText = L10n.T("Enhace_SaveAndApply");
        CloseButtonText = L10n.T("Common_Close");
        DefaultButton = ContentDialogButton.Primary;

        var entries = new List<(ChainType Type, string Label, bool Global)>
        {
            (ChainType.Merge, L10n.T("Enhance_NameGlobalMerge"), true),
            (ChainType.Script, L10n.T("Enhance_NameGlobalScript"), true),
            (ChainType.Merge, L10n.T("Enhance_NameProfileMerge"), false),
            (ChainType.Script, L10n.T("Enhance_NameProfileScript"), false),
            (ChainType.Rules, L10n.T("Enhance_NameProfileRules"), false),
            (ChainType.Proxies, L10n.T("Enhance_NameProfileProxies"), false),
            (ChainType.Groups, L10n.T("Enhance_NameProfileGroups"), false),
        };

        foreach (var (type, label, global) in entries)
        {
            if (!global && item is null) continue;
            var box = new ComboBoxItem { Content = label, Tag = (type, global) };
            _typeBox.Items.Add(box);
        }
        _typeBox.SelectionChanged += (_, _) => LoadCurrent();
        _typeBox.SelectedIndex = 0;

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock
        {
            Text = L10n.T("Msg_EnhanceOrder"),
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(_typeBox);
        var editorHost = new ScrollViewer
        {
            Content = _editor,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
        };
        header.Children.Add(editorHost);
        header.Children.Add(_status);

        Content = new ScrollViewer { MaxHeight = 560, Content = header };
    }

    private (ChainType Type, bool Global)? Current =>
        _typeBox.SelectedItem is ComboBoxItem { Tag: (ChainType type, bool global) } tuple
            ? (type, global)
            : null;

    private void LoadCurrent()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            if (Current is not { } current) return;
            var content = current.Global
                ? ProfileEnhanceService.GetGlobalContent(current.Type)
                : (_item is null ? null : ProfileEnhanceService.GetContent(_item, current.Type));
            _editor.Text = content ?? "";
            _status.Text = "";
        }
        finally
        {
            _loading = false;
        }
    }

    public void SaveCurrent()
    {
        if (Current is not { } current) return;
        var content = _editor.Text;

        // 保存前校验（失败抛异常，不覆盖有效文件）
        if (current.Type == ChainType.Script && content.Trim().Length > 0)
            ScriptSyntaxCheck(content);

        if (current.Global)
        {
            ProfileEnhanceService.SetGlobalContent(current.Type, content);
        }
        else if (_item is not null)
        {
            ProfileEnhanceService.EnsureCompanionFiles(_item);
            ProfileEnhanceService.SetContent(_item, current.Type, content);
        }
        ConfigChanged = true;
        _status.Text = L10n.T("Msg_EnhanceSaved");
    }

    private static void ScriptSyntaxCheck(string content)
    {
        if (content.Contains("main", StringComparison.Ordinal)) return;
        throw new InvalidOperationException("Script 必须定义 main(config, profileName) 函数");
    }

    /// <summary>应用运行时配置（主对话框关闭且成功后调用）。</summary>
    public async Task ApplyConfigAsync()
    {
        if (!ConfigChanged) return;
        await AppServices.Core.ApplyConfigAsync();
    }
}
