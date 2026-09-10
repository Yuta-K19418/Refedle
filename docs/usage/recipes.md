# Recipes

Pressing `s` saves the current action stack as a `.yaml` recipe, named after the source file.

Example — `people.csv`:

| nm | age | email |
|---|---|---|
| Alice | 30 | alice@example.com |
| Bob | 20 | bob@example.com |

After renaming `nm` → `name`, filling `email` with `***`, and filtering `age > 25`:

| name | age | email |
|---|---|---|
| Alice | 30 | `***` |

Pressing `s` at this point produces `people.yaml`:

```yaml
name: "people"
lastModified: 2026-07-26T12:34:56.0000000+00:00
actions:
  - type: Rename
    oldName: "nm"
    newName: "name"
  - type: Fill
    columnName: "email"
    value: "***"
  - type: Filter
    columnName: "age"
    operator: GreaterThan
    comparisonType: Number
    value: "25"
```

Recipes can then be replayed against other files via [CLI Batch Usage](cli.md), without opening the UI.
