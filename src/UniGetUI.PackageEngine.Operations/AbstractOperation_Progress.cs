using UniGetUI.PackageEngine.Enums;

namespace UniGetUI.PackageOperations;

// Progress reporting for AbstractOperation: structured, manager-neutral OperationProgress
// snapshots flow side-car via ProgressChanged and never touch the log/history path.
//
// DESIGN NOTES (maintainer feedback on #5390):
// - Structured progress callbacks and Line() logging are fully separated. ReportProgress
//   never logs; existing CLI output, return codes, retry decisions, and history entries
//   are unaffected by progress reporting.
// - ONE clock source: _utcNowProvider (default DateTime.UtcNow, injectable for tests) is
//   the only time source used by progress logic. There is no time-based UI throttling,
//   so there is no second clock to drift.
// - Speed is measured generically as deltaBytes / deltaTime from real cumulative byte
//   samples. First sample, rewound counters, stalled counters, zero elapsed time, stage
//   changes, and unknown progress never synthesize a speed. Light deterministic EMA
//   smoothing (alpha 0.3) damps per-chunk network jitter.
// - No ETA is computed.
public abstract partial class AbstractOperation
{
    /// <summary>
    /// Raised on the reporting thread whenever structured progress is reported or reset.
    /// UI subscribers must marshal to the UI thread (as with LogLineAdded/StatusChanged).
    /// Never raised from the log path; progress lines in the log do not raise this.
    /// </summary>
    public event EventHandler<OperationProgress>? ProgressChanged;

    /// <summary>Latest structured progress snapshot. Unknown until first reported.</summary>
    public OperationProgress CurrentProgress { get; private set; } = OperationProgress.Unknown;

    private readonly object ProgressLock = new();
    private Func<DateTime> UtcNowProvider = static () => DateTime.UtcNow;

    // Throughput tracker state. All fields are guarded by ProgressLock.
    private bool HasThroughputBaseline;
    private ulong LastThroughputBytes;
    private DateTime LastThroughputSampleUtc;
    private double? SmoothedBytesPerSecond;

    private const double ThroughputSmoothingAlpha = 0.3;

    /// <summary>
    /// Test hook: replaces the single clock used by progress logic. Resets tracker state
    /// so samples from different clocks are never mixed.
    /// </summary>
    internal void SetUtcNowProviderForTests(Func<DateTime> provider)
    {
        lock (ProgressLock)
        {
            UtcNowProvider = provider;
            ResetThroughputStateUnlocked();
        }
    }

    /// <summary>
    /// Reports structured progress. Enriches download reports with measured throughput,
    /// stores the snapshot as <see cref="CurrentProgress"/>, and raises
    /// <see cref="ProgressChanged"/>. Never logs, never fails the operation.
    /// Safe to call concurrently from output callbacks.
    /// </summary>
    protected void ReportProgress(OperationProgress progress)
    {
        OperationProgress enriched;
        lock (ProgressLock)
        {
            enriched = EnrichWithThroughputUnlocked(progress);
            CurrentProgress = enriched;
        }
        ProgressChanged?.Invoke(this, enriched);
    }

    /// <summary>
    /// Resets structured progress to unknown (clears any speed). Propagates via
    /// <see cref="ProgressChanged"/> like any other report so retry/restart resets
    /// reach the UI. Like <see cref="ReportProgress"/>, never touches the log.
    /// </summary>
    protected void ResetProgress() => ReportProgress(OperationProgress.Unknown);

    private OperationProgress EnrichWithThroughputUnlocked(OperationProgress progress)
    {
        DateTime now = UtcNowProvider();

        // Only the Downloading stage with full byte counters can carry a measured speed.
        // Anything else (stage change away from Downloading, unknown progress, install
        // percentages) clears the tracker and strips any attached speed.
        if (
            progress.Stage is not OperationProgressStage.Downloading
            || progress.BytesDownloaded is null
            || progress.BytesTotal is null
            || progress.BytesTotal == 0
        )
        {
            ResetThroughputStateUnlocked();
            return progress.BytesPerSecond is null
                ? progress
                : progress with
                {
                    BytesPerSecond = null,
                };
        }

        ulong bytes = progress.BytesDownloaded.Value;

        if (!HasThroughputBaseline)
        {
            // First sample establishes the baseline; there is no speed yet.
            HasThroughputBaseline = true;
            LastThroughputBytes = bytes;
            LastThroughputSampleUtc = now;
            SmoothedBytesPerSecond = null;
            return progress with { BytesPerSecond = null };
        }

        if (bytes < LastThroughputBytes)
        {
            // Counter rewound (retry/restart): the old baseline is meaningless.
            // The rewound sample becomes the new baseline; no stale speed survives.
            LastThroughputBytes = bytes;
            LastThroughputSampleUtc = now;
            SmoothedBytesPerSecond = null;
            return progress with { BytesPerSecond = null };
        }

        if (bytes == LastThroughputBytes)
        {
            // Stalled counter carries no new information: keep the previous speed instead
            // of synthesizing one, but move the baseline clock forward so the stalled
            // interval does not dilute the next real delta.
            LastThroughputSampleUtc = now;
            return progress with { BytesPerSecond = SmoothedBytesPerSecond };
        }

        TimeSpan elapsed = now - LastThroughputSampleUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            // No time elapsed: preserve the previous speed without dividing by zero.
            return progress with { BytesPerSecond = SmoothedBytesPerSecond };
        }

        double instant = (bytes - LastThroughputBytes) / elapsed.TotalSeconds;
        double smoothed =
            SmoothedBytesPerSecond is { } previous
                ? ThroughputSmoothingAlpha * instant + (1 - ThroughputSmoothingAlpha) * previous
                : instant;

        if (OperationProgress.NormalizeBytesPerSecond(smoothed) is null)
        {
            // Defensive: with a positive delta over positive time this cannot happen,
            // but never let a non-usable speed leak downstream.
            return progress with { BytesPerSecond = SmoothedBytesPerSecond };
        }

        SmoothedBytesPerSecond = smoothed;
        LastThroughputBytes = bytes;
        LastThroughputSampleUtc = now;
        return progress with { BytesPerSecond = smoothed };
    }

    private void ResetThroughputStateUnlocked()
    {
        HasThroughputBaseline = false;
        LastThroughputBytes = 0;
        LastThroughputSampleUtc = default;
        SmoothedBytesPerSecond = null;
    }
}
