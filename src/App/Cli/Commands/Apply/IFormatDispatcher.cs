using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Dispatches a prepared batch job to the monomorphized read→transform→write pipeline
/// for a specific input/output format pair.
/// </summary>
/// <remarks>
/// Abstracts the source-generated dispatch so that callers (and tests) can substitute
/// the dispatch behavior without depending on the generated static method.
/// </remarks>
internal interface IFormatDispatcher
{
    /// <summary>
    /// Runs the batch processing pipeline for the given format pair.
    /// </summary>
    /// <param name="inputFormat">The resolved format of the input file.</param>
    /// <param name="outputFormat">The resolved format of the output file.</param>
    /// <param name="inputFile">The path of the input file to read.</param>
    /// <param name="outputFile">The path of the output file to write.</param>
    /// <param name="drillDownKeyPath">The optional drill-down key path into nested records.</param>
    /// <param name="inputColumnNames">The resolved column names of the input file.</param>
    /// <param name="outputSchema">The format-agnostic output plan.</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The batch run result: exit code plus the collected cell issues.</returns>
    ValueTask<BatchRunResult> DispatchAsync(
        DataFormat inputFormat,
        DataFormat outputFormat,
        string inputFile,
        string outputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct);
}
