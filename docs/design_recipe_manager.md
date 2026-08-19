# Design: RecipeManager — YAML Serialization of the Action Stack

## Overview

Implement `RecipeManager` as the Engine-layer component responsible for persisting and restoring
the Action Stack as a human-readable `morph-recipe.yaml` file. This bridges the interactive TUI
workflow (Phase 3) with the CLI headless batch-processing mode (Phase 5).

A custom, AOT-safe YAML serializer (`RecipeYamlSerializer`) handles the YAML ↔ `Recipe` conversion,
with no external library dependency, relying entirely on the existing `MorphAction` polymorphic
model.

---

## Requirements

- Save the current Action Stack to a `.yaml` file (`morph-recipe.yaml` by default).
- Load a `.yaml` recipe file and restore the Action Stack.
- No reflection-based serialization (Native AOT compliance).
- YAML format is human-readable and hand-editable.
- Return `Result` / `Result<T>` for expected I/O and parse failures (no exception-driven flow).
- TUI exposes "Save Recipe" (`Ctrl+S`) and "Load Recipe" (File menu) to the user.
- The serializer is extensible: adding a new `MorphAction` subtype requires only a `case` branch
  in the writer and reader without structural changes.

---

## YAML Format Specification

### Example

```yaml
name: "customer-data"
description: "Normalize customer CSV export"
lastModified: 2025-12-30T12:00:00+00:00
actions:
  - type: Rename
    oldName: "user_name"
    newName: "username"
  - type: Delete
    columnName: "temp_field"
  - type: Cast
    columnName: "age"
    targetType: WholeNumber
```

### Rules

| Element | Rule |
|---------|------|
| Field keys | Always `camelCase` (e.g., `columnName`, `comparisonType`). |
| Field values | Always `PascalCase`, matching the C# enum member name or action discriminator 1:1 (e.g., `Rename`, `WholeNumber`, `GreaterThan`). **Case-sensitive**: a value that differs in casing is rejected on read instead of silently accepted. |
| Quoting | A field is quoted iff the program handles it as a free-form string. Fixed-vocabulary (enum) values and `lastModified` are never quoted. |
| Free-form string values | Always wrapped in `"..."`. Internal `"` is escaped as `\"`. Applies to `name`, `description`, `columnName`, `value`, `oldName`, `newName`, `targetFormat`. |
| Enum values | Written as the C# enum name, **unquoted** (e.g., `type: Rename`, `targetType: WholeNumber`, `operator: GreaterThan`, `comparisonType: Number`). |
| DateTimeOffset | `lastModified` is written in ISO 8601 round-trip format (`"O"`), **unquoted** — it is always a resolved timestamp value, never user-authored, so generic YAML tools can read it as a native timestamp. |
| Top-level fields | `name` (required, non-empty), `description` (omit if null), `lastModified` (omit if null), `actions` |
| Field order | Always: `name` → `description` → `lastModified` → `actions` |
| Empty actions | `actions: []` on a single line (no trailing `-` items). |
| Non-empty actions | `actions:` header, then items indented 2 spaces: `  - type: ...`, with further fields indented 4 spaces: `    key: value`. |
| `type` field | Always the **first** field of each action item; its value is the PascalCase action discriminator (e.g., `Rename`, `FormatTimestamp`). |
| Comments | Lines starting with `#` are silently skipped on read. |
| Blank lines | Silently skipped on read. |

### Action field definitions

| Action type | Discriminator | Fields (in order) |
|---|---|---|
| `RenameColumnAction` | `Rename` | `oldName` (string), `newName` (string) |
| `DeleteColumnAction` | `Delete` | `columnName` (string) |
| `CastColumnAction` | `Cast` | `columnName` (string), `targetType` (ColumnType enum) |
| `FilterAction` | `Filter` | `columnName` (string), `operator` (FilterOperator enum), `comparisonType` (ComparisonType enum), `value` (string) |
| `FillColumnAction` | `Fill` | `columnName` (string), `value` (string) |
| `FormatTimestampAction` | `FormatTimestamp` | `columnName` (string), `targetFormat` (string) |

---

## New Types

### `IRecipeManager` interface

**File**: `src/Engine/IRecipeManager.cs`

```csharp
public interface IRecipeManager
{
    /// <summary>
    /// Loads a recipe from a YAML file.
    /// </summary>
    Task<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Saves a recipe to a YAML file.
    /// Overwrites the file if it already exists.
    /// </summary>
    Task<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default);
}
```

