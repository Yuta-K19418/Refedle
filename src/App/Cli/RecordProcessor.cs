using System.Diagnostics;
using System.Globalization;
using Refedle.Engine;
using Refedle.Engine.Models;

namespace Refedle.App.Cli;

internal static class RecordProcessor
{
    public static async ValueTask<ExitCode> ProcessAsync<TReader, TWriter>(
        TReader reader,
        TWriter writer,
        IReadOnlyList<BatchOutputColumn> columns,
        CancellationToken ct)
        where TReader : struct, IRecordReader
        where TWriter : struct, IRecordWriter
    {
        await writer.WriteHeaderAsync(ct).ConfigureAwait(false);

        while (await reader.MoveNextAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            if (!reader.EvaluateFilters())
            {
                continue;
            }

            await writer.WriteStartRecordAsync(ct).ConfigureAwait(false);

            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Transform is not { } transform)
                {
                    writer.WriteCellData(i, reader.GetCellData(i));
                    continue;
                }

                var formatted = transform switch
                {
                    FillSpec fill => fill.Value,
                    TimestampFormatSpec fmt => ApplyTimestampFormat(reader.GetCellData(i).Value, fmt),
                    _ => throw new UnreachableException($"Unhandled CellTransformSpec: {transform.GetType().Name}"),
                };

                writer.WriteCellData(i, new CellData(formatted, CellPresence.Value, CellEncodingClassifier.Classify(formatted)));
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.WriteFooterAsync(ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
        return ExitCode.Success;
    }

    private static string ApplyTimestampFormat(ReadOnlySpan<char> raw, TimestampFormatSpec fmt)
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new FormatException($"Could not parse timestamp value '{raw}'.");
        }

        return parsed.ToString(fmt.TargetFormat, CultureInfo.InvariantCulture);
    }
}
