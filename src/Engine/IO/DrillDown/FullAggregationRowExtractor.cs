using System.Globalization;
using System.Text;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Extracts the DrillDown rows of an already-fetched record batch by applying
/// <see cref="KeyPathTraverser.ExtractRows"/> to each record. The schema accumulators the
/// traverser requires are scratch state, discarded with each call — only the rows are returned.
/// </summary>
public static class FullAggregationRowExtractor
{
    /// <summary>
    /// Extracts the rows reached by traversing <paramref name="keyPath"/> in every record of
    /// <paramref name="recordBatch"/>. Records where the path is absent contribute no rows;
    /// array leaves expand to one row per element. Position hashes are batch-local (1-based
    /// record index, appended with <c>:elementIndex</c> for array leaves).
    /// </summary>
    public static IReadOnlyList<FocusedTableRow> ExtractRows(
        IReadOnlyList<JsonRawBytes> recordBatch,
        IReadOnlyList<KeyPathSegment> keyPath)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(keyPath);

        var colName = KeyPathTraverser.LastKeySegment(keyPath);
        var colNameUtf8 = Encoding.UTF8.GetBytes(colName);

        List<FocusedTableRow> rows = [];
        List<string> keyOrder = [];
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        var columnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < recordBatch.Count; i++)
        {
            KeyPathTraverser.ExtractRows(
                recordBatch[i], keyPath, (i + 1).ToString(CultureInfo.InvariantCulture),
                colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
        }

        return rows;
    }
}
