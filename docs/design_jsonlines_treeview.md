# JSON Lines TreeView - Design Document

## Scope
This document covers the TreeView-based Virtual Viewport implementation for JSON Lines format.
Implements virtual rendering for efficient display of JSON Lines data as a hierarchical tree.

---

## 1. Requirements

### Functional Requirements
- **Hierarchical Display**: Display JSON Lines data as expandable/collapsible tree nodes
- **Virtual Rendering**: Render only visible nodes (~20-50)
- **On-Demand Parsing**: Parse JSON children only when node is expanded
- **Lazy Loading**: Load child nodes on expansion, not upfront

### Non-Functional Requirements
- **Zero allocations** for visible node rendering (hot path)
- **Native AOT compatible** (no reflection)
- **Thread-safe** read access for tree traversal

---

## 2. Architecture Overview

```
┌─ App Layer ──────────────────────────────────────────────────┐
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                  JsonLinesTreeView                      │ │
│  │          (TreeView with ITreeNode instances)            │ │
│  └─────────────────────────────────────────────────────────┘ │
│                            │                                 │
│        ┌───────────────────┼───────────────────┐             │
│        ▼                   ▼                   ▼             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │ JsonObject   │  │ JsonArray    │  │ JsonValue    │        │
│  │ TreeNode     │  │ TreeNode     │  │ TreeNode     │        │
│  │ (lazy parse) │  │ (lazy parse) │  │ (leaf)       │        │
│  └──────────────┘  └──────────────┘  └──────────────┘        │
│                                                              │
├─ Engine Layer ───────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │               JsonLineByteCache                         │ │
│  │    (sliding window cache, returns ReadOnlyMemory<byte>) │ │
│  └─────────────────────────────────────────────────────────┘ │
│                            │                                 │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                   JsonLineReader                        │ │
│  │      (reads line bytes via Utf8JsonReader)              │ │
│  └─────────────────────────────────────────────────────────┘ │
│                            │                                 │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                     RowIndexer                          │ │
│  │                (byte offset lookup)                     │ │
│  └─────────────────────────────────────────────────────────┘ │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Layer Separation

**Engine layer** returns `ReadOnlyMemory<byte>` (raw JSON line bytes) — not TreeNode. This avoids Engine depending on `Terminal.Gui`. The App layer constructs `TreeNode` instances directly from the raw bytes (zero-copy, zero intermediate model).

### Why TreeView over TableView

JSON Lines data is inherently hierarchical - each line contains a JSON object with nested objects and arrays. TreeView is a better fit than TableView because:
- **Native Hierarchy Support**: Expandable/collapsible nodes for nested structures
- **Built-in Lazy Loading**: Children loaded only when node is expanded
- **Natural Representation**: JSON objects and arrays map directly to tree nodes

---

## 3. Design

### 3.1 Data Model: Using Terminal.Gui's `TreeNode`

**Approach**: Inherit from Terminal.Gui's `TreeNode` class (implements `ITreeNode`)

Terminal.Gui's `ITreeNode` interface:
```csharp
public interface ITreeNode
{
    IList<ITreeNode> Children { get; }
    object Tag { get; set; }
    string Text { get; set; }
}
```

**Namespace**: `Refedle.App.Tui.Ui.Views`

Custom node types inheriting from `TreeNode`:

```csharp
/// <summary>
/// Represents a JSON object node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonObjectTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    public JsonObjectTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
    }

    public int? LineNumber { get; init; }

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

    private void LoadChildren() { /* Parse _rawJson with Utf8JsonReader */ }
}

/// <summary>
/// Represents a JSON array node in the tree.
/// </summary>
internal sealed class JsonArrayTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    public JsonArrayTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
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

    private void LoadChildren() { /* Parse _rawJson with Utf8JsonReader */ }
}

/// <summary>
/// Represents a JSON primitive value (string, number, boolean, null).
/// Has no children.
/// </summary>
internal sealed class JsonValueTreeNode : TreeNode
{
    public JsonValueTreeNode(string text) : base(text)
    {
        Children = []; // Empty, no children
    }

    public JsonValueKind ValueKind { get; init; }
}
```

**Key Design Points**:
- Inherit from `TreeNode` (implements `ITreeNode`)
- Override `Children` getter for lazy loading
- Store `ReadOnlyMemory<byte>` for deferred parsing
- Use non-generic `TreeView` (works with `ITreeNode`)

### 3.2 Class: `JsonLineReader`

**Namespace**: `Refedle.Engine.IO.JsonLines`

**Responsibilities**:
- Read JSON line bytes from file using indexed byte offsets
- Parse each line via `Utf8JsonReader` to extract raw JSON bytes
- Return `ReadOnlyMemory<byte>` per line (NOT TreeNode — no App layer dependency)

**API Design**:
```csharp
public sealed class JsonLineReader(string filePath)
{
    public IReadOnlyList<ReadOnlyMemory<byte>> ReadLineBytes(
        long byteOffset,
        int linesToSkip,
        int linesToRead
    );
}
```

### 3.3 Class: `JsonLineByteCache`

**Namespace**: `Refedle.Engine.IO.JsonLines`

**Responsibilities**:
- Maintain sliding window cache for raw line bytes (each JSON line = `ReadOnlyMemory<byte>`)
- Coordinate with RowIndexer for byte offset lookup
- Delegate to JsonLineReader for reading raw bytes

**API Design**:
```csharp
public sealed class JsonLineByteCache(RowIndexer indexer, int cacheSize = 200)
{
    public int TotalLines { get; }
    public ReadOnlyMemory<byte> GetLineBytes(int lineIndex);
}
```

### 3.4 Class: `JsonLinesTreeView`

**Namespace**: `Refedle.App.Tui.Ui.Views`

**Responsibilities**:
- Use non-generic `TreeView` (works with `ITreeNode`)
- Create `JsonObjectTreeNode` from `JsonLineByteCache.GetLineBytes()` (raw bytes → TreeNode)
- Add root nodes (one per JSON line) via `AddObject()`
- Lazy children loaded automatically via overridden `Children` property

**API Design**:
```csharp
internal sealed class JsonLinesTreeView : TreeView
{
    private readonly JsonLineByteCache _cache;

