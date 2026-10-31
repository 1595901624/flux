using Flux.Core.Utils;
using Flux.Models;
using Xunit;

namespace Flux.Tests;

public class WebDavSettingsTests
{
    [Fact]
    public void EditingServerWithoutPasswordPreservesEncryptedCredential()
    {
        var config = new VergeConfig();
        config.UpdateWebDavPassword("saved-password");
        var encrypted = config.WebDavPasswordEncrypted;
        config.WebDavUrl = "https://dav.example.com/new/";
        config.WebDavDir = "new-backups";
        config.UpdateWebDavPassword("");
        var reloaded = VergeConfig.Deserialize(config.Serialize());
        Assert.Equal(encrypted, reloaded.WebDavPasswordEncrypted);
        Assert.Equal("saved-password", DataProtector.Unprotect(reloaded.WebDavPasswordEncrypted));
        Assert.DoesNotContain("saved-password", config.Serialize());
    }

    [Fact]
    public void NewPasswordReplacesCredential()
    {
        var config = new VergeConfig();
        config.UpdateWebDavPassword("old-password");
        config.UpdateWebDavPassword("new-password");
        Assert.Equal("new-password", DataProtector.Unprotect(config.WebDavPasswordEncrypted));
    }
}
