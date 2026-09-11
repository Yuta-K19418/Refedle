namespace Refedle.App.Cli;

/// <summary>
/// Identifies which headless CLI command matched the raw command-line arguments.
/// </summary>
internal enum CliCommand
{
    /// <summary>The <c>refedle help</c> / <c>--help</c> / <c>-h</c> command.</summary>
    Help,

    /// <summary>The <c>refedle version</c> / <c>--version</c> command.</summary>
    Version,

    /// <summary>The <c>refedle update</c> command.</summary>
    Update,

    /// <summary>The <c>refedle apply</c> command.</summary>
    Apply,
}
