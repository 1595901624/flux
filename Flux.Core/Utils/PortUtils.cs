using System.Net;
using System.Net.Sockets;

namespace Flux.Core.Utils;

/// <summary>端口可用性检测（保存端口前检测占用）。</summary>
public static class PortUtils
{
    /// <summary>
    /// 检测 TCP 端口是否空闲：尝试在环回（IPv4/IPv6）上绑定。
    /// 被任何进程（含本应用内核）监听的端口都会返回 false。
    /// </summary>
    public static bool IsPortFree(int port)
    {
        if (port is < 1 or > 65535) return false;
        return CanBind(IPAddress.Loopback, port) && CanBind(IPAddress.IPv6Loopback, port);
    }

    /// <summary>批量检测：返回不可用的端口列表。</summary>
    public static IReadOnlyList<int> FindOccupied(params int[] ports) =>
        ports.Where(p => !IsPortFree(p)).ToList();

    private static bool CanBind(IPAddress address, int port)
    {
        try
        {
            using var listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(address, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch
        {
            // 检测失败时不阻塞保存
            return true;
        }
    }
}
