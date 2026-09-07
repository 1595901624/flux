using YamlDotNet.RepresentationModel;

namespace Flux.Services;

internal static class ProfileContentValidator
{
    public static void Validate(string content)
    {
        YamlMappingNode mapping;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(content));
            mapping = stream.Documents.FirstOrDefault()?.RootNode as YamlMappingNode
                ?? throw new InvalidOperationException("内容不是有效的 YAML 映射");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("内容不是有效的 YAML", ex);
        }

        var hasProxies = mapping.Children.ContainsKey(new YamlScalarNode("proxies"));
        var hasProviders = mapping.Children.ContainsKey(new YamlScalarNode("proxy-providers"));
        if (!hasProxies && !hasProviders)
            throw new InvalidOperationException("配置中缺少 proxies / proxy-providers，不是有效的 Clash 订阅");
    }
}
