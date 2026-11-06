# ADR: Split the JSON Object tree view into range nodes

- Status: Accepted
- Assumption: Files of up to 100 MB. With short keys, there can be millions of keys (up to roughly 10 million).

## Decisions

### 1. Group the top-level keys into range nodes
- **Adopted**: Place range nodes of 1,000 entries each (if there are 1,000 entries or fewer, list the key nodes directly). The slowness comes from the number of roots, so reducing the number of roots resolves it.
- **Rejected**: Keep one root per key and add them in bulk with `AddObjects`. The `IsLast()` enumeration remains, so it stays O(n²).

### 2. Keep entries as `IReadOnlyList<JsonObjectEntry>`
- **Adopted**: Hold the scan result as is, and let a range node fetch its children with `entries[i]`. Access is O(1) and no indexer is needed.
- **Rejected**: Hold only the value positions and re-read them from the file. This reduces memory, but the current estimate is only about 400-500 MB even for millions of keys (not measured), which is accepted as a rare case.

### 3. Create the range nodes at once after the scan completes
- **Adopted**: Create them all at once from `entries.Count`. The data is complete when the scan finishes, and creation takes an instant.
- **Rejected**: Add and render them one at a time. The first display does not get faster; completion only gets later.
- **Rejected**: Add range nodes while the scan is still running. Overwriting of duplicate keys and sharing across threads make it complicated.

### 4. Add the root range nodes in bulk with `AddObjects`
- **Adopted**: Add them all at once with `AddObjects`. Adding them one by one with `AddObject` is O(r³) in the number of roots r, so with about 3,000 roots (3 million keys) the same slowness as the original problem comes back. Adding in bulk brings it down to O(r²).
- **Rejected**: Use super-ranges to keep the number of roots to about 1,000 or fewer. With `AddObjects` alone, about 3,000 roots are expected to take a few tens of milliseconds (not measured). In extreme cases with about 8,000 or more roots, expanding and collapsing may take roughly 0.3-0.5 seconds, which is accepted as a rare case.

### 5. Implement this separately from the streaming approach of JSON Lines / JSON Array
- **Adopted**: Keep `JsonObjectTreeView` directly on top of `MorphTreeView`, and make `JsonObjectRangeTreeNode` inherit from `RangeTreeNodeBase`. Unlike the streaming approach, the number of entries is known from the start, so node addition is kept separate and only the nesting of ranges is reused.
- **Rejected**: Inherit from `RangeTreeViewBase`. It assumes an `IRowIndexer`.
- **Rejected**: Split the base class into two levels. Little is shared, and it would also require changes on the JSON Lines / JSON Array side.

### 6. Use `DelegateTreeBuilder`; do not factor out shared code
- **Adopted**: Pass in the same functions as `RangeTreeViewBase` directly. The default `TreeNodeBuilder` would parse all the children up front.
- **Rejected**: Factor the functions out into shared code. They are a dozen or so lines and rarely change.

### 7. Leave the drill-down check unchanged
- **Adopted**: A range node is rejected by `HandleSingleDrillDown`, so this is only confirmed by a test.
- **Rejected**: Reject it at the top of `HandleActionMenuForTree`. This is outside the scope of this document and is handled separately.
