# Drill-down

Available from the action menu (`x`), when the current view is in Tree mode. Two modes exist:

| View | Drill-down type |
|---|---|
| JSON Lines (Tree) | Full-aggregation |
| JSON Array (Tree) | Full-aggregation |
| JSON Object (Tree) | Single-node |

## Single-node drill-down

Turns the selected node itself into a table (JSON Object only — there's a single record to explore). The selected node must be a non-empty array of objects; selecting an object, a scalar (a plain value — not an object or array), or an array with non-object elements fails with an error.

![Single-node drill-down on a JSON Object in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/drilldown-node.gif)

Example — a JSON Object file:

```json
{
  "user": "alice",
  "orders": [
    { "id": 1, "item": "Book", "price": 12.5 },
    { "id": 2, "item": "Pen", "price": 1.2 }
  ]
}
```

Path: `orders`

Drilling down produces:

| id | item | price |
|---|---|---|
| 1 | Book | 12.5 |
| 2 | Pen | 1.2 |

## Full-file aggregation drill-down

Scans the entire file and aggregates the selected path across every record into a table (JSON Lines/Array).

![Full-file aggregation drill-down on a JSON array in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/drilldown-aggregate.gif)

<details>
<summary>Behavior by selected node type</summary>

The shape of the resulting table depends on what the selected path resolves to in each record:

- **Object** — one row per record, using the object's keys as columns.
- **Array** — one row per element; object elements become row columns, primitive elements become a single `value` column (always typed as Text). Selecting a specific array element (e.g. `tags[0]`) produces the same result as selecting the array itself — the whole array is always expanded.
- **Scalar** (a plain value — not an object or array) — one row with a single column (named after the path's last key), always typed as Text regardless of the actual value.

</details>

Example — a JSON Lines file (one record per line):

```json
{"user": "alice", "cart": {"orders": [{"id": 1, "item": "Book"}]}}
{"user": "bob", "cart": {"orders": [{"id": 2, "item": "Pen"}, {"id": 3, "item": "Mug"}]}}
```

Path: `cart > orders`

Drilling down scans every line and aggregates all matching arrays into one table:

| id | item |
|---|---|
| 1 | Book |
| 2 | Pen |
| 3 | Mug |
