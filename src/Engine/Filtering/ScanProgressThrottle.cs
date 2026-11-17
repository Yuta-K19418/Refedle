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
    /// The final position is always reported, including repeats; both paths record the
    /// position only when it moves forward, retrying the write after losing a race.
    /// A non-final position must strictly advance past the last recorded one by at least
    /// 1% of the total (or by any advance when that step rounds down to zero for totals
    /// under 100 rows).
    /// The final and non-final paths record the position with similar code on purpose. They are
    /// kept separate rather than merged, because merging them complicates the conditions and
    /// makes the method harder to follow.
    /// </summary>
    /// <param name="scannedRows">Rows scanned so far.</param>
    /// <param name="totalRows">Total rows to scan.</param>
    public bool ShouldNotify(int scannedRows, int totalRows)
    {
        if (scannedRows >= totalRows)
        {
            // The final position is always reported. The recorded position only ever moves
            // forward; as a safeguard, the write is made only after checking that
            // scannedRows > lastNotifiedRows still holds.
            int recordedRows = Volatile.Read(ref _lastNotifiedRows);
            while (scannedRows > recordedRows)
            {
                int observedRows = Interlocked.CompareExchange(ref _lastNotifiedRows, scannedRows, recordedRows);
                if (observedRows == recordedRows)
                {
                    break;
                }

                recordedRows = observedRows;
            }

            return true;
        }

        int notificationStep = totalRows / NotificationStepDivisor;
        int lastNotifiedRows = Volatile.Read(ref _lastNotifiedRows);
        while (scannedRows > lastNotifiedRows && scannedRows - lastNotifiedRows >= notificationStep)
        {
            int observedRows = Interlocked.CompareExchange(ref _lastNotifiedRows, scannedRows, lastNotifiedRows);

            // A match with the expected value means the write succeeded, so this call notifies.
            if (observedRows == lastNotifiedRows)
            {
                return true;
            }

            lastNotifiedRows = observedRows;
        }

        return false;
    }
}
