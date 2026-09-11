namespace Flux.Core.Contracts;

/// <summary>操作失败的类型化原因，包含步骤名与关联文件，用于 UI 呈现与诊断。</summary>
public sealed record OperationError(
    string Code,
    string Message,
    string? Step = null,
    string? File = null)
{
    public static OperationError Of(string code, string message, string? step = null, string? file = null)
        => new(code, message, step, file);

    public override string ToString()
    {
        var location = Step is null ? "" : $"[{Step}]";
        var filePart = File is null ? "" : $" ({File})";
        return $"{location}{Message}{filePart}";
    }
}

/// <summary>统一操作结果：成功携带值，失败携带类型化错误。替代静默吞异常。</summary>
public sealed record OperationResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public OperationError? Error { get; init; }

    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static OperationResult<T> Fail(OperationError error) => new() { Success = false, Error = error };
    public static OperationResult<T> Fail(string code, string message, string? step = null, string? file = null)
        => new() { Success = false, Error = OperationError.Of(code, message, step, file) };

    public T GetValueOrThrow()
    {
        if (!Success)
            throw new FluxOperationException(Error ?? new OperationError("failed", "操作失败"));
        return Value!;
    }
}

/// <summary>操作失败时抛出的异常，携带类型化错误信息。</summary>
public sealed class FluxOperationException : Exception
{
    public OperationError Error { get; }

    public FluxOperationException(OperationError error)
        : base(error.ToString())
    {
        Error = error;
    }
}
