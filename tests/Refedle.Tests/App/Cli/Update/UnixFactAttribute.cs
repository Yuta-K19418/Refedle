namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// A <see cref="FactAttribute"/> that is skipped on Windows. Used for behavior that only
/// applies on Unix (e.g. POSIX file-permission bits), keeping the OS check out of the
/// test body.
/// </summary>
internal sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Unix file permissions are not applicable on Windows.";
        }
    }
}
