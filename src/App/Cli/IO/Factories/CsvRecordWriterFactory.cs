using System.Text;
using Refedle.App.Cli.IO.Csv;
using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.IO.Factories;

[RecordWriter(DataFormat.Csv)]
internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
{
    public ValueTask<CsvRecordWriter> CreateAsync(
        string outputFile,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        // Match the other output writers: UTF-8 without a BOM.
        StreamWriter writer = new(
            outputFile, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new(new CsvRecordWriter(writer, outputSchema));
    }
}
