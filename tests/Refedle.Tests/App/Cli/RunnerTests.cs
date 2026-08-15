using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

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
          - type: rename
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
        var logger = new TestAppLogger();

        // Act
        var act = async () => await Runner.RunAsync(args!, logger);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithNoActions_WritesAllRowsUnchanged()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name,age");
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.Should().Contain("Charlie");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithRenameAction_WritesRenamedHeader()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", TestRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,new_age");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithDeleteAction_OmitsColumn()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Delete age\nactions:\n  - type: delete\n    columnName: age");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name");
        output.Should().NotContain("age");
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithFilterAction_ExcludesNonMatchingRows()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Filter age\nactions:\n  - type: filter\n    columnName: age\n    operator: greaterThan\n    comparisonType: Number\n    value: 30");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name");
        output.Should().Contain("Charlie");
        output.Should().NotContain("Alice");
        output.Should().NotContain("Bob");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithNoActions_WritesAllRowsUnchanged()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.Should().Contain("Charlie");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithRenameAction_WritesRenamedKey()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        var recipeFile = CreateTestFile("recipe.yaml", TestRecipeYaml);
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"new_age\"");
        output.Should().NotContain("\"age\"");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithDeleteAction_OmitsKey()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Delete age\nactions:\n  - type: delete\n    columnName: age");
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"name\"");
        output.Should().NotContain("\"age\"");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithFilterAction_ExcludesNonMatchingRows()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Filter age\nactions:\n  - type: filter\n    columnName: age\n    operator: greaterThan\n    comparisonType: Number\n    value: 30");
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("Charlie");
        output.Should().NotContain("Alice");
        output.Should().NotContain("Bob");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_CrossFormat_WritesExpectedOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("\"name\"");
        output.Should().Contain("\"age\"");
        output.Should().Contain("Alice");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToCsv_WithColumnFirstAppearingAfterRow200_IncludesColumnInOutput()
    {
        // Arrange — "extra" first appears at row 201, beyond the old 200-row initial-scan cap,
        // so the full-file column resolution is what surfaces it in the CSV header.
        var lines = Enumerable.Range(0, 200).Select(_ => """{"name":"x"}""").Append("""{"name":"x","extra":"late"}""");
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,extra");
        output.Should().Contain("late");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToCsv_CrossFormat_WritesExpectedOutput()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", TestJsonLinesContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Contain("name,age");
        output.Should().Contain("Alice");
    }

    [Fact]
    public async Task RunAsync_WithNonExistentInputPath_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = Path.Combine(_testDir, "nonexistent.csv");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error: Could not find file");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithNonExistentRecipePath_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = Path.Combine(_testDir, "nonexistent.yaml");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error loading recipe: File not found:");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnsupportedInputExtension_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", "[]");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Unsupported format: .JSON (Standard JSON format is not supported for batch processing. Use .jsonl for JSON Lines.)");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnsupportedOutputExtension_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.json");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Unsupported format: .JSON (Standard JSON format is not supported for batch processing. Use .jsonl for JSON Lines.)");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithUnknownExtension_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.unknown", "content");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Unsupported file extension: .UNKNOWN");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var exitCode = await Runner.RunAsync(args, logger, cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Operation cancelled");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithMalformedRecipeFile_ReturnsExitCode1()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", TestCsvContent);
        var recipeFile = CreateTestFile("recipe.yaml", "invalid: yaml: content");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error loading recipe: Unknown root-level key:");
        File.Exists(outputFile).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_CsvToCsv_WithEmptyInputFile_WritesHeaderOnly()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", "name,age\n");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().StartWith("name,age");
        output.Should().NotContain("Alice");
    }

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
    }
}
