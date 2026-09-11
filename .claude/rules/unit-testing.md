---
paths:
  - "tests/Refedle.Tests/**/*.cs"
---

# Unit Testing

Rules specific to the unit test project (`tests/Refedle.Tests`). The shared
conventions in [testing.md](testing.md) also apply.

## Directory Placement
- Test classes must **mirror the directory hierarchy** of the production code they test
- Example: a test for `src/Engine/IO/Csv/DataRowIndexer.cs` belongs in `tests/Refedle.Tests/Engine/IO/Csv/`
- A test file placed at the wrong level makes it hard to locate and signals that the test may be covering the wrong abstraction

## Coverage
- Focus on **100% coverage for the "Hot Paths"** (data processing logic)
- Prioritize core engine logic over UI state
- Unit tests must comprehensively cover not only the happy path but also error cases (error handling, invalid input, edge cases, etc.)
