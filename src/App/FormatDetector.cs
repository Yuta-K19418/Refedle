using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App;

/// <summary>
/// Detects the data format of a file based on its extension.
/// </summary>
internal static class FormatDetector
{
    /// <summary>
    /// Detects the format of an existing input file. For <c>.json</c>, the root token
    /// is inspected to distinguish a JSON Object from a JSON Array.
    /// </summary>
    /// <param name="filePath">The path to the input file.</param>
    /// <returns>A <see cref="Result{DataFormat}"/> indicating the detected format or failure.</returns>
    public static Result<DataFormat> DetectInputFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Results.Failure<DataFormat>("File path cannot be empty");
        }

        if (!File.Exists(filePath))
        {
            return Results.Failure<DataFormat>("File does not exist");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return Results.Failure<DataFormat>("File is empty");
        }

        // Use ToUpperInvariant for normalization as recommended by CA1308 to avoid
        // potential round-tripping issues in some locales (e.g., Turkish 'I').
        var rawExtension = Path.GetExtension(filePath);
        var extension = rawExtension.ToUpperInvariant();

        return extension switch
        {
            ".CSV" => Results.Success(DataFormat.Csv),
            ".JSONL" => Results.Success(DataFormat.JsonLines),
            ".JSON" => DetectJsonFormat(filePath),
            _ => Results.Failure<DataFormat>($"Unsupported file format: {rawExtension}"),
        };
    }

    /// <summary>
    /// Detects the format of an output file from its extension only — the file does
    /// not exist yet, so its content cannot be inspected. <c>.json</c> output is
    /// always JSON Array shaped.
    /// </summary>
    /// <param name="filePath">The path of the output file to be written.</param>
    /// <returns>A <see cref="Result{DataFormat}"/> indicating the detected format or failure.</returns>
    public static Result<DataFormat> DetectOutputFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Results.Failure<DataFormat>("File path cannot be empty");
        }

        var rawExtension = Path.GetExtension(filePath);
        var extension = rawExtension.ToUpperInvariant();

        return extension switch
        {
            ".CSV" => Results.Success(DataFormat.Csv),
            ".JSONL" => Results.Success(DataFormat.JsonLines),
            ".JSON" => Results.Success(DataFormat.JsonArray),
            _ => Results.Failure<DataFormat>($"Unsupported file format: {rawExtension}"),
        };
    }

    /// <summary>
    /// Distinguishes JSON Object from JSON Array by peeking at the first non-whitespace byte.
    /// </summary>
    /// <param name="filePath">Path to the .json file.</param>
    /// <returns>
    /// <see cref="DataFormat.JsonObject"/> if the root token is <c>{</c>,
    /// <see cref="DataFormat.JsonArray"/> if the root token is <c>[</c>,
    /// or a failure result for any other root token or whitespace-only content.
    /// </returns>
    private static Result<DataFormat> DetectJsonFormat(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            SkipUtf8BomIfPresent(fs);

            var b = fs.ReadByte();
            while (b != -1 && char.IsWhiteSpace((char)b))
            {
                b = fs.ReadByte();
            }

            if (b == -1)
            {
                return Results.Failure<DataFormat>("File contains no valid JSON root token");
            }

            return (char)b switch
            {
                '{' => Results.Success(DataFormat.JsonObject),
                '[' => Results.Success(DataFormat.JsonArray),
                _ => Results.Failure<DataFormat>($"Unrecognized JSON root token: '{(char)b}'"),
            };
        }
        catch (IOException ex)
        {
            return Results.Failure<DataFormat>($"Failed to read file: {ex.Message}");
        }
    }

    private static void SkipUtf8BomIfPresent(FileStream fs)
    {
        Span<byte> bom = stackalloc byte[3];
        try
        {
            fs.ReadExactly(bom);
        }
        catch (EndOfStreamException)
        {
            fs.Seek(0, SeekOrigin.Begin);
            return;
        }

        if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            return;
        }

        fs.Seek(-3, SeekOrigin.Current);
    }
}
