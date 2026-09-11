using System.Text.Json;
using Flux.Core.Config;
using Xunit;

namespace Flux.Tests;

/// <summary>Jint 脚本沙箱测试：超时、语句数、递归限制、console、禁止 CLR 访问。</summary>
public class ScriptSandboxTests
{
    private static string ConfigJson(string extra = "{}") =>
        $$"""{"mixed-port":7897,"mode":"rule","extra":{{extra}}}""";

    [Fact]
    public void Execute_main修改配置_返回JSON()
    {
        var sandbox = new ScriptSandbox();
        var result = sandbox.Execute(
            "function main(config, name) { config.mode = 'global'; return config; }",
            ConfigJson(), "测试订阅");

        using var doc = JsonDocument.Parse(result.ConfigJson);
        Assert.Equal("global", doc.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public void Execute_console输出被捕获()
    {
        var sandbox = new ScriptSandbox();
        var result = sandbox.Execute(
            "function main(config) { console.log('hello'); console.warn('warned'); return config; }",
            ConfigJson(), "n");

        Assert.Contains(result.Logs, l => l.Level == "info" && l.Message.Contains("hello"));
        Assert.Contains(result.Logs, l => l.Level == "warn" && l.Message.Contains("warned"));
    }

    [Fact]
    public void Execute_语法错误_报script_compile()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function main( { return config; }", ConfigJson(), "n"));
        Assert.Equal("script_compile", ex.Code);
    }

    [Fact]
    public void Execute_缺少main_报script_missing_main()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("var x = 1;", ConfigJson(), "n"));
        Assert.Equal("script_missing_main", ex.Code);
    }

    [Fact]
    public void Execute_main抛异常_报script_error()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function main(c) { throw new Error('inner'); }", ConfigJson(), "n"));
        Assert.Equal("script_error", ex.Code);
        Assert.Contains("inner", ex.Message);
    }

    [Fact]
    public void Execute_死循环_被语句数或超时限制终止()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function main(c) { while(true){} }", ConfigJson(), "n", timeoutMs: 500, maxStatements: 500_000));
        Assert.Contains(ex.Code, new[] { "script_timeout", "script_error" });
    }

    [Fact]
    public void Execute_无限递归_被递归深度限制终止()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function f(){ return f(); } function main(c){ f(); return c; }", ConfigJson(), "n"));
        Assert.Contains(ex.Code, new[] { "script_timeout", "script_error" });
    }

    [Fact]
    public void Execute_无System对象_禁止CLR访问()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function main(c) { var x = System; return c; }", ConfigJson(), "n"));
        Assert.Equal("script_error", ex.Code);
    }

    [Fact]
    public void Execute_返回undefined_报script_invalid_return()
    {
        var sandbox = new ScriptSandbox();
        var ex = Assert.Throws<ScriptSandboxException>(() =>
            sandbox.Execute("function main(c) { }", ConfigJson(), "n"));
        Assert.Equal("script_invalid_return", ex.Code);
    }

    [Fact]
    public void Execute_profileName传入main()
    {
        var sandbox = new ScriptSandbox();
        var result = sandbox.Execute(
            "function main(config, name) { config.profileName = name; return config; }",
            ConfigJson(), "我的订阅");

        using var doc = JsonDocument.Parse(result.ConfigJson);
        Assert.Equal("我的订阅", doc.RootElement.GetProperty("profileName").GetString());
    }
}
