namespace Refedle.Engine;

/// <summary>
/// Provides factory methods for creating Result and Result{T} instances.
/// </summary>
public static class Results
{
    /// <summary>
    /// Creates a successful result without a value.
    /// </summary>
    /// <returns>A Result representing a successful operation.</returns>
    public static Result Success() => new(true, default);

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value returned by the successful operation.</param>
    /// <returns>A Result{T} representing a successful operation with a value.</returns>
    public static Result<T> Success<T>(T value) => new(true, value);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <param name="error">The error message describing why the operation failed.</param>
    /// <returns>A Result representing a failed operation.</returns>
    /// <exception cref="ArgumentException">Thrown when error is null, empty, or whitespace.</exception>
    public static Result Failure(string error) => new(false, new ErrorMessage(error));

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <typeparam name="T">The type of the value that would have been returned on success.</typeparam>
    /// <param name="error">The error message describing why the operation failed.</param>
    /// <returns>A Result{T} representing a failed operation.</returns>
    /// <exception cref="ArgumentException">Thrown when error is null, empty, or whitespace.</exception>
    public static Result<T> Failure<T>(string error) => new(false, new ErrorMessage(error));
}
