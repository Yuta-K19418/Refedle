---
paths:
  - "src/**/*.cs"
---

# C# Coding Standards (.NET 11+ / C# 15 Strict)

## Modern Syntax (MANDATORY)

### Namespaces
- Use **File-scoped namespaces** (`namespace X;`)
- ❌ Block-scoped namespaces (`namespace X { ... }`) are **STRICTLY FORBIDDEN**

### Constructors
- Use a **conventional constructor** (with a body) if any of the following applies:
  1. It validates arguments — even a single check is enough (e.g.,
     `ArgumentNullException.ThrowIfNull`)
  2. The constructor is `private`/`protected` — primary constructors cannot carry an access
     modifier (e.g., to force construction through a static factory)
  3. It does work with a parameter, such as creating an instance (e.g., allocating an
     internal buffer with `new` using a capacity parameter)
- Otherwise, use **Primary Constructors**
- Positional records follow the same rule. A record that needs argument checks must be
  written with a conventional constructor, not as a positional record that throws from
  `init`

### Abstract Base Classes
- Mark abstract base classes in `src` as `closed` (C# 15 closed hierarchies)
- Exception: do not mark a base class that is derived from in a test project (`closed`
  forbids derivation from other assemblies, so the derived types in the tests would no longer
  compile)

### Collections
- Use **Collection Expressions** `[]`
- Example: `int[] x = [1, 2];`
- ❌ `new List<T>()` or `new T[] { ... }` are **STRICTLY FORBIDDEN**

### Pattern Matching & Type Handling
- Use `switch` expressions and `is` patterns (e.g., `is not null`)
- Use declaration patterns (`if (obj is Type t)`) or recursive patterns instead of the `as` operator
- ❌ The `as` operator is **STRICTLY FORBIDDEN** (except when required by external APIs)
- Use LINQ `OfType<T>()` for filtering and casting collections by type
- ❌ Do NOT use `Select(x => x as T).Where(x => x is not null)` or similar manual filtering patterns
- Avoid legacy `switch` statements or `==` checks for null

### Strings
- Use **Interpolated Strings** (`$"{var}"`)
- Avoid `String.Format`

### Disposal
- Use `using var` declarations

## Immutability
- Prefer **immutable by default**: data should flow through transformations rather than being mutated in place
- Mutable fields and mutable properties require justification; flag any that can be made immutable without meaningful cost

## Pure Functions
- Prefer **pure functions**: a method should compute and return its result rather than mutate state through `ref` or `out` parameters
- Return a tuple or a dedicated result type instead of using `ref`/`out` to propagate computed values back to the caller

### `out` parameters
- Use `out` **only** when implementing a `TryParse`-style method — i.e., when the method returns `bool` to signal success and needs to hand back a parsed value on success
- Do NOT use `out` for any other purpose; returning a `Result<T>` or tuple is always preferable

### `ref` parameters
- Use `ref` **only** as a performance optimization: when a large value-type (`struct`) would otherwise be copied repeatedly on every call, passing it by `ref` avoids that overhead
- Do NOT use `ref` to return computed values or to simulate multiple return values — use tuples or result types instead

### Collection Output Parameters
- Do NOT create a collection (`List<T>`, `Dictionary<TKey, TValue>`, etc.) in the caller and pass it into a method solely to have that method populate it as a side effect, when the method could just create and return the collection itself
- Exception: when a method is called repeatedly and its role is to accumulate into one shared collection across those calls (e.g., appending rows for every line scanned in a file), passing the shared collection as a parameter is allowed — recreating and merging a new collection on every call would add unnecessary allocations

## Methods

### Use Fields as the Values Themselves
- Do not pass as a parameter something that can be obtained from a field; the method gets it
  from the field

### Pass Only the Values the Method Uses
- Pass a method only the values its body actually uses. Do not pass a whole object when the
  body uses only some of its members
- Passing the whole object is allowed when the body needs the object itself, or when it uses
  all of its members

### Return Boolean Conditions Directly
- In a method that returns `bool`, do not write `if (condition) { return true; } return false;`.
  Return the condition itself, and use an expression body (`=>`) if possible
- This applies only to `if`-`return` statements that return `bool`
- Example:
  ```csharp
  // NG
  private static bool IsEmpty(int count)
  {
      if (count == 0)
      {
          return true;
      }

      return false;
  }

  // OK
  private static bool IsEmpty(int count) => count == 0;
  ```

### Combine Checks into a Single `Func<bool>`
- Do not pass parameters that are used only for a check
- Combine the check into a single `Func<bool>` that closes over the values it needs
- Make it so that downstream code only has to call that function. This avoids passing
  unnecessary parameters. Also, when the same check is evaluated multiple times, the conditions
  joined with `&&` are written in one place, which prevents forgetting to specify one of them
- Example:
  ```csharp
  // NG: session and cts are passed around only for the check
  private void OnCompleted(Session session, CancellationTokenSource cts)
  {
      if (!ReferenceEquals(_session, session) || cts.IsCancellationRequested)
      {
          return;
      }

      Save(_session.Result);
  }
  ```
  ```csharp
  // OK: combine into one closure at the origin; downstream code just calls it
  internal void Start(Session session, CancellationTokenSource cts)
  {
      _session = session;
      bool isCurrent() => ReferenceEquals(_session, session) && !cts.IsCancellationRequested;
      session.Completed += () => OnCompleted(isCurrent);
  }

  private void OnCompleted(Func<bool> isCurrent)
  {
      if (!isCurrent())
      {
          return;
      }

      Save(_session.Result);
  }
  ```

## Structure & Complexity (STRICT)

### One Type per File
- Put only one top-level type in each file, whether it is a class, struct, enum, record, or
  interface
- Also give each base and derived type of a closed hierarchy its own file (the language does
  not require them to be in the same file)
- Exception: a `private` nested type may be written in the same file as its containing class
  - Typical example: a `private record` that fits on one line, nested inside the containing
    class (e.g., `private sealed record WorkingColumn(string Name, int Index);`)
  - When a `private` nested type grows large, extracting it into a separate file follows the
    exception in [Partial Classes](#partial-classes)

### Class Size
- Target **under 300 lines**
- When a class is **approaching 200–300 lines**, proactively check whether multiple responsibilities have accumulated — it is easier to split early than after the class grows further
- If multiple responsibilities are detected, refactor by splitting the class

### Partial Classes
- `partial` is **STRICTLY FORBIDDEN** in production code, with one exception:
  - Exception: extracting a `private` nested type's definition into its own file, when including
    it in the containing file would bloat that file
    - Name the extracted file `OuterClass.TypeName.cs` (e.g.,
      `ArgumentParser.ArgumentsParseResult.cs`)
  - Do NOT use `partial` to work around the 300-line class-size guideline — splitting the same
    class across files does not reduce its actual complexity. When a class approaches or exceeds
    300 lines, split by **responsibility** into a separate class and delegate to it instead
- This rule applies to production code only; test code follows the `partial`-per-method-group
  convention in [testing.md](testing.md#naming-conventions)

### Dependency Direction
- When a change introduces a new class (whether split out of an existing class or added from scratch), check whether it forms a **bidirectional dependency** with another class — i.e., the two classes call each other's members (methods, properties, etc.)
- Access modifiers (`public`, `internal`, etc.) are irrelevant to this check
- If both A→B and B→A hold, this is a bidirectional dependency and is **forbidden**

### No Test-Only Code in Production (STRICT)
- Do NOT write code in production projects that exists only for tests: no test-only hooks, seams, callbacks, flags, or `static` mutable state; no members widened to `public` solely so tests can reach them (`internal` + `InternalsVisibleTo` to the test projects is the established mechanism and is allowed)
- If a test needs to control or observe a collaborator, introduce an **interface** and inject it via DI (constructor injection). The production implementation is registered in production; the test supplies a test double from the test project
- The interface must represent a real responsibility of the design, not a test-only concern

### No `else` Clause
- Do NOT use `else` clauses
- Use **Guard Clauses** (early return) or `continue` to keep the logic flat

### Loop Termination
- If a `while` loop's termination condition can be expressed in the loop's own condition clause, do so — do NOT write it as an `if (condition) { break; }` in the body instead
- If it cannot be cleanly expressed there (e.g., the condition depends on work that must happen inside the body first), an `if (condition) { break; }` is acceptable

### Max Nesting
- Limit indentation to a maximum of **2 levels**
- If logic requires deeper nesting, refactor by extracting methods

### Variable Declaration
- Do NOT declare a variable without an initializer (e.g., `int count;`)
- Always assign a value at the point of declaration, even if the real value is computed later (e.g., inside a `try` block) — keeping declaration and meaning together avoids forcing the reader to scan forward to find the first assignment

### Group Fields That Change Together
- Treat multiple fields that are created, updated, and discarded together as a state group, and
  extract the process that updates them together into a single instance method
- Also when another class changes the state of a state class, do not touch its properties one
  by one; go through a method that updates the multiple values together
- Purpose: when the same update is done in separate places, one of the updates can be missed.
  To prevent this, encapsulate changes to instance variables in instance methods
- Rule of thumb: this applies if a set where "whenever A is assigned, B and C are always
  assigned (or set to null) too" appears in two or more places
- Example:
  ```csharp
  // NG: three fields are updated together every time
  _startedAt = now;
  _lapCount = 0;
  _isRunning = true;
  ...
  _startedAt = null;
  _lapCount = 0;
  _isRunning = false;

  // OK: extract the process that updates them together into a single instance method
  StartMeasurement(now);
  ...
  StopMeasurement();
  ```

## Zero Warnings Policy
- The project must compile with **zero warnings**
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<AnalysisLevel>latest-all</AnalysisLevel>` must be enabled in `.csproj`
- Nullable Reference Types (NRT) must be `enabled` and strictly followed

## Error Handling
- **Engine-level**: Use the `Result<T>` pattern for expected failures in hot paths to avoid exception overhead
- Do NOT use Exceptions for flow control
- **Non-recoverable errors**: Use explicit, custom Exception types. Avoid generic `Exception`

## Legacy Patterns (STRICTLY FORBIDDEN)
- ❌ `new List<T>()` or `new T[] { ... }` (Use `[]`)
- ❌ Block-scoped namespaces (`namespace X { ... }`)
- ❌ `System.Reflection` and `System.Reflection.Emit` (Due to Native AOT constraints)

## Naming
- Follow standard .NET Naming Guidelines
- **Consistency with existing code**: if existing types follow a naming convention (e.g., a specific suffix or prefix), new types must follow the same pattern — flag any new class, interface, or member whose name breaks the established convention in its namespace or layer
- **ValueTuple element names**: use **camelCase** (e.g., `(string key, string value)`, `(int count, bool found)`). Tuple elements are destructured into local variables, so camelCase aligns with the local variable naming convention
- **Local functions**: use **lowerCamelCase** (e.g., `void setPhase(string phase)`). This is not enforced by `.editorconfig` (local functions are outside its `dotnet_naming_rule` symbol kinds), so it must be followed by convention
- **No "XAndY" method names**: do not name a method by listing its steps (e.g., `PrepareAndDispatchAsync`). Split it into one method per step (`PrepareAsync`, `DispatchAsync`) and let the caller compose them. A method that composes several steps is fine, but name it after the goal it achieves (`ConvertAsync`), not after the steps it runs
- **One name per referent**: within a method, keep the names that refer to the same thing consistent
  - The yardstick is whether, looking at the name, you can recognize it as the same thing
  - Differences that are still recognizable (such as `current` / `target`) are fine
  - Names that differ so much that they look like they belong to a different class are not allowed
  - Example:
    ```csharp
    // NG: the same FilterScanSession is called both session and scan
    private void CancelScan(FilterScanSession scan) { ... }
    if (_session is { } scan) { ... }

    // OK: unified to session
    private void CancelScan(FilterScanSession session) { ... }
    if (_session is { } session) { ... }
    ```

## ValueTuple
- `ValueTuple` (`(...)`) is for returning multiple values from a method, or as a local variable for intermediate processing within a method
- Do NOT define `ValueTuple` as a class/struct **field or property type**, including inside collections (e.g., `IReadOnlyList<(int Id, string Name)>`, `Dictionary<string, (bool, int)>`) — an unnamed tuple shape is not self-documenting once it becomes state that other members or callers rely on
- When a tuple-shaped value needs to be held as a field or property, define a `record`, `record struct`, `class`, or `struct` instead
  - Example: instead of `(int Min, int Max) Range => (...)`, define `public readonly record struct Range(int Min, int Max);` and expose `public Range Range { get; }`

## Extension Methods
- Group extension methods into a `static` class named `XxxExtensions` after the type being extended (e.g., `DateTime` → `DateTimeExtensions`)
- Do not mix them into a feature-side utility class or a class that has members other than extension methods
- If the extended type is external to the project (e.g., from the BCL), place the file in the directory of the feature that uses the extension method
- If the extended type is defined within the project, place the file in the same directory as that type
- Example:
  ```csharp
  // NG: an extension method is mixed into a feature-side utility class
  internal static class ReportHelper
  {
      internal static string BuildTitle(string name) => $"Report: {name}";
      internal static string ToReportDate(this DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
  }

  // OK: separate into a dedicated class named after the extended type (placed in the Report feature directory)
  internal static class DateTimeExtensions
  {
      internal static string ToReportDate(this DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
  }
  ```

## Project and Directory Placement
- Every class must reside in the project that matches its abstraction layer (`Engine` for core logic, `App` for TUI/presentation, `Tests` for test code)
- Even within the correct project, verify the directory is appropriate for the abstraction or domain the class belongs to — a misplaced file is a discoverability and maintainability problem
