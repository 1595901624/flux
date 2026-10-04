// 隔离 WinUI、特权服务和真实系统代理；测试编译实际 CoreProcessService。
using Flux.Core.Contracts;
using Flux.Core.Service;
using YamlDotNet.RepresentationModel;

namespace Flux.Models
{
    public sealed class ProfileItem { }
    public sealed record ProfileSelected(string Name, string Now);
}

namespace Flux.Services
{
    public sealed record LogLine(DateTime Time, string Type, string Payload);
    public static class AppServices
    {
        public static ConfigService Config { get; } = new();
        public static PrivilegeBroker Privilege { get; } = new();
        public static MihomoApiService Api { get; } = new();
        public static TestStreams Streams { get; } = new();
        public static TestProxy SysProxy { get; } = new();
    }
    public partial class ConfigService
    {
        public Flux.Models.VergeConfig Verge { get; } = new();
        public int MixedPort => 7897;
        public static void WriteAllTextAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        public void WriteRuntimeFile(string path) => throw new NotSupportedException();
        public void WriteProfileRuntimeFile(string path, Flux.Models.ProfileItem item, string content)
            => throw new NotSupportedException();
        public IReadOnlyList<Flux.Models.ProfileSelected> GetCurrentProxySelections() => [];
    }
    public static class YamlHelper
    {
        public static YamlMappingNode? ParseMapping(string text) => Flux.Core.Config.YamlOps.ParseMapping(text);
    }
    public static class Paths
    {
        public static string AppDataDir => Path.GetTempPath();
        public static string CoreExePath => "unused-mihomo.exe";
        public static string LegacyCoreExePath => "unused-legacy.exe";
        public static string RuntimeConfigFile => "unused-runtime.yaml";
        public static string VergeConfigFile { get; set; } = "unused-verge.yaml";
        public static string ProxyStateFile => throw new NotSupportedException();
        public static string PacFile => throw new NotSupportedException();
        public static string PacFileUrl => throw new NotSupportedException();
        public static void EnsureCoreExecutable() => throw new NotSupportedException();
    }
    public static class TrayService { public static bool IsElevated() => false; }
    public static class LogService
    {
        public static void App(string message, string level = "info") { }
        public static void Core(string message) { }
    }
    public sealed class TestStreams
    {
        public int Stops { get; set; }
        public void Stop() => Stops++;
    }
    public sealed class TestProxy
    {
        public int Resets { get; set; }
        public void Reset() => Resets++;
    }
    public sealed class PrivilegeBroker
    {
        public bool IsServiceReady() => false;
        public Task<ServiceCoreState?> GetServiceCoreStateAsync(CancellationToken ct)
            => Task.FromResult<ServiceCoreState?>(null);
        public Task<OperationResult<bool>> StartCoreViaServiceAsync(string config, string core, string data)
            => throw new NotSupportedException();
        public Task<OperationResult<bool>> StopCoreViaServiceAsync() => throw new NotSupportedException();
    }
}
