# JSON Array Explorer Mode — Design Document

## Scope

Implement a hierarchical tree view (Explorer Mode) for JSON Array files (`[{...}, {...}, ...]`).
The Engine layer returns `ReadOnlyMemory<byte>` per element, and the App layer constructs tree nodes
from those bytes. Both `JsonLinesTreeView` and `JsonArrayTreeView` share a common `MorphTreeView`
abstract base class that provides Vim-key navigation and Enter-to-toggle logic.

---

## 1. Requirements

### Functional Requirements

- **Element Nodes (≤ 1,000 items)**: Element nodes (`[0]: ...`, `[1]: ...`, `[2]: ...`, …) are added directly to the tree root without a wrapper node
- **Range Nodes (≥ 1,001 items)**: Elements are grouped into `JsonArrayRangeTreeNode` instances of 1,000 items each (e.g., `[0 - 999]`, `[1000 - 1999]`). Each range node lazy-loads its children on first expansion.
- **Lazy Loading**: Child nodes (sub-properties / nested arrays) of each element node are populated only when that node is first expanded. Note: for ≤ 1,000 items, the element display text (e.g., `{Object: N properties}`) is computed eagerly at view construction time — up to 1,000 `GetRow` calls are made in the constructor. Children of each element node remain lazy.
- **Nested Structures**: Recursively display nested objects and arrays using existing `JsonObjectTreeNode` / `JsonArrayTreeNode`
- **Total Count**: The total element count is displayed in the status bar when Explorer Mode is activated (via `ViewManager.SwitchToJsonArrayTree`)
- **Arrow-key Navigation**: Inherited from Terminal.Gui `TreeView` (h/j/k/l Vim keys also supported)
- **Element Boundary Index**: Use `JsonArray.RowIndexer` (implemented in the previous issue) to resolve byte offsets for any element

### Non-Functional Requirements

- **Zero allocations** on the tree-node rendering hot path
- **Native AOT compatible**: `Utf8JsonReader`; no reflection
- **Thread-safe** read access: all tree operations run on the UI thread (same model as `JsonLinesTreeView`)

---

## 2. Architecture Overview

