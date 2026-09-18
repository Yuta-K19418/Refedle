namespace Refedle.Tests.App.Schema;

/// <summary>
/// Places every test class that assigns <see cref="Refedle.App.Schema.IncrementalSchemaScannerBase.ScanStartedHook"/>
/// into one serialized xUnit collection. The hook is process-wide mutable state: without
/// <c>DisableParallelization</c>, concurrently running tests could overwrite each other's hook
/// and miss their <c>scanStarted</c> signals, turning correct builds into timeouts.
/// </summary>
[CollectionDefinition("IncrementalSchemaScannerHook", DisableParallelization = true)]
public sealed class IncrementalSchemaScannerHookCollectionDefinition;
