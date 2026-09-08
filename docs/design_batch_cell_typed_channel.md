# Design: Typed Cell Channel for CLI Batch Processing

## Requirements

`JsonLinesRecordReader.GetCellSpan()` currently reuses
`JsonObjectCellExtractor.ExtractCell()` — an extractor built for TUI
table/tree display. For JSON Object/Array values it returns display-only
preview strings (`{Object: N properties}`, `[Array: N items]`), and CLI batch
output (CSV/JSON Lines writers) passes that preview straight through, losing
the real data for any Object/Array column.

`ExtractCell` is shared with `JsonLinesTableSource`/`FocusedTableSource` and
must **not change** — its preview behavior is correct for the TUI.

Goals for the CLI batch path only:

- Preserve the original JSON text for Number/Object/Array values (no
  preview strings, no numeric reformatting), including large integers and
  exponent notation.
- Resolve JSON string escapes correctly (the shared extractor currently does
  not — accepted as an in-scope side effect for the CLI batch path only, see
  Decision Record).
- Never let a JSON string value be reinterpreted as a JSON number/boolean in
  output just because its text looks like one (e.g. a JSON string `"5"` or
  `"true"` must stay a JSON string).
- Replace the `"<null>"`/`"<error>"` string-sentinel convention (used by both
  `JsonLinesRecordReader.GetCellSpan` output and `WriteCellSpan`/`WriteJsonValue`
  input) with an explicit, non-string-based signal, so a genuine JSON string
  value of `"<null>"` is never misinterpreted as null.
- Distinguish a JSON property that is genuinely absent from one that is
  explicitly `null`, so JSON Lines → JSON Lines is a true structural
  round-trip and does not gain properties it didn't have.
- Never emit syntactically invalid JSON, regardless of source format.
- No behavior change for CSV → CSV or CSV → JSON Lines, and no behavior
  change for transformed columns (`FillSpec`/`TimestampFormatSpec`) either.
  JSON Lines → JSON Lines becomes a true structural round-trip; JSON Lines →
  CSV embeds the raw JSON text of Object/Array cells as a CSV string.

## Files Changed

| File | Change |
|------|--------|
| `src/App/Cli/IO/CellData.cs` | **New.** `CellPresence` enum, `CellEncoding` enum, `CellData` readonly `ref struct` |
| `src/App/Cli/IO/CellEncodingClassifier.cs` | **New.** Shared `bool`/`long`/`double` text heuristic, used by CSV reads and by transformed-column output |
| `src/App/Cli/IO/IRecordReader.cs` | `GetCellSpan(int) : ReadOnlySpan<char>` → `GetCellData(int) : CellData` |
| `src/App/Cli/IO/IRecordWriter.cs` | `WriteCellSpan(int, ReadOnlySpan<char>)` → `WriteCellData(int, CellData)` |
| `src/App/Cli/IO/Csv/CsvRecordReader.cs` | `GetCellData` calls `CellEncodingClassifier.Classify` inline (`Presence` always `Value`) |
| `src/App/Cli/IO/Json/JsonLinesRecordReader.cs` | `GetCellData` scans with a local `Utf8JsonReader` and derives `Presence`/`Encoding` directly from `JsonTokenType`, instead of calling `ExtractCell` |
| `src/App/Cli/IO/Csv/CsvRecordWriter.cs` | `WriteCellData` — same output behavior, now branches on `Presence` instead of matching `"<null>"`/`"<error>"` text; `Encoding` is not read (CSV output is always plain text) |
| `src/App/Cli/IO/Json/JsonLinesRecordWriter.cs` | `WriteCellData` replaces `WriteJsonValue`; branches on `Presence`/`Encoding` instead of re-parsing every `ReadOnlySpan<char>` unconditionally |
| `src/App/Cli/Commands/Apply/RecordProcessor.cs` | Passes `CellData` through for untransformed columns; wraps `FillSpec`/`TimestampFormatSpec` output via `CellEncodingClassifier.Classify` (not a hardcoded encoding) |
| `tests/Refedle.Tests/App/Cli/Commands/Apply/RecordProcessorTests.TestRecordReader.cs` | `GetCellSpan` → `GetCellData` (test double) |
| `tests/Refedle.Tests/App/Cli/Commands/Apply/RecordProcessorTests.TestRecordWriter.cs` | `WriteCellSpan` → `WriteCellData` (test double), captures `Presence`/`Encoding` for assertions |
| `tests/Refedle.Tests/App/Cli/Commands/Apply/RunnerTests.cs` | Add end-to-end cases (see "Test Plan" below) |

