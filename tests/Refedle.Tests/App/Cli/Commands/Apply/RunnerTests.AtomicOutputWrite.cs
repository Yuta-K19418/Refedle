using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli.Commands.Apply;

// Atomic-output-write coverage. A stub IFormatDispatcher makes "failed mid-write"
// deterministically observable at the Runner level: it writes partial content to the
// path it receives, then throws (or succeeds) on demand.
public sealed partial class RunnerTests
{
    [Fact]
    public async Task RunAsync_WhenDispatcherThrowsOperationCanceledMidWrite_DeletesTempFileAndNeverTouchesRealOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(new OperationCanceledException());

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Operation cancelled");
        dispatcher.ReceivedOutputFile.Should().NotBe(outputFile);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        File.Exists(outputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherThrowsNotSupportedMidWrite_DeletesTempFileAndNeverTouchesRealOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(new NotSupportedException("format pair not supported"));

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("format pair not supported");
        dispatcher.ReceivedOutputFile.Should().NotBe(outputFile);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        File.Exists(outputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherThrowsGenericExceptionMidWrite_DeletesTempFileAndNeverTouchesRealOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(new InvalidOperationException("unexpected failure"));

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error: unexpected failure");
        dispatcher.ReceivedOutputFile.Should().NotBe(outputFile);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        File.Exists(outputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherSucceeds_PublishesTempFileContentAtRealOutputPath()
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
        logger.Errors.Should().BeEmpty();
        dispatcher.ReceivedOutputFile.Should().NotBe(outputFile);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be(TestFormatDispatcher.WrittenContent);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherReturnsFailureAfterPartialWrite_DeletesTempFileAndKeepsExistingRealOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        File.WriteAllText(outputFile, "previous run output");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(result: ExitCode.Failure);

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("previous run output");
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml", "output.csv");
    }

    [Fact]
    public async Task RunAsync_WhenDispatcherThrowsMidWrite_KeepsExistingRealOutputContent()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        File.WriteAllText(outputFile, "previous run output");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher(new InvalidOperationException("unexpected failure"));

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error: unexpected failure");
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("previous run output");
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml", "output.csv");
    }

    [Fact]
    public async Task RunAsync_WhenPublishTargetIsADirectory_ReportsErrorAndCleansUpTempFile()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputDir = Path.Combine(_testDir, "output.csv");
        Directory.CreateDirectory(outputDir);
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputDir };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().NotBeEmpty();
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        Directory.Exists(outputDir).Should().BeTrue();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml");
    }

    [Fact]
    public async Task RunAsync_WithSymlinkedOutputFile_PublishesThroughLinkToTarget()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var targetFile = Path.Combine(_testDir, "target.csv");
        File.WriteAllText(targetFile, "old content");
        var linkFile = Path.Combine(_testDir, "link.csv");
        File.CreateSymbolicLink(linkFile, targetFile);
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = linkFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Should().BeEmpty();
        File.ResolveLinkTarget(linkFile, returnFinalTarget: true).Should().NotBeNull();
        var target = await File.ReadAllTextAsync(targetFile);
        target.Should().Be(TestFormatDispatcher.WrittenContent);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml", "target.csv", "link.csv");
    }

    [Fact]
    public async Task RunAsync_WithBrokenSymlinkedOutputFile_CreatesTargetThroughLink()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var targetFile = Path.Combine(_testDir, "missing-target.csv");
        var linkFile = Path.Combine(_testDir, "broken-link.csv");
        File.CreateSymbolicLink(linkFile, targetFile);
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = linkFile };
        var logger = new TestAppLogger();
        var dispatcher = new TestFormatDispatcher();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Should().BeEmpty();
        File.ResolveLinkTarget(linkFile, returnFinalTarget: true).Should().NotBeNull();
        var target = await File.ReadAllTextAsync(targetFile);
        target.Should().Be(TestFormatDispatcher.WrittenContent);
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml", "missing-target.csv", "broken-link.csv");
    }

    [Fact]
    public async Task RunAsync_WhenReplayingDeferredMessagesThrowsAfterSuccessfulBatch_DeletesTempFileAndNeverTouchesRealOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new WarningFailingLogger();
        var dispatcher = new TestFormatDispatcher(loggedWarning: "warning held while the spinner runs");

        // Act
        var exitCode = await Runner.RunAsync(args, logger, dispatcher, new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("warning sink unavailable");
        File.Exists(dispatcher.ReceivedOutputFile).Should().BeFalse();
        File.Exists(outputFile).Should().BeFalse();
        Directory.GetFiles(_testDir).Select(Path.GetFileName).Should().BeEquivalentTo("input.csv", "recipe.yaml");
    }

    /// <summary>
    /// Logger whose warning sink is broken, so replaying a held warning fails while errors still get recorded.
    /// </summary>
    private sealed class WarningFailingLogger : IAppLogger
    {
        private readonly List<string> _errors = [];

        public IReadOnlyList<string> Errors => _errors.AsReadOnly();

        public ValueTask WriteInfoAsync(string message) => ValueTask.CompletedTask;

        public ValueTask WriteWarningAsync(string message) =>
            throw new IOException("warning sink unavailable");

        public ValueTask WriteErrorAsync(string message)
        {
            _errors.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stub dispatcher that simulates the real batch pipeline: writes partial content to the
    /// output path it receives, then throws the configured exception (or reports the
    /// configured exit code and cell issues).
    /// </summary>
    private sealed class TestFormatDispatcher(
        Exception? exception = null,
        ExitCode result = ExitCode.Success,
        IReadOnlyList<CellIssue>? cellIssues = null,
        bool hasMoreCellIssues = false,
        string? loggedWarning = null) : IFormatDispatcher
    {
        public const string WrittenContent = "partial output written by the dispatcher";

        public string? ReceivedOutputFile { get; private set; }

        public async ValueTask<BatchRunResult> DispatchAsync(
            DataFormat inputFormat,
            DataFormat outputFormat,
            string inputFile,
            string outputFile,
            IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
            IReadOnlyList<string> inputColumnNames,
            BatchOutputSchema outputSchema,
            IAppLogger logger,
            CancellationToken ct)
        {
            ReceivedOutputFile = outputFile;
            await File.WriteAllTextAsync(outputFile, WrittenContent, ct);
            if (loggedWarning is not null)
            {
                await logger.WriteWarningAsync(loggedWarning);
            }

            if (exception is not null)
            {
                throw exception;
            }

            return new BatchRunResult(result, cellIssues ?? [], hasMoreCellIssues);
        }
    }
}
