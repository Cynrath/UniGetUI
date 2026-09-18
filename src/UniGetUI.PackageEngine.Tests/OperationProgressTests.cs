using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;
using LineType = UniGetUI.PackageOperations.AbstractOperation.LineType;

namespace UniGetUI.PackageEngine.Tests;

/// <summary>
/// Covers the manager-neutral <see cref="OperationProgress"/> model and the generic
/// throughput tracker in <see cref="AbstractOperation"/>: determinate/unknown rules,
/// single-clock speed measurement, EMA smoothing, reset semantics, thread safety, and
/// the guarantee that structured progress never touches the log/history path.
/// </summary>
public sealed class OperationProgressTests
{
    private class ProgressProbeOperation : AbstractOperation
    {
        public ProgressProbeOperation()
            : base(queue_enabled: false)
        {
            Metadata.Status = "probe status";
            Metadata.Title = "probe title";
            Metadata.OperationInformation = "probe info";
            Metadata.SuccessTitle = "probe success";
            Metadata.SuccessMessage = "probe success";
            Metadata.FailureTitle = "probe failure";
            Metadata.FailureMessage = "probe failure";
        }

        public void ReportForTests(OperationProgress progress) => ReportProgress(progress);

        public void ResetForTests() => ResetProgress();

        public void EmitForTests(string line, LineType type) => Line(line, type);

        public void SetClockForTests(Func<DateTime> provider) =>
            SetUtcNowProviderForTests(provider);

        protected override void ApplyRetryAction(string retryMode) { }

        protected override Task<OperationVeredict> PerformOperation() =>
            Task.FromResult(OperationVeredict.Success);

        public override Task<Uri> GetOperationIcon() =>
            Task.FromResult(new Uri("avares://UniGetUI/Assets/package_color.png"));
    }

