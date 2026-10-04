using Flux.Services;
using Microsoft.UI.Xaml.Controls;

namespace Flux.Views;

/// <summary>弹窗独立于页面继承链，显式采用当前应用语言的排版方向。</summary>
public class LocalizedContentDialog : ContentDialog
{
    public LocalizedContentDialog() => FlowDirection = LanguageLayout.Current;
}
