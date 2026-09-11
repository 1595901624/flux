using Flux.Core.Utils;
using Xunit;

namespace Flux.Tests;

/// <summary>敏感信息遮蔽测试：URL 参数、Basic Auth、JSON 字段、Bearer。</summary>
public class SensitiveMaskerTests
{
    [Fact]
    public void Mask_查询参数token被遮蔽()
    {
        var result = SensitiveMasker.Mask("https://sub.example.com/api?token=abcdef123456&flag=1");
        Assert.DoesNotContain("abcdef123456", result);
        Assert.Contains("***", result);
        Assert.Contains("flag=1", result);
    }

    [Fact]
    public void Mask_订阅URL用户信息被遮蔽()
    {
        var result = SensitiveMasker.Mask("https://user:secretpass@example.com/sub?target=clash");
        Assert.DoesNotContain("secretpass", result);
        Assert.DoesNotContain("user:", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void Mask_Bearer令牌被遮蔽()
    {
        var result = SensitiveMasker.Mask("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig");
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", result);
    }

    [Fact]
    public void Mask_JSONSecret字段被遮蔽()
    {
        var result = SensitiveMasker.Mask("""{"secret":"my-controller-secret","mode":"rule"}""");
        Assert.DoesNotContain("my-controller-secret", result);
        Assert.Contains("\"mode\":\"rule\"", result);
    }

    [Fact]
    public void Mask_普通文本不受影响()
    {
        var text = "mixed-port: 7897\nallow-lan: false\nmode: rule";
        Assert.Equal(text, SensitiveMasker.Mask(text));
    }

    [Fact]
    public void Mask_短值完全遮蔽()
    {
        Assert.Equal("***", SensitiveMasker.MaskValue("ab"));
        Assert.Equal("***", SensitiveMasker.MaskValue(""));
    }
}