No changes to `JsonObjectCellExtractor.cs`, `JsonByteExtractor.cs`,
`ColumnType.cs`, or `TypeInferrer.cs`. This design does not read or produce a
`ColumnType` anywhere in the batch path.

---

## Implementation Approach

### 1. New types (`CellData.cs`)

```csharp
internal enum CellPresence
{
    Value,
    Null,
    Missing,
    Invalid,
}

internal enum CellEncoding
{
    PlainText,
    Raw,
    Numeric,
    Boolean,
}

internal readonly ref struct CellData(
    ReadOnlySpan<char> value,
    CellPresence presence,
    CellEncoding encoding = CellEncoding.PlainText)
{
    public ReadOnlySpan<char> Value { get; } = value;
    public CellPresence Presence { get; } = presence;
    public CellEncoding Encoding { get; } = encoding;
}
```

`CellPresence` has four states:

- `Value` — a usable value is present.
- `Null` — the JSON value is explicitly `null` (JSON Lines only).
- `Missing` — the property does not exist in the source at all (JSON Lines
  only; distinct from `Null` so the writer can omit rather than nullify it).
- `Invalid` — the source could not be read (malformed JSON).

`CellEncoding` says how the writer must turn `Value` into JSON — a purely
mechanical/syntactic decision, independent of the cell's domain type
(`ColumnType` is not involved anywhere in this design; see Decision Record):

- `Raw` — `Value` is already valid JSON syntax (a genuine JSON Number,
  Object, or Array token) and must be written verbatim via `WriteRawValue`.
  Never used for CSV.
- `Numeric` — `Value` looks numeric (CSV heuristic, or a transformed value
  that looks numeric) but is not guaranteed to be valid JSON number syntax
  (`007`, `1,234`); must be re-parsed and re-serialized via
  `WriteNumberValue`.
- `Boolean` — `Value` is `"true"`/`"false"` (any casing); re-parsed via
  `bool.Parse` and written via `WriteBooleanValue`.
- `PlainText` — write `Value` unconditionally via `WriteStringValue`, no
  heuristic, no exceptions. This is what makes a JSON string `"5"` or
  `"true"` stay a JSON string in output.

### 2. `IRecordReader` / `IRecordWriter`

```csharp
CellData GetCellData(int outputColumnIndex);
void WriteCellData(int outputColumnIndex, CellData cell);
```

replace `GetCellSpan`/`WriteCellSpan` one-for-one.

### 3. `CellEncodingClassifier` (new, shared)

```csharp
internal static class CellEncodingClassifier
{
    public static CellEncoding Classify(ReadOnlySpan<char> value)
    {
        if (bool.TryParse(value, out _))
        {
            return CellEncoding.Boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return CellEncoding.Numeric;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return CellEncoding.Numeric;
        }

        return CellEncoding.PlainText;
    }
}
```

This is exactly today's `WriteJsonValue` detection order, extracted so both
`CsvRecordReader` and `RecordProcessor` (for transformed columns) can call it
— see step 7 for why transformed columns need it too, not a hardcoded
`PlainText`.

### 4. `CsvRecordReader.GetCellData`

`Presence` is always `Value` — CSV has no structural null/missing/invalid
concept; an empty column is an empty span, which the writer's `PlainText`
branch already handles correctly (`WriteStringValue("")`).

`Encoding` is `CellEncodingClassifier.Classify(value)`. Classification
now happens on the read side for every cell, not only when the destination
is JSON Lines (that was implicit before, since `WriteJsonValue` used to run
this same heuristic in the writer). The extra CPU cost when writing CSV →
CSV, where `CsvRecordWriter` never reads `Encoding`, is an accepted
trade-off — it is small (no allocation, a few `TryParse` calls) and
`Encoding` is required for CSV → JSON Lines regardless, plus any future
CSV → JSON Object/Array-typed output.

No `TableSchema` or pre-scan is used. The `Unescape` option on the Sep
reader stays `false` (unrelated to this fix — tracked separately, see
"Out of Scope" below).

