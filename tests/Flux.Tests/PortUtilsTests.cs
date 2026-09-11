using Flux.Core.Utils;
using System.Net.Sockets;
using Xunit;

namespace Flux.Tests;

/// <summary>端口占用检测测试。</summary>
public class PortUtilsTests
{
    [Fact]
    public void IsPortFree_非法端口_返回false()
    {
        Assert.False(PortUtils.IsPortFree(0));
        Assert.False(PortUtils.IsPortFree(-1));
        Assert.False(PortUtils.IsPortFree(65536));
    }

    [Fact]
    public void IsPortFree_释放端口_返回true()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
        socket.Close();
        // 端口释放后应可用
        Assert.True(PortUtils.IsPortFree(port));
    }

    [Fact]
    public void IsPortFree_已监听端口_返回false()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            Assert.False(PortUtils.IsPortFree(port));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void FindOccupied_混合端口_只返回被占用的()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var occupied = PortUtils.FindOccupied(port, 65534);
            Assert.Single(occupied);
            Assert.Equal(port, occupied[0]);
        }
        finally
        {
            listener.Stop();
        }
    }
}
