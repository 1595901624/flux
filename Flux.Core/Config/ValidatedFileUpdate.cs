namespace Flux.Core.Config;

/// <summary>先校验候选文件，再替换目标；应用失败时恢复原文件及运行状态。</summary>
public static class ValidatedFileUpdate
{
    public static async Task ApplyAsync(string target, Action<string> writeCandidate,
        Func<string, Task> validate, Func<Task>? apply = null, Func<Task>? rollback = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(target))!;
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, $".candidate-{Guid.NewGuid():N}.yaml");
        var original = File.Exists(target) ? File.ReadAllBytes(target) : null;
        var replaced = false;
        try
        {
            writeCandidate(candidate);
            await validate(candidate);
            File.Move(candidate, target, overwrite: true);
            replaced = true;
            if (apply is not null) await apply();
        }
        catch (Exception error) when (replaced)
        {
            try
            {
                if (original is null) File.Delete(target);
                else
                {
                    File.WriteAllBytes(candidate, original);
                    File.Move(candidate, target, overwrite: true);
                }
                if (rollback is not null) await rollback();
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("配置应用失败，且原配置恢复失败", error, rollbackError);
            }
            throw;
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
