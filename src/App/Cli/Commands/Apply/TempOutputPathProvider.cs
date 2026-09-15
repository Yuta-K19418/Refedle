namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Production <see cref="ITempOutputPathProvider"/>: combines the output file's directory
/// with a fresh GUID name (~122 random bits, versus ~55 for a random file name), pushing
/// the residual collision risk down to a practically negligible level. Keeping the temp
/// file in the same directory guarantees it lives on the same volume as the real output
/// path, so the final <c>File.Move</c> publish stays an atomic same-volume rename instead
/// of a copy.
/// </summary>
internal sealed class TempOutputPathProvider : ITempOutputPathProvider
{
    /// <inheritdoc/>
    public string NewPath(string outputFile) =>
        Path.Combine(Path.GetDirectoryName(outputFile) ?? string.Empty, Guid.NewGuid().ToString("N"));
}
