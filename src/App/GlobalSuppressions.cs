// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified
// documentation-comment signatures (e.g. ~M:N.T.Method(ParamType)), so parameter
// types must be spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// HelpDialog
[assembly: SuppressMessage(
    "Usage",
    "MA0136:Raw String contains an implicit end of line character",
    Scope = "member",
    Target = "~F:Refedle.App.Views.Dialogs.HelpDialog.HelpText",
    Justification = "Repository policy fixes C# source files to LF, so the UI text is deterministic.")]

// ViewManager
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.ViewManager._breadcrumbBar",
    Justification = "BreadcrumbBar is added to the Window (_container) and is disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.ViewManager._contentContainer",
    Justification = "ContentContainer is added to the Window (_container) and is disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToCsvTable(Refedle.Engine.IO.IRowIndexer,Refedle.Engine.Models.TableSchema)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToJsonLinesTree(Refedle.Engine.IO.IRowIndexer)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToJsonArrayTree(Refedle.Engine.IO.IRowIndexer)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToJsonObjectTree(System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.JsonObject.JsonObjectEntry})",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToJsonLinesTableView(Refedle.Engine.IO.IRowIndexer,Refedle.Engine.Models.TableSchema)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Style",
    "IDE0010:Populate switch",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.RefreshCurrentTableView",
    Justification = "Only CsvTable/JsonLinesTable refresh here; all other modes are a no-op via the default arm.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.ShowError(System.String)",
    Justification = "Child views are owned by the container and disposed via SwapView.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.SwitchToFocusedTable(Refedle.App.DrillDownState)",
    Justification = "Owned by container via SwapView.")]
[assembly: SuppressMessage(
    "Style",
    "IDE0010:Populate switch",
    Scope = "member",
    Target = "~M:Refedle.App.ViewManager.ReturnFromDrillDown",
    Justification = "Only tree ViewMode values are valid PreviousMode; the default arm throws for any other member.")]

// MainWindow
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.MainWindow._progressBar",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.MainWindow._progressLabel",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.MainWindow.InitializeMenu",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Design",
    "MA0147:Avoid async void method for delegate",
    Scope = "member",
    Target = "~M:Refedle.App.MainWindow.InitializeMenu",
    Justification = "Terminal.Gui menu callbacks require Action, so asynchronous commands must use async void.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.MainWindow.InitializeStatusBar",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.MainWindow.ShowIndexingProgress",
    Justification = "Child views added to the Window will be disposed automatically when the Window is disposed.")]

// AppKeyHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleHelp",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleActionMenuForTable(Refedle.App.Views.MorphTableView)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleSingleDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleFullAggregationDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "The dialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]

// RecipeCommandHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.RecipeCommandHandler.SaveAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.RecipeCommandHandler.LoadAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]

// FileDialogHandler
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.ShowAsync",
    Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically.")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.LoadJsonObjectAsync(System.String)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.LoadCsvAsync(System.String,Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.LoadJsonLinesAsync(Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.LoadJsonArrayAsync(Refedle.Engine.IO.IRowIndexer)",
    Justification = "UI top-level handler")]

// KeyPathBuilder
[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Scope = "member",
    Target = "~M:Refedle.App.KeyPathBuilder.Build(Terminal.Gui.Views.ITreeNode)",
    Justification = "IReadOnlyList<KeyPathSegment> is the KeyPath contract shared with FullAggregationDrillDownRequest; the concrete List<KeyPathSegment> used to build it is an implementation detail that should not leak out.")]

// TuiApplication
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.TuiApplication.Create",
    Justification = "The created IApplication and MainWindow are returned to the caller, which is responsible for disposal.")]

// Cli.JsonLinesRecordWriter
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.JsonLinesRecordWriter",
    Justification = "JsonLinesRecordWriter is a struct designed for monomorphization as per ADR. It implements IRecordWriter which inherits from IDisposable and IAsyncDisposable, but CA1001 analyzer may be confused by structs or specific field types.")]

// Cli.JsonLinesRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.JsonLinesRecordReader",
    Justification = "JsonLinesRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordWriter.")]

// Cli.JsonObjectRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.JsonObjectRecordReader",
    Justification = "JsonObjectRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordReader.")]

