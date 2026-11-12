// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified
// documentation-comment signatures (e.g. ~M:N.T.Method(ParamType)), so parameter
// types must be spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// HelpDialog
[assembly: SuppressMessage(
    "Usage",
    "MA0136:Raw String contains an implicit end of line character",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.Views.Dialogs.HelpDialog.HelpText",
    Justification = "Repository policy fixes C# source files to LF, so the UI text is deterministic.")]

// Cli.HelpCommand
[assembly: SuppressMessage(
    "Usage",
    "MA0136:Raw String contains an implicit end of line character",
    Scope = "member",
    Target = "~F:Refedle.App.Cli.Commands.Help.HelpCommand.HelpText",
    Justification = "Repository policy fixes C# source files to LF, so the help text is deterministic.")]

// ViewManager
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.ViewManager._breadcrumbBar",
    Justification = "BreadcrumbBar is added to the Window (_container) and is disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.ViewManager._contentContainer",
    Justification = "ContentContainer is added to the Window (_container) and is disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToCsvTable(Refedle.Engine.IO.IRowIndexer,Refedle.Engine.Models.TableSchema)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToJsonLinesTree(Refedle.Engine.IO.IRowIndexer)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToJsonArrayTree(Refedle.Engine.IO.IRowIndexer)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToJsonObjectTree(System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.JsonObject.JsonObjectEntry})",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToJsonLinesTableView(Refedle.Engine.IO.IRowIndexer,Refedle.Engine.Models.TableSchema)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.RebuildLazyTransformer(Refedle.App.Tui.UI.Views.JsonLinesTableView,Refedle.App.Tui.UI.Views.JsonLinesTableSource,Refedle.App.Tui.UI.Views.LazyTransformer,Refedle.Engine.Models.TableSchema)",
    Justification = "The new table source is owned by the view and disposed with it.")]
[assembly: SuppressMessage(
    "Style",
    "IDE0010:Populate switch",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.RefreshCurrentTableView",
    Justification = "Only CsvTable/JsonLinesTable refresh here; all other modes are a no-op via the default arm.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.ShowError(System.String)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.SwitchToFocusedTable(Refedle.App.Tui.DrillDownState)",
    Justification = "Owned by container via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.CreateFocusedTableView(Refedle.App.Tui.UI.Views.IExtendedTableSource,Terminal.Gui.Views.TableStyle)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Style",
    "IDE0010:Populate switch",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.ViewManager.ReturnFromDrillDown",
    Justification = "Only tree ViewMode values are valid PreviousMode; the default arm throws for any other member.")]

// FilterScanManager
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.FilterScanManager._session",
    Justification = "The session is disposed on the UI thread by CancelScan (view swap, FilterScanManager.Dispose) or released by CompleteScan, which hands the transformer to the table view and disposes the CTS alone.")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FilterScanManager.RunScanAsync(System.Func{System.Boolean})",
    Justification = "Top-level observer of a fire-and-forget scan: a row-read fault is arbitrary, must be observed, and is marshaled to the UI thread.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FilterScanManager.StartScan(Refedle.App.Tui.UI.Views.FocusedTableTransformer,Refedle.App.Tui.UI.Views.FocusedTableFilterRowIndexer,Terminal.Gui.Views.TableStyle)",
    Justification = "The loading view is owned by the container via the swap-view delegate; the session owns the linked CTS and the transformer, and disposes both on the UI thread when the scan is cancelled (the completed path releases the CTS alone and hands the transformer to the table view).")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.FilterScanManager._tableView",
    Justification = "While set, the table view is the current content view and is disposed via the ViewManager's current-view tracking.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FilterScanSession.Dispose",
    Justification = "Cancellation must complete before the token source is disposed. Session state is UI-thread exclusive and worker notifications re-enter through the UI-thread dispatcher, so Dispose runs on the UI thread where synchronous cancel is the established pattern.")]

// MainWindow
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.MainWindow.InitializeMenu",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Design",
    "MA0147:Avoid async void method for delegate",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.MainWindow.InitializeMenu",
    Justification = "Terminal.Gui menu callbacks require Action, so asynchronous commands must use async void.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.MainWindow.InitializeStatusBar",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]

// AppKeyHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleHelp",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleActionMenuForTable(Refedle.App.Tui.UI.Views.MorphTableView)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleSingleDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleFullAggregationDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]

// RecipeCommandHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.RecipeCommandHandler.SaveAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.RecipeCommandHandler.LoadAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]

// FileDialogHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FileDialogHandler.ShowAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FileDialogHandler.LoadJsonObjectAsync(System.String)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FileDialogHandler.LoadCsvAsync(System.String,Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FileDialogHandler.LoadJsonLinesAsync(Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.FileDialogHandler.LoadJsonArrayAsync(Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]

// KeyPathBuilder
[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.KeyPathBuilder.Build(Terminal.Gui.Views.ITreeNode)",
    Justification = "IReadOnlyList<KeyPathSegment> is the KeyPath contract shared with FullAggregationDrillDownRequest; the concrete List<KeyPathSegment> used to build it is an implementation detail that should not leak out.")]