### 5. `JsonLinesRecordReader.GetCellData`

A new local `Utf8JsonReader` scans `_currentLineBytes.Span` for the target
column's property (same per-call scan strategy as `ExtractCell` — the
existing extractor is left untouched, so its reader is not shared, and this
new scan does **not** call `ExtractCell`/`FormatValue`/`FormatNumber` at
all — those exist to build TUI preview/display strings, which is exactly
the behavior this fix must avoid).

- **Presence**: property not found → `Missing`; JSON `null` → `Null`;
  unreadable/malformed JSON (root is not `StartObject`, or a `JsonException`
  is thrown, or `reader.Read()` fails after the property name) → `Invalid`;
  otherwise `Value`. No more `"<null>"`/`"<error>"` string sentinels, and
  `Missing` is now distinct from `Null`.
- **Number**: `Encoding = Raw`, `Value = Encoding.UTF8.GetString(reader.ValueSpan)`
  — for a `Number` token, `ValueSpan` is the literal digits of the token, so
  this is a direct, lossless UTF-8 decode with no semantic
  re-interpretation. `Raw` is correct here regardless of magnitude or
  exponent form: `Utf8JsonReader` only ever produces a `Number` token for
  text that is already syntactically valid per JSON's number grammar
  (arbitrary precision, no `Int64` limit — see Decision Record), so there is
  no size/form check to get wrong. This is a deliberate difference from
  `JsonObjectCellExtractor.FormatNumber`, which parses the number and calls
  `ToString()`, silently rewriting `1.50` to `1.5` and losing precision for
  integers beyond what `double` can represent exactly.
- **Object / Array**: `Encoding = Raw`, `Value =
  Encoding.UTF8.GetString(JsonByteExtractor.ExtractValueBytes(ref reader, _currentLineBytes).Span)`.
  `Utf8JsonReader.ValueSpan` is *empty* at `StartObject`/`StartArray` — it
  does not contain the nested content — so `JsonByteExtractor`'s existing
  depth-tracking byte-range extractor (already used elsewhere in the Engine
  for exactly this purpose) is reused to slice the full nested JSON text
  first.
- **String**: `Encoding = PlainText`, `Value = reader.GetString()` — the
  standard escape-resolving API. `JsonObjectCellExtractor.FormatValue` uses
  `Encoding.UTF8.GetString(reader.ValueSpan)` instead, which leaves escapes
  unresolved (a known, separate issue on the TUI extractor); using
  `reader.GetString()` here is the ordinary, correct way to read a JSON
  string with `Utf8JsonReader` and, as an accepted side effect, means CLI
  batch output no longer exhibits that unresolved-escape symptom (the TUI
  path is untouched and its issue remains open there).
- **Boolean**: `Encoding = Boolean`, `Value` is the static literal `"true"`
  or `"false"` selected by `reader.TokenType == JsonTokenType.True` — no
  allocation.

### 6. `JsonLinesRecordWriter.WriteCellData`

Replaces `WriteJsonValue`. Branch order:

1. `Presence == Missing` → omit the property entirely (no
   `WritePropertyName` call for this column).
2. `Presence == Null` → JSON `null`.
3. `Presence == Invalid` → empty string.
4. `Presence == Value` → dispatch on `Encoding`:
   - `Raw` → `WriteRawValue(value)` (verbatim; always valid JSON syntax by
     construction — see step 5).
   - `Numeric` → re-parse with the same
     `long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, ...)`
     / `double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, ...)`
     order and overloads as `CellEncodingClassifier`, then
     `WriteNumberValue` — this is the exact reformat `WriteJsonValue`
     performs today, so CSV numeric output is byte-for-byte unchanged, and
     pathological CSV text (`007`, `1,234`, `(5)`) is normalized into a
     valid JSON number instead of being spliced in verbatim.
   - `Boolean` → `WriteBooleanValue(bool.Parse(value))`.
   - `PlainText` → `WriteStringValue(value)` unconditionally — no
     `TryParse` fallbacks. An empty `Value` falls here too
     (`WriteStringValue("")`), so no separate empty-value branch is needed.

`WriteRawValue(ReadOnlySpan<char>, bool)` is used for the `Raw` branch
instead of `WriteNumberValue`, because `WriteNumberValue` only accepts
numeric CLR types (`long`, `double`, etc.) and would force re-parsing,
re-collapsing `1.50` back to `1.5` — the exact loss the reader side was
changed to avoid. `WriteRawValue` is confirmed present on `Utf8JsonWriter` in
the .NET 10 reference assembly.

