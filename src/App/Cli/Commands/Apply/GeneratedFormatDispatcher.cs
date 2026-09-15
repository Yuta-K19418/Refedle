using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Production <see cref="IFormatDispatcher"/> that delegates to the source-generated
/// <see cref="Generated.FormatDispatcher"/> monomorphization dispatch.
/// </summary>
internal sealed class GeneratedFormatDispatcher : IFormatDispatcher
{
    /// <inheritdoc/>
    public ValueTask<ExitCode> DispatchAsync(
        DataFormat inputFormat,
        DataFormat outputFormat,
        string inputFile,
        string outputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct) => Generated.FormatDispatcher.DispatchAsync(
            inputFormat,
            outputFormat,
            inputFile,
            outputFile,
            drillDownKeyPath,
            inputColumnNames,
            outputSchema,
            logger,
            ct);
}
