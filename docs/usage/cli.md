# CLI Batch Usage

![Previewing a recipe with --dry-run, applying it, and reopening the result](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/apply.gif)

| Input \ Output | CSV | JSON Lines | JSON (`.json`) |
|---|---|---|---|
| CSV | ✅ | ✅ | ✅ |
| JSON Lines | ✅ | ✅ | ✅ |
| JSON Array | ✅ ¹ | ✅ ¹ | ✅ ¹ |
| JSON Object | ✅ ¹ | ✅ ¹ | ✅ ¹ |

¹ JSON Array / JSON Object input requires a drill-down-scoped recipe — the recipe's `drillDownKeyPath` selects the table to transform. Bare (non-drill-down) batch mode for these formats is out of scope. JSON Lines input works with or without a drill-down scope.

```bash
refedle apply --input <input> --recipe <recipe.yaml> --output <output>
```

`.json` output is always a JSON array (`[{...}, ...]`), regardless of row count or input format.

Format dispatch (reader → transform → writer) is resolved at compile time via a source generator (`src/Generators/FormatDispatcherGenerator.cs`), not reflection.

## Dry Run

Adding `--dry-run` validates the recipe and input without writing any output file. It performs every preparation step of `apply` — recipe load, format detection, recipe validation, column resolution, and output schema build — and prints a summary of the resolved plan (formats, drill-down scope, input columns, output schema with transforms, and filters) to stdout. With `--dry-run`, `--output` becomes optional; when given, its format is also detected and reported (still without writing the file). Failures are reported exactly as in a normal run.

```bash
refedle apply --input <input> --recipe <recipe.yaml> [--output <output>] --dry-run
```
