using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

public sealed class OperationProgressTests
{
    [Fact]
    public void Unknown_IsIndeterminate()
    {
        Assert.False(OperationProgress.Unknown.IsDeterminate);
        Assert.Null(OperationProgress.Unknown.Percentage);
        Assert.Equal(OperationProgressStage.Unknown, OperationProgress.Unknown.Stage);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(500, 1000, 50)]
    [InlineData(1000, 1000, 100)]
    [InlineData(326UL * 1024 * 1024, 624UL * 1024 * 1024, 52)]
    public void FromDownload_ByteBasedPercentage(ulong downloaded, ulong total, double expected)
    {
        var progress = OperationProgress.FromDownload(downloaded, total);
        Assert.True(progress.IsDeterminate);
        Assert.Equal(expected, Math.Round(progress.Percentage!.Value));
        Assert.Equal(OperationProgressStage.Downloading, progress.Stage);
        Assert.Equal(downloaded, progress.BytesDownloaded);
        Assert.Equal(total, progress.BytesTotal);
    }

    [Fact]
    public void FromDownload_ZeroTotal_IsIndeterminate()
    {
        var progress = OperationProgress.FromDownload(0, 0);
        Assert.False(progress.IsDeterminate);
        Assert.Equal(OperationProgressStage.Downloading, progress.Stage);
    }

    [Fact]
    public void FromDownload_ZeroTotal_WithReportedProgress_IsDeterminate()
    {
        var progress = OperationProgress.FromDownload(0, 0, 42);
        Assert.True(progress.IsDeterminate);
        Assert.Equal(42, progress.Percentage);
    }

    [Fact]
    public void FromDownload_ZeroReported_WithZeroTotal_StaysIndeterminate()
    {
        // A bare 0% with no byte totals carries no information; no fake percent.
        var progress = OperationProgress.FromDownload(0, 0, 0);
        Assert.False(progress.IsDeterminate);
    }