The old `"<null>"`/`"<error>"` string-sentinel comparison is removed
entirely, which also fixes a latent bug where a genuine JSON string cell
containing the literal text `<null>` or `<error>` was misclassified.

### 7. `CsvRecordWriter.WriteCellData`

Only `Presence`/`Value` are read (`Encoding` is not needed — CSV output is
always plain text regardless of encoding):

- `Null`, `Missing`, or `Invalid` → write nothing (same as today's
  `"<null>"`/`"<error>"` → empty behavior, now also covering `Missing`)
- `Value` → CSV-escape `Value` and write it (unchanged)

### 8. `RecordProcessor`

- Pass-through columns: `writer.WriteCellData(i, reader.GetCellData(i))`
- Transformed columns (`FillSpec`, `TimestampFormatSpec`): build
  `new CellData(formattedValue, CellPresence.Value, CellEncodingClassifier.Classify(formattedValue))`.
  A hardcoded `PlainText` here would be a behavior change: today,
  transformed text flows through `WriteJsonValue`'s heuristic exactly like
  any other cell, so a numeric-looking fill value (e.g. a `FillSpec`
  default of `"0"`) is currently emitted as a JSON number. Reusing the same
  classifier preserves that.

### Behavior Matrix

| Input | Output | Object/Array column |
|---|---|---|
| CSV | CSV | Unchanged |
| CSV | JSON Lines | Unchanged (CSV never produces `Raw`; numeric-looking cells are still reformatted through `WriteNumberValue` exactly as today) |
| JSON Lines | CSV | Raw JSON text placed in the CSV cell as a string |
| JSON Lines | JSON Lines | Structurally identical to the input, including properties that were absent (no longer gain a `null`); string values are no longer misclassified as numbers/booleans |

---

## Decision Record

### Rationale

**Introduce `GetCellData`/`WriteCellData` instead of patching
`GetCellSpan`'s extraction logic in place:** An early proposal was to keep
returning `ReadOnlySpan<char>` but swap `ExtractCell` for a raw-byte
extractor. That does not carry presence/encoding information, so the writer
would still need to guess (via string-sentinel matching or ad hoc parsing)
whether a span is null, invalid, already-valid JSON, or plain text — which
is the root cause of the current bug and of the `"<null>"`/`"<error>"`
sentinel fragility. Making the reader the single source of truth for
presence and encoding removes all guessing from the writer.

