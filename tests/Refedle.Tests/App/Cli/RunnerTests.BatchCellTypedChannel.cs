using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

// End-to-end coverage for the typed CellData channel. These exercise the real readers/writers
// via Runner.RunAsync.
public sealed partial class RunnerTests
{
    private const string EmptyRecipeYaml = "name: Empty\nactions: []";

    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — Object/Array raw JSON preservation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithObjectAndArrayContainingNonAscii_PreservesRawJson()
    {
        // Arrange
        const string line = """{"id":1,"data":{"city":"café","nested":{"count":2}},"tags":["a","café","b"]}""";
        var inputFile = CreateTestFile("input.jsonl", line);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var inputDoc = JsonDocument.Parse(line);
        using var outputDoc = JsonDocument.Parse(outputLine);
        outputDoc.RootElement.GetProperty("data").GetRawText().Should().Be(inputDoc.RootElement.GetProperty("data").GetRawText());
        outputDoc.RootElement.GetProperty("tags").GetRawText().Should().Be(inputDoc.RootElement.GetProperty("tags").GetRawText());
    }

    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — Number lexical form preservation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.50")]
    [InlineData("1e10")]
    [InlineData("9223372036854775808")] // Int64.MaxValue + 1, beyond Int64 range
    public async Task RunAsync_JsonLinesToJsonLines_WithNumberToken_PreservesLexicalForm(string numberLiteral)
    {
        // Arrange
        var line = $$"""{"value":{{numberLiteral}}}""";
        var inputFile = CreateTestFile("input.jsonl", line);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        outputDoc.RootElement.GetProperty("value").GetRawText().Should().Be(numberLiteral);
    }

    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — String value handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithStringEscapes_ResolvesToPlain()
    {
        // Arrange
        const string line = """{"text":"line1\nline2 \"quoted\""}""";
        var inputFile = CreateTestFile("input.jsonl", line);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var inputDoc = JsonDocument.Parse(line);
        using var outputDoc = JsonDocument.Parse(outputLine);
        outputDoc.RootElement.GetProperty("text").GetString().Should().Be(inputDoc.RootElement.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("5")]
    [InlineData("true")]
    public async Task RunAsync_JsonLinesToJsonLines_WithStringLookingNumericOrBoolean_StaysJsonString(string stringLiteral)
    {
        // Arrange
        var line = $$"""{"value":"{{stringLiteral}}"}""";
        var inputFile = CreateTestFile("input.jsonl", line);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        var value = outputDoc.RootElement.GetProperty("value");
        value.ValueKind.Should().Be(JsonValueKind.String);
        value.GetString().Should().Be(stringLiteral);
    }

    [Theory]
    [InlineData("<null>")]
    [InlineData("<error>")]
    public async Task RunAsync_JsonLinesToJsonLines_WithSentinelLookingString_StaysLiteral(string stringLiteral)
    {
        // Arrange
        var line = $$"""{"value":"{{stringLiteral}}"}""";
        var inputFile = CreateTestFile("input.jsonl", line);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        var value = outputDoc.RootElement.GetProperty("value");
        value.ValueKind.Should().Be(JsonValueKind.String);
        value.GetString().Should().Be(stringLiteral);
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_MissingVersusExplicitNull_Distinguishes()
    {
        // Arrange
        const string content = """
            {"a":1,"b":2}
            {"a":3}
            {"a":4,"b":null}
            """;
        var inputFile = CreateTestFile("input.jsonl", content);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLines = await File.ReadAllLinesAsync(outputFile);
        outputLines.Should().HaveCount(3);

        using var missingDoc = JsonDocument.Parse(outputLines[1]);
        missingDoc.RootElement.TryGetProperty("b", out _).Should().BeFalse();

        using var nullDoc = JsonDocument.Parse(outputLines[2]);
        nullDoc.RootElement.GetProperty("b").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // -------------------------------------------------------------------------
    // CSV → JSON Lines — numeric normalization (regression guards)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithLeadingZeroNumber_NormalizesToJsonNumber()
    {
        // Arrange
        const string content = "id,code\n1,007\n";
        var inputFile = CreateTestFile("input.csv", content);
        var recipeFile = CreateTestFile("recipe.yaml", EmptyRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        var code = outputDoc.RootElement.GetProperty("code");
        code.ValueKind.Should().Be(JsonValueKind.Number);
        code.GetRawText().Should().Be("7");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithNumericLookingFill_EmitsJsonNumber()
    {
        // Arrange
        const string content = "name,status\nAlice,active\n";
        const string recipeYaml = "name: Fill status\nactions:\n  - type: Fill\n    columnName: status\n    value: 0";
        var inputFile = CreateTestFile("input.csv", content);
        var recipeFile = CreateTestFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        var status = outputDoc.RootElement.GetProperty("status");
        status.ValueKind.Should().Be(JsonValueKind.Number);
        status.GetRawText().Should().Be("0");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithNumericLookingTimestampFormat_EmitsJsonNumber()
    {
        // Arrange
        const string content = "date\n2024-03-15\n";
        const string recipeYaml = "name: Format date\nactions:\n  - type: FormatTimestamp\n    columnName: date\n    targetFormat: yyyyMMdd";
        var inputFile = CreateTestFile("input.csv", content);
        var recipeFile = CreateTestFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var outputLine = (await File.ReadAllLinesAsync(outputFile)).Single();
        using var outputDoc = JsonDocument.Parse(outputLine);
        var date = outputDoc.RootElement.GetProperty("date");
        date.ValueKind.Should().Be(JsonValueKind.Number);
        date.GetRawText().Should().Be("20240315");
    }
}
