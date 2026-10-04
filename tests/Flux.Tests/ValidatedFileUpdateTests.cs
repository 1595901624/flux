using Flux.Core.Config;
using Xunit;

namespace Flux.Tests;

public sealed class ValidatedFileUpdateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flux-config-tests", Guid.NewGuid().ToString("N"));
    private string Target => Path.Combine(_directory, "runtime.yaml");

    public ValidatedFileUpdateTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Target, "last-good");
    }

    [Fact]
    public async Task ValidationFailureNeverReplacesLastGoodFileOrApplies()
    {
        var applied = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidatedFileUpdate.ApplyAsync(Target,
            path => File.WriteAllText(path, "invalid"),
            path =>
            {
                Assert.Equal("invalid", File.ReadAllText(path));
                Assert.Equal("last-good", File.ReadAllText(Target));
                throw new InvalidOperationException("kernel rejected candidate");
            },
            () => { applied = true; return Task.CompletedTask; }));
        Assert.False(applied);
        Assert.Equal("last-good", File.ReadAllText(Target));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ApplyFailureRestoresFileBeforeRecoveringRuntime()
    {
        var recovered = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidatedFileUpdate.ApplyAsync(Target,
            path => File.WriteAllText(path, "candidate"), _ => Task.CompletedTask,
            () =>
            {
                Assert.Equal("candidate", File.ReadAllText(Target));
                throw new InvalidOperationException("reload and restart failed");
            },
            () =>
            {
                Assert.Equal("last-good", File.ReadAllText(Target));
                recovered = true;
                return Task.CompletedTask;
            }));
        Assert.True(recovered);
        Assert.Equal("last-good", File.ReadAllText(Target));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task SuccessCommitsValidatedFile()
    {
        await ValidatedFileUpdate.ApplyAsync(Target, path => File.WriteAllText(path, "valid"),
            path => { Assert.Equal("valid", File.ReadAllText(path)); return Task.CompletedTask; });
        Assert.Equal("valid", File.ReadAllText(Target));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ApplyFailureRemovesNewFileWhenNoOriginalExists()
    {
        File.Delete(Target);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidatedFileUpdate.ApplyAsync(Target,
            path => File.WriteAllText(path, "candidate"), _ => Task.CompletedTask,
            () => throw new InvalidOperationException("failed")));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
