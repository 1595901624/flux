using Flux.Core.Hotkeys;
using Xunit;

namespace Flux.Tests;

/// <summary>热键组合解析测试：解析、规范化、冲突判断。</summary>
public class HotkeyComboParserTests
{
    [Theory]
    [InlineData("Ctrl+Shift+F1", "Ctrl+Shift+F1")]
    [InlineData("Ctrl+Alt+Home", "Ctrl+Alt+Home")]
    [InlineData("Alt+X", "Alt+X")]
    [InlineData("Win+N", "Win+N")]
    public void Parse_合法组合_规范化输出(string input, string expected)
    {
        var combo = HotkeyComboParser.Parse(input);
        Assert.NotNull(combo);
        Assert.Equal(expected, combo!.Display);
    }

    [Fact]
    public void Parse_修饰键_映射正确()
    {
        var combo = HotkeyComboParser.Parse("Ctrl+Shift+Z")!;
        Assert.Equal(HotkeyComboParser.ModControl | HotkeyComboParser.ModShift, combo.Modifiers);
        Assert.Equal((uint)'Z', combo.VirtualKey);
    }

    [Fact]
    public void Parse_功能键()
    {
        var combo = HotkeyComboParser.Parse("F12")!;
        Assert.Equal(0x7Bu, combo.VirtualKey); // VK_F12 = 0x7B
        Assert.Equal(0u, combo.Modifiers);
    }

    [Fact]
    public void Parse_非法输入_返回null()
    {
        Assert.Null(HotkeyComboParser.Parse(""));
        Assert.Null(HotkeyComboParser.Parse(null));
        Assert.Null(HotkeyComboParser.Parse("Ctrl"));           // 只有修饰键
        Assert.Null(HotkeyComboParser.Parse("Ctrl+Shift+Alt")); // 只有修饰键
        Assert.Null(HotkeyComboParser.Parse("Ctrl+NotAKey!"));  // 未知主键
        Assert.Null(HotkeyComboParser.Parse("Ctrl+A+B"));       // 多个主键
    }

    [Fact]
    public void Parse_小写修饰键()
    {
        var combo = HotkeyComboParser.Parse("ctrl+shift+f")!;
        Assert.Equal("Ctrl+Shift+F", combo.Display);
    }

    [Fact]
    public void Conflicts_同键同修饰_冲突()
    {
        var a = HotkeyComboParser.Parse("Ctrl+Shift+F1")!;
        var b = HotkeyComboParser.Parse("Ctrl+Shift+F1")!;
        Assert.True(HotkeyComboParser.Conflicts(a, b));
    }

    [Fact]
    public void Conflicts_修饰键不同_不冲突()
    {
        var a = HotkeyComboParser.Parse("Ctrl+F1")!;
        var b = HotkeyComboParser.Parse("Ctrl+Shift+F1")!;
        Assert.False(HotkeyComboParser.Conflicts(a, b));
    }

    [Fact]
    public void Conflicts_主键不同_不冲突()
    {
        var a = HotkeyComboParser.Parse("Ctrl+F1")!;
        var b = HotkeyComboParser.Parse("Ctrl+F2")!;
        Assert.False(HotkeyComboParser.Conflicts(a, b));
    }
}
