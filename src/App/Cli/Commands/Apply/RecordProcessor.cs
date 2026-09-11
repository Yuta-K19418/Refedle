using Refedle.App.Cli.IO;
using Refedle.Engine;

namespace Refedle.App.Cli.Commands.Apply;

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

                writer.WriteCellData(i, CellTransformFormatter.Format(transform, reader.GetCellData(i)));
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.WriteFooterAsync(ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
        return ExitCode.Success;
    }
}
