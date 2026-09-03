// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified documentation
// signatures, so member types are spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// IndexTaskManagerTests
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.IndexTaskManagerTests.Dispose_WithoutPriorStart_DoesNotThrow",
    Justification = "manager is disposed via act() below; suppress false positive.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.IndexTaskManagerTests.CancelCurrent_AfterDisposal_ThrowsObjectDisposedException",
    Justification = "manager is disposed via manager.Dispose() below; suppress false positive.")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Tests.App.IndexTaskManagerTests.BlockingIndexer.FirstCheckpointReached",
    Justification = "Test stub mirrors IRowIndexer's Action-based event contract.")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Tests.App.IndexTaskManagerTests.BlockingIndexer.ProgressChanged",
    Justification = "Test stub mirrors IRowIndexer's Action-based event contract.")]
[assembly: SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Scope = "member",
    Target = "~E:Refedle.Tests.App.IndexTaskManagerTests.BlockingIndexer.BuildIndexCompleted",
    Justification = "Test stub mirrors IRowIndexer's Action-based event contract.")]

// JsonLinesTableSourceTests
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Views.JsonLinesTableSourceTests.Dispose_DisposesRowByteCache",
    Justification = "Ownership transferred to source")]

// JsonArrayBatchSourceReaderTests
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Cli.JsonArrayBatchSourceReaderTests.ReadBatch_ReturnsRawElementBytesFromTheCheckpoint",
    Justification = "ElementReader ownership is transferred to the JsonArrayBatchSourceReader under test, which is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Cli.JsonArrayBatchSourceReaderTests.ReadBatch_AfterDispose_ThrowsObjectDisposedException",
    Justification = "ElementReader ownership is transferred to the JsonArrayBatchSourceReader under test, which is disposed.")]

// JsonLinesBatchSourceReaderTests
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Cli.JsonLinesBatchSourceReaderTests.ReadBatch_ReturnsRawLineBytesFromTheCheckpoint",
    Justification = "RowReader ownership is transferred to the JsonLinesBatchSourceReader under test, which is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Cli.JsonLinesBatchSourceReaderTests.ReadBatch_AfterDispose_ThrowsObjectDisposedException",
    Justification = "RowReader ownership is transferred to the JsonLinesBatchSourceReader under test, which is disposed.")]

// FullAggregationRecordReaderTests (JSON Lines)
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Tests.App.Cli.FullAggregationRecordReaderTests.BuildJsonLinesReader(System.Collections.Generic.IReadOnlyList{System.String},System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.DrillDown.KeyPathSegment},System.Collections.Generic.IReadOnlyList{System.String},System.Collections.Generic.IReadOnlyList{Refedle.Engine.Filtering.BatchFilterSpec})",
    Justification = "RowReader ownership is transferred to the reader under test; each test disposes it.")]
