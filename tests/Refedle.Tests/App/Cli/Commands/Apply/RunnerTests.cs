using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed partial class RunnerTests : IDisposable
{
    private const string TestCsvContent = """
        name,age
        Alice,30
        Bob,25
        Charlie,35
        """;

    private const string TestJsonLinesContent = """
        {"name":"Alice","age":30}
        {"name":"Bob","age":25}
        {"name":"Charlie","age":35}
        """;

    private const string TestRecipeYaml = """
        name: Test Recipe
        description: Test description
        actions:
          - type: Rename
            oldName: age
            newName: new_age
        """;

    private readonly string _testDir;

    public RunnerTests()
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
        Func<Task<ExitCode>> act = async () => await Runner.RunAsync(args!, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithNoActions_WritesAllRowsUnchanged()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name,age");
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.Should().Contain("Charlie");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithRenameAction_WritesRenamedHeader()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", TestRecipeYaml);
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,new_age");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithDeleteAction_OmitsColumn()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Delete age\nactions:\n  - type: Delete\n    columnName: age");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name");
        output.Should().NotContain("age");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithFilterAction_ExcludesNonMatchingRows()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Filter age\nactions:\n  - type: Filter\n    columnName: age\n    operator: GreaterThan\n    comparisonType: Number\n    value: 30");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name");
        output.Should().Contain("Charlie");
        output.Should().NotContain("Alice");
        output.Should().NotContain("Bob");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithNoActions_WritesAllRowsUnchanged()
    {
        // Arrange
        string inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.Should().Contain("Charlie");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithRenameAction_WritesRenamedKey()
    {
        // Arrange
        string inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        string recipeFile = CreateTestFile("recipe.yaml", TestRecipeYaml);
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"new_age\"");
        output.Should().NotContain("\"age\"");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithDeleteAction_OmitsKey()
    {
        // Arrange
        string inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Delete age\nactions:\n  - type: Delete\n    columnName: age");
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"name\"");
        output.Should().NotContain("\"age\"");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithFilterAction_ExcludesNonMatchingRows()
    {
        // Arrange
        string inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Filter age\nactions:\n  - type: Filter\n    columnName: age\n    operator: GreaterThan\n    comparisonType: Number\n    value: 30");
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("Charlie");
        output.Should().NotContain("Alice");
        output.Should().NotContain("Bob");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_CrossFormat_WritesExpectedOutput()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"name\"");
        output.Should().Contain("\"age\"");
        output.Should().Contain("Alice");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToCsv_WithColumnFirstAppearingAfterRow200_IncludesColumnInOutput()
    {
        // Arrange — "extra" first appears at row 201, beyond the old 200-row initial-scan cap,
        // so the full-file column resolution is what surfaces it in the CSV header.
        IEnumerable<string> lines = Enumerable.Range(0, 200).Select(_ => """{"name":"x"}""").Append("""{"name":"x","extra":"late"}""");
        string inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,extra");
        output.Should().Contain("late");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToCsv_CrossFormat_WritesExpectedOutput()
    {
        // Arrange
        string inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name,age");
        output.Should().Contain("Alice");
    }

    [Fact]
    public async Task RunAsync_WithNonExistentInputPath_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = Path.Combine(_testDir, "nonexistent.csv");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting input format: File does not exist");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithZeroByteInputFile_ReturnsExitCode1()
    {
        // Arrange — a zero-byte input file is rejected by input format detection,
        // aligning CLI behavior with the TUI's FormatDetector.
        string inputFile = CreateTestFile("input.jsonl", "");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting input format: File is empty");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithNonExistentRecipePath_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = Path.Combine(_testDir, "nonexistent.yaml");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error loading recipe: File not found:");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnsupportedInputExtension_ReturnsExitCode1()
    {
        // Arrange — .xml is an extension no phase of this feature will support.
        string inputFile = CreateTestFile("input.xml", "[]");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting input format: Unsupported file format: .xml");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnsupportedOutputExtension_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.xml");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting output format: Unsupported file format: .xml");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnknownExtension_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.unknown", "content");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Error detecting input format: Unsupported file format: .unknown");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter(), ct: cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Operation cancelled");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithMalformedRecipeFile_ReturnsExitCode1()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", TestCsvContent);
        string recipeFile = CreateTestFile("recipe.yaml", "invalid: yaml: content");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error loading recipe: Unknown root-level key:");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithEmptyInputFile_WritesHeaderOnly()
    {
        // Arrange
        string inputFile = CreateTestFile("input.csv", "name,age\n");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,age");
        output.Should().NotContain("Alice");
    }

    private string CreateTestFile(string fileName, string content)
    {
        string filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
    }

    private string CreateTestFileWithUtf8Bom(string fileName, string content)
    {
        string filePath = Path.Combine(_testDir, fileName);
        File.WriteAllBytes(filePath, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(content)]);
        return filePath;
    }
}
