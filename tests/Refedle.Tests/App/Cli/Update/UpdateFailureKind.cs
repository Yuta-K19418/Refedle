namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// The kind of infrastructure exception a throwing fake should raise, so
/// <c>UpdateCommand</c> exception-translation tests can be driven from <c>[InlineData]</c>.
/// </summary>
public enum UpdateFailureKind
{
    Network,
    Timeout,
    FileIo,
    CorruptArchive,
    PermissionDenied,
}
