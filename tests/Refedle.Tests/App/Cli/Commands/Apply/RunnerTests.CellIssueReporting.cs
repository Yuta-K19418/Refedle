using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;

namespace Refedle.Tests.App.Cli.Commands.Apply;

// End-of-run cell-issue warning coverage: issues collected by the pipeline are reported once,
// only after a successful run has published its output.
public sealed partial class RunnerTests
{
    [Fact]
    public async Task RunAsync_WhenDispatcherSucceedsWithCellIssues_WarningsFollowPublishedOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(
            cellIssues: [new CellIssue(3, "age", "twenty", "not recognized as a timestamp")]);

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert — the output is published and the issues are reported after it.
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        (await File.ReadAllTextAsync(outputFile)).Should().Be(TestFormatDispatcher.WrittenContent);
        logger.Warnings.Should().Equal(
            "Some source cells could not be processed as specified:",
            "Row 3, column \"age\": not recognized as a timestamp (\"twenty\")");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherSucceedsWithoutCellIssues_WritesNoWarnings()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherSucceedsWithMoreCellIssues_WritesOverflowWarning()
    {
        // Arrange — the overflow flag comes from the dispatch result, so a dropped or
        // hard-coded HasMoreCellIssues in RunAsync must surface here.
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var issues = Enumerable.Range(1, CellIssue.MaxReportedIssues)
            .Select(rowNumber => new CellIssue(rowNumber, "age", $"bad{rowNumber}", "not recognized as a timestamp"))
            .ToArray();
        var dispatcher = new TestFormatDispatcher(cellIssues: issues, hasMoreCellIssues: true);

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert — header, the capped entries in row order, then the closing indicator.
        exitCode.Should().Be(ExitCode.Success);
        logger.Warnings.Should().HaveCount(CellIssue.MaxReportedIssues + 2);
        logger.Warnings[0].Should().Be("Some source cells could not be processed as specified:");
        logger.Warnings[1].Should().Be("Row 1, column \"age\": not recognized as a timestamp (\"bad1\")");
        logger.Warnings[CellIssue.MaxReportedIssues].Should().Be(
            $"Row {CellIssue.MaxReportedIssues}, column \"age\": not recognized as a timestamp (\"bad{CellIssue.MaxReportedIssues}\")");
        logger.Warnings[^1].Should().Be(
            $"More than {CellIssue.MaxReportedIssues} source cells could not be processed; only the first {CellIssue.MaxReportedIssues} are listed.");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherSucceedsWithCellIssues_PublishesOutputBeforeWarning()
    {
        // Arrange — the logger probes the real output path from inside WriteWarningAsync,
        // so emitting warnings before the atomic publish would fail this test.
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new OutputCheckingLogger(outputFile);
        var dispatcher = new TestFormatDispatcher(
            cellIssues: [new CellIssue(3, "age", "twenty", "not recognized as a timestamp")]);

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Warnings.Should().HaveCount(2);
        logger.OutputExistedForEveryWarning.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherFailsWithCellIssues_WritesNoWarnings()
    {
        // Arrange — a failed run discards its output and reports its own error; cell-level
        // detail would only add noise on top of that.
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(
            result: ExitCode.Failure,
            cellIssues: [new CellIssue(1, "age", "twenty", "not recognized as a timestamp")]);

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Warnings.Should().BeEmpty();
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_CsvWithUnparseableTimestampCell_SucceedsAndWarnsForRowAndColumn()
    {
        // Arrange — end-to-end through the real pipeline: the bad cell passes through into
        // the output and is reported once, after the output is published.
        const string content = """
            date
            2024-03-15
            not-a-date
            """;
        const string recipeYaml = "name: Format date\nactions:\n  - type: FormatTimestamp\n    columnName: date\n    targetFormat: yyyy/MM/dd";
        var inputFile = CreateTestFile("input.csv", content);
        var recipeFile = CreateTestFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("2024/03/15");
        output.Should().Contain("not-a-date");
        logger.Warnings.Should().HaveCount(2);
        logger.Warnings[0].Should().Be("Some source cells could not be processed as specified:");
        logger.Warnings[1].Should().StartWith("Row 2, column \"date\":");
        logger.Warnings[1].Should().Contain("not-a-date");
    }

    [Fact]
    public async Task RunAsync_JsonLinesWithUnreadableCell_SucceedsAndWarnsWithoutAValue()
    {
        // Arrange — a malformed value token makes the reader hand back an Invalid cell,
        // which is reported with no raw value (there is nothing readable to show).
        const string content = """
            {"date":"2024-03-15"}
            {"date": @@@}
            """;
        const string recipeYaml = "name: Format date\nactions:\n  - type: FormatTimestamp\n    columnName: date\n    targetFormat: yyyy/MM/dd";
        var inputFile = CreateTestFile("input.jsonl", content);
        var recipeFile = CreateTestFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Warnings.Should().HaveCount(2);
        logger.Warnings[1].Should().StartWith("Row 2, column \"date\":");
        logger.Warnings[1].Should().NotContain("(");
        var outputLines = await File.ReadAllLinesAsync(outputFile);
        outputLines.Should().HaveCount(2);
        outputLines[1].Should().Be("""{"date":""}""");
    }

    [Fact]
    public async Task RunAsync_JsonLinesWithFillOnUnreadableCell_FillsValueAndWritesNoWarnings()
    {
        // Arrange — the FillSpec contract end to end: the fill value overwrites the
        // unreadable cell too, and Fill has no reportable case, so nothing is warned.
        const string content = """
            {"date":"2024-03-15"}
            {"date": @@@}
            """;
        const string recipeYaml = "name: Fill date\nactions:\n  - type: Fill\n    columnName: date\n    value: N/A";
        var inputFile = CreateTestFile("input.jsonl", content);
        var recipeFile = CreateTestFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
        var outputLines = await File.ReadAllLinesAsync(outputFile);
        outputLines.Should().HaveCount(2);
        outputLines.Should().AllSatisfy(line => line.Should().Be("""{"date":"N/A"}"""));
    }

    /// <summary>
    /// Logger double that probes the real output path from inside <see cref="IAppLogger.WriteWarningAsync"/>,
    /// so a test can prove warnings are emitted only after the atomic publish has completed.
    /// </summary>
    private sealed class OutputCheckingLogger(string outputFile) : IAppLogger
    {
        public List<string> Warnings { get; } = [];

        public bool OutputExistedForEveryWarning { get; private set; } = true;

        public ValueTask WriteInfoAsync(string message) => ValueTask.CompletedTask;

        public ValueTask WriteWarningAsync(string message)
        {
            Warnings.Add(message);
            OutputExistedForEveryWarning &= File.Exists(outputFile);
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteErrorAsync(string message) => ValueTask.CompletedTask;
    }
}
