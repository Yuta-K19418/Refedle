using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Refedle.Engine;

/// <summary>
/// Represents the result of an operation that can fail with an error.
/// This type enables zero-allocation error handling for hot paths.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly ErrorMessage _error;

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public string Error
    {
        get
        {
            return IsSuccess ? throw new InvalidOperationException("Cannot access Error on a successful result.") : _error.Value;
        }
    }

    internal Result(bool isSuccess, ErrorMessage error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            action();
        }

        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsFailure)
        {
            action(Error);
        }

        return this;
    }

    /// <summary>
    /// Determines whether the current result is equal to another result.
    /// </summary>
    /// <param name="other">The result to compare with this result.</param>
    /// <returns>true if the results are equal; otherwise, false.</returns>
    public bool Equals(Result other) => IsSuccess == other.IsSuccess && _error.Equals(other._error);

    /// <summary>
    /// Determines whether the specified object is equal to the current result.
    /// </summary>
    /// <param name="obj">The object to compare with this result.</param>
    /// <returns>true if the specified object is equal to the current result; otherwise, false.</returns>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Result other && Equals(other);

    /// <summary>
    /// Returns the hash code for this result.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(IsSuccess, _error);

    /// <summary>
    /// Returns a string representation of this result.
    /// </summary>
    /// <returns>"Success" if successful, or "Failure({error})" if failed.</returns>
    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error})";

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">The first result to compare.</param>
    /// <param name="right">The second result to compare.</param>
    /// <returns>true if the results are equal; otherwise, false.</returns>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    /// <param name="left">The first result to compare.</param>
    /// <param name="right">The second result to compare.</param>
    /// <returns>true if the results are not equal; otherwise, false.</returns>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail with an error.
/// This type enables zero-allocation error handling for hot paths.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly ErrorMessage _error;

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value
    {
        get
        {
            return IsFailure
                ? throw new InvalidOperationException("Cannot access Value on a failed result.")
                : _value ?? throw new UnreachableException();
        }
    }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public string Error
    {
        get
        {
            return IsSuccess ? throw new InvalidOperationException("Cannot access Error on a successful result.") : _error.Value;
        }
    }

    internal Result(bool isSuccess, T value)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = default;
    }

    internal Result(bool isSuccess, ErrorMessage error)
    {
        IsSuccess = isSuccess;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Transforms the value if the result is successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return IsSuccess
            ? new Result<TNew>(true, mapper(Value))
            : new Result<TNew>(false, _error);
    }

    /// <summary>
    /// Binds the result to another operation if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsSuccess
            ? binder(Value)
            : new Result<TNew>(false, _error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            action(Value);
        }

        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsFailure)
        {
            action(Error);
        }

        return this;
    }

    /// <summary>
    /// Determines whether the current result is equal to another result.
    /// </summary>
    /// <param name="other">The result to compare with this result.</param>
    /// <returns>true if the results are equal; otherwise, false.</returns>
    public bool Equals(Result<T> other)
    {
        return IsSuccess == other.IsSuccess
            && (IsSuccess
                ? EqualityComparer<T?>.Default.Equals(_value, other._value)
                : _error.Equals(other._error));
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current result.
    /// </summary>
    /// <param name="obj">The object to compare with this result.</param>
    /// <returns>true if the specified object is equal to the current result; otherwise, false.</returns>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Result<T> other && Equals(other);

    /// <summary>
    /// Returns the hash code for this result.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(IsSuccess, _value, _error);

    /// <summary>
    /// Returns a string representation of this result.
    /// </summary>
    /// <returns>"Success({value})" if successful, or "Failure({error})" if failed.</returns>
    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">The first result to compare.</param>
    /// <param name="right">The second result to compare.</param>
    /// <returns>true if the results are equal; otherwise, false.</returns>
    public static bool operator ==(Result<T> left, Result<T> right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    /// <param name="left">The first result to compare.</param>
    /// <param name="right">The second result to compare.</param>
    /// <returns>true if the results are not equal; otherwise, false.</returns>
    public static bool operator !=(Result<T> left, Result<T> right)
        => !left.Equals(right);
}
