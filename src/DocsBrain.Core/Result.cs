namespace DocsBrain.Core;

public sealed record Error(string Code, string Message);

public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value) { _value = value; _error = null; }
    private Result(Error error) { _value = default; _error = error; }

    public bool IsSuccess => _error is null;
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Result has no value.");
    public Error Error => _error ?? throw new InvalidOperationException("Result has no error.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
