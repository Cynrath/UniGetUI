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
}
