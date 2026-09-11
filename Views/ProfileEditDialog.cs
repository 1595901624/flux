using Flux.Models;
using Flux.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Flux.Views;

/// <summary>
/// 订阅信息编辑对话框：名称/说明/URL/User-Agent/超时/更新间隔/证书校验/更新通道。
/// </summary>
public sealed class ProfileEditDialog : ContentDialog
{
    private readonly ProfileItem _item;
    private readonly TextBox _nameBox = new() { PlaceholderText = "订阅名称" };
    private readonly TextBox _descBox = new() { PlaceholderText = "描述（可选）" };
    private readonly TextBox _urlBox = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _uaBox = new() { PlaceholderText = "默认 clash-verge/v2.5.2" };
    private readonly TextBox _timeoutBox = new();
    private readonly TextBox _intervalBox = new();
    private readonly CheckBox _invalidCertBox = new() { Content = "接受无效 TLS 证书（危险）" };
    private readonly CheckBox _autoUpdateBox = new() { Content = "参与自动更新" };
    private readonly ComboBox _channelBox = new();

    public ProfileEditDialog(ProfileItem item, XamlRoot root)
    {
        _item = item;
        XamlRoot = root;
        Title = $"编辑订阅：{item.Name}";
        PrimaryButtonText = "保存";
        CloseButtonText = "取消";
        DefaultButton = ContentDialogButton.Primary;

        _nameBox.Text = item.Name;
        _descBox.Text = item.Desc;
        _urlBox.Text = item.Url;
        _uaBox.Text = item.Option.UserAgent ?? "";
        _timeoutBox.Text = item.Option.TimeoutSeconds.ToString();
        _intervalBox.Text = item.Option.UpdateInterval.ToString();
        _invalidCertBox.IsChecked = item.Option.DangerAcceptInvalidCerts;
        _autoUpdateBox.IsChecked = item.Option.AllowAutoUpdate;

        foreach (var (label, value) in new[]
        {
            ("自动回退（直连→内核→系统）", "auto"),
            ("直连", "direct"),
            ("经内核代理", "self"),
            ("经系统代理", "system"),
        })
        {
            _channelBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        foreach (var i in _channelBox.Items.OfType<ComboBoxItem>())
            if ((string)i.Tag == item.Option.UpdateChannel) _channelBox.SelectedItem = i;
        if (_channelBox.SelectedItem is null) _channelBox.SelectedIndex = 0;

        var form = new StackPanel { Spacing = 10, MinWidth = 420 };
        form.Children.Add(Field("名称", _nameBox));
        form.Children.Add(Field("描述", _descBox));
        if (item.Type == "remote")
        {
            form.Children.Add(Field("订阅 URL", _urlBox));
            form.Children.Add(Field("User-Agent", _uaBox));
            form.Children.Add(Field("更新通道", _channelBox));
            form.Children.Add(Field("更新间隔（分钟，0=不自动）", _intervalBox));
            form.Children.Add(_autoUpdateBox);
            form.Children.Add(_invalidCertBox);
        }
        form.Children.Add(Field("下载超时（秒）", _timeoutBox));

        Content = new ScrollViewer
        {
            MaxHeight = 480,
            Content = form,
        };
    }

    private static StackPanel Field(string label, FrameworkElement control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.7,
            FontFamily = new FontFamily("Segoe UI"),
        });
        panel.Children.Add(control);
        return panel;
    }

    /// <summary>应用编辑；返回错误消息或 null。</summary>
    public string? Apply()
    {
        var name = _nameBox.Text.Trim();
        if (name.Length == 0) return "名称不能为空";
        if (!int.TryParse(_timeoutBox.Text, out var timeout) || timeout < 5 || timeout > 300)
            return "下载超时必须是 5-300 秒";
        if (!int.TryParse(_intervalBox.Text, out var interval) || interval < 0)
            return "更新间隔必须是非负整数";
        if (_channelBox.SelectedItem is not ComboBoxItem { Tag: string channel })
            return "请选择更新通道";

        AppServices.Subscription.EditInfoAsync(
            _item,
            name: name,
            desc: _descBox.Text.Trim(),
            url: _item.Type == "remote" ? _urlBox.Text.Trim() : null,
            userAgent: _uaBox.Text.Trim(),
            timeoutSeconds: timeout,
            updateInterval: interval,
            dangerAcceptInvalidCerts: _invalidCertBox.IsChecked == true,
            updateChannel: channel,
            allowAutoUpdate: _autoUpdateBox.IsChecked == true).Wait();

        return null;
    }
}