```
┌─ App Layer ────────────────────────────────────────────────────────────┐
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              MorphTreeView (abstract)                           │   │
│  │   Vim-key navigation, 't' toggle, Enter toggle                  │   │
│  └──────────────────────┬──────────────────────────────────────────┘   │
│                         │                                              │
│          ┌──────────────┴───────────────┐                              │
│          ▼                              ▼                              │
│  ┌───────────────────────┐       ┌──────────────────────────────────┐  │
│  │  JsonLinesTreeView    │       │       JsonArrayTreeView          │  │
│  │  (creates row nodes)  │       │  (≤1000: element nodes directly) │  │
│  └───────────────────────┘       │  (≥1001: range nodes, lazy)      │  │
│                                  └─────────────┬────────────────────┘  │
│                                                │                       │
│                         ┌──────────────────────┴─────────────────┐     │
│                         │  (≥1001 items only)                    │     │
│                         ▼                                        │     │
│  ┌──────────────────────────────────────────┐                    │     │
│  │        JsonArrayRangeTreeNode            │                    │     │
│  │  Text = "[0 - 999]"  (lazy-loads 1,000   │                    │     │
│  │   child element nodes on first expand)   │                    │     │
│  └──────────────────────────────────────────┘                    │     │
│           │               │               │                      │     │
│           ▼               ▼               ▼                      │     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  (existing)│     │
│  │JsonObject    │  │JsonArray     │  │JsonValue     │            │     │
│  │TreeNode      │  │TreeNode      │  │TreeNode      │            │     │
│  │[0]: {Obj: N} │  │[1]: [Arr: M] │  │[2]: 42       │            │     │
│  └──────────────┘  └──────────────┘  └──────────────┘            │     │
│                                                                        │
├─ Engine Layer ─────────────────────────────────────────────────────────┤
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              ElementByteCache                                   │   │
│  │    (SlidingWindowLruCache<ReadOnlyMemory<byte>>)                │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              │                                         │
│                              ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              ElementReader                                      │   │
│  │  (MmapService + rolling buffer + Utf8JsonReader)                │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              │                                         │
│                              ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              JsonArray.RowIndexer  (already implemented)        │   │
│  │                   (byte offset lookup)                          │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

### Layer Separation

The Engine layer (`ElementReader`, `ElementByteCache`) returns `ReadOnlyMemory<byte>` per element
and has **no dependency on Terminal.Gui**. The App layer converts raw bytes into tree nodes.
`MorphTreeView` centralises Vim-key navigation so neither `JsonLinesTreeView` nor `JsonArrayTreeView`
duplicates that logic.

---

## 3. Class Design

### 3.1 Engine: `JsonArray.ElementReader`

**Namespace**: `Refedle.Engine.IO.JsonArray`

**Responsibility**: Read raw element bytes from a JSON Array file given a checkpoint byte
offset. Mirrors `JsonLines.RowReader` but uses `Utf8JsonReader` to locate element
boundaries instead of scanning for `\n`.

**I/O Mechanism**: Uses `MmapService` for consistent memory-mapped random access, identical
to `JsonLines.RowReader`. `MmapService` is opened in the constructor and closed in `Dispose`.

**Constructor**: Uses a traditional constructor body (not primary constructor syntax) to
perform validation with `ArgumentException.ThrowIfNullOrWhiteSpace(filePath)` and open
`MmapService`, mirroring `JsonArray.RowIndexer`.

**Parameter Validation**: `elementsToSkip` and `elementsToFetch` must be non-negative;
`ArgumentOutOfRangeException` is thrown for negative values, matching the contract of
`RowIndexer.GetCheckPoint`.

**Algorithm (rolling buffer with synthetic array context)**:

`RowIndexer.GetCheckPoint` returns a `byteOffset` pointing to the first byte of a
top-level element (e.g., the `{` of an object). From that file position the bytes look like:

```
{e_k...}, {e_k+1...}, ..., {e_n...}]
```

To allow `Utf8JsonReader` to handle element separators (commas, whitespace) automatically,
a single synthetic `[` byte is prepended to the first buffer fill, placing all elements at
depth 1 of the reader's context. `Read()` then advances through elements and their
separators without any manual byte scanning; an element boundary is detected when the
reader returns to depth 1 after the closing token. Elements spanning buffer boundaries
are handled by saving `JsonReaderState`, compacting unconsumed bytes, and refilling —
identical to the rolling-buffer loop in `RowIndexer.BuildIndex`. The `bufferOriginFileOffset`
is adjusted by −1 on the first fill to account for the prepended synthetic byte.

Element bytes are captured by recording `TokenStartIndex` at the start of each element and
computing the end from `BytesConsumed` when the element is complete. These bytes are copied
to a heap array and returned as `ReadOnlyMemory<byte>`.

**Allocation note**: `ReadElementBytes` allocates a `List<ReadOnlyMemory<byte>>` and a `new byte[length]`
per element. This is a one-time setup cost per range expansion and is not subject to the
"zero allocations on the rendering hot path" NFR, which applies to rendering only.

**API**:

```csharp
public sealed class ElementReader : IDisposable
{
    public ElementReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        // open MmapService; throw InvalidOperationException on failure
    }

    // Reads raw JSON bytes for elements starting at byteOffset.
    // byteOffset: a checkpoint byte offset from RowIndexer.GetCheckPoint.
    // elementsToSkip: number of elements to skip before collecting (non-negative).
    // elementsToFetch: maximum elements to collect after skipping (non-negative).
    // Returns raw JSON bytes per element (NOT TreeNode — no App layer dependency).
    // Throws ArgumentOutOfRangeException for negative elementsToSkip/elementsToFetch.
    // Throws ObjectDisposedException after Dispose() has been called.
    public IReadOnlyList<ReadOnlyMemory<byte>> ReadElementBytes(
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch
    );

    public void Dispose();
}
```

**Thread Safety**: NOT thread-safe. All calls occur on the UI thread.

### 3.2 Engine: `JsonArray.ElementByteCache`

**Namespace**: `Refedle.Engine.IO.JsonArray`

**Responsibility**: Sliding window LRU cache for element bytes. Extends
`SlidingWindowLruCache<ReadOnlyMemory<byte>>` and delegates to `ElementReader`.
Mirrors `JsonLines.RowByteCache` exactly, including the `GetRow` dispose guard.

**API**:

```csharp
public sealed class ElementByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<ReadOnlyMemory<byte>>(indexer, capacity, prefetchWindow),
      IDisposable
{
    private readonly ElementReader _reader = new(indexer.FilePath);
    private bool _disposed;

    // Dispose guard — must override GetRow to match RowByteCache behaviour.
    public override ReadOnlyMemory<byte> GetRow(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return base.GetRow(index);
    }

    protected override ReadOnlyMemory<byte> EmptyValue => ReadOnlyMemory<byte>.Empty;

    protected override IEnumerable<ReadOnlyMemory<byte>> LoadRows(
        long byteOffset, int rowOffsetToSkip, int rowsToFetch) =>
        _reader.ReadElementBytes(byteOffset, rowOffsetToSkip, rowsToFetch);

    public void Dispose()
    {
        if (_disposed) return;
        _reader.Dispose();
        _disposed = true;
    }
}
```

### 3.3 App: `JsonArrayRangeTreeNode`

**Namespace**: `Refedle.App.Tui.Ui.Views`

**Responsibility**: Represents a 1,000-item range within a large JSON Array (used only when
`TotalRows > 1,000`). On first `Children` access (lazy), reads element bytes from
`ElementByteCache` and constructs child element nodes for that range.

**Display Format**: Same element node format as direct expansion, grouped under a range header:

```
▼ [0 - 999]
  ▶ [0]: {Object: 2 properties}
  ▶ [1]: [Array: 2 items]
  ▶ [2]: 42
  ...
