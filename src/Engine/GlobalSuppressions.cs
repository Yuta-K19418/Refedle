// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified documentation
// signatures, so parameter/event types are spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// DataFormat
[assembly: SuppressMessage(
    "Design",
    "MA0104:Do not create a type with a name from the BCL",
    Scope = "type",
    Target = "~T:Refedle.Engine.Types.DataFormat",
    Justification = "DataFormat is the established public domain type; renaming it would break the public API.")]

// MmapService
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Engine.IO.MmapService.Open(System.String,System.IO.FileAccess)",
    Justification = "MmapService ownership is transferred to the caller via Result<T>")]

// RowIndexerBase — Action-based events are an intentional design decision; see class remarks.
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.RowIndexerBase.FirstCheckpointReached",
    Justification = "See class remarks for rationale")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.RowIndexerBase.ProgressChanged",
    Justification = "See class remarks for rationale")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.RowIndexerBase.BuildIndexCompleted",
    Justification = "See class remarks for rationale")]

// IRowIndexer
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.IRowIndexer.FirstCheckpointReached",
    Justification = "See RowIndexerBase remarks for rationale")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.IRowIndexer.ProgressChanged",
    Justification = "See RowIndexerBase remarks for rationale")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Engine.IO.IRowIndexer.BuildIndexCompleted",
    Justification = "See RowIndexerBase remarks for rationale")]