`CancellationToken` cancellation propagates as `OperationCanceledException` (standard .NET behavior).
I/O and parse failures are returned as `Results.Failure` / `Results.Failure<Recipe>`.

---

### `RecipeManager` class

**File**: `src/Engine/RecipeManager.cs`

```csharp
public sealed class RecipeManager : IRecipeManager
{
    private readonly RecipeYamlSerializer _serializer = new();

    public async Task<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default);
    public async Task<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default);
}
```

**`SaveAsync` algorithm:**
1. Validate `filePath` is not null or whitespace.
2. Call `_serializer.Serialize(recipe)` → YAML string.
3. Write the string to `filePath` via `File.WriteAllTextAsync` with `UTF-8` (no BOM).
4. Wrap `IOException` in `Results.Failure(ex.Message)`.

**`LoadAsync` algorithm:**
1. Validate `filePath` is not null or whitespace.
2. Check file exists; return `Results.Failure<Recipe>("File not found: {filePath}")` if not.
3. Read the file via `File.ReadAllTextAsync` with `UTF-8`.
4. Call `_serializer.Deserialize(yaml)`.
5. Return the result directly.
6. Wrap `IOException` in `Results.Failure<Recipe>(ex.Message)`.

---

### `RecipeYamlSerializer` class

**File**: `src/Engine/RecipeYamlSerializer.cs`
**Access**: `internal sealed`

#### `Serialize(Recipe recipe) → string`

Builds the YAML output using `StringBuilder`:

```
name: "{recipe.Name}"
[description: "{recipe.Description}"]          -- omit if null
[lastModified: {recipe.LastModified:O}]         -- omit if null
actions: []                                     -- if no actions
actions:                                        -- if actions present
  - type: {PascalCase discriminator}
    {field}: {formattedValue}
    ...
```

String quoting helper: wrap every free-form string value in `"..."` and escape inner `"` as `\"`.
Enum values and `lastModified` bypass the helper and are written unquoted.

#### `Deserialize(string yaml) → Result<Recipe>`

Line-by-line state machine parser.

**States:**

| State | Description |
|---|---|
| `Root` | Parsing top-level `key: value` pairs |
| `Actions` | Encountered `actions:` or `actions: []`; ready for list items |
| `ActionItem` | Accumulating `key: value` pairs for the current action |

**State transitions:**
- Any state: blank line or `#` comment → skip.
- `Root`: `key: value` at column 0 → store top-level field.
- `Root`: `actions: []` → store empty actions list, stay `Root` (no items follow).
- `Root`: `actions:` → transition to `Actions`.
- `Actions`: line matching `  - type: {value}` → start new action item, transition to `ActionItem`.
- `ActionItem`: line matching `    {key}: {value}` → store field for current action.
- `ActionItem`: line matching `  - type: {value}` → finalize current action, start next.
- End of input: finalize current action (if any).

**Action dispatch (finalizing an action):**

```csharp
switch (type)
{
    case "Rename":
        // requires: oldName, newName
    case "Delete":
        // requires: columnName
    case "Cast":
        // requires: columnName, targetType (parsed as ColumnType enum)
    // "Filter", "Fill", "FormatTimestamp" → added with their features
    default:
        return Results.Failure<Recipe>($"Unknown action type: '{type}'");
}
```

**Error cases returned as `Results.Failure<Recipe>`:**
- Missing `name` field.
- Unrecognised action `type` value.
- Missing required field for an action type (e.g., `oldName` missing for `Rename`).
- Invalid enum value (e.g., `targetType: UnsupportedType`).
- Malformed `key: value` line at unexpected indentation.

**Value parsing helpers:**
- Unquote: if value starts and ends with `"`, strip them and unescape `\"` → `"`.
- Enum parse: `Enum.TryParse<T>(value, ignoreCase: false, out var result)`.

---

## TUI Integration

### Changes to `MainWindow.cs`

**New field:**
```csharp
private readonly RecipeManager _recipeManager = new();
```

**Updated `InitializeMenu()`:**
Add two items to the File `MenuBarItem`:

```
_Open          (existing)
_Save Recipe   → HandleSaveRecipeAsync()
_Load Recipe   → HandleLoadRecipeAsync()
_Exit          (existing)
```

**Updated `InitializeStatusBar()`:**
Add one shortcut:

