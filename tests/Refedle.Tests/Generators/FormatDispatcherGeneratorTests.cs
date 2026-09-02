using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Refedle.Generators;

namespace Refedle.Tests.Generators;

public sealed class FormatDispatcherGeneratorTests
{
    private const string HintName = "FormatDispatcher.g.cs";

    // The generated dispatcher relies on the App project's implicit usings for
    // System.Collections.Generic (IReadOnlyList), which the harness compilation lacks.
    private const string GlobalUsingsSource = """
        global using System.Collections.Generic;
        """;

    private const string DataFormatSource = """
        namespace Refedle.Engine.Types;

        public enum DataFormat
        {
            Csv,
            JsonLines,
        }
        """;

    private const string BatchOutputSchemaSource = """
        namespace Refedle.Engine;

        public sealed class BatchOutputSchema
        {
            public IReadOnlyList<string> Columns { get; } = [];
        }
        """;

    private const string KeyPathSegmentSource = """
        namespace Refedle.Engine.IO.DrillDown;

        public enum KeyPathSegmentKind
        {
            Key,
            Index,
        }

        public struct KeyPathSegment
        {
            public KeyPathSegment(string value, KeyPathSegmentKind kind) { }
        }
        """;

    private const string AppCliStubsSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine.Types;

        namespace Refedle.App.Cli;

        public enum ExitCode
        {
            Success,
            Failure,
        }

        public sealed class Arguments
        {
        }

        public interface IAppLogger
        {
        }

        public interface IRecordReader : IDisposable
        {
        }

        public interface IRecordWriter : IDisposable, IAsyncDisposable
        {
        }

        public struct CsvRecordReader : IRecordReader
        {
            public void Dispose() { }
        }

        public struct JsonLinesRecordReader : IRecordReader
        {
            public void Dispose() { }
        }

        public struct CsvRecordWriter : IRecordWriter
        {
            public void Dispose() { }

            public ValueTask DisposeAsync() => default;
        }

        public struct JsonLinesRecordWriter : IRecordWriter
        {
            public void Dispose() { }

            public ValueTask DisposeAsync() => default;
        }

        public static class RecordProcessor
        {
            public static ValueTask<ExitCode> ProcessAsync<TReader, TWriter>(
                TReader reader,
                TWriter writer,
                IReadOnlyList<string> columns,
                CancellationToken ct)
                where TReader : struct, IRecordReader
                where TWriter : struct, IRecordWriter
                => new(ExitCode.Success);
        }

        public sealed class RecordReaderAttribute : Attribute
        {
            public RecordReaderAttribute(DataFormat format) { }
        }

        public sealed class RecordWriterAttribute : Attribute
        {
            public RecordWriterAttribute(DataFormat format) { }
        }
        """;

    private const string FactoryInterfacesSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.IO.DrillDown;

        namespace Refedle.App.Cli.Factories;

        internal interface IRecordReaderFactory<TReader>
            where TReader : struct, IRecordReader
        {
            ValueTask<TReader> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
        }

        internal interface IRecordWriterFactory<TWriter>
            where TWriter : struct, IRecordWriter
        {
            ValueTask<TWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
        }
        """;

    private const string AllFactoriesSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.IO.DrillDown;
        using Refedle.Engine.Types;

        namespace Refedle.App.Cli.Factories;