    [Fact]
    public void FromDownload_ProcessedGreaterThanTotal_ClampsTo100()
    {
        var progress = OperationProgress.FromDownload(150, 100);
        Assert.True(progress.IsDeterminate);
        Assert.Equal(100, progress.Percentage);
        // Real byte counters are preserved even when the percent clamps.
        Assert.Equal(150UL, progress.BytesDownloaded);
        Assert.Equal(100UL, progress.BytesTotal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromDownload_InvalidReported_WithZeroTotal_IsIndeterminate(double reported)
    {
        var progress = OperationProgress.FromDownload(0, 0, reported);
        Assert.False(progress.IsDeterminate);
    }

    [Theory]
    [InlineData(-5.0, 0.0)]
    [InlineData(150.0, 100.0)]
    [InlineData(double.NaN, null)]
    public void NormalizePercentage_ClampsAndRejects(double input, double? expected)
    {
        Assert.Equal(expected, OperationProgress.NormalizePercentage(input));
    }

    [Fact]
    public void NormalizePercentage_Null_StaysNull()
    {
        Assert.Null(OperationProgress.NormalizePercentage(null));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(0.5, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void FromInstall_KnownVsUnknown(double value, bool expectedDeterminate)
    {
        var progress = OperationProgress.FromInstall(value);
        Assert.Equal(expectedDeterminate, progress.IsDeterminate);
        Assert.Equal(OperationProgressStage.Installing, progress.Stage);
        if (expectedDeterminate)
            Assert.Equal(value, progress.Percentage);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromInstall_Invalid_IsIndeterminate(double value)
    {
        Assert.False(OperationProgress.FromInstall(value).IsDeterminate);
    }

    [Fact]
    public void FromInstall_Over100_ClampsTo100()
    {
        Assert.Equal(100, OperationProgress.FromInstall(150).Percentage);
    }

    [Theory]
    [InlineData(75, true)]
    [InlineData(0, false)]
    [InlineData(double.NaN, false)]
    public void FromUninstall_KnownVsUnknown(double value, bool expectedDeterminate)
    {
        var progress = OperationProgress.FromUninstall(value);
        Assert.Equal(expectedDeterminate, progress.IsDeterminate);
        Assert.Equal(OperationProgressStage.Uninstalling, progress.Stage);
    }

    [Fact]
    public void Completed_Is100Finalizing()
    {
        Assert.True(OperationProgress.Completed.IsDeterminate);
        Assert.Equal(100, OperationProgress.Completed.Percentage);
        Assert.Equal(OperationProgressStage.Finalizing, OperationProgress.Completed.Stage);
    }

    [Fact]
    public void QueuedAndFinalizing_AreIndeterminate()
    {
        Assert.False(OperationProgress.Queued.IsDeterminate);
        Assert.False(OperationProgress.Finalizing.IsDeterminate);
    }

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

        public void SetClockForTests(Func<DateTime> provider) =>
            SetUtcNowProviderForTests(provider);

        protected override void ApplyRetryAction(string retryMode) { }

        protected override Task<OperationVeredict> PerformOperation() =>
            Task.FromResult(OperationVeredict.Success);

        public override Task<Uri> GetOperationIcon() =>
            Task.FromResult(new Uri("avares://UniGetUI/Assets/package_color.png"));
    }

    [Fact]
    public void ReportProgress_DedupesIdenticalReports()
    {
        using var op = new ProgressProbeOperation();
        int events = 0;
        op.ProgressChanged += (_, _) => events++;

        var first = OperationProgress.FromDownload(50, 100);
        op.ReportForTests(first);
        op.ReportForTests(first);
        op.ReportForTests(OperationProgress.FromDownload(50, 100));

        Assert.Equal(1, events);
        Assert.Equal(first, op.CurrentProgress);
    }

    [Fact]
    public void ReportProgress_RaisesStageChanges()
    {
        using var op = new ProgressProbeOperation();
        var raised = new List<OperationProgress>();
        op.ProgressChanged += (_, p) => raised.Add(p);

        op.ReportForTests(OperationProgress.FromDownload(50, 100));
        op.ReportForTests(OperationProgress.FromInstall(50));

        Assert.Equal(2, raised.Count);
        Assert.Equal(OperationProgressStage.Downloading, raised[0].Stage);
        Assert.Equal(OperationProgressStage.Installing, raised[1].Stage);
    }

    [Fact]
    public void ResetProgress_ReturnsToUnknown()
    {
        using var op = new ProgressProbeOperation();
        op.ReportForTests(OperationProgress.FromDownload(50, 100));
        Assert.True(op.CurrentProgress.IsDeterminate);

        op.ResetForTests();
        Assert.Equal(OperationProgress.Unknown, op.CurrentProgress);
    }

    [Fact]
    public async Task OperationRun_ResetsProgressOnRetry()
    {
        using var op = new AutoRetryProbeOperation();

        var seen = new List<OperationProgress>();
        op.ProgressChanged += (_, p) => seen.Add(p);

        await op.MainThread();
        Assert.Equal(OperationStatus.Succeeded, op.Status);
        // First attempt reported 50%, retry reset to Unknown, second attempt succeeded.
        Assert.Contains(seen, static p => p is { IsDeterminate: true, Percentage: 50 });
        Assert.Contains(seen, static p => p == OperationProgress.Unknown);
    }

    private sealed class AutoRetryProbeOperation : ProgressProbeOperation
    {
        private int _attempts;

        protected override Task<OperationVeredict> PerformOperation()
        {
            _attempts++;
            if (_attempts == 1)
            {
                ReportProgress(OperationProgress.FromDownload(50, 100));
                return Task.FromResult(OperationVeredict.AutoRetry);
            }
            return Task.FromResult(OperationVeredict.Success);
        }
    }

    [Fact]
    public void Formatter_DeterminateDownload_IncludesPercentAndBytes()
    {
        var text = OperationProgressFormatter.Format(OperationProgress.FromDownload(50, 100));
        Assert.Contains("50%", text);
    }

    [Fact]
    public void Formatter_IndeterminateInstall_ShowsInstalling()
    {
        var text = OperationProgressFormatter.Format(OperationProgress.FromInstall(null));
        Assert.Contains("Installing", text);
    }

    [Fact]
    public void Formatter_Unknown_DoesNotThrow()
    {
        var text = OperationProgressFormatter.Format(OperationProgress.Unknown);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    /// <summary>
    /// Deterministic manual clock for throughput tests: production uses
    /// <see cref="DateTime.UtcNow"/>, tests advance time explicitly.
    /// </summary>
    private sealed class ManualClock
    {
        private DateTime _now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

        public Func<DateTime> Provider => () => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private static (ProgressProbeOperation Op, ManualClock Clock) CreateClockedProbe()
    {
        var op = new ProgressProbeOperation();
        var clock = new ManualClock();
        op.SetClockForTests(clock.Provider);
        return (op, clock);
    }

    private static void ReportDownload(
        ProgressProbeOperation op,
        ulong downloaded,
        ulong total = 10UL * 1024 * 1024
    ) => op.ReportForTests(OperationProgress.FromDownload(downloaded, total));

    [Fact]
    public void FirstDownloadSample_HasNoSpeed()
    {
        var (op, _) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 1_000_000);

            Assert.True(op.CurrentProgress.IsDeterminate);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
            Assert.False(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void SecondValidSample_CalculatesSpeed()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 1_048_576);

            Assert.Equal(1_048_576.0, op.CurrentProgress.BytesPerSecond);
            Assert.True(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void SpeedDeltaCalculation_IsDeltaBytesOverDeltaTime()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 1_048_576);
            clock.Advance(TimeSpan.FromSeconds(4));
            ReportDownload(op, 3UL * 1_048_576);

            // (3 MiB - 1 MiB) / 4 s = 0.5 MiB/s.
            Assert.Equal(524_288.0, op.CurrentProgress.BytesPerSecond);
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
                ReportDownload(op, 1_048_576); // instant = 1 MiB/s
                clock.Advance(TimeSpan.FromSeconds(1));
                ReportDownload(op, 3UL * 1_048_576); // instant = 2 MiB/s
                return op.CurrentProgress.BytesPerSecond;
            }
        }

        double? first = RunSequence();
        double? second = RunSequence();

        Assert.NotNull(first);
        Assert.Equal(first, second);
        // EMA with alpha 0.3: 0.3 * 2 MiB/s + 0.7 * 1 MiB/s = 1.3 MiB/s.
        Assert.Equal(1.3 * 1_048_576.0, first!.Value, precision: 5);
    }

    [Fact]
    public void ZeroTimeDelta_KeepsPreviousSpeedWithoutNaN()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            // Second sample at the very same timestamp: no speed yet, no NaN.
            ReportDownload(op, 1_048_576);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2UL * 1_048_576);
            double? speed = op.CurrentProgress.BytesPerSecond;
            Assert.NotNull(speed);

            // More bytes but no time elapsed: previous speed preserved, finite.
            ReportDownload(op, 3UL * 1_048_576);
            Assert.Equal(speed, op.CurrentProgress.BytesPerSecond);
            Assert.True(op.CurrentProgress.HasThroughput);
        }
    }