// Cli.FullAggregationRecordReader
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.App.Cli.FullAggregationRecordReader`1",
    Justification = "FullAggregationRecordReader is a struct designed for monomorphization (RecordProcessor.ProcessAsync<TReader, TWriter>). It implements IRecordReader (which inherits IDisposable) and disposes _batchSource and _valueBuffer in Dispose(), but the CA1001 analyzer is confused by structs — same false positive as JsonLinesRecordReader.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA1849:Call async methods when in an async method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Sonar Code Smell",
    "S6966",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]
[assembly: SuppressMessage(
    "Design",
    "MA0042:Use the async version of a method",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.JsonLinesRecordWriter.WriteEndRecordAsync(System.Threading.CancellationToken)",
    Justification = "Flush to IBufferWriter is synchronous and fast")]

// Cli.Runner
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Runner.RunAsync(Refedle.App.Cli.Arguments,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Top-level CLI handler reports any unexpected exception as an error exit code.")]

// Cli.Factories
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Factories.JsonLinesRecordWriterFactory.CreateAsync(System.String,Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Factories.JsonLinesRecordReaderFactory.CreateAsync(System.String,System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.DrillDown.KeyPathSegment},System.Collections.Generic.IReadOnlyList{System.String},Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Factories.JsonArrayRecordReaderFactory.CreateAsync(System.String,System.Collections.Generic.IReadOnlyList{Refedle.Engine.IO.DrillDown.KeyPathSegment},System.Collections.Generic.IReadOnlyList{System.String},Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Cli.Factories.CsvRecordWriterFactory.CreateAsync(System.String,Refedle.Engine.BatchOutputSchema,Refedle.App.Cli.IAppLogger,System.Threading.CancellationToken)",
    Justification = "Ownership is transferred to the caller.")]

// Views.Dialogs
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.DeleteColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.FilterColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.CastColumnDialog.#ctor(System.String,Refedle.Engine.Types.ColumnType,Refedle.Engine.Types.DataFormat)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.RenameColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.HelpDialog.#ctor",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.FormatTimestampDialog.#ctor(System.String)",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.FillColumnDialog.#ctor(System.String)",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Scope = "member",
    Target = "~F:Refedle.App.Views.Dialogs.ActionMenuDialog._listView",
    Justification = "Child views added to Dialog will be disposed automatically when the Dialog is disposed.")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.App.Views.Dialogs.ActionMenuDialog.#ctor(System.String[],System.Action{System.String})",
    Justification = "Child views are owned by Dialog and disposed when Dialog is disposed.")]

// Intentional synchronous UI and scan operations
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Program.<Main>$(System.String[])",
    Justification = "ConsoleCancelEventHandler must cancel synchronously when the process receives Ctrl+C.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.AppState.RenewCtsWithCancel",
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
    Target = "~M:Refedle.App.AppKeyHandler.HandleHelp",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleActionMenuForTable(Refedle.App.Views.MorphTableView)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleSingleDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.AppKeyHandler.HandleFullAggregationDrillDown(Terminal.Gui.Views.ITreeNode,Refedle.Engine.Types.DataFormat)",
    Justification = "Terminal.Gui modal dialogs run synchronously from key handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.ModeController.ToggleJsonLinesModeAsync",
    Justification = "Result is accessed only after the continuation has completed successfully.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.FileDialogHandler.LoadCsvAsync(System.String,Refedle.Engine.IO.IRowIndexer)",
    Justification = "Result is accessed only after the continuation has completed successfully.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Schema.Csv.IncrementalSchemaScanner.ReadRows(System.Int32,System.Int32)",
    Justification = "The scanner base contract runs this batch reader synchronously on a background task.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Schema.Csv.IncrementalSchemaScanner.ReadColumnNames",
    Justification = "The scanner base contract runs this header reader synchronously on a background task.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleRenameColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleDeleteColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleCastColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleFilterColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleFillColumn",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
[assembly: SuppressMessage(
    "Design",
    "MA0045:Do not use blocking calls, even when the calling method must become async",
    Scope = "member",
    Target = "~M:Refedle.App.Views.ColumnActionHandler.HandleFormatTimestamp",
    Justification = "Terminal.Gui modal dialogs run synchronously from action handlers.")]
