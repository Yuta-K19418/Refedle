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
        StreamWriter writer = new(outputFile, append: false, Encoding.UTF8);
        return new(new CsvRecordWriter(writer, outputSchema));
    }
}
