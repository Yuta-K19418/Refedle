# Sliding Window LRU Cache - Design Document

## Scope

Improve the caching strategy for `DataRowCache` (CSV) and `RowByteCache` (JSON Lines) by replacing the current "clear-all-and-reload" approach with a sliding window prefetch combined with LRU eviction. The two classes are also unified under a shared generic interface and abstract base class.

---

## ADR: Architecture Decision Record

### Context

Both `DataRowCache` and `RowByteCache` share an identical but inefficient caching strategy: on any cache miss, the entire cache is cleared and a new window of 200 rows is loaded from disk. This means that scrolling even one row past the cached window triggers a full 200-row reload, discarding rows that are still relevant.

### Decision Log

The following decisions were made through design discussion.

#### Strategy: Sliding Window + LRU

**Considered:**
- Option A: Sliding window with differential load (load only missing rows on miss)
- Option B: LRU cache (individual row eviction, random-access-friendly)
- **Adopted:** Combination of A and B — prefetch a centered window on miss, evict via LRU

**Rationale:** The combination retains the sequential-scan efficiency of sliding window prefetch while preventing the "full cache wipe" problem through LRU eviction.

#### Prefetch Window: 20 rows, center-aligned

On a cache miss for row N, rows `[N-10, N+9]` (clamped to valid range) are loaded in a single I/O.

**Considered:** Forward-biased window (N to N+19) to optimize downward scrolling.  
**Rejected:** Refedle supports bidirectional scrolling, so center-aligned is more appropriate.

**Note on multiple I/O per render:** If the terminal displays more rows than the prefetch window size, the initial render may trigger more than one I/O. This was explicitly accepted as a non-issue — the improvement to scrolling hit rate outweighs the minor cost of occasional extra I/O on first render. Prefetch window size is configurable via the constructor to allow tuning if needed.

#### Already-cached rows within prefetch window

When loading a prefetch window, some rows may already be in the LRU cache. For each row in the window:
- **Key found in dictionary:** skip node creation; update LRU position only (move to MRU head)
- **Key not found:** create or reuse a node and add to cache

This avoids redundant node allocations for already-cached rows while keeping the I/O sequential and simple.

#### Heap allocation minimization: lazy allocation + node reuse

**Considered:** Pre-allocate all `capacity` nodes upfront at construction time.  
**Rejected:** Files with fewer than `capacity` rows would incur unnecessary allocations.

**Adopted:** Lazy allocation with evicted-node reuse:
- While cache is not full: allocate `LinkedListNode` objects on demand (`new` per miss)
- Once cache is full: reuse the evicted tail node by overwriting its value — zero `new` allocations per operation

**Node reuse safety rule:** Before reusing an evicted node, `Clear()` must always be called to reset all properties. The correctness of `Clear()` is verified by a dedicated unit test that asserts every property is in its default state after the call.

`Dictionary` is initialized with `capacity` to prevent rehashing as entries are added.

#### Code unification: ~~interface +~~ abstract base class

~~**Interface `IRowCache<TRow>`** (`Refedle.Engine.IO` namespace):~~
- ~~Single method: `TRow Get(int index)`~~
- ~~All external classes and unit tests reference this interface only~~
- ~~Ensures testability via mocking and allows future alternative implementations without inheriting the base class~~

**Abstract base class `SlidingWindowLruCache<TRow>`** (`Refedle.Engine.IO` namespace):
- ~~Implements `IRowCache<TRow>`~~
- Encapsulates LRU structure (`Dictionary` + `LinkedList`), prefetch logic, and node reuse
- Is an implementation detail — external classes must not reference it directly

**Concrete classes:**
- `DataRowCache : SlidingWindowLruCache<CsvDataRow> ~~, IRowCache<CsvDataRow>~~`
- `RowByteCache : SlidingWindowLruCache<ReadOnlyMemory<byte>> ~~, IRowCache<ReadOnlyMemory<byte>>~~`

**Decision: Reject Generic Interface due to CA1859**
**Considered:** Generic interface `IRowCache<TRow>`.
**Rejected:** While architecturally clean, using an interface for fields triggers C# warning **CA1859** (Change type of field to concrete type for improved performance). In high-performance TUI rendering paths, devirtualization of these calls is critical.
**Adopted:** Use the abstract base class `SlidingWindowLruCache<TRow>` or concrete types directly in fields to ensure optimal JIT optimization and zero warnings.

#### Separate preparatory rename commit

