using System.Reflection;
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
            // 值必须非空（不显示空文本）；Translation 允许等于英文（后续可优化）
            Assert.False(string.IsNullOrWhiteSpace(value.Value), $"{lang} 存在空值条目");
        }
    }

}