        [RecordReader(DataFormat.Csv)]
        internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
        {
            public ValueTask<CsvRecordReader> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new CsvRecordReader());
        }

        [RecordReader(DataFormat.JsonLines)]
        internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
        {
            public ValueTask<JsonLinesRecordReader> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new JsonLinesRecordReader());
        }

        [RecordWriter(DataFormat.Csv)]
        internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
        {
            public ValueTask<CsvRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new CsvRecordWriter());
        }

        [RecordWriter(DataFormat.JsonLines)]
        internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
        {
            public ValueTask<JsonLinesRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new JsonLinesRecordWriter());
        }
        """;

    private const string ReaderFactoriesOnlySource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.IO.DrillDown;
        using Refedle.Engine.Types;

        namespace Refedle.App.Cli.Factories;

        [RecordReader(DataFormat.Csv)]
        internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
        {
            public ValueTask<CsvRecordReader> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new CsvRecordReader());
        }

        [RecordReader(DataFormat.JsonLines)]
        internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
        {
            public ValueTask<JsonLinesRecordReader> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new JsonLinesRecordReader());
        }
        """;

    private const string WriterFactoriesOnlySource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.Types;

        namespace Refedle.App.Cli.Factories;

        [RecordWriter(DataFormat.Csv)]
        internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
        {
            public ValueTask<CsvRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new CsvRecordWriter());
        }

        [RecordWriter(DataFormat.JsonLines)]
        internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
        {
            public ValueTask<JsonLinesRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new JsonLinesRecordWriter());
        }
        """;

    // Regression baseline for the current CSV/JSON Lines reader/writer dispatch, captured
    // before DrillDown readers/writers are added. The generator emits
    // Environment.NewLine line breaks, so the snapshot is normalized per platform.
    private const string CurrentDispatchSource = """
        // <auto-generated/>
        #nullable enable
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.Types;
        using Refedle.Engine.IO.DrillDown;
        using Refedle.App.Cli;
        using Refedle.App.Cli.Factories;

        namespace Refedle.App.Cli.Generated;

        internal static class FormatDispatcher
        {
            public static async ValueTask<ExitCode> DispatchAsync(
                DataFormat inputFormat,
                DataFormat outputFormat,
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                return (inputFormat, outputFormat) switch
                {
                    (DataFormat.Csv, DataFormat.Csv) =>
                        await RunCsvToCsvAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
                    (DataFormat.Csv, DataFormat.JsonLines) =>
                        await RunCsvToJsonLinesAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
                    (DataFormat.JsonLines, DataFormat.Csv) =>
                        await RunJsonLinesToCsvAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
                    (DataFormat.JsonLines, DataFormat.JsonLines) =>
                        await RunJsonLinesToJsonLinesAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
                    _ => throw new NotSupportedException($"Unsupported format combination: {inputFormat} -> {outputFormat}")
                };
            }

            private static async ValueTask<ExitCode> RunCsvToCsvAsync(
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                var readerFactory = new CsvRecordReaderFactory();
                using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);
                var writerFactory = new CsvRecordWriterFactory();
                await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);
                return await RecordProcessor.ProcessAsync<CsvRecordReader, CsvRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
            }

            private static async ValueTask<ExitCode> RunCsvToJsonLinesAsync(
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                var readerFactory = new CsvRecordReaderFactory();
                using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);
                var writerFactory = new JsonLinesRecordWriterFactory();
                await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);
                return await RecordProcessor.ProcessAsync<CsvRecordReader, JsonLinesRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
            }

            private static async ValueTask<ExitCode> RunJsonLinesToCsvAsync(
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                var readerFactory = new JsonLinesRecordReaderFactory();
                using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);
                var writerFactory = new CsvRecordWriterFactory();
                await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);
                return await RecordProcessor.ProcessAsync<JsonLinesRecordReader, CsvRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
            }

            private static async ValueTask<ExitCode> RunJsonLinesToJsonLinesAsync(
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                var readerFactory = new JsonLinesRecordReaderFactory();
                using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);
                var writerFactory = new JsonLinesRecordWriterFactory();
                await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);
                return await RecordProcessor.ProcessAsync<JsonLinesRecordReader, JsonLinesRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
            }
        }

        """;

    // A DataFormat with JsonArray, for the nested-generic reader-type regression below.
    private const string DataFormatWithJsonArraySource = """
        namespace Refedle.Engine.Types;

        public enum DataFormat
        {
            Csv,
            JsonLines,
            JsonArray,
        }
        """;

    // A reader factory whose reader type is itself generic:
    // IRecordReaderFactory<FullAggregationRecordReader<JsonArrayBatchSourceReader>>.
    private const string NestedGenericReaderStubsSource = """
        using System;

        namespace Refedle.App.Cli;

        public interface IBatchSourceReader : IDisposable
        {
        }

        public readonly struct JsonArrayBatchSourceReader : IBatchSourceReader
        {
            public void Dispose() { }
        }

        public struct FullAggregationRecordReader<TBatchSourceReader> : IRecordReader
            where TBatchSourceReader : struct, IBatchSourceReader
        {
            public void Dispose() { }
        }
        """;

    private const string NestedGenericFactoriesSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.IO.DrillDown;
        using Refedle.Engine.Types;

        namespace Refedle.App.Cli.Factories;

        [RecordReader(DataFormat.JsonArray)]
        internal readonly struct JsonArrayRecordReaderFactory : IRecordReaderFactory<FullAggregationRecordReader<JsonArrayBatchSourceReader>>
        {
            public ValueTask<FullAggregationRecordReader<JsonArrayBatchSourceReader>> CreateAsync(string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new FullAggregationRecordReader<JsonArrayBatchSourceReader>());
        }

        [RecordWriter(DataFormat.Csv)]
        internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
        {
            public ValueTask<CsvRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
                => new(new CsvRecordWriter());
        }
        """;

    // The closed generic reader type must survive intact into the ProcessAsync<TReader, TWriter>
    // type-argument position — i.e. FullAggregationRecordReader<JsonArrayBatchSourceReader> with
    // its trailing '>'. A first-'>' slice would truncate it and the generated source would not compile.
    private const string NestedGenericDispatchSource = """
        // <auto-generated/>
        #nullable enable
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Refedle.Engine;
        using Refedle.Engine.Types;
        using Refedle.Engine.IO.DrillDown;
        using Refedle.App.Cli;
        using Refedle.App.Cli.Factories;

        namespace Refedle.App.Cli.Generated;

        internal static class FormatDispatcher
        {
            public static async ValueTask<ExitCode> DispatchAsync(
                DataFormat inputFormat,
                DataFormat outputFormat,
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                return (inputFormat, outputFormat) switch
                {
                    (DataFormat.JsonArray, DataFormat.Csv) =>
                        await RunJsonArrayToCsvAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
                    _ => throw new NotSupportedException($"Unsupported format combination: {inputFormat} -> {outputFormat}")
                };
            }

            private static async ValueTask<ExitCode> RunJsonArrayToCsvAsync(
                string inputFile,
                string outputFile,
                IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
                IReadOnlyList<string> inputColumnNames,
                BatchOutputSchema outputSchema,
                IAppLogger logger,
                CancellationToken ct)
            {
                var readerFactory = new JsonArrayRecordReaderFactory();
                using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);
                var writerFactory = new CsvRecordWriterFactory();
                await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);
                return await RecordProcessor.ProcessAsync<FullAggregationRecordReader<JsonArrayBatchSourceReader>, CsvRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
            }
        }

        """;

    [Fact]
    public async Task Execute_WithNestedGenericReaderFactory_KeepsClosedGenericTypeIntact()
    {
        // Arrange
        var test = new CSharpSourceGeneratorTest<FormatDispatcherGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("GlobalUsings.cs", GlobalUsingsSource),
                    ("DataFormat.cs", DataFormatWithJsonArraySource),
                    ("BatchOutputSchema.cs", BatchOutputSchemaSource),
                    ("KeyPathSegment.cs", KeyPathSegmentSource),
                    ("AppCliStubs.cs", AppCliStubsSource),
                    ("NestedGenericReaderStubs.cs", NestedGenericReaderStubsSource),
                    ("FactoryInterfaces.cs", FactoryInterfacesSource),
                    ("Factories.cs", NestedGenericFactoriesSource),
                },
                GeneratedSources =
                {
                    (typeof(FormatDispatcherGenerator), HintName, NestedGenericDispatchSource.ReplaceLineEndings()),
                },
            },
        };

        // Act
        var run = test.RunAsync();

        // Assert
        await run;
    }

    [Fact]
    public async Task Execute_WithCsvAndJsonLinesFactories_GeneratesCurrentDispatchSource()
    {
        // Arrange
        var test = new CSharpSourceGeneratorTest<FormatDispatcherGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("GlobalUsings.cs", GlobalUsingsSource),
                    ("DataFormat.cs", DataFormatSource),
                    ("BatchOutputSchema.cs", BatchOutputSchemaSource),
                    ("KeyPathSegment.cs", KeyPathSegmentSource),
                    ("AppCliStubs.cs", AppCliStubsSource),
                    ("FactoryInterfaces.cs", FactoryInterfacesSource),
                    ("Factories.cs", AllFactoriesSource),
                },
                GeneratedSources =
                {
                    (typeof(FormatDispatcherGenerator), HintName, CurrentDispatchSource.ReplaceLineEndings()),
                },
            },
        };

        // Act
        var run = test.RunAsync();

        // Assert
        await run;
    }

    [Fact]
    public async Task Execute_WithReaderFactoriesOnly_GeneratesNoSource()
    {
        // Arrange
        var test = new CSharpSourceGeneratorTest<FormatDispatcherGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("GlobalUsings.cs", GlobalUsingsSource),
                    ("DataFormat.cs", DataFormatSource),
                    ("BatchOutputSchema.cs", BatchOutputSchemaSource),
                    ("KeyPathSegment.cs", KeyPathSegmentSource),
                    ("AppCliStubs.cs", AppCliStubsSource),
                    ("FactoryInterfaces.cs", FactoryInterfacesSource),
                    ("ReaderFactories.cs", ReaderFactoriesOnlySource),
                },
            },
        };

        // Act
        var run = test.RunAsync();

        // Assert
        await run;
    }

    [Fact]
    public async Task Execute_WithWriterFactoriesOnly_GeneratesNoSource()
    {
        // Arrange
        var test = new CSharpSourceGeneratorTest<FormatDispatcherGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("GlobalUsings.cs", GlobalUsingsSource),
                    ("DataFormat.cs", DataFormatSource),
                    ("BatchOutputSchema.cs", BatchOutputSchemaSource),
                    ("KeyPathSegment.cs", KeyPathSegmentSource),
                    ("AppCliStubs.cs", AppCliStubsSource),
                    ("FactoryInterfaces.cs", FactoryInterfacesSource),
                    ("WriterFactories.cs", WriterFactoriesOnlySource),
                },
            },
        };

        // Act
        var run = test.RunAsync();

        // Assert
        await run;
    }
}
