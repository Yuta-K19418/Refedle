# Design: Preserve the Original JSON Number Representation in Cell Extraction

## Background

`JsonObjectCellExtractor` parsed JSON numbers into long/double and converted them back to strings, so the original representation was lost (e.g. `1.50` was displayed as `1.5`).

## Decisions

### 1. The extractor returns the raw text of number tokens

- **Decision:** Stop parsing number tokens and converting them back to strings; decode the raw UTF-8 bytes (`ValueSpan`) as-is and return them.
- **Rationale:** The display side (`LazyTransformerBase.ShouldFormat` / `CastMarkers`) can identify cast columns, so numeric formatting only needs to be applied to cast columns, by `CellDisplayFormatter`. The CLI cell output (`JsonObjectCellReader`) already outputs the raw bytes, so this also aligns with its behavior.
- **Rejected alternative:** Pass "whether the column was numeric from the start" to `ExtractCell` and switch formatting on and off. The formatting decision would be duplicated with the display side, and all five callers would need to change.

### 2. Representations are not normalized

- **Decision:** Exponent notation (`1e3`), `-0`, integers beyond the int64 range, and similar values are displayed exactly as written in the JSON. Integers beyond the int64 range no longer go through double, so they are displayed at their exact value.
- **Rejected alternative:** Normalize only specific representations, such as exponent notation. Where to draw the line on which representations to normalize would be arbitrary.

### 3. Values that cannot be interpreted as WholeNumber are not treated as integers

- **Decision:** For values such as `1e3` and `1.0`, the following behavior is the specification.
  1. Casting to WholeNumber displays `<invalid>`.
  2. In a numeric filter compared as WholeNumber, rows whose value cannot be interpreted as an integer do not match. For example, with the filter `> 999`, a row whose cell value is `1e3` is excluded (previously it matched, because extraction converted it to `1000`).
  3. String filter operators compare the cell value, in its written representation, against the string specified in the filter. For example, when the cell value is `1.50`, specifying `1.50` with Equals matches, but specifying `1.5` does not (the comparison checks whether the characters are the same, not whether the values are numerically equal). FloatingPoint numeric comparisons still compare raw representations as numbers.
- **Rationale:** Both the display and the filter treat these values as "not integers", so their behavior is consistent with each other.
- **Rejected alternatives:**
  - Make `FilterEvaluator` / `CellDisplayFormatter` accept integral values (zero fractional part, exponent notation). The impact would extend to CSV filters and casts as well.
  - Keep the extracted values normalized and make only the display use the raw representation.
