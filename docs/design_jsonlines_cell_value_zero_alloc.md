# Design: JSON Lines Cell Value Zero-Allocation

## In Scope

- Replace the per-cell heap-allocated `string` in
  `JsonLinesRecordReader.GetCellData`/`ReadPropertyValue` (Number, Object,
  Array, String branches) with an `ArrayPool<char>`-backed buffer reused
  across calls, using `Encoding.UTF8.GetChars` (Number/Object/Array) and
  `Utf8JsonReader.CopyString` (String).
- Buffer lifecycle management: lazy rent on first use, grow-and-return-old
  on overflow (rent at least `Math.Max(MinimumSize, minimumLength)`, where
  `MinimumSize` is 256 chars), and `Return` in `Dispose()`.
- Formalize the resulting "`CellData.Value` is valid only until the next
  `GetCellData` call" contract in XML documentation (`CellData.cs` and/or
  `IRecordReader.GetCellData`). Existing call sites
  (`RecordProcessor`, `JsonLinesRecordWriter`, `CsvRecordWriter`) already
  consume the value synchronously and immediately, so this is a
  documentation change backed by an already-verified invariant, not a
  behavior change to those call sites.
- Unit tests covering buffer reuse across consecutive cells in the same
  row, growth beyond the initial buffer size, escape-sequence resolution,
  and the empty-string boundary.
- A `BenchmarkDotNet` benchmark (`[MemoryDiagnoser]`), following the
  existing `JsonObjectCellExtractorBenchmarks.cs` pattern, to measure the
  resulting allocation count for `GetCellData`.

## Out of Scope

- **`CsvRecordReader`/`CsvRecordWriter`**: unchanged. Sep already returns
  `ReadOnlySpan<char>` directly with no per-cell allocation; the issue this
  design addresses is JSON Lines–specific.
- **`JsonLinesRecordWriter`**: unchanged. It only consumes `CellData.Value`
  and has no reason to distinguish a pooled-buffer-backed span from a
  `string`-backed one.
- **`Boolean`/`Null`/`Missing`/`Invalid` branches of `GetCellData`**:
  unchanged. These already carry no per-cell allocation today.
- **`JsonObjectCellExtractor`/`JsonByteExtractor`** (shared Engine-layer
  code also used by the TUI table/tree views): unchanged.
  `JsonByteExtractor.ExtractValueBytes` was confirmed to already be
  allocation-free (it only slices `ReadOnlyMemory<byte>`), so it needs no
  changes for this design to reach zero allocation. The known, separately
  tracked TUI-side bugs in `FormatValue`/`FormatNumber` are not addressed
  here.
- **`RecordProcessor`**: unchanged. `CellData`'s shape is not changing, so
  no caller-side code needs to change.
- **`ColumnType`/`TypeInferrer`**: unrelated, unchanged.
- **`IRecordReader`/`IRecordWriter` interface signatures**: unchanged.
  `GetCellData` still returns `CellData`; only the implementing struct's
  `readonly` modifier changes.
- **A byte-native JSON Lines → JSON Lines pipeline** (Alternative C in
  `docs/design_batch_cell_typed_channel.md`): still not pursued.
  `ArrayPool<char>` pooling alone reaches zero allocation without the added
  complexity of a second, format-pair-specific pipeline.
- **Wiring the new benchmark into CI**: it is a manually-run diagnostic,
  matching every other `BenchmarkDotNet` class already in this repository.

---

## Files Changed

| File | Change |
|------|--------|
| `src/App/Cli/JsonLinesRecordReader.cs` | Main change — see "Implementation Approach" below |
| `src/App/Cli/IRecordReader.cs` | XML doc update only: document on `GetCellData` that the returned `CellData.Value` is valid only until the next `GetCellData` call, and becomes invalid once the reader is `Dispose()`d |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.cs` | Add cases for buffer reuse across cells, growth beyond the initial size, escape-sequence resolution, and the empty-string boundary |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderBenchmarks.cs` | **New.** `[MemoryDiagnoser]` benchmark for `GetCellData`, following the `JsonObjectCellExtractorBenchmarks.cs` pattern |

