---
paths:
  - "tests/**/*.cs"
---

# Testing

## Framework
- Use **xUnit** as the primary testing framework

## Directory Placement
- Test classes must **mirror the directory hierarchy** of the production code they test
- Example: a test for `src/Engine/IO/Csv/DataRowIndexer.cs` belongs in `tests/Refedle.Tests/Engine/IO/Csv/`
- A test file placed at the wrong level makes it hard to locate and signals that the test may be covering the wrong abstraction

## Naming Conventions

### Test Class Names
- **Pattern**: `[ClassName]Tests` (sealed class)
- **Partial Classes**: For large test classes, split by method group using partial files
  - `CsvDataRowIndexerTests.cs` (base)
  - `CsvDataRowIndexerTests.BuildIndex.cs` (method-specific)

### Test Method Names
- **Pattern**: `[MethodName]_[Scenario/Condition]_[ExpectedBehavior]`
- Examples:
  - `Constructor_WithNullFilePath_ThrowsArgumentException`
  - `BuildIndex_WithSimpleCsv_IndexesAllRows`
  - `Detect_JsonArray_ReturnsJsonArray`

## AAA Pattern (Arrange-Act-Assert)
- **MANDATORY**: All tests must follow the AAA pattern with explicit comments
- Always include `// Arrange`, `// Act`, `// Assert` comments
- Keep each section focused and concise
- Example:
  ```csharp
  [Fact]
  public void BuildIndex_WithSimpleCsv_IndexesAllRows()
  {
      // Arrange
      var content = "header1,header2\nvalue1,value2\nvalue3,value4";
      File.WriteAllText(_testFilePath, content);
      var indexer = new CsvDataRowIndexer(_testFilePath);

      // Act
      indexer.BuildIndex();

      // Assert
      indexer.TotalRows.Should().Be(2);
  }
  ```

## Resource Cleanup
- When tests create external resources (files, streams, connections, etc.), ensure they are properly cleaned up after the test completes

## Avoid Logic in Tests
- **Do NOT use control flow statements** (`if`, `else`, `while`, `for`, `foreach`, `switch`) inside test methods
- Tests should be deterministic and straightforward with no branching logic
- If you need to test multiple conditions, use `[Theory]` with `[InlineData]` instead
- Reference: [Microsoft - Avoid logic in tests](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#avoid-logic-in-unit-tests)

## Parameterized Tests
- When testing multiple similar cases, use `[Theory]` with `[InlineData]` instead of writing separate test methods
- Example:
  ```csharp
  [Theory]
  [InlineData(ColumnType.WholeNumber, ColumnType.Text, ColumnType.Text)]
  [InlineData(ColumnType.FloatingPoint, ColumnType.Text, ColumnType.Text)]
  [InlineData(ColumnType.Boolean, ColumnType.WholeNumber, ColumnType.Text)]
  public void UpdateColumnType_VariousConversions_UpdatesCorrectly(
      ColumnType initialType,
      ColumnType newType,
      ColumnType expectedType)
  ```

## Null-Forgiving Operator (`!`) in Tests
- **ALLOWED ONLY** when explicitly testing validation logic that requires null inputs
- Primary use case: passing `null!` to verify `ArgumentNullException` or similar null-checking behavior
- Example:
  ```csharp
  var exception = Assert.Throws<ArgumentNullException>(() => Method(null!));
  ```
- Do NOT use `!` to work around test setup issues or lazy test data initialization
- If you need nullable values in tests for non-validation scenarios, use proper nullable types instead

## Assertions

### AwesomeAssertions
- Use **AwesomeAssertions** for general logic and high-level behavioral tests
- Provides more readable and expressive assertions
- Example: `result.Should().Be(expected);`

### Standard xUnit Asserts
- Use **Standard xUnit Asserts** (e.g., `Assert.Equal`) ONLY for tests intended to run in **Native AOT** environments
- Example: `Assert.Equal(expected, actual);`

## Coverage
- Focus on **100% coverage for the "Hot Paths"** (data processing logic)
- Prioritize core engine logic over UI state