```
Ctrl+S  "Save Recipe"  → HandleSaveRecipeAsync()
```

**`HandleSaveRecipeAsync()`:**
1. Guard: return if `_state.CurrentMode` is not `CsvTable` or `JsonLinesTable` / `JsonLinesTree`.
2. Show a `SaveDialog` (`.yaml` filter, default filename `morph-recipe.yaml`).
3. If cancelled, return.
4. Construct `Recipe`:
   - `Name`: `Path.GetFileNameWithoutExtension(_state.CurrentFilePath)`
   - `Actions`: `_state.ActionStack`
   - `LastModified`: `DateTimeOffset.UtcNow`
5. `await _recipeManager.SaveAsync(recipe, path)`.
6. On failure: `_viewManager.ShowError(result.Error)`.

**`HandleLoadRecipeAsync()`:**
1. Guard: return if no file is currently loaded.
2. Show an `OpenDialog` (`.yaml` filter).
3. If cancelled, return.
4. `await _recipeManager.LoadAsync(path)`.
5. On failure: `_viewManager.ShowError(result.Error)`.
6. On success: `_state.ActionStack = result.Value.Actions`, then `_viewManager.RefreshCurrentTableView()`.

---

## Affected Files

| File | Change |
|------|--------|
| `src/Engine/IRecipeManager.cs` | New: public interface |
| `src/Engine/RecipeManager.cs` | New: public sealed class |
| `src/Engine/RecipeYamlSerializer.cs` | New: internal sealed class |
| `src/App/MainWindow.cs` | Add `_recipeManager`, File menu items, `Ctrl+S` shortcut |
| `tests/Refedle.Tests/Engine/RecipeYamlSerializerTests.cs` | New: unit tests for serializer |
| `tests/Refedle.Tests/Engine/RecipeManagerTests.cs` | New: unit tests for file I/O |

---

## Test Cases

### `RecipeYamlSerializerTests`

**Serialization:**
- `Serialize_EmptyActions_ProducesActionsEmptyListLine`
- `Serialize_WithRenameAction_ProducesCorrectYaml`
- `Serialize_WithDeleteAction_ProducesCorrectYaml`
- `Serialize_WithCastAction_ProducesCorrectYaml`
- `Serialize_NullDescription_OmitsDescriptionField`
- `Serialize_NullLastModified_OmitsLastModifiedField`
- `Serialize_StringValueWithDoubleQuote_EscapesQuoteCharacter`
- `Serialize_FieldOrder_NameFirstActionsLast`

**Deserialization:**
- `Deserialize_ValidYaml_ReturnsRecipeWithCorrectName`
- `Deserialize_EmptyActionList_ReturnsEmptyActions`
- `Deserialize_RenameAction_ParsesOldNameAndNewName`
- `Deserialize_DeleteAction_ParsesColumnName`
- `Deserialize_CastAction_ParsesColumnNameAndTargetType`
- `Deserialize_MultipleActions_PreservesOrder`
- `Deserialize_UnknownActionType_ReturnsFailure`
- `Deserialize_MissingRequiredField_ReturnsFailure`
- `Deserialize_InvalidEnumValue_ReturnsFailure`
- `Deserialize_MissingNameField_ReturnsFailure`
- `Deserialize_CommentLines_AreIgnored`
- `Deserialize_BlankLines_AreIgnored`
- `Deserialize_EscapedQuoteInStringValue_ParsesCorrectly`

**Round-trip:**
- `RoundTrip_RenameAction_ProducesEquivalentRecipe`
- `RoundTrip_DeleteAction_ProducesEquivalentRecipe`
- `RoundTrip_CastAction_ProducesEquivalentRecipe`
- `RoundTrip_MultipleActions_PreservesAllActions`
- `RoundTrip_WithNullableFields_PreservesNullability`

### `RecipeManagerTests`

- `SaveAsync_ThenLoadAsync_RoundTrip_ReturnsEquivalentRecipe`
- `SaveAsync_CreatesFileAtSpecifiedPath`
- `SaveAsync_OverwritesExistingFile`
- `LoadAsync_NonExistentFile_ReturnsFailure`
- `LoadAsync_EmptyFile_ReturnsFailure`
- `LoadAsync_InvalidYaml_ReturnsFailure`
- `SaveAsync_NullFilePath_ThrowsArgumentException`
- `LoadAsync_NullFilePath_ThrowsArgumentException`

---

## Architecture Decision Log