No changes to `CsvRecordReader.cs`, `CsvRecordWriter.cs`, `JsonLinesRecordWriter.cs`, `CellData.cs`, `IRecordWriter.cs`, `RecordProcessor.cs`, `JsonByteExtractor.cs`, `JsonObjectCellExtractor.cs`, `ColumnType.cs`, or `TypeInferrer.cs` — see "Out of Scope" above.

---

## Implementation Approach

### 1. `PooledValueBuffer`: a reference-type wrapper around the pooled buffer

`JsonLinesRecordReader` is a `struct` implementing `IRecordReader`, and
`RecordProcessor.ProcessAsync<TReader, TWriter>` (`where TReader : struct,
IRecordReader`) takes it **by value**. The generated dispatcher does:

```csharp
using var reader = await readerFactory.CreateAsync(...);
return await RecordProcessor.ProcessAsync<TReader, TWriter>(reader, writer, ...);
```

The `reader` passed into `ProcessAsync` is a **copy** of the dispatcher's
`reader`. `Dispose()` is only ever called on the dispatcher's original (the
`using var reader`) — never on the copy that `ProcessAsync` actually uses to
call `GetCellData` in its loop.

A plain `char[]?` field directly on `JsonLinesRecordReader` would break
under this copying: each struct copy has its own independent field slot, so
the copy inside `ProcessAsync` would rent a buffer into its own slot, while
the copy that gets `Dispose()`d (the dispatcher's original) has a slot that
was never touched. The actually-rented array would never be returned to
`ArrayPool<char>.Shared` — a real leak. (This was flagged in code review
against an earlier draft of this design that used exactly that plain-field
shape.)

The existing `_rowReader` field already avoids this problem for a different
piece of state: it's a **reference-type** field (`RowReader?`), assigned
once in the constructor. Every struct copy's `_rowReader` field holds a
reference to the *same* `RowReader` instance, so it doesn't matter which
copy's `Dispose()` runs — they all release the same underlying object.
`PooledValueBuffer` applies the identical pattern to the pooled buffer:
wrapping it in a small reference type, assigned once in the constructor, so
every struct copy shares the same buffer identity and the same disposal
responsibility.

(An intermediate alternative — changing `ProcessAsync`'s `reader` parameter
to `ref TReader` to avoid the copy at its source, rather than working around
it — was considered and ruled out: `ProcessAsync` is `async` and `await`s
inside a `while` loop, and C# does not allow `ref`/`out`/`in` parameters on
`async` methods, since the compiler cannot keep a byref alive across an
`await` suspension point. This is a hard language constraint, not a design
choice. See "Decision Record" for the other alternatives considered.)

```csharp
private sealed class PooledValueBuffer : IDisposable
{
    private const int MinimumSize = 256;

    private char[]? _buffer;
    private bool _disposed;

    public char[] Reserve(int minimumLength)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_buffer is not null && _buffer.Length >= minimumLength)
        {
            return _buffer;
        }

        if (_buffer is not null)
        {
            ArrayPool<char>.Shared.Return(_buffer);
        }

        _buffer = ArrayPool<char>.Shared.Rent(Math.Max(MinimumSize, minimumLength));
        return _buffer;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer is not null)
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = null;
        }

        _disposed = true;
    }
}

private readonly PooledValueBuffer _valueBuffer = new();
```

`Reserve` is the only access path to the wrapped `char[]` — the backing
array stays `private` to `PooledValueBuffer`. The wrapper tracks its own
`_disposed` flag rather than relying on `JsonLinesRecordReader`'s: since any
struct copy could in principle call `Dispose()`, the wrapper's idempotency
must not depend on which copy calls it or in what order. `Reserve` throws
`ObjectDisposedException` if already disposed, so a rent after disposal
fails loudly instead of silently renting a buffer nothing will ever return.

`minimumLength` is always sized as an upper bound on the resulting char
count *before* the write happens (UTF-8 byte count for Number/Object/Array,
raw escaped byte count for String — see step 2), so the underlying
`Encoding.UTF8.GetChars`/`Utf8JsonReader.CopyString` call never hits its
buffer-too-small path. `Reserve` is therefore the only place buffer sizing
decisions are made; no exception-driven retry is needed (consistent with
the project's "no exceptions for flow control" rule).

### 2. Per-token-type extraction helpers, replacing `ReadPropertyValue`'s inline expressions

`ReadPropertyValue` changes from `private static` to a `private` instance
method (still taking `Utf8JsonReader reader` by value — the existing
comment explaining that choice, and the `S1541`/`CS8168`/`CS8347` reasoning
behind it, remains valid and unchanged; only the enclosing method stops
being `static` so it can reach `_valueBuffer` through `this`). Its `Number`
and `StartObject`/`StartArray`/`String` arms are extracted into three small
instance helpers so the switch expression stays a single dispatch and each
helper stays under the nesting/complexity limits:

```csharp
private readonly CellData NumberToCellData(Utf8JsonReader reader)
{
    var bytes = reader.ValueSpan;
    var buffer = _valueBuffer.Reserve(bytes.Length);
    var charsWritten = Encoding.UTF8.GetChars(bytes, buffer);
    return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
}

private readonly CellData ObjectOrArrayToCellData(Utf8JsonReader reader, JsonRawBytes containingBytes)
{
    var bytes = JsonByteExtractor.ExtractValueBytes(ref reader, containingBytes).Span;
    var buffer = _valueBuffer.Reserve(bytes.Length);
    var charsWritten = Encoding.UTF8.GetChars(bytes, buffer);
    return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
}

private readonly CellData StringToCellData(Utf8JsonReader reader)
{
    var buffer = _valueBuffer.Reserve(reader.ValueSpan.Length);
    var charsWritten = reader.CopyString(buffer);
    return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.PlainText);
}
```

`reader.ValueSpan.Length` (raw UTF-8 byte count) is a safe upper bound for
the decoded char count in both cases: a multi-byte UTF-8 sequence always
decodes to fewer chars than its byte count, and every JSON escape sequence
(`\n`, `\uXXXX`, …) occupies more source bytes than the character(s) it
resolves to. `ReadPropertyValue`'s switch expression then becomes:

```csharp
private readonly CellData ReadPropertyValue(Utf8JsonReader reader, JsonRawBytes containingBytes)
{
    return reader.TokenType switch
    {
        JsonTokenType.Null => new CellData([], CellPresence.Null),
        JsonTokenType.Number => NumberToCellData(reader),
        JsonTokenType.StartObject or JsonTokenType.StartArray => ObjectOrArrayToCellData(reader, containingBytes),
        JsonTokenType.String => StringToCellData(reader),
        JsonTokenType.True => new CellData("true", CellPresence.Value, CellEncoding.Boolean),
        JsonTokenType.False => new CellData("false", CellPresence.Value, CellEncoding.Boolean),
        _ => new CellData([], CellPresence.Invalid),
    };
}
```

### 3. `GetCellData` keeps its `readonly` modifier

Unlike an earlier draft of this design (which used a plain `char[]?` field
reassigned directly on the struct, and so needed to drop `readonly` from
`GetCellData`), `_valueBuffer` here is a `readonly PooledValueBuffer` field
— assigned once via its field initializer, never reassigned. Calling
`_valueBuffer.Reserve(...)` from inside a `readonly` struct member is legal:
`readonly` on a struct method only forbids reassigning the struct's *own*
fields, not calling mutating methods on the reference-type objects those
fields point to (a `readonly` struct method invoking a mutating method
through a `readonly` reference-type field was confirmed to compile cleanly).
`GetCellData`, `ReadPropertyValue`, and the three new helper methods
(`NumberToCellData`/`ObjectOrArrayToCellData`/`StringToCellData`) are all
`readonly`. This mirrors why `EvaluateFilters`/`ThrowIfDisposed` are already
`readonly` today despite the struct holding other mutable reference-type
state (`_rowReader`).

### 4. `Dispose()`

```csharp
public void Dispose()
{
    if (_disposed)
    {
        return;
    }

    _rowReader?.Dispose();
    _rowReader = null;

    _valueBuffer.Dispose();

    _disposed = true;
}
```

### 5. `GetCellData` documentation

Add an XML doc remark on `IRecordReader.GetCellData` — moved here from
`CellData.Value` per code review, since this is a contract of the *reader*
implementation, not of the data struct itself — stating that the returned
`CellData.Value` span is valid only until the reader's next `GetCellData`
call, and becomes invalid once the reader is `Dispose()`d. This validity
guarantee must hold for every `IRecordReader` implementation, so the
interface doc says nothing about how the storage is backed —
`CsvRecordReader` returns Sep-provided spans directly and never pools
anything.

The `ArrayPool<char>` detail — the buffer is pooled and reused, not
freshly allocated per cell — is `JsonLinesRecordReader`-specific and goes
on that struct's own XML doc (or `_valueBuffer`'s), not on the interface
member.

---

## Decision Record

### Rationale

**`ArrayPool<char>` buffer reuse, not a per-cell `string`:** This is
exactly the performance investment `docs/design_batch_cell_typed_channel.md`
deferred ("Alternative D" in its Decision Record) rather than bundling into
a correctness fix. That design's stated blockers — buffer lifecycle
management, the "valid only until next call" constraint, and re-rent
handling on overflow — are what this design resolves.

**`Utf8JsonReader.CopyString`, not a hand-rolled unescape:** `CopyString`
is the standard, escape-resolving API for copying a JSON string's decoded
characters into a caller-owned buffer without allocating (confirmed via a
scratch program: it returns the number of characters written as `int`, and
throws `ArgumentException` if the destination is too small). Re-implementing
JSON string unescaping by hand would be strictly worse: more code, and a new
opportunity to diverge from `Utf8JsonReader`'s own (already-correct)
unescaping behavior.

**Pre-sized buffers, not catch-and-retry on `ArgumentException`:** Both
`Encoding.UTF8.GetChars` and `Utf8JsonReader.CopyString` throw when the
destination span is too small rather than reporting a required size. Sizing
the buffer to an upper bound (`reader.ValueSpan.Length`, confirmed always
`>=` the eventual char count for both UTF-8 decoding and JSON-escape
resolution) before writing avoids ever depending on that exception path,
consistent with the project's rule against using exceptions for flow
control.

**`ReadPropertyValue` becomes an instance method:** it needs `this` to
reach `_valueBuffer`. The existing comment on this method explains why
`Utf8JsonReader` is taken *by value* rather than `ref` (a `ref
Utf8JsonReader` parameter makes the ref struct return value
ref-safety-inferred as escaping through that parameter, which fails to
compile — `CS8168`/`CS8347`). That reasoning is about the parameter's own
by-value-vs-by-ref shape, not about whether the method is `static`, so it is
unaffected by this change and the comment does not need to be rewritten.

**Splitting `ReadPropertyValue`'s switch arms into three helper methods:**
each arm now needs two statements (reserve the buffer, then decode) instead
of one expression, which no longer fits cleanly inside a switch
*expression* arm. Extracting
`NumberToCellData`/`ObjectOrArrayToCellData`/`StringToCellData` keeps the
switch itself a flat, single-expression dispatch (unchanged structure from
today) rather than converting it to a switch *statement* with a nested body
per branch.

**Why `PooledValueBuffer` (a reference-type wrapper), not a plain `char[]?`
field:** see "Implementation Approach", step 1, for the full mechanics. In
short: `JsonLinesRecordReader` is copied by value into
`RecordProcessor.ProcessAsync`, and only the dispatcher's original copy is
ever `Dispose()`d. A plain value-type field would let the `ProcessAsync`-side
copy rent a buffer that nothing ever returns to the pool. Wrapping the
buffer in a reference type, constructed once, gives every copy of the
struct a shared identity for that state — the same technique the existing
`_rowReader` field already relies on.

**Why not `ref TReader` in `ProcessAsync` instead:** this would remove the
copy at its source rather than working around it, but `ProcessAsync` is
`async` and `await`s inside a loop; C# does not allow `ref`/`out`/`in`
parameters on `async` methods. Not viable regardless of preference.

**Why not the other two alternatives raised in review — moving disposal
ownership into `RecordProcessor`, or converting `JsonLinesRecordReader` to a
`class`:** moving disposal ownership to `RecordProcessor` requires changing
`RecordProcessor.cs`, which breaks the Out-of-Scope boundary already agreed
for this design. Converting the struct to a `class` removes the copy
problem at its root, but affects every assumption downstream of `TReader :
struct` across the batch pipeline — far beyond this issue's allocation-
reduction scope.

**Trade-offs accepted with `PooledValueBuffer`:**
- It is itself a heap allocation — one per `JsonLinesRecordReader`
  instance, paid once at construction. This does not reintroduce the
  per-cell allocation this design removes, but it means the change trades
  "many small allocations" for "one small allocation plus one wrapper
  object," not literally zero allocations across the reader's lifetime.
- `GetCellData` gains one extra pointer indirection per call
  (`_valueBuffer.Reserve(...)` instead of a direct field read), negligible
  next to the `string` allocation being removed.
- This fix is scoped to `_valueBuffer` specifically. It does not prevent a
  future change from adding another plain value-type field to
  `JsonLinesRecordReader` that needs its own disposal and reintroducing the
  same bug class; only converting the struct to a class would close that
  off structurally, which is out of scope here.

**Why `Reserve`, not `EnsureCapacity` or `Rent`:** `EnsureCapacity` is
already a BCL naming convention (`List<T>.EnsureCapacity`,
`StringBuilder.EnsureCapacity`) whose return value is the resulting `int`
capacity, not the buffer itself — this method's shape doesn't match that
convention and would be misleading. `Rent` matches `ArrayPool<T>.Rent`'s
shape (`int -> T[]`) but implies a fresh rent on every call, which is
misleading here since the method reuses the existing buffer whenever it's
already large enough. `Reserve` was chosen to read as "secure and hand back
a buffer of at least this size" without either implication. `Arrange` was
also considered and rejected: this project's test methodology (`.claude/rules/testing.md`)
uses `// Arrange` as a fixed vocabulary term for the AAA pattern, and reusing
it as a production method name in the same codebase would be confusing.

**Why `PooledValueBuffer` owns its own `_disposed` flag, rather than
relying on `JsonLinesRecordReader`'s:** any struct copy could in principle
call `Dispose()` (the same reasoning that motivates the wrapper's existence
in the first place), so the wrapper's idempotency must not depend on which
copy calls it, or on the calling struct's own disposal bookkeeping.

**Why no `BitOperations.RoundUpToPowerOf2`:** an earlier draft rounded the
requested size up to the next power of two before calling
`ArrayPool<char>.Shared.Rent`. `RoundUpToPowerOf2(uint)` returns `0` when
the input exceeds the largest representable power of two, which would turn
an extremely large token into an invalid `Rent(0)` call. `Rent` already
buckets its internal pools by power-of-two size, so rounding up before
calling it is redundant — `ArrayPool<char>.Shared.Rent(Math.Max(MinimumSize,
minimumLength))` achieves the same doubling-growth behavior without the
overflow risk.

### Consequences

- `GetCellData`'s output is unchanged for every existing test case; only
  the backing storage of `CellData.Value` changes (pooled buffer instead of
  an independently-GC-managed `string`).
- `CellData.Value`'s "valid only until the next `GetCellData` call"
  constraint moves from an accepted-but-inert observation (in
  `docs/design_batch_cell_typed_channel.md`'s Consequences — each `string`
  was independently valid regardless of subsequent calls, so nothing could
  actually break it) to a load-bearing invariant: reusing the same buffer
  means a stale `CellData` handed to a caller that violates the contract
  would observe corrupted data. All current call sites were audited and
  consume the value synchronously before the next call, so this is not a
  behavior change today, but it is now something a future change to
  `RecordProcessor` or a writer must not break — hence documenting it
  explicitly (see "Implementation Approach", step 5).
- `JsonLinesRecordReader` remains a single-consumer, sequential-access
  struct, matching its existing contract; the pooled buffer does not change
  its thread-safety posture (it was never safe to share across concurrent
  calls).
- One rented `char[]` buffer lives for the lifetime of a `JsonLinesRecordReader`
  instance (from first non-`Boolean`/`Null` cell read until `Dispose()`),
  instead of a fresh `string` per cell being independently collected by the
  GC. This trades a bounded, reused allocation for an unbounded stream of
  small ones — the intended effect of this change.
- One `PooledValueBuffer` object is allocated per `JsonLinesRecordReader`
  instance, once, at construction — see "Trade-offs accepted with
  `PooledValueBuffer`" above.
- `GetCellData` and its new helper methods stay `readonly`; no existing
  caller or interface signature changes as a result of this design (see
  "Implementation Approach", step 3).

### Test Plan

In addition to `JsonLinesRecordReaderTests.cs`'s existing per-token-type
coverage (unaffected — same expected `Value`/`Presence`/`Encoding` per
case), add:

- Reading two or more columns from the same row in sequence, asserting each
  `CellData.Value.ToString()` is correct at the point it's read — the
  actual `RecordProcessor` consumption pattern, exercised directly against
  buffer reuse.
- A String value long enough to force growth past the initial 256-char
  buffer, asserting the grown buffer still produces the correct value.
- A JSON string containing escape sequences (e.g. an embedded quote and a
  `\n`), asserting the resolved text, not the raw escaped source.
- An empty JSON string (`""`), asserting `PooledValueBuffer.Reserve`'s
  `Math.Max(MinimumSize, minimumLength)` floor is exercised without error.
- Copying the reader (`var copy = reader;`), disposing the *original*, then
  calling `GetCellData` on the *copy* — the copy's own `_disposed` field is
  still `false` (it's a separate struct copy), so `JsonLinesRecordReader`'s
  own `ThrowIfDisposed()` does not catch this; the call must still throw
  `ObjectDisposedException` via `PooledValueBuffer.Reserve`'s own guard,
  since the *shared* `PooledValueBuffer` was disposed by the original.
  (Calling `GetCellData` after `Dispose()` on the *same* instance is not a
  sufficient test here — that path is already caught by
  `JsonLinesRecordReader.ThrowIfDisposed()` before `Reserve` is ever
  reached.)
- A zero-allocation regression test: call `GetCellData` once to warm up the
  buffer (forcing the initial `Reserve`), snapshot
  `GC.GetAllocatedBytesForCurrentThread()`, call `GetCellData` again for a
  Number/Object/Array/String cell, and assert the snapshot is unchanged.
- The new `JsonLinesRecordReaderBenchmarks.cs`: representative Number,
  String, Object, and Array columns benchmarked with `[MemoryDiagnoser]`,
  confirming zero managed allocation after warm-up for `GetCellData`.
  Benchmark methods return a scalar (e.g. `cell.Value.Length`) rather than
  `CellData` itself, since `CellData` is a `ref struct` and BenchmarkDotNet
  benchmark methods cannot return `ref struct` types. `[GlobalSetup]` warms
  up the reader (forcing the initial `Reserve`) before measurement begins,
  so measured `Allocated` reflects steady-state per-cell cost, not
  first-call setup cost. `[GlobalCleanup]` calls the benchmark reader's
  `Dispose()` and cleans up its temporary input file — `Dispose()` is the
  only path that returns the rented `char[]` to `ArrayPool<char>.Shared`,
  so skipping cleanup would permanently remove each instance's warmed-up
  buffer from the shared pool for the process lifetime.
