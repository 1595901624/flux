using System.Security.Principal;
using Microsoft.Win32;

namespace Flux.Service;

public sealed record ServiceClientContext(string Sid, string Name, bool IsAdministrator, string? ExecutablePath = null);

internal static class UserDataDirectoryResolver
{
    private const string ProfileList = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    public static IReadOnlyList<string> ResolveAllowed(ServiceClientContext client)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(client.Sid)) return result;
        using var key = Registry.LocalMachine.OpenSubKey($@"{ProfileList}\{client.Sid}");
        var profile = key?.GetValue("ProfileImagePath") as string;
        if (!string.IsNullOrWhiteSpace(profile))
        {
            profile = Environment.ExpandEnvironmentVariables(profile);
            result.Add(Path.GetFullPath(Path.Combine(profile, "AppData", "Roaming", "flux")));
        }

        if (!string.IsNullOrWhiteSpace(client.ExecutablePath))
        {
            var exeDir = Path.GetDirectoryName(Path.GetFullPath(client.ExecutablePath));
            if (exeDir is not null && File.Exists(Path.Combine(exeDir, ".config", "PORTABLE")))
                result.Add(Path.GetFullPath(Path.Combine(exeDir, ".config", "flux")));
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static ServiceClientContext Capture()
    {
        using var identity = WindowsIdentity.GetCurrent(true)
            ?? throw new UnauthorizedAccessException("无法获取管道客户端身份");
        var sid = identity.User?.Value ?? throw new UnauthorizedAccessException("无法识别管道客户端 SID");
        var principal = new WindowsPrincipal(identity);
        return new ServiceClientContext(sid, identity.Name,
            principal.IsInRole(WindowsBuiltInRole.Administrator));
    }
}
