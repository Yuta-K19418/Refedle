namespace Refedle.Engine.Filtering;

/// <summary>
/// Decides whether a scan position is worth reporting, so progress observers are not flooded.
/// A position is reported once the scan has advanced by at least 1% of the total rows since the
/// last reported position; the final position is always reported.
/// </summary>
public sealed class ScanProgressThrottle
{
    private const int NotificationStepDivisor = 100;

    private int _lastNotifiedRows;

    /// <summary>
    /// Returns <see langword="true"/> when the caller should report the given scan position.
    /// A non-final position must strictly advance past the last recorded one (even when the step is
    /// zero for totals under 100 rows); the final position always returns true, including repeats.
    /// Safe to call from multiple threads; the recorded position only moves forward, and a
    /// position that still qualifies after losing a race is retried against the newer position.
    /// </summary>
    /// <param name="scannedRows">Rows scanned so far.</param>
    /// <param name="totalRows">Total rows to scan.</param>
    public bool ShouldNotify(int scannedRows, int totalRows)
    {
        var isFinal = scannedRows >= totalRows;
        var notificationStep = totalRows / NotificationStepDivisor;
        var lastNotifiedRows = Volatile.Read(ref _lastNotifiedRows);
        while (scannedRows > lastNotifiedRows && (isFinal || scannedRows - lastNotifiedRows >= notificationStep))
        {
            var observedRows = Interlocked.CompareExchange(ref _lastNotifiedRows, scannedRows, lastNotifiedRows);
            if (observedRows == lastNotifiedRows)
            {
                return true;
            }

            lastNotifiedRows = observedRows;
        }

        return isFinal;
    }
}
