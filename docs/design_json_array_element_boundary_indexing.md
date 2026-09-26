# JSON Array Element Boundary Indexing — Design Document

## Scope
Implement element boundary indexing for JSON Array format (`[{...}, {...}, ...]`).
The design mirrors `JsonLines.RowIndexer` as closely as possible; the only structural
difference is that element boundaries are detected with `Utf8JsonReader` instead of
scanning for `\n` bytes.

---

## 1. Requirements

### Functional Requirements
- Index each top-level element of a JSON Array file by its byte offset (checkpoint-based)
- Support background indexing while unblocking display after the first checkpoint
- Provide the same `IRowIndexer` contract as `JsonLines.RowIndexer`, enabling reuse of
  `SlidingWindowLruCache` and the rest of the viewer pipeline without modification

### Non-Functional Requirements
- **Zero allocations** during the indexing hot path (`ArrayPool<byte>` for buffers)
- **Native AOT compatible** (`Utf8JsonReader` is AOT-safe; no reflection)
- **Thread-safe** checkpoint reads during background indexing
- **Streaming**: fixed-size rolling buffer; file is never fully loaded into memory

---

## 2. Format Characteristics

### Comparison with JSON Lines

| Aspect | JSON Lines | JSON Array |
|--------|-----------|------------|
| Root structure | Sequence of top-level values | Single root `[...]` |
| Element boundary | `\n` byte | `Utf8JsonReader` token at depth 1 |
| Self-contained rows | Yes — each line is independent | No — structural context required |
| Buffer strategy | Fixed sequential reads | Rolling window (elements may span buffer boundaries) |

### Why `Utf8JsonReader` is required
A `\n` byte inside a JSON string is always escaped (`\n`), so a literal newline is a
reliable boundary. In JSON Array files, there is no single-byte boundary marker;
elements may span multiple lines and the only safe way to find their end is to parse
the JSON structure.

---

## 3. Design

### 3.1 Class: `JsonArray.RowIndexer`

```csharp
namespace Refedle.Engine.IO.JsonArray;

public sealed class RowIndexer : RowIndexerBase
{
    private const int BufferSize = 1024 * 1024;   // 1 MB — same as JsonLines
    private const int CheckPointInterval = 1000;   // same as JsonLines

    public RowIndexer(string filePath);

    // IRowIndexer — identical contract to JsonLines.RowIndexer
    public override string FilePath { get; }
    public override long TotalRows { get; }        // element count
    public override long BytesRead { get; }
    public override void BuildIndex(CancellationToken ct = default);
    public override (long byteOffset, int rowOffset) GetCheckPoint(long targetRow);
}
```

`TotalRows` semantically maps to "total elements indexed so far." Callers (including
`SlidingWindowLruCache`) are unaware of the format difference.

### 3.2 Checkpoint Strategy

Identical to `JsonLines.RowIndexer`:

| Trigger | Action |
|---------|--------|
| First element found | Set `_checkpoints[0]` to its byte offset |
| Every 1000th element | Add checkpoint; update `TotalRows`; fire `FirstCheckpointReached` / `ProgressChanged` |
| End of file | Finalise `TotalRows`; fire `FirstCheckpointReached` (guard for small files); fire `BuildIndexCompleted` |

`_checkpoints` stores the byte offset of the element that starts each interval
(element 0, 1000, 2000, …). `GetCheckPoint(n)` returns
`(_checkpoints[n / 1000], (int)(n % 1000))` — same arithmetic as JsonLines.

**Out-of-range clamping**: when `n / 1000 >= _checkpoints.Count`, the ideal
checkpoint index is clamped to `_checkpoints.Count - 1`. The `rowOffset` is then
computed as `(int)(n - clampedIndex * 1000)` (not `n % 1000`), which preserves the
full distance from the last available checkpoint. This ensures that the caller can
still seek forward from the checkpoint to the target element (or detect EOF when the
file has fewer elements than requested).

Unlike `JsonLines.RowIndexer` which pre-seeds `_checkpoints = [0]` (the file starts
with the first row), `JsonArray.RowIndexer` initialises `_checkpoints` as empty and
populates index 0 when element 0 is found. `GetCheckPoint` returns `(-1, 0)` when
the list is empty; `SlidingWindowLruCache.Prefetch` already guards against negative
offsets with `if (byteOffset < 0) return`.

**Required implementation detail**: `GetCheckPoint` must include an explicit empty-list
guard as the first statement to prevent `IndexOutOfRangeException` when called before
any element is indexed:

```csharp
if (_checkpoints.Count == 0) return (-1L, 0);
```

### 3.3 Rolling Buffer Algorithm

