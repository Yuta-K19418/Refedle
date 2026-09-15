namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Mints a temp-file path for an atomic output write.
/// </summary>
internal interface ITempOutputPathProvider
{
    /// <summary>
    /// Returns a new temp-file path in the same directory as <paramref name="outputFile"/>.
    /// The name is highly collision-resistant rather than mathematically guaranteed unique:
    /// it is a fresh random name, so an accidental match with an unrelated existing file is
    /// practically negligible. Computes a string only — no file is created; the physical
    /// file is created later by the writer factory, which opens the path exactly once (no
    /// reserve-then-reopen gap).
    /// </summary>
    /// <param name="outputFile">The real output path the temp file will be renamed to.</param>
    /// <returns>A new, highly collision-resistant path in the same directory as <paramref name="outputFile"/>.</returns>
    string NewPath(string outputFile);
}
