using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Flux.Core.Contracts;

namespace Flux.Core.Config;

/// <summary>
/// 受限 JavaScript 沙箱（Jint）：只暴露配置对象与 console，禁止 CLR/文件/网络访问，
/// 强制超时、语句数与递归深度限制。脚本必须定义 main(config, profileName) 并返回配置对象。
/// </summary>
public sealed class ScriptSandbox
{
    public const int DefaultTimeoutMs = 5000;
    public const int DefaultMaxStatements = 2_000_000;
    public const int DefaultMaxRecursionDepth = 128;
    private const int MaxLogEntries = 200;

    /// <summary>执行脚本。configJson 为配置的 JSON 文本；返回值序列化为 JSON 文本。</summary>
    /// <exception cref="ScriptSandboxException">脚本语法错误、缺少 main、超时或返回值非法。</exception>
    public ScriptResult Execute(
        string script,
        string configJson,
        string profileName,
        int timeoutMs = DefaultTimeoutMs,
        int maxStatements = DefaultMaxStatements,
        int maxRecursionDepth = DefaultMaxRecursionDepth)
    {
        var logs = new List<ChainLogEntry>();
        var engine = new Engine(options => options
            .TimeoutInterval(TimeSpan.FromMilliseconds(timeoutMs))
            .MaxStatements(maxStatements)
            .LimitRecursion(maxRecursionDepth)
            .Strict());

        AttachConsole(engine, logs);

        JsValue main;
        try
        {
            engine.Execute(script);
            main = engine.GetValue("main");
        }
        catch (ScriptPreparationException ex)
        {
            throw new ScriptSandboxException("script_compile", $"脚本语法错误: {ex.Message}", logs);
        }
        catch (Acornima.SyntaxErrorException ex)
        {
            throw new ScriptSandboxException("script_compile", $"脚本语法错误: {ex.Message}", logs);
        }
        catch (JavaScriptException ex)
        {
            // Jint 4 将部分解析错误也包装为 JavaScriptException，按消息特征区分
            var message = ex.Message ?? "";
            if (message.StartsWith("Unexpected token", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("Unexpected end", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("Unexpected identifier", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Invalid left-hand side", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("has already been declared", StringComparison.OrdinalIgnoreCase))
                throw new ScriptSandboxException("script_compile", $"脚本语法错误: {message}", logs);
            throw new ScriptSandboxException("script_error", $"脚本执行错误: {message}", logs);
        }
        catch (TimeoutException)
        {
            throw new ScriptSandboxException("script_timeout", $"脚本执行超时（{timeoutMs}ms）", logs);
        }
        catch (JintException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptSandboxException("script_timeout", $"脚本执行超时（{timeoutMs}ms）", logs);
        }
        catch (JintException ex)
        {
            throw new ScriptSandboxException("script_error", $"脚本执行失败: {ex.Message}", logs);
        }

        if (main.IsUndefined())
            throw new ScriptSandboxException("script_missing_main", "脚本缺少 main(config, profileName) 函数", logs);

        JsValue configValue;
        try
        {
            configValue = engine.Evaluate($"JSON.parse({JsString(configJson)})");
        }
        catch (JavaScriptException ex)
        {
            throw new ScriptSandboxException("config_parse", $"配置 JSON 解析失败: {ex.Message}", logs);
        }

        JsValue result;
        try
        {
            result = engine.Invoke(main, configValue, profileName);
        }
        catch (JavaScriptException ex)
        {
            var message = ex.Message ?? "";
            if (message.Contains("not a function", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("is not callable", StringComparison.OrdinalIgnoreCase))
                throw new ScriptSandboxException("script_missing_main", "main 不可调用：脚本必须定义 main(config, profileName) 函数", logs);
            throw new ScriptSandboxException("script_error", $"main 执行错误: {message}", logs);
        }
        catch (TimeoutException)
        {
            throw new ScriptSandboxException("script_timeout", $"main 执行超时（{timeoutMs}ms）", logs);
        }
        catch (JintException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptSandboxException("script_timeout", $"main 执行超时（{timeoutMs}ms）", logs);
        }
        catch (JintException ex)
        {
            throw new ScriptSandboxException("script_error", $"main 执行失败: {ex.Message}", logs);
        }

        if (result.IsUndefined() || result.IsNull())
            throw new ScriptSandboxException("script_invalid_return", "main 必须返回配置对象", logs);

        string resultJson;
        try
        {
            engine.SetValue("__flux_result", result);
            resultJson = engine.Evaluate("JSON.stringify(__flux_result)").AsString();
        }
        catch (Exception ex)
        {
            throw new ScriptSandboxException("script_invalid_return", $"返回值无法序列化: {ex.Message}", logs);
        }

        return new ScriptResult(resultJson, logs);
    }

    private static string JsString(string text) =>
        System.Text.Json.JsonSerializer.Serialize(text);

    private static void AttachConsole(Engine engine, List<ChainLogEntry> logs)
    {
        var consoleObj = engine.Evaluate("({})").AsObject();
        foreach (var level in new[] { "log", "info", "warn", "error" })
        {
            var capturedLevel = level == "log" ? "info" : level;
            consoleObj.Set(level, new ClrFunction(
                engine,
                level,
                (_, args) =>
                {
                    if (logs.Count < MaxLogEntries)
                    {
                        logs.Add(new ChainLogEntry(
                            capturedLevel,
                            "",
                            "script",
                            string.Join(" ", args.Select(Describe))));
                    }
                    return JsValue.Undefined;
                }));
        }
        engine.SetValue("console", consoleObj);
    }

    private static string Describe(JsValue value)
    {
        if (value.IsString()) return value.AsString();
        if (value.IsUndefined()) return "undefined";
        if (value.IsNull()) return "null";
        try
        {
            return value.ToObject()?.ToString() ?? value.ToString();
        }
        catch
        {
            return value.ToString();
        }
    }
}

/// <summary>脚本执行结果：新配置 JSON 与 console 日志。</summary>
public sealed record ScriptResult(string ConfigJson, IReadOnlyList<ChainLogEntry> Logs);

/// <summary>脚本沙箱失败：携带步骤码、消息与执行期间的 console 日志。</summary>
public sealed class ScriptSandboxException : Exception
{
    public string Code { get; }
    public IReadOnlyList<ChainLogEntry> Logs { get; }

    public ScriptSandboxException(string code, string message, IReadOnlyList<ChainLogEntry> logs)
        : base(message)
    {
        Code = code;
        Logs = logs;
    }
}