Before implementing the cache strategy, a standalone commit renames `GetLineBytes` → `GetRow` (JSON Lines) to align with CSV's existing naming. This keeps the cache strategy diff clean and reviewable in isolation.

---

## 1. Requirements

### Functional Requirements

- On cache miss, prefetch 20 rows centered on the requested row
- Retain existing cached rows on miss (no full-cache wipe)
- Evict least-recently-used rows when cache exceeds capacity
- Reuse evicted `LinkedListNode` objects to avoid post-warmup heap allocations
- ~~Expose a common generic interface `IRowCache<TRow>` for external consumers~~
- Use a common generic base class `SlidingWindowLruCache<TRow>` for implementation sharing
- Preserve existing public behavior: `DataRowCache` for CSV, `RowByteCache` for JSON Lines

### Non-Functional Requirements

- **Zero allocations** after cache reaches capacity (hot path)
- **High performance devirtualization:** Use concrete types for fields to avoid interface call overhead (CA1859)
- **Native AOT compatible** (no reflection)
- **Thread-safety:** future-proof; document assumptions
- Prefetch window size and capacity configurable via constructor

---

## 2. Design

### ~~2.1 Interface: `IRowCache<TRow>`~~

```csharp
~~namespace Refedle.Engine.IO;

public interface IRowCache<TRow>
{
    int TotalRows { get; }
    TRow Get(int index);
}~~
```

### 2.1 Abstract Base Class: `SlidingWindowLruCache<TRow>`

#### Responsibilities
- Maintain LRU structure (`Dictionary<int, LinkedListNode<CacheEntry<TRow>>>` + `LinkedList<CacheEntry<TRow>>`)
- On miss: calculate prefetch window, load rows via abstract method, populate cache
- On hit: move node to MRU head
- On eviction: reuse tail node via `Clear()` + reassignment

#### Key structures

```csharp
// Value type stored in each LinkedListNode.
// Stored as a struct so it is laid out inline inside the LinkedListNode heap object,
// avoiding a separate heap allocation per entry.
internal struct CacheEntry<TRow>
{
    internal int RowIndex;
    internal TRow Value;

    internal void Clear(TRow emptyValue)
    {
        RowIndex = -1;
        Value = emptyValue;
    }
}
```

`CacheEntry` does not know what an "empty" `TRow` looks like, so the caller supplies it via `emptyValue`. The base class exposes an abstract property that each subclass overrides:

```csharp
protected abstract TRow EmptyValue { get; }

// Usage on eviction:
entry.Clear(EmptyValue);
```

Concrete overrides:
```csharp
// DataRowCache (CSV)
protected override CsvDataRow EmptyValue => [];

// RowByteCache (JSON Lines)
protected override ReadOnlyMemory<byte> EmptyValue => ReadOnlyMemory<byte>.Empty;
```

This avoids the null-forgiving operator (`!`) while keeping `CacheEntry` free of format-specific knowledge.

#### API

```csharp
namespace Refedle.Engine.IO;

public abstract class SlidingWindowLruCache<TRow> ~~: IRowCache<TRow>~~
{
    private const int DefaultCapacity = 200;
    private const int DefaultPrefetchWindow = 20;

    protected SlidingWindowLruCache(
        IRowIndexer indexer,
        int capacity = DefaultCapacity,
        int prefetchWindow = DefaultPrefetchWindow);

    public int TotalRows { get; }
    public TRow GetRow(int index);

    // Subclasses implement file I/O for a range of rows
    protected abstract IEnumerable<TRow> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch);
}
```

### 2.2 Concrete Classes

```csharp
// CSV
public sealed class DataRowCache(
    IRowIndexer indexer,
    int columnCount,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<CsvDataRow>(indexer, capacity, prefetchWindow) ~~, IRowCache<CsvDataRow>~~
{
    protected override IEnumerable<CsvDataRow> LoadRows(
        long byteOffset, int rowOffsetToSkip, int rowsToFetch);
}

// JSON Lines
public sealed class RowByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<ReadOnlyMemory<byte>>(indexer, capacity, prefetchWindow),
      ~~IRowCache<ReadOnlyMemory<byte>>,~~ IDisposable
{
    protected override IEnumerable<ReadOnlyMemory<byte>> LoadRows(
        long byteOffset, int rowOffsetToSkip, int rowsToFetch);
    public void Dispose();
}
```

### 2.3 LRU Algorithm

