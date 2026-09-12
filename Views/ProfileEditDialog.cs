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
    private readonly TextBox _nameBox = new() { PlaceholderText = L10n.T("ProfileEdit_NamePlaceholder") };
    private readonly TextBox _descBox = new() { PlaceholderText = L10n.T("ProfileEdit_DescPlaceholder") };
    private readonly TextBox _urlBox = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _uaBox = new() { PlaceholderText = L10n.T("ProfileEdit_UaPlaceholder") };
    private readonly TextBox _timeoutBox = new();
    private readonly TextBox _intervalBox = new();
    private readonly CheckBox _invalidCertBox = new() { Content = L10n.T("ProfileEdit_InvalidCertLabel") };
    private readonly CheckBox _autoUpdateBox = new() { Content = L10n.T("ProfileEdit_AutoUpdateLabel") };
    private readonly ComboBox _channelBox = new();

    public ProfileEditDialog(ProfileItem item, XamlRoot root)
    {
        _item = item;
        XamlRoot = root;
        Title = L10n.F("Msg_EditProfileTitle", item.Name);
        PrimaryButtonText = L10n.T("Common_Save");
        CloseButtonText = L10n.T("Common_Cancel");
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
            (L10n.T("ProfileEdit_ChannelAuto"), "auto"),
            (L10n.T("Fmt_ModeDirect"), "direct"),
            (L10n.T("ProfileEdit_ChannelCore"), "self"),
            (L10n.T("ProfileEdit_ChannelSystem"), "system"),
        })
        {
            _channelBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        foreach (var i in _channelBox.Items.OfType<ComboBoxItem>())
            if ((string)i.Tag == item.Option.UpdateChannel) _channelBox.SelectedItem = i;
        if (_channelBox.SelectedItem is null) _channelBox.SelectedIndex = 0;

        var form = new StackPanel { Spacing = 10, MinWidth = 420 };
        form.Children.Add(Field(L10n.T("ProfileEdit_FieldName"), _nameBox));
        form.Children.Add(Field(L10n.T("ProfileEdit_FieldDesc"), _descBox));
        if (item.Type == "remote")
        {
            form.Children.Add(Field(L10n.T("ProfileEdit_FieldUrl"), _urlBox));
            form.Children.Add(Field(L10n.T("ProfileEdit_FieldUa"), _uaBox));
            form.Children.Add(Field(L10n.T("ProfileEdit_FieldChannel"), _channelBox));
            form.Children.Add(Field(L10n.T("ProfileEdit_FieldInterval"), _intervalBox));
            form.Children.Add(_autoUpdateBox);
            form.Children.Add(_invalidCertBox);
        }
        form.Children.Add(Field(L10n.T("ProfileEdit_FieldTimeout"), _timeoutBox));

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
        if (name.Length == 0) return L10n.T("Msg_NameRequired");
        if (!int.TryParse(_timeoutBox.Text, out var timeout) || timeout < 5 || timeout > 300)
            return L10n.T("Msg_TimeoutInvalid");
        if (!int.TryParse(_intervalBox.Text, out var interval) || interval < 0)
            return L10n.T("Msg_IntervalInvalid");
        if (_channelBox.SelectedItem is not ComboBoxItem { Tag: string channel })
            return L10n.T("Msg_ChooseChannel");

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
