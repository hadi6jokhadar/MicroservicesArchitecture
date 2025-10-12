namespace IhsanDev.Shared.Application.Common.Models;

public class Result<T>
{
    private Result(bool succeeded, T? data, string? error = null)
    {
        Succeeded = succeeded;
        Data = data;
        Error = error;
    }

    public bool Succeeded { get; }
    public T? Data { get; }
    public string? Error { get; }

    public static Result<T> Success(T data) => new(true, data);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// For collections
public class Result
{
    private Result(bool succeeded, string? error = null)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public string? Error { get; }

    public static Result Success() => new(true);
    public static Result Failure(string error) => new(false, error);
}