▶ [1000 - 1999]
```

`CreateElementNode` peeks the first token to determine node type, creates the appropriate
`JsonObjectTreeNode` / `JsonArrayTreeNode` / `JsonValueTreeNode`, then prepends `[{index}]: `
to the node's `Text`. This avoids calling `JsonTreeNodeHelper.CreateChildNode`, which
produces the abbreviated `{...}` / `[...]` form used for nested child nodes.

**Memory Management**: Children remain in heap memory after expansion (Plan A). Viewport-based
auto-release is deferred to a future independent issue.

**Constructor Null Guard**: `ArgumentNullException.ThrowIfNull(cache)` in the constructor body.

```csharp
internal sealed class JsonArrayRangeTreeNode : TreeNode
{
    private readonly ElementByteCache _cache;
    private readonly int _startIndex;
    private readonly int _count;
    private bool _childrenLoaded;

    public JsonArrayRangeTreeNode(ElementByteCache cache, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _startIndex = startIndex;
        _count = count;
        Text = $"[{startIndex} - {startIndex + count - 1}]";
    }

    public override IList<ITreeNode> Children
    {
        get
        {
            if (!_childrenLoaded)
            {
                LoadChildren();
                _childrenLoaded = true;
            }
            return base.Children;
        }
        set => base.Children = value;
    }

    private void LoadChildren() { /* read _count elements starting at _startIndex from _cache; build element nodes */ }

    internal static ITreeNode CreateElementNode(ReadOnlyMemory<byte> bytes, int index)
    {
        // Peek first token → create typed node → prepend "[{index}]: " to node.Text
    }
}
```

### 3.4 App: `MorphTreeView`

**Namespace**: `Refedle.App.Tui.Ui.Views`

**Responsibility**: Abstract base class for all format-specific tree views. Provides
Vim-key navigation (h/j/k/l/g/G/Ctrl+d/Ctrl+u), `'t'`-key table-mode toggle,
Enter-to-toggle expand/collapse, and the global-shortcut passthrough guard.
Mirrors the role of `MorphTableView` in the table view hierarchy.

**`JsonLinesTreeView` change**: inherits from `MorphTreeView` instead of `TreeView`.
All shared code (`_vimKeys`, `_onTableModeToggle`, `OnKeyDown`, `HandleNonVimKey`,
`ConsumeAction`, `OnAccepted`) moves to this base class. `JsonLinesTreeView` no longer
overrides `OnKeyDown`.

```csharp
internal abstract class MorphTreeView : TreeView
{
    private readonly VimKeyTranslator _vimKeys = new();
    private readonly Action _onTableModeToggle;

    protected MorphTreeView(Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        _onTableModeToggle = onTableModeToggle;
        Accepted += OnAccepted;
    }

