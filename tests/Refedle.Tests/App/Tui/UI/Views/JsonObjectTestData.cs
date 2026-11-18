using System.Globalization;
using System.Text;
using Refedle.Engine.IO.JsonObject;

namespace Refedle.Tests.App.Tui.UI.Views;

/// <summary>JSON object test data shared by the JSON object tree tests.</summary>
internal static class JsonObjectTestData
{
    internal static JsonRawBytes ToBytes(string json) =>
        Encoding.UTF8.GetBytes(json);

    internal static List<JsonObjectEntry> CreateNumberedEntries(int count) =>
        [.. Enumerable
            .Range(0, count)
            .Select(i => new JsonObjectEntry(
                string.Create(CultureInfo.InvariantCulture, $"k{i}"),
                ToBytes(i.ToString(CultureInfo.InvariantCulture))))];
}
