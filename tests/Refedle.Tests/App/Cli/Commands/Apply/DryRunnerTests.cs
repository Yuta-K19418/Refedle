using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed class DryRunnerTests : IDisposable
{
    private const string TestCsvContent = """
        name,age,city
        Alice,30,Osaka
        Bob,25,Tokyo
        """;

    private readonly string _testDir;

    public DryRunnerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithNullArgs_ThrowsArgumentNullException()
    {
        // Arrange
        Arguments? args = null;
        TestAppLogger logger = new();

        // Act
        Func<Task<ExitCode>> act = async () => await DryRunner.RunAsync(args!, logger, new TestStatusReporter());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter(), cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Operation cancelled");
    }

    [Fact]
    public async Task RunAsync_WithOutput_PrintsSummaryWithoutWritingOutput()
    {
        // Arrange — rename + fill + filter resolve to renamed output columns, a transform and a filter line
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: Rich\nactions:\n  - type: Rename\n    oldName: age\n    newName: years\n  - type: Fill\n    columnName: name\n    value: REDACTED\n  - type: Filter\n    columnName: years\n    operator: GreaterThanOrEqual\n    comparisonType: Number\n    value: 30");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        logger.Infos.Should().Equal(
            "Dry run OK",
            "  Input format: Csv",
            "  Output format: Csv",
            "  Drill-down key path: (none)",
            "  Resolved input columns (3): name, age, city",
            "  Output schema:",
            "    name -> name  [fill: REDACTED]",
            "    age -> years",
            "    city -> city",
            "  Filters:",
            "    age >= 30");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithoutOutput_PrintsAllApplicableSummaryLines()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", "id,name\n1,Alice\n");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert — the no-output path omits the Output format line and the empty Filters section
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        logger.Infos.Should().Equal(
            "Dry run OK",
            "  Input format: Csv",
            "  Drill-down key path: (none)",
            "  Resolved input columns (2): id, name",
            "  Output schema:",
            "    id -> id",
            "    name -> name");
        logger.Infos.Should().NotContain(line => line.StartsWith("  Output format:", StringComparison.Ordinal));
        logger.Infos.Should().NotContain(line => line.StartsWith("  Filters:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithDrillDownScopedRecipe_PrintsKeyPath()
    {
        // Arrange — a JSON Lines full-aggregation scope resolves the drilled-down columns
        string inputFile = CreateTestFile("input.jsonl", "{\"orders\":[{\"id\":1}]}\n");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DD\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Infos.Should().Contain("  Drill-down key path: orders");
        logger.Infos.Should().Contain("  Resolved input columns (1): id");
    }

    [Fact]
    public async Task RunAsync_WithTimestampFormatAction_PrintsTransformSuffix()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", "created_at\n2020-01-01\n");
        string recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: Format\nactions:\n  - type: FormatTimestamp\n    columnName: created_at\n    targetFormat: yyyy-MM-dd");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Infos.Should().Contain("    created_at -> created_at  [timestamp format: yyyy-MM-dd]");
    }

    [Fact]
    public async Task RunAsync_WithDeleteAction_OmitsDeletedColumnFromOutputSchema()
    {
        // Arrange — city is deleted, so only name and age appear in the output schema
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Delete city\nactions:\n  - type: Delete\n    columnName: city");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert — no output-schema line may represent the deleted source column
        exitCode.Should().Be(ExitCode.Success);
        logger.Infos.Should().Contain("    name -> name");
        logger.Infos.Should().Contain("    age -> age");
        logger.Infos.Should().NotContain(line => line.StartsWith("    city -> ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithNonExistentRecipePath_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = Path.Combine(_testDir, "nonexistent.yaml");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert — the same wording the normal Runner path reports
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be($"Error loading recipe: File not found: {recipeFile}");
    }

    [Fact]
    public async Task RunAsync_WithNonExistentInputPath_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = Path.Combine(_testDir, "nonexistent.csv");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting input format: File does not exist");
    }

    [Fact]
    public async Task RunAsync_WithJsonArrayInputAndBaseTableRecipe_ReturnsExitCode1()
    {
        // Arrange — JSON Array input requires a DrillDown-scoped recipe
        string inputFile = CreateTestFile("input.json", """[{"orders":[{"id":1}]}]""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Base\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert — the same wording the normal Runner path reports
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be(
            "Error validating recipe: Recipe 'Base' has no DrillDown scope, but JsonArray input requires one");
    }

    [Fact]
    public async Task RunAsync_WithUnsupportedOutputExtension_ReturnsExitCode1()
    {
        // Arrange — --output is honored when given: its format is detected and must be supported
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new()
        {
            InputFile = inputFile,
            RecipeFile = recipeFile,
            OutputFile = Path.Combine(_testDir, "output.xml"),
            IsDryRun = true,
        };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting output format: Unsupported file format: .xml");
    }

    [Fact]
    public async Task RunAsync_WithUnresolvableDrillDownKeyPath_ReturnsExitCode1()
    {
        // Arrange — the recipe's DrillDown key does not exist in the JSON Object input
        string inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DD\nactions: []\ndrillDownKeyPath:\n  - key: missing");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("""Error resolving columns: DrillDown path key "missing" was not found.""");
    }

    [Fact]
    public async Task RunAsync_OnSuccess_ReportsValidatingPhaseAndStillPrintsSummary()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();
        TestStatusReporter status = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, status);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        status.Phases.Should().Equal("Validating...");
        logger.Infos.Should().StartWith("Dry run OK");
    }

    [Fact]
    public async Task RunAsync_WhenPreparationFails_ReportsValidatingPhaseAndStillLogsTheError()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        Arguments args = new()
        {
            InputFile = inputFile,
            RecipeFile = Path.Combine(_testDir, "missing.yaml"),
            IsDryRun = true,
        };
        TestAppLogger logger = new();
        TestStatusReporter status = new();

        // Act
        ExitCode exitCode = await DryRunner.RunAsync(args, logger, status);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        status.Phases.Should().Equal("Validating...");
        logger.Errors.Should().ContainSingle().Which.Should().Contain("missing.yaml");
    }

    [Fact]
    public async Task RunAsync_WithNullStatusReporter_ThrowsArgumentNullException()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, IsDryRun = true };
        TestAppLogger logger = new();

        // Act
        Func<Task<ExitCode>> act = async () => await DryRunner.RunAsync(args, logger, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private string CreateTestFile(string fileName, string content)
    {
        string filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
    }
}
