using System.Text;
using System.Text.RegularExpressions;

namespace Flux.Core.Proxy;

/// <summary>CheckNetIsolation 文本协议和批处理命令；不执行系统变更。</summary>
public static class LoopbackExemptions
{
    private const string FamilyPattern = @"[A-Za-z0-9.][A-Za-z0-9.\-]*_[A-Za-z0-9]{13}";

    public static HashSet<string> ParseOutput(byte[] output)
    {
        // Windows 本地代码页、UTF-8 的包名均为 ASCII。只解析包名，无须猜测本地化标题的代码页。
        var encoding = output.Length >= 2 && output[0] == 0xff && output[1] == 0xfe ? Encoding.Unicode
            : output.Length >= 2 && output[0] == 0xfe && output[1] == 0xff ? Encoding.BigEndianUnicode
            : output.Length >= 2 && output[1] == 0 ? Encoding.Unicode
            : Encoding.UTF8;
        return ParseFamilies(encoding.GetString(output));
    }

    public static HashSet<string> ParseFamilies(string output)
        => Regex.Matches(output, FamilyPattern + @"\b")
            .Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string BuildScript(string toolPath, IEnumerable<string> toAdd, IEnumerable<string> toRemove)
    {
        if (toolPath.IndexOfAny(['"', '\r', '\n', '%']) >= 0)
            throw new ArgumentException("Invalid executable path", nameof(toolPath));
        var script = new StringBuilder("@echo off\r\n");
        void Append(string operation, IEnumerable<string> families)
        {
            foreach (var family in families)
            {
                // 名称嵌入 cmd 脚本，拒绝空白、元字符和不完整包名。
                if (!Regex.IsMatch(family, @"\A" + FamilyPattern + @"\z"))
                    throw new ArgumentException("Invalid package family name", nameof(families));
                script.Append('"').Append(toolPath).Append("\" LoopbackExempt ").Append(operation)
                    .Append(" -n=").Append(family).Append("\r\nif errorlevel 1 exit /b %errorlevel%\r\n");
            }
        }
        Append("-a", toAdd);
        Append("-d", toRemove);
        return script.Append("exit /b 0\r\n").ToString();
    }

    public static bool MatchesSelection(IReadOnlyDictionary<string, bool> selected, HashSet<string> actual)
        => selected.All(row => row.Value == actual.Contains(row.Key));
}
