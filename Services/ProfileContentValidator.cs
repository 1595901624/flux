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
                ?? throw new InvalidOperationException(Flux.Services.L10n.T("Validator_InvalidYamlMapping"));
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(Flux.Services.L10n.T("Validator_InvalidYaml"), ex);
        }

        var hasProxies = mapping.Children.ContainsKey(new YamlScalarNode("proxies"));
        var hasProviders = mapping.Children.ContainsKey(new YamlScalarNode("proxy-providers"));
        if (!hasProxies && !hasProviders)
            throw new InvalidOperationException(Flux.Services.L10n.T("Validator_NotClash"));
    }
}
