using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Flux.Models;
using Flux.Services;
using QRCoder;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Flux.Views;

/// <summary>订阅二维码对话框（QRCoder 本地生成，不上传到任何外部服务）。</summary>
public sealed class QrDialog : ContentDialog
{
    public QrDialog(ProfileItem item, XamlRoot root)
    {
        XamlRoot = root;
        Title = L10n.F("Msg_QrTitle", item.Name);
        CloseButtonText = L10n.T("Common_Close");

        var image = new Image
        {
            Width = 260,
            Height = 260,
            Stretch = Stretch.Uniform,
        };

        var urlBox = new TextBox
        {
            Text = item.Url,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        };

        var panel = new StackPanel { MinWidth = 320 };
        panel.Children.Add(image);
        panel.Children.Add(urlBox);
        Content = panel;

        var url = item.Url;
        Loaded += async (_, _) =>
        {
            try
            {
                var png = await Task.Run(() =>
                {
                    var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
                    return new PngByteQRCode(data).GetGraphic(pixelsPerModule: 8);
                });

                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await stream.WriteAsync(png.AsBuffer());
                stream.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(stream);
                image.Source = bmp;
            }
            catch (Exception ex)
            {
                Content = new TextBlock
                {
                    Text = L10n.F("Msg_QrFailed", ex.Message),
                    TextWrapping = TextWrapping.Wrap,
                };
            }
        };
    }
}
