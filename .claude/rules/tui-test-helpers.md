---
paths:
  - "tests/Refedle.Tests/App/Tui/**/*.cs"
  - "tests/Refedle.IntegrationTests/App/Tui/**/*.cs"
  - "tests/Refedle.TestHelpers/App/Tui/**/*.cs"
  - "tests/Refedle.TestHelpers/IO/**/*.cs"
---

# TUI Test Helpers

Shared helpers, test doubles, and temp files for the TUI tests.
For the layers themselves, see [tui-test-layers.md](tui-test-layers.md).

## Where a helper lives
Put a helper at the narrowest place that covers all of its users.

| Used by | Put it in | Form |
|---|---|---|
| Both test projects | `tests/Refedle.TestHelpers`, in the folder that mirrors the production code (`App/Tui/...`) | `internal` top-level type, one per file |
| Several classes of one project | The same folder and namespace as the tests that use it | `internal` top-level type, one per file. Static helpers go in a static class and are used with `using static` |
| One test class (and its partial files) | Inside that class | Nested `private` type or method |

- Never copy a helper into both projects. Move it to `Refedle.TestHelpers`.
- `Refedle.TestHelpers` is a library, not a test project: it contains no tests, is excluded from coverage with `[assembly: ExcludeFromCodeCoverage]`, and may reference `xunit.assert` and AwesomeAssertions. It sees App and Engine internals, and exposes its own internals to the two test projects, through `InternalsVisibleTo`.
- Reference helpers with a per-file `using`. Do not add them as global usings in a `.csproj`.

## Naming doubles and helpers
- The name says what the type does: `EmptyCountingDisposableTableSource`, not `FakeSource` or `MockRowIndexer`. A generic word alone (`Fake`, `Mock`, `Helper`, `TableSource`) is not a name.
- In one project, never give two types, or two shared helpers (used by more than one test class), the same name with different behavior. A private helper used inside one class only needs to make sense in that class, but never reuses the name of a shared helper.

## Temp files
- Use `TempFile` and `TempFileCollection` from `Refedle.TestHelpers.IO`. Each gets its own `Guid`-based path and is deleted on dispose, so tests running in parallel never share a file.
  - `ReservePath` reserves a path without creating the file. `CreateEmpty` also creates an empty file.
  - Do not use `Path.GetTempFileName()`. `Path.ChangeExtension(Path.GetTempFileName(), ...)` leaves the original `.tmp` file behind.
- For a file used by one test only, create it with a local `using` declaration. For a file shared by the tests of a class, create it in the fixture and dispose it in `Dispose()`.
- If a fixture constructor can fail after it has created files, dispose what it created and rethrow. xUnit does not dispose a fixture whose constructor threw.