`Utf8JsonReader` requires a contiguous `ReadOnlySpan<byte>`. To handle elements that
span buffer boundaries, unconsumed bytes are compacted to the front of the buffer
before each refill:

```
After reader pass:
  [####consumed####][---remaining---][ empty ]
   0                ^consumed        ^dataEnd

After compaction + refill:
  [---remaining---][---new data---][ empty ]
   0                ^remainingLen   ^newDataEnd
```

`bufferOriginFileOffset` tracks the file position corresponding to `buffer[0]` and
is advanced by `reader.BytesConsumed` after each pass. The absolute byte offset of
any token is `bufferOriginFileOffset + reader.TokenStartIndex`.

`JsonReaderState` is preserved across buffer refills so that the reader correctly
maintains depth tracking through the root `[...]`.

### 3.4 Element Detection

```
Depth 0: before / after the root `[`
Depth 1: top-level elements of the root array  ← element starts recorded here
Depth 2+: interior of an element
```

Element boundaries are detected with `Read()` and explicit depth tracking rather than
`TrySkip()`. This allows any element to span multiple buffer fills regardless of its
total size, because only one token at a time needs to fit in the buffer.

Token classification at depth 1:

1. `reader.Read()` returns `false` → more data needed; save `JsonReaderState`,
   compact the buffer, and refill. `currentElementStart` (if already set) persists
   across the refill because it is declared outside the inner loop.
2. `EndArray` at depth 0 → root array is complete; exit.
3. Tokens at depth > 1 → interior of a structured element; skip (`continue`).
4. `StartObject` / `StartArray` at depth 1 → record `currentElementStart` and
   continue reading. The element is complete when the matching `EndObject` /
   `EndArray` returns the reader to depth 1.
5. `EndObject` / `EndArray` at depth 1 → the structured element is complete;
   record it and clear `currentElementStart`.
6. Primitive tokens (String, Number, True, False, Null) at depth 1 → the token
   itself is a complete element; record it immediately.

**Oversized string detection**: if `reader.Read()` returns `false` and
`reader.BytesConsumed == 0` while the buffer is already full (`remainingLen ==
BufferSize`), the current token exceeds the buffer capacity and no progress is
possible. This can only happen for string values larger than 1 MB. In that case,
throw `NotSupportedException("JSON string value exceeds maximum supported size.")`,
mirroring the naming style of `JsonLines.RowReader`.

### 3.5 Thread Safety

Identical model to `JsonLines.RowIndexer`:

| Member | Mechanism |
|--------|-----------|
| `_checkpoints` writes | `Lock` |
| `_checkpoints` reads (`GetCheckPoint`) | `Lock` |
| `TotalRows`, `BytesRead` | `Interlocked` |
| Event invocations | Fired from `BuildIndex` thread; subscribers marshal if needed |

### 3.6 Pipeline Integration

`JsonArray.RowIndexer` implements `IRowIndexer`, so the entire existing pipeline
works unchanged:

```
JsonArray.RowIndexer  (this issue)
        ↓
SlidingWindowLruCache  (no changes)
        ↓
ElementByteCache  (future — mirrors RowByteCache, uses ElementReader)
        ↓
TableView / TUI
```

`RowIndexerFactory.Create(DataFormat.JsonArray)` is updated in this issue to return a
`new JsonArray.RowIndexer(filePath)`.

---

## 4. Implementation Plan

### 4.1 Core Algorithm (Pseudocode)

```
fileSize = FileInfo(filePath).Length
buffer = ArrayPool.Rent(BufferSize)
try:
  FileSize = fileSize          // assign base class property (consumed by OnProgressChanged)
  using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
  state = default(JsonReaderState)
  bufferOriginFileOffset = 0L
  fileReadOffset = 0L
  remainingLen = 0
  elementCount = 0L
  currentElementStart = -1L    // -1 = not currently inside an element

loop:
  ct.ThrowIfCancellationRequested()

  // Detect stuck: buffer full and nothing consumable (oversized string token)
  if remainingLen == BufferSize:
    throw NotSupportedException("JSON string value exceeds maximum supported size.")

  bytesRead = RandomAccess.Read(handle, buffer[remainingLen..], fileReadOffset)
  if bytesRead == 0: break
  fileReadOffset += bytesRead
  dataEnd = remainingLen + bytesRead
  isFinalBlock = (fileReadOffset >= fileSize)
  Interlocked.Add(ref _bytesRead, bytesRead)

  reader = new Utf8JsonReader(buffer[0..dataEnd], isFinalBlock, state)

  innerLoop:
    if not reader.Read(): break innerLoop

    if reader.CurrentDepth == 0 && reader.TokenType == EndArray: goto done
    if reader.CurrentDepth != 1: continue innerLoop   // skip root markers (depth 0) AND element interior (depth 2+)

    // --- depth 1 tokens ---

    if reader.TokenType in {StartObject, StartArray}:
      if currentElementStart < 0:
        currentElementStart = bufferOriginFileOffset + reader.TokenStartIndex
      continue innerLoop    // wait for matching End* to come back to depth 1

    if reader.TokenType in {EndObject, EndArray}:
      RecordElement(currentElementStart)
      currentElementStart = -1L
      continue innerLoop

    // Primitive token at depth 1: self-contained element
    RecordElement(bufferOriginFileOffset + reader.TokenStartIndex)

  state = reader.CurrentState
  consumed = (int)reader.BytesConsumed
  bufferOriginFileOffset += consumed
  remainingLen = dataEnd - consumed
  Buffer.BlockCopy(buffer, consumed, buffer, 0, remainingLen)

RecordElement(elementStart):
  if elementCount == 0:
    lock(_lock): _checkpoints.Add(elementStart)            // checkpoint 0 at E0
  else if elementCount % CheckPointInterval == 0:
    Interlocked.Exchange(ref _totalRows, elementCount)
    lock(_lock): _checkpoints.Add(elementStart)            // checkpoint k at E(k×1000)
    OnFirstCheckpointReached()
    OnProgressChanged(BytesRead, FileSize)
  elementCount++                                           // increment after checkpoint check (0-based index)

done:
  Interlocked.Exchange(ref _totalRows, elementCount)
  OnFirstCheckpointReached()   // guard for small files (<1000 elements); fires after TotalRows is finalised

finally:
  OnFirstCheckpointReached()   // guarantee event fires even on cancellation or error
  ArrayPool.Return(buffer)     // must be in finally to prevent pool leak on exception
  OnBuildIndexCompleted()      // unconditional
```

### 4.2 Edge Cases

| Case | Handling |
|------|----------|
| Empty array `[]` | `TotalRows = 0`; `OnFirstCheckpointReached()` fires at `done:` label (after `TotalRows` finalised) and in `finally` (idempotent); `ArrayPool.Return` and `OnBuildIndexCompleted()` fire in `finally` |
| Single element | `_checkpoints = [element0Offset]`; fires at end of scan |
| Primitives as elements | Handled as self-contained depth-1 tokens; no special skip needed |
| Structured element spans buffer boundary | `Read()` returns `false`; `JsonReaderState` + `currentElementStart` preserved; buffer compacted and refilled |
| Deeply nested or large structured elements | Depth tracking processes one token at a time regardless of total element size; no buffer size limit for structured elements |
| String value > 1 MB | `Read()` returns `false` with `BytesConsumed == 0` and `remainingLen == BufferSize`; throws `NotSupportedException` |
| Cancellation | `OperationCanceledException`; `FirstCheckpointReached` fires via `finally` guard; `BuildIndexCompleted` still fires |

---

## 5. Files to Create / Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/JsonArray/RowIndexer.cs` | Create | Core streaming indexer |
| `src/App/Tui/Workers/RowIndexerFactory.cs` | Modify | Add `DataFormat.JsonArray` case |
| `tests/Refedle.Tests/Engine/IO/JsonArray/RowIndexerTests.cs` | Create | Shared fixtures |
| `tests/Refedle.Tests/Engine/IO/JsonArray/RowIndexerTests.BuildIndex.cs` | Create | `BuildIndex` correctness tests |
| `tests/Refedle.Tests/Engine/IO/JsonArray/RowIndexerTests.GetCheckPoint.cs` | Create | `GetCheckPoint` correctness + concurrency tests |
| `tests/Refedle.Tests/Engine/IO/JsonArray/RowIndexerBenchmarks.cs` | Create | BenchmarkDotNet perf tests |

---

## 6. Testing Strategy

### 6.1 Unit Tests

**BuildIndex**

| Test | Input | Expected |
|------|-------|----------|
| Empty array | `[]` | `TotalRows = 0`; `BuildIndexCompleted` fired |
| Single primitive | `[42]` | `TotalRows = 1`; 1 checkpoint |
| Single object | `[{"a":1}]` | `TotalRows = 1`; checkpoint offset points to `{` |
| Byte-level offset (leading whitespace) | `  [{"a":1}]` (2-byte leading whitespace) | `byteOffset` equals exact byte offset of `{`, not `[` |
| Mixed types | `[{}, [], 1, "s", null, true]` | `TotalRows = 6`; correct checkpoint |
| Deeply nested | `[{"a":{"b":{"c":1}}}]` | `TotalRows = 1`; depth tracking traverses all levels without error |
| 1 001 elements | 1001-element array | 2 checkpoints; `FirstCheckpointReached` fired once |
| Checkpoint 1 byte-level offset | 1001-element array where element 1000 has a known, unique byte offset | `_checkpoints[1]` equals the offset of E1000, not E999 |
| Structured element spans buffer | Object whose tokens span a buffer boundary | Correctly indexed; `JsonReaderState` + `currentElementStart` resume across refill |
| String value > 1 MB | Single string element `> 1 MB` | `NotSupportedException` thrown |
| Cancellation | Any array | `OperationCanceledException`; `BuildIndexCompleted` still fires |

**GetCheckPoint**

| Test | Scenario | Expected |
|------|----------|----------|
| Before any indexing | `GetCheckPoint(0)` on fresh instance | `byteOffset = -1` |
| Element 0 | After first element indexed | `byteOffset` = offset of `{`; `rowOffset = 0` |
| Element 500 | Fewer than 1000 elements | `byteOffset` = checkpoint 0; `rowOffset = 500` |
| Element 1000 | Exactly at checkpoint | `byteOffset` = checkpoint 1; `rowOffset = 0` |
| Concurrent read | `GetCheckPoint` from second thread during `BuildIndex` | No exception |
| Out of range | `GetCheckPoint` beyond `TotalRows` | Clamped to last checkpoint |

### 6.2 Benchmarks

```csharp
[MemoryDiagnoser]
class RowIndexerBenchmarks
```

| Benchmark | Description |
|-----------|-------------|
| `BuildIndex_1kElements` | 1 000 small objects |
| `BuildIndex_100kElements` | 100 000 small objects |
| `BuildIndex_1mElements` | 1 000 000 small objects |
| `GetCheckPoint_First` | `GetCheckPoint(0)` |
| `GetCheckPoint_Middle` | `GetCheckPoint(N / 2)` |
| `GetCheckPoint_Last` | `GetCheckPoint(N - 1)` |

Target: zero heap allocations in `BuildIndex` hot path.

---

## 7. Decision Record

### Rationale
The design mirrors `JsonLines.RowIndexer` — same interface, same checkpoint interval,
same thread-safety model — so the entire viewer pipeline (`SlidingWindowLruCache`,
`RowIndexerFactory`, progress events) works without modification.

`Utf8JsonReader` is the only BCL JSON reader that is AOT-safe, allocation-free in the
hot path, and supports stateful resumption across buffer boundaries via
`JsonReaderState`. Element boundaries are detected with `Read()` + explicit depth
tracking rather than `TrySkip()`. This approach processes one token at a time,
meaning only a single token needs to fit in the 1 MB buffer; structured elements of
any size are handled correctly regardless of buffer boundaries.

### Alternatives Considered

**A. Dedicated `IElementBoundaryIndexer` interface storing `(Offset, Length)` per element**
- Rejected: requires a parallel viewer pipeline for JsonArray. Memory grows linearly
  with element count (12 MB per 1 M elements vs. ~8 KB for checkpoints).
  `SlidingWindowLruCache` already handles random access via its LRU eviction for `u/d`
  jumps and other non-sequential access patterns.

**B. Load entire file, then scan with `JsonDocument`**
- Rejected: violates zero-allocation and streaming requirements; `JsonDocument`
  builds a full in-memory DOM.

**C. Reuse `JsonLines.RowIndexer` with a pre-processing step**
- Rejected: JSON Array is not line-oriented; no reliable single-byte boundary exists.

### Consequences
- **Easier**: `SlidingWindowLruCache`, `RowIndexerFactory`, and all progress UI work
  immediately with no changes.
- **Easier**: Future `ElementByteCache` follows the same pattern as `RowByteCache` —
  extend `SlidingWindowLruCache<ReadOnlyMemory<byte>>`, override `LoadRows` with an
  `ElementReader` that uses `Utf8JsonReader` to scan forward from a checkpoint.
- **Trade-off**: `u/d` jumps that miss the LRU cache require scanning up to 999
  elements with `Utf8JsonReader` (slower than JsonLines' `IndexOf('\n')`). Acceptable
  for typical JSON data; a future per-element full index can be added if profiling
  shows a bottleneck.
- **Note**: `GetCheckPoint` stores only byte offsets, not `JsonReaderState`. A future
  `ElementReader` will need to either re-derive structural context from byte 0 of the
  checkpoint element, or this type can be extended with a `GetCheckPointWithState`
  method when that work begins.