    [Fact]
    public void BackwardByteCounter_ResetsSpeedToNull()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2UL * 1_048_576);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            // Counter rewound (retry/restart): baseline resets, no stale speed.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 512);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            // The rewound sample is the new baseline: next delta measures from it.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 512 + 1_048_576);
            Assert.Equal(1_048_576.0, op.CurrentProgress.BytesPerSecond);
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
            ReportDownload(op, 1_048_576);
            double? speed = op.CurrentProgress.BytesPerSecond;
            Assert.NotNull(speed);

            // Stalled counter carries no new information: keep the previous
            // speed instead of synthesizing a meaningless new one.
            clock.Advance(TimeSpan.FromSeconds(5));
            ReportDownload(op, 1_048_576);
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
            ReportDownload(op, 1_048_576);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            // Download -> install: speed is stripped, never carried over.
            op.ReportForTests(OperationProgress.FromInstall(50));
            Assert.Null(op.CurrentProgress.BytesPerSecond);
            Assert.False(op.CurrentProgress.HasThroughput);

            // A fresh download starts without a stale speed.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2UL * 1_048_576);
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
    }

    [Fact]
    public void ResetProgress_ClearsSpeed()
    {
        var (op, clock) = CreateClockedProbe();
        using (op)
        {
            ReportDownload(op, 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 1_048_576);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            op.ResetForTests();
            Assert.Equal(OperationProgress.Unknown, op.CurrentProgress);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            // Same counters after a reset behave like a first sample again.
            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 1_048_576);
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
            ReportDownload(op, 1_048_576);
            Assert.NotNull(op.CurrentProgress.BytesPerSecond);

            op.ReportForTests(OperationProgress.Unknown);
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            clock.Advance(TimeSpan.FromSeconds(1));
            ReportDownload(op, 2UL * 1_048_576);
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

            op.ReportForTests(OperationProgress.Queued with { BytesPerSecond = 999 });
            Assert.Null(op.CurrentProgress.BytesPerSecond);

            op.ReportForTests(OperationProgress.Completed with { BytesPerSecond = 999 });
            Assert.Null(op.CurrentProgress.BytesPerSecond);
        }
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
        Assert.False((OperationProgress.Unknown with { BytesPerSecond = value }).HasThroughput);
    }

    [Fact]
    public void NormalizeBytesPerSecond_KeepsPositiveFinite()
    {
        Assert.Equal(3.5, OperationProgress.NormalizeBytesPerSecond(3.5));
        Assert.True((OperationProgress.Unknown with { BytesPerSecond = 3.5 }).HasThroughput);
        Assert.Null(OperationProgress.NormalizeBytesPerSecond(null));
    }

    [Fact]
    public void Formatter_DownloadWithSpeed_AppendsThroughput()
    {
        var progress = OperationProgress.FromDownload(21UL * 1_048_576, 100UL * 1_048_576) with
        {
            BytesPerSecond = 3.8 * 1_048_576.0,
        };

        string text = OperationProgressFormatter.Format(progress);

        Assert.Contains("21%", text);
        Assert.Contains("/", text);
        Assert.Contains("MB/s", text);
    }

    [Fact]
    public void Formatter_DownloadWithoutSpeed_KeepsLegacyFormat()
    {
        string text = OperationProgressFormatter.Format(
            OperationProgress.FromDownload(21UL * 1_048_576, 100UL * 1_048_576)
        );

        Assert.Contains("21%", text);
        Assert.DoesNotContain("/s", text);
    }

    [Theory]
    [InlineData(512.0, "B/s")]
    [InlineData(2048.0, "KB/s")]
    [InlineData(3.8 * 1024 * 1024, "MB/s")]
    [InlineData(3.0 * 1024 * 1024 * 1024, "GB/s")]
    public void Formatter_ThroughputUnits(double bytesPerSecond, string expectedUnit)
    {
        var progress =
            OperationProgress.FromDownload(50, 100) with { BytesPerSecond = bytesPerSecond };

        Assert.Contains(expectedUnit, OperationProgressFormatter.Format(progress));
    }

    [Fact]
    public void Formatter_ByteUnit_DoesNotConfuseWithKiloUnit()
    {
        var progress = OperationProgress.FromDownload(50, 100) with { BytesPerSecond = 512.0 };

        string text = OperationProgressFormatter.Format(progress);
        Assert.Contains("512", text);
        Assert.DoesNotContain("KB/s", text);
        Assert.DoesNotContain("MB/s", text);
        Assert.DoesNotContain("GB/s", text);
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

    [Fact]
    public async Task ReportProgress_RapidConcurrentCallbacks_AreSafeAndFinite()
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
}
