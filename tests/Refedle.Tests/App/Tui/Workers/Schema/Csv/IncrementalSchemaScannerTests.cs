using System.Diagnostics;
using System.IO.Pipes;
using AwesomeAssertions;
using Refedle.App.Tui.Workers.Schema.Csv;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers.Schema.Csv;

public sealed class IncrementalSchemaScannerTests : IDisposable
{
    private static readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(1);

    private readonly string _tempFilePath;

    public IncrementalSchemaScannerTests()
    {
        _tempFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public async Task InitialScanAsync_WithSimpleCsv_ReturnsSchema()
    {
        // Arrange
        var csvContent = "Id,Name,Age\nvalue1,value2,value3\nvalue4,value5,value6";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.ColumnCount.Should().Be(3);
        schema.Columns[0].Name.Should().Be("Id");
        schema.Columns[1].Name.Should().Be("Name");
        schema.Columns[2].Name.Should().Be("Age");
    }

    [Fact]
    public async Task InitialScanAsync_WithHeaderOnlyFile_ReturnsDefaultSchema()
    {
        // Arrange
        var csvContent = "Id,Name,Age\n";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.ColumnCount.Should().Be(3);
        schema.Columns[0].Name.Should().Be("Id");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task InitialScanAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var csvContent = "";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task InitialScanAsync_WithNumericValues_DetectsNumericTypes()
    {
        // Arrange
        var csvContent = "Id,Salary,Bonus\n123,45.6,789\n100,200.5,300";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[1].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[2].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public async Task InitialScanAsync_WithBooleanValues_DetectsBooleanTypes()
    {
        // Arrange
        var csvContent = "IsActive,IsVerified,IsCompleted\ntrue,false,TRUE\nFALSE,true,False";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[1].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[2].Type.Should().Be(ColumnType.Boolean);
    }

    [Fact]
    public async Task InitialScanAsync_WithMixedValues_DetectsTextTypes()
    {
        // Arrange
        var csvContent = "ProductId,Description,Price\n123,text,45.6\ntext,789,text";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public async Task InitialScanAsync_WithMissingValues_MarksNullable()
    {
        // Arrange
        var csvContent = "FirstName,MiddleName,LastName\n,,Doe\nJohn,,\n,,Smith";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].IsNullable.Should().BeTrue();
        schema.Columns[1].IsNullable.Should().BeTrue();
        schema.Columns[2].IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task InitialScanAsync_Constructor_WithNonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
        var scanner = new IncrementalSchemaScanner(nonexistentPath);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task StartBackgroundScanAsync_CancelledWhileReadingRows_ReturnsCurrentSchema()
    {
        // Arrange
        // The row after the initial 200 puts text into the numeric Id column, so a scan
        // that ignored cancellation would return a refined schema instead of currentSchema.
        var initialRows = string.Concat(Enumerable.Repeat("1,a\n", 200));
        var csvContent = "Id,Name\n" + initialRows + "text,b\n";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var fileScanner = new IncrementalSchemaScanner(_tempFilePath);
        var currentSchema = await fileScanner.InitialScanAsync();

        // The pipe read blocks until the writer opens and supplies rows, so the token is
        // cancelled while the scan is waiting for its remaining rows.
        await using var pipe = await CreateScanPipeAsync();
        var pipeScanner = new IncrementalSchemaScanner(pipe.PipePath);
        using var cts = new CancellationTokenSource();
        var backgroundTask = pipeScanner.StartBackgroundScanAsync(currentSchema, cts.Token);
        var writer = await pipe.WriterTask.WaitAsync(_waitTimeout);

        // Act
        cts.Cancel();
        await using (writer)
        {
            await writer.WriteAsync(csvContent);
        }

        var finalSchema = await backgroundTask.WaitAsync(_waitTimeout);

        // Assert
        finalSchema.Should().BeSameAs(currentSchema);
    }

    /// <summary>
    /// Creates a pipe the background scan reads from: a named pipe on Windows, a FIFO on
    /// Unix. The Windows server stream is created synchronously here, before the scan
    /// starts, so the scan's open cannot lose the race with it. The writer task's open
    /// blocks until the scan opens the read side, so both branches run it on the thread
    /// pool; awaiting it means the scan is reading.
    /// </summary>
    private static async Task<ScanPipe> CreateScanPipeAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            var pipeName = "refedle-scan-" + Guid.NewGuid().ToString("N");
            return new ScanPipe(@"\\.\pipe\" + pipeName, fifoPath: null, pipeName);
        }

        var tempDirectory = Path.GetTempPath();
        var fifoName = "refedle-scan-" + Guid.NewGuid().ToString("N") + ".csv";
        var fifoPath = Path.Combine(tempDirectory, fifoName);
        using var fifoProcess = Process.Start("mkfifo", fifoPath);
        await fifoProcess.WaitForExitAsync().WaitAsync(_waitTimeout);
        fifoProcess.ExitCode.Should().Be(0);
        return new ScanPipe(fifoPath, fifoPath, pipeName: null);
    }

    /// <summary>
    /// The pipe the background scan reads from, with a writer task that completes once
    /// the scan opens the read side. Disposing runs even when the test fails partway;
    /// it deletes the FIFO file on Unix and disposes the Windows server stream, which the
    /// test's writer disposal has normally already closed.
    /// </summary>
    private sealed class ScanPipe(string pipePath, string? fifoPath, string? pipeName) : IAsyncDisposable
    {
        private readonly NamedPipeServerStream? _serverStream = pipeName is null
            ? null
            : new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);

        private Task<StreamWriter>? _writerTask;
        private StreamWriter? _namedPipeWriter;

        public string PipePath { get; } = pipePath;

        public Task<StreamWriter> WriterTask => _writerTask ??= StartWriter();

        private Task<StreamWriter> StartWriter()
        {
            return _serverStream is not null
                ? ConnectWriterAsync(_serverStream)
                : Task.Run(() => new StreamWriter(PipePath));
        }

        private async Task<StreamWriter> ConnectWriterAsync(NamedPipeServerStream serverStream)
        {
            await serverStream.WaitForConnectionAsync();
            _namedPipeWriter = new StreamWriter(serverStream);
            return _namedPipeWriter;
        }

        public async ValueTask DisposeAsync()
        {
            if (fifoPath is not null)
            {
                File.Delete(fifoPath);
            }

            if (_namedPipeWriter is not null)
            {
                await _namedPipeWriter.DisposeAsync();
            }

            if (_serverStream is not null)
            {
                await _serverStream.DisposeAsync();
            }
        }
    }
}
