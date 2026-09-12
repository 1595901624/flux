using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Flux.Utils;

public static class Format
{
    public static string Bytes(double bytes) => Bytes((long)bytes);

    public static string Bytes(long bytes) => bytes switch
    {
        >= 1L << 50 => $"{bytes / (double)(1L << 50):F2} TB",
        >= 1L << 40 => $"{bytes / (double)(1L << 40):F2} GB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F1} KB",
        _ => $"{bytes} B",
    };

    /// <summary>测速 URL 简化显示（去协议与路径）。</summary>
    public static string TestUrl(string url)
    {
        var u = url.Replace("https://", "").Replace("http://", "");
        var idx = u.IndexOf('/');
        return idx > 0 ? u[..idx] : u;
    }

    /// <summary>延迟文字（-1 未知，0 超时）。</summary>
    public static string DelayText(int delay) => delay switch
    {
        < 0 => "",
        0 => Flux.Services.L10n.T("Format_Timeout"),
        _ => $"{delay} ms",
    };

    private static readonly SolidColorBrush GreenBrush = new(Colors.ForestGreen);
    private static readonly SolidColorBrush BlueBrush = new(Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush OrangeBrush = new(Colors.DarkOrange);
    private static readonly SolidColorBrush RedBrush = new(Colors.IndianRed);
    private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

    public static SolidColorBrush DelayBrush(int delay) => delay switch
    {
        < 0 => GrayBrush,
        0 => RedBrush,
        < 250 => GreenBrush,
        < 500 => BlueBrush,
        < 1000 => OrangeBrush,
        _ => RedBrush,
    };

    public static string ModeText(string mode) => mode switch
    {
        "rule" => Flux.Services.L10n.T("Fmt_ModeRule"),
        "global" => Flux.Services.L10n.T("Fmt_ModeGlobal"),
        "direct" => Flux.Services.L10n.T("Fmt_ModeDirect"),
        _ => mode,
    };
}
