using Refedle.App.Cli.Commands.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Commands.Update;

/// <summary>
/// Returns a fixed RID result so update-flow tests are independent of the host platform
/// (CI runs on Windows, where <c>refedle update</c> is unsupported).
/// </summary>
internal sealed class StubRuntimeIdentifierResolver(Result<string> rid) : IRuntimeIdentifierResolver
{
    public Result<string> Resolve() => rid;
}