    /// <summary>
    /// Deterministic manual clock. Production uses DateTime.UtcNow via the default
    /// provider; tests advance time explicitly, which also proves the tracker honors
    /// the injected clock (a DateTime.UtcNow leak would break the frozen-clock tests).
    /// </summary>
    private sealed class ManualClock
    {
        private DateTime _now = new(2026, 1, 12, 12, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private const ulong OneMiB = 1024UL * 1024;
    private const ulong TenMiB = 10UL * 1024 * 1024;

    private static (ProgressProbeOperation Op, ManualClock Clock) CreateClockedProbe()
    {
        var op = new ProgressProbeOperation();
        var clock = new ManualClock();
        op.SetClockForTests(clock.Now);
        return (op, clock);
    }

    private static void ReportDownload(
        ProgressProbeOperation op,
        ulong downloaded,
        ulong total = TenMiB
    ) => op.ReportForTests(OperationProgress.FromDownload(downloaded, total));

    // ── Model: determinate vs unknown ──────────────────────────────────────

    [Fact]
    public void Unknown_IsIndeterminate()
    {
        Assert.False(OperationProgress.Unknown.IsDeterminate);
        Assert.Null(OperationProgress.Unknown.Percentage);
        Assert.False(OperationProgress.Unknown.HasThroughput);
    }

    [Fact]
    public void FromDownload_Mid_IsDeterminateWithDerivedPercentage()
    {
        var progress = OperationProgress.FromDownload(326, 624);

        Assert.True(progress.IsDeterminate);
        Assert.Equal(326UL, progress.BytesDownloaded);
        Assert.Equal(624UL, progress.BytesTotal);
        Assert.Equal(52, Math.Round(progress.Percentage!.Value));
    }

    [Fact]
    public void FromDownload_ZeroBytes_IsDeterminateZero_NotUnknown()
    {
        var progress = OperationProgress.FromDownload(0, TenMiB);

        Assert.True(progress.IsDeterminate);
        Assert.Equal(0, progress.Percentage);
    }

    [Fact]
    public void FromDownload_Full_IsDeterminateHundred()
    {
        var progress = OperationProgress.FromDownload(TenMiB, TenMiB);

        Assert.True(progress.IsDeterminate);
        Assert.Equal(100, progress.Percentage);
    }

    [Fact]
    public void FromDownload_ZeroTotal_IsIndeterminate_NotFakeZero()
    {
        var progress = OperationProgress.FromDownload(1234, 0);

        Assert.False(progress.IsDeterminate);
        Assert.Null(progress.Percentage);
        Assert.Equal(OperationProgressStage.Downloading, progress.Stage);
    }

    [Fact]
    public void FromDownload_Overshoot_ClampsPercentageKeepsRealBytes()
    {
        var progress = OperationProgress.FromDownload(150, 100);

        Assert.True(progress.IsDeterminate);
        Assert.Equal(100, progress.Percentage);
        Assert.Equal(150UL, progress.BytesDownloaded);
        Assert.Equal(100UL, progress.BytesTotal);
    }

    [Theory]
    [InlineData(34.0)]
    [InlineData(0.0)]
    [InlineData(100.0)]
    public void FromInstall_Known_IsDeterminate(double percent)
    {
        var progress = OperationProgress.FromInstall(percent);

        Assert.True(progress.IsDeterminate);
        Assert.Equal(percent, progress.Percentage);
        Assert.Equal(OperationProgressStage.Installing, progress.Stage);
    }

    [Theory]
    [InlineData(150.0)]
    public void FromInstall_AboveHundred_Clamps(double percent)
    {
        Assert.Equal(100, OperationProgress.FromInstall(percent).Percentage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromInstall_Unknown_StaysIndeterminateWithoutFakePercent(double? percent)
    {
        var progress = OperationProgress.FromInstall(percent);

        Assert.False(progress.IsDeterminate);
        Assert.Null(progress.Percentage);
        Assert.Equal(OperationProgressStage.Installing, progress.Stage);
    }

    [Fact]
    public void FromUpdate_And_FromUninstall_CarryTheirStage()
    {
        Assert.Equal(
            OperationProgressStage.Updating,
            OperationProgress.FromUpdate(10).Stage
        );
        Assert.Equal(
            OperationProgressStage.Uninstalling,
            OperationProgress.FromUninstall(10).Stage
        );
        Assert.False(OperationProgress.FromUpdate(null).IsDeterminate);
        Assert.False(OperationProgress.FromUninstall(null).IsDeterminate);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-12.5)]
    public void NormalizeBytesPerSecond_RejectsNonPositiveAndNonFinite(double value)
    {
        Assert.Null(OperationProgress.NormalizeBytesPerSecond(value));
        Assert.False(
            (OperationProgress.Unknown with { BytesPerSecond = value }).HasThroughput
        );
    }

    [Fact]
    public void NormalizeBytesPerSecond_KeepsPositiveFinite()
    {
        Assert.Equal(3.5, OperationProgress.NormalizeBytesPerSecond(3.5));
        Assert.True(
            (OperationProgress.Unknown with { BytesPerSecond = 3.5 }).HasThroughput
        );
        Assert.Null(OperationProgress.NormalizeBytesPerSecond(null));
    }

    // ── Throughput: one clock, real bytes over real time ───────────────────

    [Fact]
    public void FirstDownloadSample_HasNoSpeed()
    {
        var (op, _) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, OneMiB);

            Assert.True(op.CurrentProgress.IsDeterminate);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
            Assert.False(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void FrozenClock_SecondSample_HasNoSpeed_ProvesInjectedClockIsUsed()
    {
        // The clock never advances: elapsed time is exactly zero. A DateTime.UtcNow
        // leak inside the tracker would observe real elapsed time and produce a
        // (huge, fake) speed; the injected clock correctly yields no speed.
        var (op, _) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            ReportDownload(op, OneMiB);

            Assert.Null(op.CurrentProgress.BytesPerSecond);
            Assert.False(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void SecondValidSample_CalculatesDeltaBytesOverDeltaTime()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);

            Assert.Equal((double)OneMiB, op.CurrentProgress.BytesPerSecond);
            Assert.True(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void SpeedDelta_IsMeasuredFromPreviousSample()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, OneMiB);
            clock.Advance(TimeSpan.FromSeconds(4));
            ReportDownload(op, 3 * OneMiB);

            // (3 MiB - 1 MiB) / 4 s = 0.5 MiB/s.
            Assert.Equal((double)(OneMiB / 2), op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void Smoothing_IsDeterministicExponentialMovingAverage()
    {
        static double? RunSequence()
        {
            var (op, clock) = CreateClockedProbe();
            using (op)
            {
                ReportDownload(op, 0);
                clock.Advance(TimeSpan.FromSeconds(1));
                ReportDownload(op, OneMiB); // instant = 1 MiB/s
                clock.Advance(TimeSpan.FromSeconds(1));
                ReportDownload(op, 3 * OneMiB); // instant = 2 MiB/s
                return op.CurrentProgress.BytesPerSecond;
            }
        }

        double? first = RunSequence();
        double? second = RunSequence();

        Assert.NotNull(first);
        Assert.Equal(first, second);
        // EMA with alpha 0.3: 0.3 * 2 MiB/s + 0.7 * 1 MiB/s = 1.3 MiB/s.
        Assert.InRange(first!.Value, 1.3 * OneMiB - 1, 1.3 * OneMiB + 1);
    }

    [Fact]
    public void ZeroTimeDelta_PreservesPreviousSpeedWithoutNaN()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            // Second sample at the very same timestamp: no speed yet, no NaN.
            ReportDownload(op, OneMiB);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2 * OneMiB);
            double? speed = op.CurrentProgress.BytesPerSecond;
            Assert.NotNull(speed);

            // More bytes but no time elapsed: previous speed preserved, finite.
            ReportDownload(op, 3 * OneMiB);
            Assert.Equal(speed, op.CurrentProgress.BytesPerSecond);
            Assert.True(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void BackwardByteCounter_ResetsSpeedAndStartsNewBaseline()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2 * OneMiB);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            // Counter rewound (retry/restart): no stale speed survives.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 512);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            // The rewound sample is the new baseline: next delta measures from it.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 512 + OneMiB);
            Assert.Equal((double)OneMiB, op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void RepeatedByteCount_PreservesPreviousSpeed()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);
            double? speed = op.CurrentProgress.BytesPerSecond;
            Assert.NotNull(speed);

            // Stalled counter carries no new information: keep the previous speed
            // instead of synthesizing a meaningless new one.
            clock.Advance(TimeSpan.FromSeconds(5));
            ReportDownload(op, OneMiB);
            Assert.Equal(speed, op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void StageTransition_ResetsSpeed()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            // Download -> install: speed is stripped, never carried over.
            op.ReportForTests(OperationProgress.FromInstall(50));
            Assert.Null(op.CurrentProgress.BytesPerSecond);
            Assert.False(op.CurrentProgress.HasThroughput);

            // A fresh download starts without a stale speed.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2 * OneMiB);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void ResetProgress_ClearsSpeedAndReturnsToUnknown()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            op.ResetForTests();
            Assert.Equal(OperationProgress.Unknown, op.CurrentProgress);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            // Same counters after a reset behave like a first sample again.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void UnknownProgress_ClearsSpeed()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, OneMiB);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            op.ReportForTests(OperationProgress.Unknown);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2 * OneMiB);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void NonDownloadingStages_NeverCarrySpeed()
    {
        var (op, _) = CreateClockedProbe();
        using (op)
        {
            // Even a hand-built installing report with speed is sanitized.
            op.ReportForTests(OperationProgress.FromInstall(50) with { BytesPerSecond = 999 });
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            op.ReportForTests(OperationProgress.Unknown with { BytesPerSecond = 999 });
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void EveryReport_PropagatesExactlyOneEvent()
    {
        using var op = new ProgressProbeOperation();
        int events = 0;
        op.ProgressChanged += (_, _) => events++;

        // No throttling/coalescing: stage changes, determinate updates, and resets
        // all propagate immediately on the reporting thread.
        op.ReportForTests(OperationProgress.ForStage(OperationProgressStage.Downloading));
        op.ReportForTests(OperationProgress.FromDownload(50, 100));
        op.ResetForTests();

        Assert.Equal(3, events);
    }

    [Fact]
    public async Task RapidConcurrentReports_AreSafeAndFinite()
    {
        using var op = new ProgressProbeOperation();
        var seenSpeeds = new System.Collections.Concurrent.ConcurrentBag<double?>();
        op.ProgressChanged += (_, progress) => seenSpeeds.Add(progress.BytesPerSecond);

        await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(worker =>
                    Task.Run(() =>
                    {
                        for (ulong step = 0; step < 50; step++)
                            op.ReportForTests(
                                OperationProgress.FromDownload(
                                    (ulong)worker * 1000 + step,
                                    100_000
                                )
                            );
                    })
                )
        );

        foreach (double? speed in seenSpeeds)
            Assert.True(
                speed is null
                    || (!double.IsNaN(speed.Value)
                        && !double.IsInfinity(speed.Value)
                        && speed.Value > 0),
                $"Non-finite speed leaked: {speed}"
            );

        OperationProgress current = op.CurrentProgress;
        Assert.True(current.IsDeterminate);
        Assert.True(
            current.BytesPerSecond is null
                || (!double.IsNaN(current.BytesPerSecond.Value)
                    && !double.IsInfinity(current.BytesPerSecond.Value)
                    && current.BytesPerSecond.Value > 0)
        );
    }

    // ── Separation: progress never touches log/history ─────────────────────

    [Fact]
    public void ReportProgress_DoesNotWriteToOperationOutput()
    {
        using var op = new ProgressProbeOperation();
        var clock = new ManualClock();
        op.SetClockForTests(clock.Now);

        op.ReportForTests(OperationProgress.ForStage(OperationProgressStage.Downloading));
        ReportDownload(op, OneMiB);
        clock.Advance(TimeSpan.FromSeconds(1));
        ReportDownload(op, 2 * OneMiB);
        op.ResetForTests();

        // The constructor only emits a ProgressIndicator line, which is excluded
        // from the output by design; structured reports add nothing at all.
        Assert.Empty(op.GetOutput());
    }

    [Fact]
    public void ProgressIndicatorLogLines_DoNotCreateStructuredProgress()
    {
        using var op = new ProgressProbeOperation();
        int progressEvents = 0;
        op.ProgressChanged += (_, _) => progressEvents++;

        // Raw per-frame progress text flows through the normal log path only.
        op.EmitForTests("[###.....] 30% (3.0 MB/10.0 MB)", LineType.ProgressIndicator);
        op.EmitForTests("Fetching download url...", LineType.Information);

        Assert.Equal(0, progressEvents);
        Assert.Equal(OperationProgress.Unknown, op.CurrentProgress);
        Assert.Single(op.GetOutput());
        Assert.Equal("Fetching download url...", op.GetOutput()[0].Item1);
    }

    // ── Retry resets progress ──────────────────────────────────────────────

    private sealed class AutoRetryProbeOperation : ProgressProbeOperation
    {
        private int _attempts;

        protected override Task<OperationVeredict> PerformOperation()
        {
            _attempts++;
            if (_attempts == 1)
            {
                // First attempt reports real progress, then asks for a retry.
                ReportProgress(OperationProgress.FromDownload(50, 100));
                return Task.FromResult(OperationVeredict.AutoRetry);
            }

            // Retry restarts observationally (as PackageOperation does per attempt).
            ReportProgress(OperationProgress.ForStage(OperationProgressStage.Downloading));
            return Task.FromResult(OperationVeredict.Success);
        }
    }

    [Fact]
    public async Task AutoRetry_AttemptBoundary_ResetsToUnknown()
    {
        using var op = new AutoRetryProbeOperation();
        var seen = new List<OperationProgress>();
        op.ProgressChanged += (_, p) => seen.Add(p);

        await op.MainThread();

        Assert.Equal(OperationStatus.Succeeded, op.Status);
        Assert.Contains(seen, static p => p is { IsDeterminate: true, Percentage: 50 });
        // The retry attempt restarts observationally with indeterminate progress and
        // no speed, after the determinate report of the first attempt.
        int determinateIndex = seen.FindIndex(
            static p => p is { IsDeterminate: true, Percentage: 50 }
        );
        Assert.True(determinateIndex >= 0);
        Assert.Contains(
            seen.Skip(determinateIndex + 1),
            static p => !p.IsDeterminate
                && p.Stage == OperationProgressStage.Downloading
                && p.BytesPerSecond is null
        );
    }

    // ── Formatter ──────────────────────────────────────────────────────────

    [Fact]
    public void Formatter_DeterminateDownload_IncludesPercentAndByteCounters()
    {
        string text = OperationProgressFormatter.Format(
            OperationProgress.FromDownload(21 * OneMiB, 100 * OneMiB)
        );

        Assert.Contains("21%", text);
        Assert.Contains("/", text);
        Assert.DoesNotContain("/s", text);
    }

    [Fact]
    public void Formatter_DownloadWithSpeed_AppendsThroughput()
    {
        var progress =
            OperationProgress.FromDownload(21 * OneMiB, 100 * OneMiB)
            with
            {
                BytesPerSecond = 1.2 * OneMiB,
            };

        string text = OperationProgressFormatter.Format(progress);

        Assert.Contains("21%", text);
        Assert.Contains("/", text);
        Assert.Contains("/s", text);
        Assert.Contains("MB", text);
    }

    [Fact]
    public void Formatter_IndeterminateInstall_ShowsStageWithoutPercent()
    {
        string text = OperationProgressFormatter.Format(
            OperationProgress.FromInstall(null)
        );

        Assert.Contains("Installing", text);
        Assert.DoesNotContain("%", text);
    }

    [Fact]
    public void Formatter_Unknown_DoesNotThrow()
    {
        Assert.False(string.IsNullOrWhiteSpace(OperationProgressFormatter.Format(OperationProgress.Unknown)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Formatter_UnusableSpeed_IsOmitted(double bytesPerSecond)
    {
        var progress =
            OperationProgress.FromDownload(50, 100) with { BytesPerSecond = bytesPerSecond };

        Assert.DoesNotContain("/s", OperationProgressFormatter.Format(progress));
    }
}
