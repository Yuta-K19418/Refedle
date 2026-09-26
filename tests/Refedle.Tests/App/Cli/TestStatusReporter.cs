namespace Refedle.Tests.App.Cli;

/// <summary>
/// Displays nothing and records the phase names the code under test reports.
/// </summary>
internal sealed class TestStatusReporter : Refedle.App.Cli.IStatusReporter
{
    private readonly List<string> _phases = [];

    public IReadOnlyList<string> Phases => _phases.AsReadOnly();

    /// <inheritdoc/>
    public ValueTask<T> RunAsync<T>(string initialPhase, Func<Action<string>, ValueTask<T>> work)
    {
        _phases.Add(initialPhase);
        return work(_phases.Add);
    }
}