    private void OnAccepted(object? sender, CommandEventArgs e)
    {
        var node = SelectedObject;
        if (node is null) return;
        if (IsExpanded(node)) { Collapse(node); return; }
        Expand(node);
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.T)
        {
            _onTableModeToggle();
            return true;
        }

        var action = _vimKeys.Translate(key.KeyCode);
        return action switch
        {
            VimAction.PendingGSequence => true,
            VimAction.MoveDown  => ConsumeAction(() => AdjustSelection(offset: 1,               expandSelection: false)),
            VimAction.MoveUp    => ConsumeAction(() => AdjustSelection(offset: -1,              expandSelection: false)),
            VimAction.PageDown  => ConsumeAction(() => AdjustSelection(offset:  Viewport.Height, expandSelection: false)),
            VimAction.PageUp    => ConsumeAction(() => AdjustSelection(offset: -Viewport.Height, expandSelection: false)),
            VimAction.MoveLeft  => base.OnKeyDown(new Key(KeyCode.CursorLeft)),
            VimAction.MoveRight => base.OnKeyDown(new Key(KeyCode.CursorRight)),
            VimAction.GoToFirst => ConsumeAction(GoToFirst),
            VimAction.GoToEnd   => ConsumeAction(GoToEnd),
            _                   => HandleNonVimKey(key),
        };
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcuts from being consumed by TreeView's incremental search.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode)) return false;
        return base.OnKeyDown(key);
    }

    private static bool ConsumeAction(Action action) { action(); return true; }
}
```

### 3.5 App: `JsonArrayTreeView`

**Namespace**: `Refedle.App.Tui.Ui.Views`

**Responsibility**: `MorphTreeView` subclass. Creates `ElementByteCache` and populates the
tree root directly — without a wrapper node. For arrays of ≤ 1,000 items, element nodes are
added directly via `AddObject`. For arrays of ≥ 1,001 items, 1,000-item `JsonArrayRangeTreeNode`
instances are created and added instead. Inherits all Vim-key handling and `'t'`-key
table-mode toggle from `MorphTreeView`.

**Factory method**: The constructor is `private`. Callers use `JsonArrayTreeView.Create(...)`.
A constructor hides allocation-heavy work from the call site; surfacing it as a named factory
method makes the cost explicit to callers.

```csharp
internal sealed class JsonArrayTreeView : MorphTreeView
{
    private const int RangeSize = 1_000;
    private readonly ElementByteCache _cache;

    private JsonArrayTreeView(ElementByteCache cache, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _cache = cache;
    }

    internal static JsonArrayTreeView Create(IRowIndexer indexer, Action onTableModeToggle)
    {
        var cache = new ElementByteCache(indexer);
        var view = new JsonArrayTreeView(cache, onTableModeToggle);
        var totalRows = indexer.TotalRows;

        if (totalRows <= RangeSize)
        {
            for (int i = 0; i < totalRows; i++)
            {
                var bytes = cache.GetRow(i);
                if (bytes.IsEmpty) continue;
                view.AddObject(JsonArrayRangeTreeNode.CreateElementNode(bytes, i));
            }
            return view;
        }

        var start = 0;
        while (start < totalRows)
        {
            var count = Math.Min(RangeSize, totalRows - start);
            view.AddObject(new JsonArrayRangeTreeNode(cache, start, count));
            start += RangeSize;
        }
        return view;
    }

