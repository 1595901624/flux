using System.Text.RegularExpressions;

namespace Flux.Core.Hotkeys;

/// <summary>解析后的热键组合。</summary>
public sealed record HotkeyCombo(uint Modifiers, uint VirtualKey, string Display)
{
    public string DisplayString => Display;
}

/// <summary>
/// 热键组合解析："Ctrl+Shift+F1"、"Alt+X"、"Win+N" 等文本
/// 与 Win32 RegisterHotKey 的 Modifiers/VirtualKey 双向转换。
/// </summary>
public static partial class HotkeyComboParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    private static readonly Dictionary<string, uint> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = ModControl,
        ["control"] = ModControl,
        ["shift"] = ModShift,
        ["alt"] = ModAlt,
        ["win"] = ModWin,
        ["windows"] = ModWin,
    };

    private static readonly Dictionary<string, uint> FunctionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["space"] = 0x20,
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["tab"] = 0x09,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E, ["del"] = 0x2E,
        ["insert"] = 0x2D, ["ins"] = 0x2D,
        ["home"] = 0x24, ["end"] = 0x23,
        ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
        ["numpad0"] = 0x60, ["numpad1"] = 0x61, ["numpad2"] = 0x62, ["numpad3"] = 0x63,
        ["numpad4"] = 0x64, ["numpad5"] = 0x65, ["numpad6"] = 0x66, ["numpad7"] = 0x67,
        ["numpad8"] = 0x68, ["numpad9"] = 0x69,
        ["oem3"] = 0xC0, // ` 键
    };

    [GeneratedRegex(@"^[A-Za-z0-9]$")]
    private static partial Regex SingleCharKey();

    /// <summary>解析组合键文本。非法返回 null。</summary>
    public static HotkeyCombo? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        uint modifiers = 0;
        uint key = 0;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (ModifierNames.TryGetValue(part, out var mod))
            {
                if (key != 0) return null; // 修饰键必须在主键之前
                modifiers |= mod;
                continue;
            }

            if (key != 0) return null; // 只允许一个主键
            if (part.Length == 1 && SingleCharKey().IsMatch(part))
            {
                key = char.ToUpperInvariant(part[0]);
            }
            else if (part.StartsWith('F') && int.TryParse(part[1..], out var fn) && fn is >= 1 and <= 24)
            {
                key = (uint)(0x6F + fn); // VK_F1 = 0x70
            }
            else if (FunctionKeys.TryGetValue(part, out var vk))
            {
                key = vk;
            }
            else
            {
                return null;
            }
        }

        if (key == 0) return null; // 只有修饰键
        return new HotkeyCombo(modifiers, key, Normalize(modifiers, key));
    }

    /// <summary>从 Modifiers/VirtualKey 生成规范化显示文本（如 "Ctrl+Shift+F1"）。</summary>
    public static string Normalize(uint modifiers, uint key)
    {
        var sb = new System.Text.StringBuilder();
        if ((modifiers & ModControl) != 0) sb.Append("Ctrl+");
        if ((modifiers & ModShift) != 0) sb.Append("Shift+");
        if ((modifiers & ModAlt) != 0) sb.Append("Alt+");
        if ((modifiers & ModWin) != 0) sb.Append("Win+");
        sb.Append(KeyName(key));
        return sb.ToString();
    }

    private static string KeyName(uint key)
    {
        if (key is >= 0x70 and <= 0x87) return $"F{key - 0x6F}";
        if (key is >= 'A' and <= 'Z' || key is >= '0' and <= '9') return ((char)key).ToString();
        foreach (var (name, vk) in FunctionKeys)
        {
            if (vk == key)
                return name switch
                {
                    "escape" or "return" or "del" or "ins" => name, // 不会命中（首键优先）
                    _ => char.ToUpperInvariant(name[0]) + name[1..],
                };
        }
        return $"0x{key:X2}";
    }

    /// <summary>两个组合是否占用相同物理按键。</summary>
    public static bool Conflicts(HotkeyCombo a, HotkeyCombo b) =>
        a.VirtualKey == b.VirtualKey &&
        (a.Modifiers & (ModControl | ModShift | ModAlt | ModWin)) ==
        (b.Modifiers & (ModControl | ModShift | ModAlt | ModWin));
}