**No `ColumnType` anywhere in this design:** `WriteJsonValue` today does not
consult `ColumnType` at all — it runs its own `bool`/`long`/`double` text
heuristic directly, regardless of source. An earlier revision of this design
carried `ColumnType` in `CellData` "for type information" alongside a
representational flag, but nothing in the batch read/write path actually
consumes a domain-type classification; the only question the writer ever
needs answered is the mechanical one `CellEncoding` answers ("how do I turn
this text into JSON"). Carrying `ColumnType` as well would be unused state.
`TypeInferrer.InferType`'s known imprecision for `Int64`-overflowing or
exponent-form numbers (it falls back to `Text`) is therefore irrelevant to
this design, not merely "worked around" — it is never called.

**Why `CellEncoding` has exactly four values, not two and not more:**

- `Raw` and `Numeric` cannot be merged. CSV's numeric heuristic uses
  permissive `NumberStyles` (to match today's `WriteJsonValue` exactly) that
  accept text which is not valid JSON number *syntax* — leading zeros, a
  leading `+`, thousands separators, parenthesized negatives. If CSV
  numeric-looking values were `Raw`, `WriteRawValue` would splice
  non-conforming text directly into JSON output, producing invalid JSON.
  `Numeric` routes them through the existing re-parse + `WriteNumberValue`
  step instead, which normalizes them into valid JSON exactly as today's
  code does. (Two alternatives were considered and rejected for this
  specific point — see "Alternatives Considered" F and G below.)
- `Raw` cannot absorb JSON Number by re-parsing it either (i.e. treating
  JSON numbers as `Numeric` instead of `Raw`): unlike CSV text, a JSON
  `Number` token is guaranteed by `Utf8JsonReader` itself to already be
  valid JSON number syntax, so re-parsing it buys nothing and actively
  loses the original lexical form (`1.50` → `1.5`) and precision for
  integers beyond `Int64`/`double` range.
- `PlainText` and `Boolean` cannot be merged. `PlainText` is defined as
  "always `WriteStringValue`, no heuristic" specifically so a JSON string
  `"5"` or `"true"` is never reinterpreted — but a genuine JSON `Boolean`
  token (or CSV text like `TRUE`) must still become a real JSON boolean, not
  the string `"true"`. Keeping `Boolean` separate lets `PlainText` be
  unconditional while `Boolean` still gets `WriteBooleanValue`.
- `PlainText` and `Numeric` cannot be merged for the same reason in the CSV
  direction: merging them would mean CSV `007`/`1,234` are emitted as JSON
  strings, a regression from today's normalized `WriteNumberValue` output.

**Classification moves from the writer into the reader for CSV and for
transformed columns:** Today, `WriteJsonValue`'s heuristic runs once, in the
writer, on whatever text arrives, regardless of source. Because `PlainText`
is now unconditional (no heuristic in the writer at all), any text that
should still be recognized as numeric/boolean must be classified before it
reaches the writer. `CellEncodingClassifier` is extracted as a shared
helper (step 3) so `CsvRecordReader` and `RecordProcessor`'s
`FillSpec`/`TimestampFormatSpec` output both classify identically to how
`WriteJsonValue` classifies today — this is what keeps CSV → JSON Lines and
transformed-column output byte-for-byte unchanged. The cost is that this
classification now runs on every CSV cell even when the destination is CSV
(where `CsvRecordWriter` ignores `Encoding` entirely); this is accepted as
minor (no allocation) and unavoidable in general, since the same reader
output is also used for CSV → JSON Lines.

**`reader.GetString()` for String, not `ExtractCell`/`FormatValue`:**
`FormatValue`'s `String` branch uses `Encoding.UTF8.GetString(reader.ValueSpan)`,
which leaves JSON escape sequences unresolved (a known, separate issue on
the TUI extractor). `reader.GetString()` is the standard, escape-resolving
way to read a JSON string with `Utf8JsonReader`. Using it here is not a
deliberate fix of that issue (the TUI extractor itself is untouched and its
issue remains open there) — it is simply the correct API for newly-written
extraction code; deliberately reimplementing the unresolved-escape behavior
to match `ExtractCell` would mean writing more fragile code to
intentionally reproduce a bug.

**`CellPresence.Missing` distinct from `CellPresence.Null`:** The writer
must be able to omit a property versus explicitly null it, or a sparse JSON
Lines input would gain a `null`-valued property for every output column it
didn't originally have — breaking the "true structural round-trip" goal.

**Per-cell heap-allocated `string`, not an `ArrayPool<char>`-backed buffer:**
A pooled-buffer version would make `GetCellData` effectively
zero-allocation, but requires buffer lifecycle management (`Return` on
dispose), an implicit "value valid only until the next `GetCellData` call"
constraint, and re-rent handling on overflow. None of that is needed to fix
the correctness bug, and the current baseline (`ExtractCell`) already
allocates a `string` per cell, so this is not a regression. Deferred as a
distinct performance investment (see "Out of Scope").

**Byte (JSON) and char (CSV) cell values unified on `ReadOnlySpan<char>`:**
Sep's public reader API (`SepReader.Row.Col.Span`) is `ReadOnlySpan<char>`
only — there is no byte-span variant to unify toward instead. Standardizing
on `char` keeps `CsvRecordReader`/`CsvRecordWriter` untouched on the type
level and keeps `RecordProcessor.ProcessAsync<TReader, TWriter>` a single
generic pipeline across all four format combinations. The cost is that
`JsonLinesRecordReader` must UTF-8-decode (byte → char) for every non-`Boolean`
value; there was no alternative that avoided this without giving up the
single generic pipeline (see "Alternatives Considered").

### Alternatives Considered

**A — Make `CsvRecordReader` byte-native (`ReadOnlySpan<byte>`) instead of
JSON Lines going char-native.** Rejected: Sep does not expose a byte-based
read API at all (byte spans only appear on the *write* side,
`SepWriter`). Not achievable without replacing the CSV library.

**B — Decode the whole JSON Lines line to `char` up front, then scan.**
Rejected: `Utf8JsonReader` is byte-only; there is no char-based
equivalent, so the scan itself would still operate on bytes regardless.
Worse, if the line contains non-ASCII characters, `Utf8JsonReader`'s byte
offsets stop lining up with the pre-decoded char buffer's offsets, making
this approach actively incorrect, not just redundant.

**C — Split CSV and JSON Lines into separate, non-unified pipelines
(abandon the single `ReadOnlySpan<char>` contract).** Rejected for this
fix: a JSON Lines → JSON Lines-only path could stay byte-native end to end
and skip the byte→char conversion entirely, but that would require a second
`RecordProcessor` entry point and a source-generator dispatch branch limited
to one format pair, in exchange for avoiding a conversion cost that has not
been measured. Kept as a possible future optimization, not adopted now
(`Utf8JsonWriter.WriteRawValue(ReadOnlySpan<byte>, bool)` exists, confirming
it would be technically possible later).

**D — `ArrayPool<char>` buffer reuse in `GetCellData` for zero-allocation
byte→char conversion.** Rejected for this fix; see rationale above. Revisit
first if a future benchmark shows the per-cell allocation is a real
bottleneck.

**E — Whole-value JSON-parse attempt in the writer to detect
Object/Array/Number (instead of the reader carrying `Encoding`).**
Considered and dropped: a CSV cell whose text happens to be syntactically
valid JSON (e.g. a CSV value of `123`) would be wrongly promoted to a JSON
number/object in JSON Lines output — a correctness regression, not a fix.

**F — Have `CsvRecordReader` normalize numeric `Value` at extraction time
(store the parsed `long`/`double`'s canonical string instead of the raw CSV
span) and set `Encoding = Raw`.** Rejected: `GetCellData` is called once
per cell and its `CellData` is consumed by whichever writer is active.
Normalizing `Value` would fix JSON Lines output but corrupt CSV → CSV output
(e.g. an input CSV cell `TRUE` or `007` would come back out as `True` or
`7`, since there is no way to hand back two different representations to
two different writers from one call). CSV's `Value` must stay the original
span unconditionally to preserve CSV → CSV fidelity; only the *decision of
how to write it* may vary by target format, which is exactly what
`Encoding` (interpreted only by the JSON Lines writer) achieves.

**G — Tighten the CSV numeric heuristic itself to strict JSON-number
grammar (reject leading zeros, `+`, thousands separators, parentheses) and
set `Encoding = Raw` from that stricter check.** Rejected: this would
change classification for those edge-case inputs from `Numeric` to
`PlainText`, which changes CSV → JSON Lines output for them (e.g. `1,234`
would become the JSON string `"1,234"` instead of today's normalized JSON
number `1234`) — a needless behavior change when the simpler fix (keeping
`Numeric` and its re-parse step) reaches the identical
`WriteNumberValue`-normalized output that CSV → JSON Lines already produces
today, with no grammar-tightening required.

### Consequences

- `CellPresence`/`CellEncoding`/`CellData` are internal to `src/App/Cli/IO/`;
  TUI code paths (`ExtractCell` and its callers) and the Engine-wide
  `ColumnType`/`TypeInferrer` vocabulary are completely unaffected.
- The `"<null>"`/`"<error>"` sentinel convention is removed from the batch
  write path; a JSON string cell whose literal content is `<null>` or
  `<error>` is no longer misclassified.
- JSON Lines → JSON Lines becomes a genuine structural round-trip for
  Object/Array/Number/String values, including large integers and exponent
  notation, and including properties that were absent rather than `null`;
  a JSON string is never reinterpreted as a number/boolean regardless of its
  text content; JSON Lines → CSV places the exact source JSON text into the
  CSV cell for Object/Array columns instead of a display preview.
- As an accepted side effect (not a goal in itself), CLI batch output for
  JSON string values with escape sequences is now correct
  (`reader.GetString()`); the TUI display path (`JsonObjectCellExtractor`)
  is untouched and its equivalent unresolved-escape issue remains open
  there.
- CSV cell classification (`CellEncodingClassifier`) now runs on every
  read regardless of output format, not only when writing JSON Lines as
  today. No output behavior changes; the cost is a per-cell classification
  pass that is wasted work for CSV → CSV.
- Per-cell `string` allocation for JSON Lines Number/Object/Array/String
  values is unchanged from the current baseline (not a regression); a
  follow-up `ArrayPool<char>`-based optimization is possible but out of
  scope here.
- `RecordProcessor`'s generic `ProcessAsync<TReader, TWriter>` pipeline,
  and the `FormatDispatcherGenerator`-generated dispatch matrix, are
  unaffected in shape — only the per-cell payload type changes.
- `IRecordReader`/`IRecordWriter` implementations remain single-consumer,
  sequential-access structs (as today): a `CellData` returned by
  `GetCellData` borrows from state owned by that specific reader/writer
  instance (a GC-managed `string`, or a buffer the writer itself owns) and
  is only valid for the duration of the current record; instances are not
  safe to share across concurrent calls, matching the existing sequential
  `RecordProcessor.ProcessAsync` contract.

### Test Plan

In addition to reader/writer unit tests for each `CellPresence`/`CellEncoding`
combination (including `CellEncodingClassifier` unit tests for
`007`/`1,234`/`TRUE`/plain text), `RunnerTests.cs` end-to-end cases (JSON
Lines input, asserted by parsing output with `JsonDocument` for JSON Lines
targets and by raw cell-text comparison for CSV targets) must cover:

- A nested Object and a nested Array value, including non-ASCII characters.
- `1.50` (trailing zero preserved), `1e10` (exponent), and an integer beyond
  `Int64` range — all three must round-trip as JSON numbers, not strings.
  Assert on `JsonElement.GetRawText()` equaling the exact original literal,
  not just `ValueKind == JsonValueKind.Number` — numeric accessors (e.g.
  `GetDouble()`) normalize the value and cannot prove the original lexical
  representation (trailing zero, exponent form) survived.
- A JSON string value containing JSON escape sequences (e.g. embedded quotes
  or `\n`), asserting the resolved text, not the raw escape sequence.
- A JSON string value whose content looks numeric/boolean (`"5"`, `"true"`)
  — must round-trip as a JSON string, not a number/boolean.
- A string value whose literal content is exactly `<null>` or `<error>`
  (must be written as that literal string, not treated as null/invalid).
- A property that is missing from the source line versus one explicitly set
  to `null` (must be distinguishable in the output).
- CSV `007` → JSON number `7` (existing behavior, regression guard — see
  "Out of Scope").
- A `FillSpec`/`TimestampFormatSpec`-transformed column whose output text
  looks numeric (e.g. a numeric fill default) still round-trips as a JSON
  number, matching current behavior.

### Out of Scope

Filed as separate issues, not addressed by this change:

- TableSchema pre-scan limited to the first 200 rows/lines.
- Filter/FormatTimestamp trusting a single type resolved at pre-scan time.
- Whether to unify `WholeNumber`/`FloatingPoint` into a single `Number` type
  in `ColumnType` (unaffected by this design either way, since `ColumnType`
  is not used here).
- CSV's permissive numeric grammar (`007`, `1,234`, `(5)`) being normalized
  into different JSON number text (`7`, `1234`, `-5`) rather than preserved
  verbatim or rejected as text — this is existing, unchanged behavior (see
  Alternatives F and G), the same class of representation-loss limitation
  as `JsonObjectCellExtractor.FormatNumber`'s known numeric-reformatting
  bug. A future column-level output-encoding override (e.g. a
  `CastColumnAction` extension or a new `CellTransformSpec` subtype to
  force a column to always be emitted as text) is a possible follow-up but
  is not designed or implemented here.
- Sep's `Unescape` option left `false`, so quoted-empty CSV cells (`,"",`)
  retain their quote characters as literal text.
- `JsonObjectCellExtractor.FormatValue`'s unresolved-escape bug (TUI path;
  left as-is deliberately — this fix does not touch `ExtractCell`, and only
  incidentally avoids the same symptom in the unrelated CLI batch code path
  it replaces).
- `JsonObjectCellExtractor.FormatNumber`'s numeric-reformatting bug (TUI
  path; same reasoning).
- A JSON Lines → JSON Lines byte-native pipeline that skips the byte→char
  conversion entirely (see Alternative C) — unlikely to be pursued, since
  `ArrayPool<char>` pooling alone can reach zero-allocation without
  splitting the pipeline.
