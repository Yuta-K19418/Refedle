using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.IntegrationTests.App.Tui.UI;

/// <summary>Table schema builders shared by the integration tests.</summary>
internal static class TestSchemas
{
    internal static TableSchema CreateCsvTextColumnSchema() => new()
    {
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        SourceFormat = DataFormat.Csv
    };
}
