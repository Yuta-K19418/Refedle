using Refedle.App.Cli.IO;
using Refedle.Engine;

namespace Refedle.App.Cli.Commands.Apply;

internal static class RecordProcessor
{
    public static async ValueTask<BatchRunResult> ProcessAsync<TReader, TWriter>(
        TReader reader,
        TWriter writer,
        IReadOnlyList<BatchOutputColumn> columns,
        CancellationToken ct)
        where TReader : struct, IRecordReader
        where TWriter : struct, IRecordWriter
    {
        await writer.WriteHeaderAsync(ct).ConfigureAwait(false);

        var issues = new CellIssueCollector();
        var rowNumber = 0;

        while (await reader.MoveNextAsync(ct).ConfigureAwait(false))
        {
            rowNumber++;
            ct.ThrowIfCancellationRequested();

            if (!reader.EvaluateFilters())
            {
                continue;
            }

            await writer.WriteStartRecordAsync(ct).ConfigureAwait(false);

            for (var i = 0; i < columns.Count; i++)
            {
                var cell = reader.GetCellData(i);
                var resolvedCell = CellResolver.ResolveCell(columns[i].Transform, cell);
                if (resolvedCell.HasIssue)
                {
                    issues.Add(rowNumber, columns[i].SourceName, resolvedCell.Cell.Value, resolvedCell.Reason);
                }

                writer.WriteCellData(i, resolvedCell.Cell);
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.WriteFooterAsync(ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
        return new BatchRunResult(ExitCode.Success, issues.Issues, issues.HasMore);
    }
}