    protected override void Dispose(bool disposing) { /* dispose _cache */ }
}
```

---

## 4. Files to Create / Files to Modify

### Files to Create

| File | Layer | Purpose |
|------|-------|---------|
| `src/Engine/IO/JsonArray/ElementReader.cs` | Engine | MmapService + rolling-buffer element byte reader |
| `src/Engine/IO/JsonArray/ElementByteCache.cs` | Engine | Sliding window LRU cache for element bytes |
| `src/App/Tui/Ui/Views/MorphTreeView.cs` | App | Abstract base: Vim-key navigation + Enter toggle |
| `src/App/Tui/Ui/Views/JsonArrayRangeTreeNode.cs` | App | 1,000-item range node; lazy-loads child element nodes on first expansion |
| `src/App/Tui/Ui/Views/JsonArrayTreeView.cs` | App | `MorphTreeView` subclass; orchestrates cache + range/element nodes |
| `tests/Refedle.Tests/Engine/IO/JsonArray/ElementReaderTests.cs` | Tests | Unit tests for `ElementReader` |
| `tests/Refedle.Tests/Engine/IO/JsonArray/ElementByteCacheTests.cs` | Tests | Unit tests for `ElementByteCache` |
| `tests/Refedle.Tests/Engine/IO/JsonArray/ElementByteCacheBenchmarks.cs` | Tests | BenchmarkDotNet perf tests for `ElementByteCache` hot path |
| `tests/Refedle.Tests/App/Tui/Ui/Views/JsonArrayRangeTreeNodeTests.cs` | Tests | Unit tests for range-node tree-building logic |

### Files to Modify

| File | Change |
|------|--------|
| `src/App/Tui/Ui/Views/JsonLinesTreeView.cs` | Change base class from `TreeView` to `MorphTreeView`; pass `onTableModeToggle` to `base(...)`; remove `_vimKeys`, `_onTableModeToggle`, `OnKeyDown`, `HandleNonVimKey`, `ConsumeAction`, `OnAccepted` (all moved to base) |
| `src/App/Tui/ViewMode.cs` | Add `JsonArrayTree` and `JsonArrayTable` enum values |
| `src/App/Tui/Ui/ViewManager.cs` | Implement `SwitchToJsonArrayTree(IRowIndexer)` and `ToggleJsonArrayModeAsync()`; add `private long _jsonArrayTotalRows` field for status bar display |
| `src/App/Tui/Ui/MainWindow.cs` (or `FileDialogHandler`) | After JSON Array indexing completes, call `SwitchToJsonArrayTree` |

---

## 5. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Engine returns `ReadOnlyMemory<byte>`, not `ITreeNode` | Engine must not depend on Terminal.Gui; mirrors JsonLines pattern |
| No wrapper root node | A wrapper node (`[ n items ]`) forces an extra expand keystroke before any element is visible; contradicts explorer-mode UX. Structural symmetry with `JsonLinesTreeView` is also preserved. |
| ≤ 1,000 items: direct element nodes | Small arrays don't need grouping; immediate visibility on open. |
| ≥ 1,001 items: 1,000-item `JsonArrayRangeTreeNode` | Groups elements so that unexpanded ranges never allocate child nodes; prevents OOM for large arrays without an arbitrary cap. |
| Memory management: Plan A (do nothing on collapse) | Expanded children remain in Gen2 heap. GC churn from repeatedly releasing/reallocating 1,000-node batches (Plan C) outweighs the memory savings. Viewport-based auto-release (Plan B) is deferred to a future issue. |
| Total count in status bar | `IRowIndexer.TotalRows` is available at `ViewManager.SwitchToJsonArrayTree` call time; no change to `JsonArrayTreeView` required. |
| Reuse `JsonObjectTreeNode`, `JsonArrayTreeNode`, `JsonValueTreeNode` | Identical structural nodes; zero duplication |
| Option B display format (`[0]: {Object: 2 properties}`) | Richer than `{...}`; user sees element count/property count before expanding |
| Extend `SlidingWindowLruCache<ReadOnlyMemory<byte>>` | Reuses prefetch and LRU eviction; same API as `RowByteCache` |
| Synthetic `[` prepended to buffer in `ElementReader` | Places elements at depth 1 so `Utf8JsonReader.Read()` advances past commas automatically; no manual separator scanning needed |
| `MmapService` for `ElementReader` I/O | Consistent with `RowReader`; supports random-access reads without reopening the file handle |
| All tree operations on the UI thread | Same model as `JsonLinesTreeView`; no synchronization needed |
| `MorphTreeView` abstract base class | Mirrors `MorphTableView`; eliminates Vim-key duplication between `JsonLinesTreeView` and `JsonArrayTreeView` |

### Null Value Representation

Same as `JsonLinesTreeView`:

| JSON Value | Display |
|------------|---------|
| `null` | `<null>` |
| Empty string | `""` |
| `"null"` | `null` |

---

## 6. Dependencies

| Dependency | Status | Notes |
|------------|--------|-------|
| `JsonArray.RowIndexer` | Implemented | Provides `GetCheckPoint(targetRow)` |
| `SlidingWindowLruCache<T>` | Existing | Base class for `ElementByteCache` |
| `MmapService` | Existing | Used by `ElementReader` for memory-mapped I/O |
| `JsonObjectTreeNode`, `JsonArrayTreeNode`, `JsonValueTreeNode` | Existing | Reused as-is |
| `MorphTreeView` | New (this issue) | Abstract base; Vim-key navigation shared with `JsonLinesTreeView` |
| `VimKeyTranslator` | Existing | Used by `MorphTreeView` for keyboard navigation |
| `System.Text.Json.Utf8JsonReader` | BCL | AOT-safe; zero-allocation hot path |
| Terminal.Gui `TreeView`, `TreeNode`, `ITreeNode` | Existing | Base for all tree classes |

---

## 7. Testing Strategy

### 7.1 Unit Tests — `ElementReader`

| Test | Input | Expected |
|------|-------|----------|
| `Constructor_WithNullFilePath_ThrowsArgumentNullException` | `null` | `ArgumentNullException` |
| `Constructor_WithWhiteSpacePath_ThrowsArgumentException` | `"   "` | `ArgumentException` |
| `Constructor_WithNonExistentFilePath_ThrowsInvalidOperationException` | Non-existent path | `InvalidOperationException` |
| `ReadElementBytes_SingleObject_ReturnsObjectBytes` | `[{"a":1}]`, offset at `{` | 1 entry; bytes parse to `{"a":1}` |
| `ReadElementBytes_MultipleElements_ReturnsCorrectCount` | `[1, 2, 3]`, offset at `1` | 3 entries |
| `ReadElementBytes_WithSkip_SkipsCorrectElements` | `[1, 2, 3]`, skip=1, fetch=2 | Bytes for `2` and `3` |
| `ReadElementBytes_ElementSpansBufferBoundary_ReturnsCompleteBytes` | Large object split across 1 MB boundary | Correct bytes returned |
| `ReadElementBytes_FetchBeyondEnd_ReturnsAvailableElements` | `[1, 2]`, fetch=10 | 2 entries |
| `ReadElementBytes_WithZeroFetchCount_ReturnsEmptyList` | Any array, fetch=0 | Empty list |
| `ReadElementBytes_WithNegativeSkipCount_ThrowsArgumentOutOfRangeException` | elementsToSkip=-1 | `ArgumentOutOfRangeException` |
| `ReadElementBytes_WithNegativeFetchCount_ThrowsArgumentOutOfRangeException` | elementsToFetch=-1 | `ArgumentOutOfRangeException` |
| `ReadElementBytes_EmptyArray_ReturnsEmptyList` | `[]` | Empty list |
| `ReadElementBytes_MixedTypeArray_ReturnsCorrectElements` | `[1, "str", true, null, {}, []]` | 6 entries, each with correct type bytes |
| `ReadElementBytes_SkipPastEnd_ReturnsEmptyList` | `[1, 2]`, skip=5 | Empty list |
| `ReadElementBytes_AfterDispose_ThrowsObjectDisposedException` | Disposed instance | `ObjectDisposedException` |

### 7.2 Unit Tests — `ElementByteCache`

| Test | Scenario | Expected |
|------|----------|----------|
| `GetRow_FirstAccess_ReturnsCorrectBytes` | `GetRow(0)` on a fresh cache | Correct bytes for element 0 |
| `GetRow_LastValidIndex_ReturnsCorrectBytes` | `GetRow(TotalRows - 1)` | Correct bytes for last element |
| `GetRow_WithinCachedRange_ReturnsCachedBytes` | Cache hit on second call | Same bytes returned; `ElementReader` called once |
| `GetRow_OutsideCachedRange_UpdatesCacheWindow` | Cache miss | Bytes loaded; window advances |
| `GetRow_NegativeIndex_ReturnsEmpty` | `GetRow(-1)` | `ReadOnlyMemory<byte>.Empty` |
| `GetRow_IndexEqualToTotalElements_ReturnsEmpty` | `GetRow(TotalRows)` | `ReadOnlyMemory<byte>.Empty` |
| `GetRow_AfterDisposal_ThrowsObjectDisposedException` | Disposed instance | `ObjectDisposedException` |
| `Dispose_CalledTwice_DoesNotThrow` | Double dispose | No exception |
| `Dispose_AllowsFileDeletion_AfterDispose` | Create an `ElementByteCache` backed by a temp file; call `cache.Dispose()`; then call `File.Delete(filePath)` | No `IOException` thrown. Verifies mmap handle release on Windows (open handles block deletion). On macOS, `unlink(2)` semantics mean the call always succeeds regardless of disposal — the test serves as documentation on that platform. |

### 7.3 Unit Tests — `JsonArrayRangeTreeNode`

| Test | Input | Expected |
|------|-------|----------|
| `Constructor_WithNullCache_ThrowsArgumentNullException` | `null` | `ArgumentNullException` |
| `Constructor_SetsCorrectDisplayText` | `startIndex=0, count=1000` | `Text = "[0 - 999]"` |
| `Constructor_SetsCorrectDisplayText_PartialRange` | `startIndex=1000, count=500` | `Text = "[1000 - 1499]"` |
| `Children_FirstAccess_LoadsElementNodes` | Range covering 3 elements | `Children.Count = 3` |
| `Children_EmptyRange_ReturnsEmptyChildren` | `count = 0` | `Children.Count = 0` |
| `Children_ObjectElement_CreatesJsonObjectTreeNode` | `[{"a":1}]` | Child is `JsonObjectTreeNode` with `Text` starting with `[0]:` |
| `Children_ArrayElement_CreatesJsonArrayTreeNode` | `[[1,2]]` | Child is `JsonArrayTreeNode` with `Text` starting with `[0]:` |
| `Children_PrimitiveElement_CreatesJsonValueTreeNode` | `[42]` | Child is `JsonValueTreeNode` with `Text = "[0]: 42"` |
| `Children_NullElement_CreatesJsonValueTreeNode` | `[null, 1]` | First child is `JsonValueTreeNode` with `Text = "[0]: <null>"` |
| `Children_InvalidJsonElement_CreatesJsonValueTreeNodeWithErrorText` | Malformed bytes | Child is `JsonValueTreeNode` with `Text` containing `[Invalid JSON]` |
| `Children_SecondAccess_ReturnsSameCount` | Any range | `Children.Count` is identical on second access |
| `Children_SkipsEmptyBytes_WhenCacheReturnsEmpty` | Cache returns `Empty` for some indices | Empty-byte entries are skipped; `Children.Count` equals non-empty count |

### 7.4 Unit Tests — `JsonArrayTreeView`

| Test | Input | Expected |
|------|-------|----------|
| `Constructor_SmallArray_AddsElementNodesDirectly` | `TotalRows = 5` | Top-level objects are element nodes, not `JsonArrayRangeTreeNode` |
| `Constructor_ExactBoundary_AddsElementNodesDirectly` | `TotalRows = 1000` | Top-level objects are element nodes, not `JsonArrayRangeTreeNode` |
| `Constructor_LargeArray_AddsRangeNodes` | `TotalRows = 1001` | Top-level objects are all `JsonArrayRangeTreeNode` |
| `Constructor_LargeArray_CorrectRangeCount` | `TotalRows = 2500` | 3 range nodes: `[0-999]`, `[1000-1999]`, `[2000-2499]` |
| `Constructor_EmptyArray_AddsNoNodes` | `TotalRows = 0` | No top-level objects |
| `Constructor_SmallArray_SkipsEmptyBytes_WhenCacheReturnsEmpty` | Cache returns `Empty` for some indices | Empty-byte entries are skipped; fewer nodes than `TotalRows` |

### 7.5 Unit Tests — `ViewManager` (additions to existing `ViewManagerTests`)

| Test | Expected |
|------|----------|
| `SwitchToJsonArrayTree_WithValidIndexer_SetsCurrentView` | Current view is `JsonArrayTreeView` |
| `SwitchToJsonArrayTree_WithValidIndexer_ShowsTotalCountInStatusBar` | Status bar text contains `TotalRows` value |
| `SwitchToJsonArrayTree_WithNullIndexer_ThrowsArgumentNullException` | `ArgumentNullException` |
| `SwitchToJsonArrayTree_AfterDisposal_ThrowsObjectDisposedException` | `ObjectDisposedException` |
| `ToggleJsonArrayModeAsync_WhenLive_DoesNotThrow` | Called on a live `ViewManager` | Completes without exception |
| `ToggleJsonArrayModeAsync_AfterDisposal_ThrowsObjectDisposedException` | Disposed instance | `ObjectDisposedException` |