    public JsonLinesTreeView(RowIndexer indexer)
    {
        _cache = new JsonLineByteCache(indexer);
        LoadInitialRootNodes();
    }

    private void LoadInitialRootNodes()
    {
        // Create TreeNodes from raw bytes (App layer responsibility)
        for (var i = 0; i < Math.Min(_cache.TotalLines, 50); i++)
        {
            var lineBytes = _cache.GetLineBytes(i);
            var rootNode = new JsonObjectTreeNode(lineBytes) { LineNumber = i + 1 };
            AddObject(rootNode);
        }
    }
}
```

**Note**: Non-generic `TreeView` uses `ITreeNode.Text` for display and `ITreeNode.Children` for hierarchy. No `AspectGetter` or `DelegateTreeBuilder` needed since our nodes implement `ITreeNode` via `TreeNode` inheritance.

---

## 4. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Inherit from `TreeNode` (implements `ITreeNode`) | Use Terminal.Gui's built-in tree infrastructure |
| Use non-generic `TreeView` | Works directly with `ITreeNode`, no builder needed |
| Override `Children` for lazy loading | Parse JSON only when node is expanded |
| Store raw JSON as `ReadOnlyMemory<byte>` | Lazy parsing - children parsed only on expand |
| Engine returns `ReadOnlyMemory<byte>`, not TreeNode | Maintain Engine/App layer separation — Engine must not depend on `Terminal.Gui` |
| Virtual root node management | Handle files with millions of lines |

### Null Value Representation

| JSON Value | Display |
|------------|---------|
| `null`     | `<null>` |
| Empty string | (empty) |
| `"null"`   | `null` |

### Thread Safety Model

| Operation | Thread Safety |
|-----------|---------------|
| `ReadLineBytes()` | NOT thread-safe |
| `GetLineBytes()` | NOT thread-safe |
| `LoadChildren()` | NOT thread-safe |

All operations are designed to be called from the UI thread.

### TreeView Display Format

```
├─ Line 1: {...}
│  ├─ id: 1
│  ├─ name: "Alice"
│  └─ details: {...}
│     ├─ email: "alice@example.com"
│     └─ tags: [...]
│        ├─ [0]: "admin"
│        └─ [1]: "user"
├─ Line 2: {...}
│  ├─ id: 2
│  └─ name: "Bob"
```

---

## 5. Files to Create

| File | Layer | Purpose |
|------|-------|---------|
| `src/Engine/IO/JsonLines/JsonLineReader.cs` | Engine | Read raw JSON line bytes from file using RowIndexer offsets |
| `src/Engine/IO/JsonLines/JsonLineByteCache.cs` | Engine | Sliding window cache for raw line bytes (`ReadOnlyMemory<byte>`) |
| `src/App/Tui/Ui/Views/JsonTreeNodes.cs` | App | JsonObjectTreeNode, JsonArrayTreeNode, JsonValueTreeNode (inherit TreeNode) |
| `src/App/Tui/Ui/Views/JsonLinesTreeView.cs` | App | Non-generic TreeView wrapper, creates TreeNodes from cached bytes |

### Files to Modify

| File | Change |
|------|--------|
| `src/App/Tui/Ui/MainWindow.cs` | Add `.jsonl` to OpenDialog, add `LoadJsonLinesFileAsync()`, add `SwitchToTreeView()` |
| `src/App/Tui/ViewMode.cs` | Add `JsonLinesTree` enum value |

---

## 6. Dependencies

- **RowIndexer** (implemented): Provides byte offset lookup
- **System.Text.Json**: For `Utf8JsonReader` parsing
- **Terminal.Gui**: For `TreeView`, `TreeNode`, and `ITreeNode`

---

## 7. Existing Code to Reuse

| File | Pattern to Reuse |
|------|------------------|
| `src/Engine/IO/JsonLines/RowIndexer.cs` | Byte offset indexing (already implemented) |
| `src/Engine/IO/CsvDataRowCache.cs` | Sliding window cache pattern |
| `src/App/Tui/Ui/MainWindow.cs` | View switching pattern |

---

## 8. Future Enhancements

1. **Background Root Loading**: Async loading of additional root nodes
2. **Search/Filter**: Search for keys or values within the tree
3. **Copy Path**: Copy JSON path to clipboard (e.g., `[0].details.email`)

---

## 9. References

- Related Issue: Table Mode Virtual Viewport
- Existing Pattern: `CsvDataRowCache`, `TreeView` in Terminal.Gui
- JSON Lines Specification: https://jsonlines.org/
