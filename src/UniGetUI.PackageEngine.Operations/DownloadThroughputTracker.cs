namespace UniGetUI.PackageOperations;

/// <summary>
/// Attempt-level estimator converting cumulative download byte counters into a
/// smoothed bytes-per-second throughput. Shared by every byte-counter source
/// (WinGet native COM callbacks, HTTP <c>DownloadOperation</c>, future
/// managers) so manager mappers stay stateless and never synthesize speeds.
/// Thread-safe: all sample state is guarded by a private lock.
/// </summary>
/// <remarks>
/// Smoothing is an exponential moving average with <see cref="SmoothingFactor"/>
/// 0.3: each new instantaneous sample contributes 30% and history 70%, giving
/// an effective memory of ~3 samples. That damps single-sample jitter caused
/// by coarse byte quantization and irregular native callback intervals, while
/// still reacting to genuine throughput changes within about a second of
/// progress reports (which arrive at least on every 1% step and are additionally
/// coalesced at 200ms in <c>AbstractOperation</c>). The estimator is a pure
/// function of (bytes, timestamp) samples with O(1) state and no wall-clock
/// windows, so it is fully deterministic and unit-testable.
/// </remarks>
internal sealed class DownloadThroughputTracker
{
    /// <summary>
    /// EMA weight of the newest instantaneous sample. Justified above.
    /// </summary>
    internal const double SmoothingFactor = 0.3;

    private readonly object _lock = new();
    private ulong? _previousBytes;
    private DateTime _previousTimestampUtc;
    private double? _smoothedBytesPerSecond;
    private bool _hasSample;

    /// <summary>
    /// Observes a cumulative byte counter at the given UTC timestamp and
    /// returns the smoothed throughput, or null when no meaningful speed can
    /// be reported yet:
    /// <list type="bullet">
    /// <item>The first sample only establishes the baseline (null).</item>
    /// <item>A non-positive time delta keeps the previous speed (null when
    /// there is none) without touching the stored sample, so a later sample
    /// with a valid timestamp still measures across the full interval.</item>
    /// <item>An unchanged counter carries no new information: the previous
    /// speed is preserved and the stored sample is untouched, so a resume
    /// measures truthfully across the stall instead of collapsing to zero.</item>
    /// <item>A backwards counter (retry, restart, rewind) resets the baseline
    /// and returns null; the rewound sample becomes the new first sample.</item>
    /// </list>
    /// The result is always null or a finite positive value: NaN/Infinity can
    /// never leak to the UI.
    /// </summary>
    public double? Observe(ulong bytesDownloaded, DateTime timestampUtc)
    {
        lock (_lock)
        {
            if (!_hasSample)
            {
                _previousBytes = bytesDownloaded;
                _previousTimestampUtc = timestampUtc;
                _smoothedBytesPerSecond = null;
                _hasSample = true;
                return null;
            }

            ulong previousBytes = _previousBytes!.Value;

            if (bytesDownloaded < previousBytes)
            {
                // Counter rewound: start over from this sample.
                _previousBytes = bytesDownloaded;
                _previousTimestampUtc = timestampUtc;
                _smoothedBytesPerSecond = null;
                return null;
            }

            if (bytesDownloaded == previousBytes)
                return _smoothedBytesPerSecond;

            double elapsedSeconds = (timestampUtc - _previousTimestampUtc).TotalSeconds;
            if (elapsedSeconds <= 0)
                return _smoothedBytesPerSecond;

            double instant = (bytesDownloaded - previousBytes) / elapsedSeconds;
            if (double.IsNaN(instant) || double.IsInfinity(instant) || instant <= 0)
                return _smoothedBytesPerSecond;

            _smoothedBytesPerSecond = _smoothedBytesPerSecond.HasValue
                ? SmoothingFactor * instant + (1.0 - SmoothingFactor) * _smoothedBytesPerSecond.Value
                : instant;
            _previousBytes = bytesDownloaded;
            _previousTimestampUtc = timestampUtc;
            return _smoothedBytesPerSecond;
        }
    }

    /// <summary>
    /// Drops all sample state. Called on stage transitions away from
    /// downloading, on unknown progress, and at the start of every execution
    /// attempt so retries never leak a stale speed.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _previousBytes = null;
            _previousTimestampUtc = default;
            _smoothedBytesPerSecond = null;
            _hasSample = false;
        }
    }
}
