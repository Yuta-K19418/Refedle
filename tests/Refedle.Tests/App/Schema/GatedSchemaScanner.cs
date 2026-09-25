using Refedle.App.Schema;
using Refedle.Engine.Models;

namespace Refedle.Tests.App.Schema;

/// <summary>
/// A scanner whose initial scan stays pending until <see cref="Release"/> is called, then
/// either returns the configured schema or throws the configured failure. The background
/// scan returns the current schema, throws the configured background failure, or completes
/// as canceled when <c>backgroundCanceled</c> is set.
/// </summary>
internal sealed class GatedSchemaScanner(
    TableSchema schema,
    Exception? failure = null,
    Exception? backgroundFailure = null,
    bool backgroundCanceled = false) : ISchemaScanner
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a task that completes once the initial scan has begun.
    /// </summary>
    public Task Started => _started.Task;

    /// <summary>
    /// Lets the pending initial scan finish.
    /// </summary>
    public void Release() => _gate.TrySetResult();

    public async Task<TableSchema> InitialScanAsync()
    {
        _started.TrySetResult();
        await _gate.Task.ConfigureAwait(false);
        return failure is null ? schema : throw failure;
    }

    public Task<TableSchema> StartBackgroundScanAsync(TableSchema currentSchema, CancellationToken cancellationToken)
    {
        if (backgroundCanceled)
        {
            return Task.FromCanceled<TableSchema>(new CancellationToken(canceled: true));
        }

        return backgroundFailure is null
            ? Task.FromResult(currentSchema)
            : Task.FromException<TableSchema>(backgroundFailure);
    }
}
