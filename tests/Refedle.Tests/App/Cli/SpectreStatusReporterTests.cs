using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class SpectreStatusReporterTests
{
    [Fact]
    public async Task RunAsync_ReturnsTheWorkResult()
    {
        // Arrange
        SpectreStatusReporter reporter = new();

        // Act
        int result = await reporter.RunAsync("Working...", _ => ValueTask.FromResult(42));

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_WhenWorkChangesPhases_CompletesWithoutError()
    {
        // Arrange
        SpectreStatusReporter reporter = new();

        // Act
        string result = await reporter.RunAsync("First [with markup]...", async setPhase =>
        {
            setPhase("Second [/] phase...");
            await Task.Yield();
            setPhase("Third...");
            return "done";
        });

        // Assert
        result.Should().Be("done");
    }

    [Fact]
    public async Task RunAsync_WhenWorkThrows_PropagatesTheException()
    {
        // Arrange
        SpectreStatusReporter reporter = new();

        // Act
        Func<Task<int>> act = async () => await reporter.RunAsync<int>(
            "Working...", _ => throw new InvalidOperationException("boom"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task RunAsync_WhenWorkIsCancelled_PropagatesOperationCanceledException()
    {
        // Arrange
        SpectreStatusReporter reporter = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task<int>> act = async () => await reporter.RunAsync<int>(
            "Working...", async _ =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                return 0;
            });

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithNullWork_ThrowsArgumentNullException()
    {
        // Arrange
        SpectreStatusReporter reporter = new();

        // Act
        Func<Task<int>> act = async () => await reporter.RunAsync<int>("Working...", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
