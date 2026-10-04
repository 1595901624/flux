using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Flux.Tests;

/// <summary>
/// i18n 资源结构测试：13 语言 resw 必须可解析、键集一致、值非空；
/// L10n 加载器在资源不可用时必须回退到资源键（不抛异常、不返回空文本）。
/// </summary>
public class LocalizationReswTests
{
    private static string? FindStringsDir()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "Strings");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "zh-CN", "Resources.resw")))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public static TheoryData<string> Languages => new()
    {
        "zh-CN", "en-US", "zh-TW", "ja", "ko", "de", "es", "ru", "tr", "id", "fa", "ar", "tt",
    };

    [Fact]
    public void Resw_全部语言_键集一致()
    {
        var stringsDir = FindStringsDir();
        Assert.NotNull(stringsDir);

        HashSet<string>? reference = null;
        foreach (var lang in Languages)
        {
            var path = Path.Combine(stringsDir!, lang, "Resources.resw");
            Assert.True(File.Exists(path), $"缺少 {lang} 资源文件");
            var doc = XDocument.Load(path);
            var keys = doc.Descendants("data")
                .Select(d => d.Attribute("name")?.Value)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .ToHashSet();
            Assert.NotEmpty(keys);
            if (reference is null)
            {
                reference = keys;
            }
            else
            {
                var missing = reference.Except(keys).ToList();
                Assert.True(missing.Count == 0, $"{lang} 缺少键: {string.Join(", ", missing)}");
                var extra = keys.Except(reference).ToList();
                Assert.True(extra.Count == 0, $"{lang} 多余键: {string.Join(", ", extra)}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Resw_全部条目_值非空(string lang)
    {
        var stringsDir = FindStringsDir();
        Assert.NotNull(stringsDir);
        var doc = XDocument.Load(Path.Combine(stringsDir!, lang, "Resources.resw"));
        foreach (var value in doc.Descendants("value"))
        {
            // 产品名、协议名和各语言通用术语可以与英文相同。
            Assert.False(string.IsNullOrWhiteSpace(value.Value), $"{lang} 存在空值条目");
        }
    }

    private static Dictionary<string, string> ReadResources(string lang)
        => XDocument.Load(Path.Combine(FindStringsDir()!, lang, "Resources.resw"))
            .Descendants("data").ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);

    [Theory]
    [MemberData(nameof(Languages))]
    public void ResourcesHaveUniqueKeysAndMatchingFormatArguments(string lang)
    {
        // ToDictionary also rejects duplicate keys, which a HashSet would hide.
        var source = ReadResources("en-US");
        var translated = ReadResources(lang);
        static string[] Arguments(string value) => Regex.Matches(value, @"(?<!\{)\{(\d+)(?:[, :][^{}]*)?\}(?!\})")
            .Select(m => m.Groups[1].Value).Order().ToArray();
        foreach (var (key, value) in translated)
            Assert.True(Arguments(source[key]).SequenceEqual(Arguments(value)), $"{lang}/{key}: format arguments differ");
    }

    [Fact]
    public void LiteralApplicationResourceLookupsExist()
    {
        var root = Path.GetDirectoryName(FindStringsDir())!;
        var resources = ReadResources("en-US");
        var files = new[] { "Views", "ViewModels", "Services" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "App.xaml.cs"));
        foreach (var file in files)
        foreach (Match match in Regex.Matches(File.ReadAllText(file), "L10n\\.[TF]\\(\"([^\"]+)\""))
        {
            var key = match.Groups[1].Value;
            Assert.True(resources.ContainsKey(key + ".Text") || resources.ContainsKey(key), $"{file}: missing text resource {key}");
        }
    }

    [Fact]
    public void XamlVisibleStringsAndToggleStatesAreLocalized()
    {
        var root = Path.GetDirectoryName(FindStringsDir())!;
        var resources = ReadResources("en-US");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var languageTags = new HashSet<string>();
        foreach (var language in Languages) languageTags.Add(language);
        // Protocol values, product names, symbols and address examples are intentionally literal.
        var literals = new HashSet<string>
        {
            "Flux", "GitHub", "PowerShell", "CMD", "gvisor", "system", "mixed", "fake-ip", "redir-host",
            "any:53", "198.18.0.1/16", "127.0.0.1:9097", "↑", "↓", "↑ —", "↓ —", " · ",
        };
        var properties = new HashSet<string> { "Text", "Content", "Header", "Description", "PlaceholderText", "ToolTipService.ToolTip" };
        var files = Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.xaml")
            .Append(Path.Combine(root, "MainWindow.xaml"));
        foreach (var file in files)
        foreach (var element in XDocument.Load(file).Descendants())
        {
            var uid = element.Attribute(x + "Uid")?.Value;
            // WinUI applies every resource under x:Uid to that control. A text alias
            // sharing a Button's UID crashes page construction even when Content exists.
            if (uid is not null && element.Name.LocalName is "Button" or "ComboBoxItem" or "RadioButton")
                Assert.False(resources.ContainsKey(uid + ".Text"), $"{file}: {uid} has Text resource but control requires Content");
            if (element.Name.LocalName == "ToggleSwitch")
            {
                Assert.True(resources.ContainsKey(uid + ".OnContent"), $"{file}: toggle needs localized OnContent");
                Assert.True(resources.ContainsKey(uid + ".OffContent"), $"{file}: toggle needs localized OffContent");
            }
            // Language names remain in their native script so users can find their language.
            if (element.Name.LocalName == "ComboBoxItem" && languageTags.Contains(element.Attribute("Tag")?.Value ?? ""))
                continue;
            foreach (var attribute in element.Attributes().Where(a => properties.Contains(a.Name.LocalName)))
            {
                var value = attribute.Value;
                if (value.Length == 0 || value.StartsWith('{') || literals.Contains(value)) continue;
                Assert.True(resources.ContainsKey(uid + "." + attribute.Name.LocalName), $"{file}: unlocalized {attribute.Name}={value}");
            }
            // Buttons may store Content as an inline text node rather than an attribute.
            if (element.Name.LocalName == "Button" && element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                Assert.True(resources.ContainsKey(uid + ".Content"), $"{file}: unlocalized button text");
        }
    }

}
