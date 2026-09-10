# TUI Usage

```bash
refedle [--file <path>] [--recipe <path.yaml>]
```

Key bindings:

| Key | Action |
|---|---|
| `o` | Open file |
| `s` | Save recipe |
| `t` | Toggle Tree/Table view (JSON Lines only) |
| `x` | Action menu (Column/Row Actions, Drill-down) |
| `c` | Clear action stack |
| `Backspace` | Back from drill-down |
| `?` | Help |
| `q` | Quit (confirms if there are unsaved actions) |

## Column/Row Actions

Available from the action menu (`x`):

| View | Column/Row Actions |
|---|---|
| CSV (Table) | ✅ |
| JSON Lines (Table) | ✅ |
| Table from drill-down (any format) | ✅ |

**Rename** — renames a column.

![Renaming a column in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/rename.gif)

Before:

| name | age |
|---|---|
| Alice | 30 |

After (renamed `name` to `person name`):

| person name | age |
|---|---|
| Alice | 30 |

**Delete** — removes a column from the dataset.

Before:

| name | age |
|---|---|
| Alice | 30 |

After (deleted `age`):

| name |
|---|
| Alice |

**Cast** — converts a column's values to a different type (text, whole number, floating point, etc.).

Before:

| age |
|---|
| "30" |

After (cast `age` from text to whole number):

| age |
|---|
| 30 |

**Filter** — keeps only the rows where a column matches a condition (equals, not-equals, greater/less than, etc.). Multiple filters combine with AND.

![Filtering rows by a column condition in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/filter.gif)

Before:

| age |
|---|
| 30 |
| 20 |

After (filtered `age > 25`):

| age |
|---|
| 30 |

**Fill** — overwrites every value in a column with a fixed value; useful for anonymization, masking, or bulk initialization.

![Masking a column with Fill in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/fill.gif)

Before:

| email |
|---|
| alice@example.com |
| bob@example.com |
| carol@example.com |

After (filled with `***`):

| email |
|---|
| `***` |
| `***` |
| `***` |

**Format Timestamp** — reformats a Timestamp column's string values into a different date/time format.

![Reformatting a timestamp column in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/format-timestamp.gif)

Before:

| created_at |
|---|
| 2024-01-15T09:30:00Z |
| 2024-03-02T14:05:00Z |
| 2024-06-21T08:45:00Z |

After (formatted as `yyyy-MM-dd`):

| created_at |
|---|
| 2024-01-15 |
| 2024-03-02 |
| 2024-06-21 |

See [drill-down](drilldown.md) for turning nested JSON into a table, and [recipes](recipes.md) for saving these actions to replay from the CLI.
