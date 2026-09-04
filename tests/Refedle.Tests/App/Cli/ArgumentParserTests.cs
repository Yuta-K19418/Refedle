using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class ArgumentParserTests
{
    [Fact]
    public void Parse_WithAllRequiredFlags_ReturnsSuccess()
    {
        // Arrange
        string[] args = ["--input", "input.csv", "--recipe", "recipe.yaml", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().Be("input.csv");
        result.Value.RecipeFile.Should().Be("recipe.yaml");
        result.Value.OutputFile.Should().Be("output.csv");
    }

    [Fact]
    public void Parse_WithMissingInputFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--recipe", "recipe.yaml", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing required flag: --input");
    }

    [Fact]
    public void Parse_WithMissingRecipeFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--input", "input.csv", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing required flag: --recipe");
    }

    [Fact]
    public void Parse_WithMissingOutputFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--input", "input.csv", "--recipe", "recipe.yaml"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing required flag: --output");
    }

    [Fact]
    public void Parse_WithUnknownFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--input", "input.csv", "--recipe", "recipe.yaml", "--output", "output.csv", "--unknown", "value"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unknown flag: --unknown");
    }

    [Fact]
    public void Parse_WithLegacyCliFlag_ReturnsUnknownFlagFailure()
    {
        // Arrange — "--cli" was removed; it must now be rejected like any unrecognized flag
        string[] args = ["--cli", "--input", "input.csv", "--recipe", "recipe.yaml", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unknown flag: --cli");
    }

    [Fact]
    public void Parse_WithFlagMissingValue_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--input", "--recipe", "recipe.yaml", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing value for --input");
    }

    [Fact]
    public void Parse_WithEmptyArgs_ReturnsFailure()
    {
        // Arrange
        string[] args = [];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No arguments provided");
    }

    [Fact]
    public void Parse_WithBareValueWithoutFlag_ReturnsFailure()
    {
        // Arrange — "input.csv" appears without a preceding --flag
        string[] args = ["input.csv", "--recipe", "recipe.yaml", "--output", "output.csv"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid flag: 'input.csv'");
    }

    [Fact]
    public void Parse_WithBareVersionToken_ReturnsInvalidFlagFailure()
    {
        // Arrange — "version" is a bare token, not a recognized flag
        string[] args = ["version"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Invalid flag: 'version'");
    }

    [Fact]
    public void Parse_WithInputFlagAtEnd_ReturnsFailure()
    {
        // Arrange — --input is the last argument with no following value
        string[] args = ["--recipe", "recipe.yaml", "--output", "output.csv", "--input"];

        // Act
        var result = ArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing value for --input");
    }
}
