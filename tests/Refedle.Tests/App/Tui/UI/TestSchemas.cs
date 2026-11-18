using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.UI;

/// <summary>Table schema builders shared by the unit tests; each variant is named for what it builds.</summary>
internal static class TestSchemas
{
    internal static TableSchema MakeJsonArraySchema(params (string name, ColumnType type)[] cols) =>
        new()
        {
            Columns =
            [
                .. cols.Select(
                    (c, i) =>
                        new ColumnSchema
                        {
                            Name = c.name,
                            Type = c.type,
                            ColumnIndex = i,
                        }
                ),
            ],
            SourceFormat = DataFormat.JsonArray,
        };

    internal static TableSchema MakeCsvSchema(params (string name, ColumnType type)[] cols) =>
        new()
        {
            Columns =
            [
                .. cols.Select(
                    (c, i) =>
                        new ColumnSchema
                        {
                            Name = c.name,
                            Type = c.type,
                            ColumnIndex = i,
                        }
                ),
            ],
            SourceFormat = DataFormat.Csv,
        };

    internal static TableSchema CreateJsonLinesIdSchema() => new()
    {
        Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
        SourceFormat = DataFormat.JsonLines
    };

    internal static Refedle.Engine.Models.TableSchema CreateCsvIdSchema() => new()
    {
        Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
        SourceFormat = Refedle.Engine.Types.DataFormat.Csv
    };
}