// TuiApplication
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.TuiApplication.Create",
    Justification = "The created IApplication and MainWindow are returned to the caller, which is responsible for disposal.")]

// Cli.JsonLinesRecordWriter
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.IO.Json.JsonLinesRecordWriter",
    Justification = "JsonLinesRecordWriter is a struct designed for monomorphization as per ADR. It implements IRecordWriter which inherits from IDisposable and IAsyncDisposable, but CA1001 analyzer may be confused by structs or specific field types.")]

// Cli.JsonArrayRecordWriter
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.IO.Json.JsonArrayRecordWriter",
    Justification = "JsonArrayRecordWriter is a struct designed for monomorphization as per ADR. It implements IRecordWriter which inherits from IDisposable and IAsyncDisposable, but CA1001 analyzer may be confused by structs or specific field types — same false positive as JsonLinesRecordWriter.")]

// Cli.BareJsonLinesRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.IO.Json.BareJsonLinesRecordReader",
    Justification = "BareJsonLinesRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordWriter.")]

// Cli.JsonObjectRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.IO.Json.JsonObjectRecordReader",
    Justification = "JsonObjectRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordReader.")]

// Cli.FullAggregationRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.IO.Json.FullAggregationRecordReader`1",
    Justification = "FullAggregationRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _batchSource and _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordReader.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA1849:Call async methods when in an async method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Sonar Code Smell",
    "S6966",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Design",
    "MA0042:Use the async version of a method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Reliability",
    "CA1849:Call async methods when in an async method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonArrayRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Sonar Code Smell",
    "S6966",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonArrayRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Design",
    "MA0042:Use the async version of a method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Json.JsonArrayRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]

// Cli.Runner
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Commands.Apply.Runner.RunAsync(Refedle.App.Cli.Parsing.Arguments,Refedle.App.Cli.IAppLogger,Refedle.App.Cli.Commands.Apply.IFormatDispatcher,Refedle.App.Cli.Commands.Apply.ITempOutputPathProvider,Refedle.App.Cli.IStatusReporter,System.Threading.CancellationToken)",
    Justification = "Top-level CLI handler reports any unexpected exception as an error exit code.")]

// Cli.DryRunner
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Commands.Apply.DryRunner.RunAsync(Refedle.App.Cli.Parsing.Arguments,Refedle.App.Cli.IAppLogger,Refedle.App.Cli.IStatusReporter,System.Threading.CancellationToken)",
    Justification = "Top-level CLI handler reports any unexpected exception as an error exit code.")]

// Cli.IO.Factories
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Factories.JsonLinesRecordWriterFactory.CreateAsync(System.String,Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Factories.JsonArrayRecordWriterFactory.CreateAsync(System.String,Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Factories.JsonLinesRecordReaderFactory.CreateAsync(System.String,System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.DrillDown.KeyPathSegment},System.Collections.Generic.IReadOnlyList{System.String},Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Factories.JsonArrayRecordReaderFactory.CreateAsync(System.String,System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.DrillDown.KeyPathSegment},System.Collections.Generic.IReadOnlyList{System.String},Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.IO.Factories.CsvRecordWriterFactory.CreateAsync(System.String,Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]

// Views.Dialogs
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.DeleteColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.FilterColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.CastColumnDialog.#ctor(System.String,Refedle.Engine.Types.ColumnType,Refedle.Engine.Types.DataFormat)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.RenameColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.HelpDialog.#ctor",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.FormatTimestampDialog.#ctor(System.String)",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.FillColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Tui.UI.Views.Dialogs.ActionMenuDialog._listView",
    Justification = "Child views added to Dialog will be disposed automatically when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.Dialogs.ActionMenuDialog.#ctor(System.String[],System.Action{System.String})",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.LoadingView.#ctor(System.String)",
    Justification = "The label is owned by the view and disposed when the view is disposed.")]

// Intentional synchronous UI and scan operations
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.CliCancellationScope.HandleCancelKeyPress(System.Object,System.ConsoleCancelEventArgs)",
    Justification = "ConsoleCancelEventHandler must cancel synchronously when the process receives Ctrl+C.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.AppState.RenewCtsWithCancel",
    Justification = "Cancellation must complete before the replaced token source is disposed.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.FormatDetector.DetectJsonFormat(System.String)",
    Justification = "This small, synchronous format probe would otherwise require changing the detector API.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.Workers.Schema.Csv.IncrementalSchemaScanner.ReadColumnNames",
    Justification = "The scanner base contract runs this header reader synchronously on a background task.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleHelp",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleActionMenuForTable(Refedle.App.Tui.UI.Views.MorphTableView)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleSingleDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.AppKeyHandler.HandleFullAggregationDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleRenameColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleDeleteColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleCastColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleFilterColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleFillColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Tui.UI.Views.ColumnActionHandler.HandleFormatTimestamp",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
