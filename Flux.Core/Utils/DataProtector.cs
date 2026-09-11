using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace Flux.Core.Utils;

/// <summary>
/// Windows DPAPI（CurrentUser）敏感字段加密。仅 Windows 平台可用。
/// </summary>
[SupportedOSPlatform("windows")]
public static class DataProtector
{
    private static readonly byte[] Entropy = "Flux.Core.DPAPI.v1"u8.ToArray();

    /// <summary>加密明文，返回 Base64；空输入返回空串。</summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>解密 Base64 密文；失败返回空串（不抛出，避免坏数据阻塞启动）。</summary>
    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(cipherText), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return "";
        }
    }
}
