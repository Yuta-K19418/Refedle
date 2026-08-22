---
paths:
  - "src/**/*.cs"
---

# C# Coding Standards (.NET 10+ / C# 14 Strict)

## Modern Syntax (MANDATORY)

### Namespaces
- Use **File-scoped namespaces** (`namespace X;`)
- ❌ Block-scoped namespaces (`namespace X { ... }`) are **STRICTLY FORBIDDEN**

### Constructors
- Use **Primary Constructors** for classes/structs/records, with one exception: if the
  constructor must be narrower in accessibility than the type itself (e.g. `private`/
  `protected`, to force construction through a static factory), use a conventional
  constructor with a body — primary constructors cannot carry an explicit accessibility
  modifier in current C#, so this case is not achievable with primary-constructor syntax

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

## Structure & Complexity (STRICT)

### Class Size
- Target **under 300 lines**
- When a class is **approaching 200–300 lines**, proactively check whether multiple responsibilities have accumulated — it is easier to split early than after the class grows further
- If multiple responsibilities are detected, refactor by splitting the class

### Partial Classes
- `partial` is **STRICTLY FORBIDDEN** in production code, with one exception:
  - Exception: extracting a `private` nested type's definition into its own file, when including
    it in the containing file would bloat that file
  - Do NOT use `partial` to work around the 300-line class-size guideline — splitting the same
    class across files does not reduce its actual complexity. When a class approaches or exceeds
    300 lines, split by **responsibility** into a separate class and delegate to it instead
- This rule applies to production code only; test code follows the `partial`-per-method-group
  convention in [testing.md](testing.md#naming-conventions)

### Dependency Direction
- When a change introduces a new class (whether split out of an existing class or added from scratch), check whether it forms a **bidirectional dependency** with another class — i.e., the two classes call each other's members (methods, properties, etc.)
- Access modifiers (`public`, `internal`, etc.) are irrelevant to this check
- If both A→B and B→A hold, this is a bidirectional dependency and is **forbidden**

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

## ValueTuple
- `ValueTuple` (`(...)`) is for returning multiple values from a method, or as a local variable for intermediate processing within a method
- Do NOT define `ValueTuple` as a class/struct **field or property type**, including inside collections (e.g., `IReadOnlyList<(int Id, string Name)>`, `Dictionary<string, (bool, int)>`) — an unnamed tuple shape is not self-documenting once it becomes state that other members or callers rely on
- When a tuple-shaped value needs to be held as a field or property, define a `record`, `record struct`, `class`, or `struct` instead
  - Example: instead of `(int Min, int Max) Range => (...)`, define `public readonly record struct Range(int Min, int Max);` and expose `public Range Range { get; }`

## Project and Directory Placement
- Every class must reside in the project that matches its abstraction layer (`Engine` for core logic, `App` for TUI/presentation, `Tests` for test code)
- Even within the correct project, verify the directory is appropriate for the abstraction or domain the class belongs to — a misplaced file is a discoverability and maintainability problem