```
GetRow(index):
  if index out of range → return default
  if index in dictionary:
    move node to MRU head
    return node value
  else:
    Prefetch(index)
    return dictionary[index].Value

Prefetch(requestedRow):
  halfWindow = prefetchWindow / 2
  windowStart = clamp(requestedRow - halfWindow, 0, TotalRows - prefetchWindow)
  windowEnd   = clamp(windowStart + prefetchWindow - 1, 0, TotalRows - 1)

  (byteOffset, rowOffsetToSkip) = indexer.GetCheckPoint(windowStart)
  rows = LoadRows(byteOffset, rowOffsetToSkip, windowEnd - windowStart + 1)

  foreach (rowIndex, rowValue) in rows:
    if rowIndex in dictionary:
      move node to MRU head   // already cached, just refresh LRU
    else:
      if cache is full:
        node = evict tail node
        node.entry.Clear()
      else:
        node = new LinkedListNode<CacheEntry<TRow>>()
      node.entry = { RowIndex = rowIndex, Value = rowValue }
      dictionary[rowIndex] = node
      prepend node to MRU head
```

### 2.4 Thread Safety

- Not thread-safe by design (consistent with current implementation)
- All access is assumed to occur on the TUI rendering thread
- Document this assumption; flag for future review if parallel access is required

---

## 3. Implementation Plan

### Step 0: Rename existing methods (standalone commit)

| File | Change |
|------|--------|
| `src/Engine/IO/JsonLines/RowByteCache.cs` | `GetLineBytes` → `GetRow` |
| `src/App/Tui/Ui/Views/JsonLinesTableSource.cs` | update call site |
| `src/App/Tui/Ui/Views/JsonLinesTreeView.cs` | update call site |
| `tests/` | update all test call sites |

### Step 1: Skeleton

| File | Action |
|------|--------|
| ~~`src/Engine/IO/IRowCache.cs`~~ | ~~Create interface~~ |
| `src/Engine/IO/CacheEntry.cs` | Create entry struct |
| `src/Engine/IO/SlidingWindowLruCache.cs` | Create abstract base class |
| `src/Engine/IO/Csv/DataRowCache.cs` | Refactor to extend base |
| `src/Engine/IO/JsonLines/RowByteCache.cs` | Refactor to extend base |
| `tests/.../SlidingWindowLruCacheTests.cs` | Create test class skeleton |
| `tests/.../DataRowCacheTests.cs` | Update existing tests |
| `tests/.../RowByteCacheTests.cs` | Update existing tests |

### Step 2: Logic implementation

- Implement LRU logic in `SlidingWindowLruCache<TRow>`
- Implement `LoadRows` in `DataRowCache` and `RowByteCache`
- Complete all unit tests

---

## 4. Testing Strategy

### 4.1 Unit Tests

**`CacheEntry<TRow>.Clear(emptyValue)`**
- `RowIndex == -1` after `Clear()` is called
- `Value == emptyValue` after `Clear()` is called

**`SlidingWindowLruCache<TRow>` ~~ (via `IRowCache<TRow>`)~~**

| # | Scenario |
|---|----------|
| 1 | Cache miss: prefetch window is loaded |
| 2 | Cache hit: no I/O, LRU position updated |
| 3 | Prefetch window clamped at start of file (row 0) |
| 4 | Prefetch window clamped at end of file |
| 5 | Eviction: LRU tail is evicted when capacity is reached |
| 6 | Node reuse: evicted node object is reused (no new allocation after warmup) |
| 7 | Already-cached rows in prefetch window: no duplicate entries, LRU updated |
| 8 | File with fewer rows than capacity: no excess allocations |
| 9 | Out-of-range index returns default value |
| 10 | TotalRows = 0 returns default value |

### 4.2 Benchmarks

- Compare cache hit rate: old strategy vs new strategy under sequential and random access patterns
- Verify zero allocations after cache warmup using `MemoryDiagnoser`

---

## 5. Acceptance Criteria

- ~~[ ] `IRowCache<TRow>` interface defined; all external callers use it~~
- [ ] `SlidingWindowLruCache<TRow>` implements prefetch + LRU correctly
- [ ] No full-cache wipe on miss
- [ ] Node reuse verified: zero `LinkedListNode` allocations after cache is full
- [ ] `CacheEntry.Clear()` unit test passes
- [ ] All existing tests updated and passing
- [ ] `dotnet build` with zero warnings
- [ ] `dotnet format` clean