### ADR-1: Custom YAML serializer vs. external YAML library

**Context**

Recipe data must be saved as human-readable YAML. Several options exist for YAML serialization.

**Options**

- **A — YamlDotNet**: Most popular C# YAML library. Default mode uses reflection; AOT requires
  `StaticContext` API (v14+) with significant boilerplate and an external dependency.
- **B — YamlDotNet (StaticContext)**: AOT-safe but complex, adds a NuGet dependency that must
  be kept compatible with `IsAotCompatible=true` on the Engine project.
- **C — Custom serializer**: Hand-written writer and state-machine reader for the specific,
  deterministic YAML schema this project produces and consumes. No external dependency.

**Decision**: Option C.

**Rationale**

The Recipe model is a closed, well-defined structure: a handful of top-level scalar fields and
a flat list of action objects. Each action type has at most four fields, all of which are either
strings or enums. The YAML produced is a strict subset of YAML 1.2; no anchors, aliases,
multi-document streams, or complex types are needed.

Given this limited surface area, a custom serializer is:
- **AOT-safe**: zero reflection, zero dynamic dispatch.
- **Dependency-free**: keeps `Refedle.Engine.csproj` free of new NuGet packages.
- **Deterministic**: the writer produces canonical output, which simplifies the reader to a
  simple line-by-line state machine.
- **Extensible**: supporting a new `MorphAction` subtype requires adding one `case` branch in
  both writer and reader — identical effort to the `[JsonDerivedType]` + `JsonSourceGeneration`
  registration required for JSON.

The trade-off is that the custom reader does not support arbitrary YAML (only the canonical
output of our own writer). This is acceptable because recipe files are produced by this tool
or by users who hand-edit files following the documented format.

---

### ADR-2: `Result<T>` for I/O errors vs. exceptions

**Context**

`RecipeManager.LoadAsync` / `SaveAsync` can fail for multiple expected reasons: file not found,
invalid YAML, missing required field, unrecognised action type. Two error-propagation strategies
were considered.

**Options**

- **A — Exceptions**: Throw `FileNotFoundException`, `RecipeParseException`, etc.
- **B — `Result<T>` pattern**: Return `Result<Recipe>` / `Result` per the established Engine
  pattern.

**Decision**: Option B.

**Rationale**

The project guidelines state that the Engine layer uses the `Result<T>` pattern for expected
failures. All failure cases listed above are expected at runtime (the user may select any file
via the OS dialog). Using `Result<T>` allows callers (`MainWindow`) to handle errors without
try/catch, keeping the TUI integration flat and consistent with the existing `FileLoader`
error-reporting pattern (`AppState.LastError`). `OperationCanceledException` from
`CancellationToken` is not an "expected failure" and propagates normally.

---

### ADR-3: Recipe `Name` derived from file path vs. user-provided

**Context**

When saving a recipe from the TUI, a `Name` field is required by the `Recipe` record. Two
approaches were considered.

**Options**

- **A — User input dialog**: Show a text-input dialog for the user to type a recipe name before
  the save-file dialog.
- **B — Derived from source file**: Use `Path.GetFileNameWithoutExtension(currentFilePath)` as
  the recipe name automatically.

**Decision**: Option B.

**Rationale**

Adding an extra dialog before the file picker increases interaction steps for the common case.
The source file's base name (e.g., `customer-data` from `customer-data.csv`) is a reasonable,
meaningful default. The generated YAML file is human-editable, so users can change the `name`
field manually if desired. This keeps the save flow to a single dialog (file chooser), consistent
with the "Open" flow.

---

### ADR-4: `SaveDialog` vs `OpenDialog` for save path selection

**Context**

Terminal.Gui 2.0-develop does not expose a `SaveDialog` component in the public API.

**Options**

- **A — Use `OpenDialog`**: Repurpose the existing `OpenDialog` for save path selection; user
  navigates to a directory and types a filename.
- **B — Simple text input**: Show an `InputDialog`-style dialog for the user to type a full path.

**Decision**: Option A.

**Rationale**

`OpenDialog` in Terminal.Gui 2.0 allows typing a path directly in the filename field, which
covers the save-path selection use case adequately. It also presents a file browser, letting
users navigate to the desired directory before entering the filename. Option B (full path input)
is error-prone and less discoverable. If a dedicated `SaveDialog` becomes available in a future
Terminal.Gui release, the `HandleSaveRecipeAsync` method is the only touch point to update.
