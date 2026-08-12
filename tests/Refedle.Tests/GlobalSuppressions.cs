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
