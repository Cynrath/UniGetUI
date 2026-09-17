#if WINDOWS
using Microsoft.Management.Deployment;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Managers.WingetManager;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

public sealed class WinGetNativeProgressTests
{
    [Fact]
    public void MapInstall_Queued_IsQueuedIndeterminate()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress { State = PackageInstallProgressState.Queued }
        );
        Assert.False(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Queued, mapped.Stage);
    }

    [Fact]
    public void MapInstall_Downloading_PrefersBytes()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress
            {
                State = PackageInstallProgressState.Downloading,
                BytesDownloaded = 326,
                BytesRequired = 624,
                DownloadProgress = 10, // bytes win over the reported value
            }
        );
        Assert.True(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Downloading, mapped.Stage);
        Assert.Equal(52, Math.Round(mapped.Percentage!.Value));
        Assert.Equal(326UL, mapped.BytesDownloaded);
        Assert.Equal(624UL, mapped.BytesTotal);
    }

    [Fact]
    public void MapInstall_Downloading_UnknownStaysIndeterminate()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress
            {
                State = PackageInstallProgressState.Downloading,
                BytesDownloaded = 0,
                BytesRequired = 0,
                DownloadProgress = 0,
            }
        );
        Assert.False(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Downloading, mapped.Stage);
    }

    [Fact]
    public void MapInstall_Installing_KnownIsDeterminate()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress
            {
                State = PackageInstallProgressState.Installing,
                InstallationProgress = 34,
            }
        );
        Assert.True(mapped.IsDeterminate);
        Assert.Equal(34, mapped.Percentage);
        Assert.Equal(OperationProgressStage.Installing, mapped.Stage);
    }

    [Fact]
    public void MapInstall_Installing_UnknownIsIndeterminate()
    {
        // The installer supplied no usable progress: never synthesize a total.
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress
            {
                State = PackageInstallProgressState.Installing,
                InstallationProgress = 0,
            }
        );
        Assert.False(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Installing, mapped.Stage);
    }

    [Fact]
    public void MapInstall_PostInstall_IsFinalizingIndeterminate()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress { State = PackageInstallProgressState.PostInstall }
        );
        Assert.False(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Finalizing, mapped.Stage);
    }

    [Fact]
    public void MapInstall_Finished_IsCompleted()
    {
        var mapped = WinGetProgressMapper.MapInstall(
            new InstallProgress { State = PackageInstallProgressState.Finished }
        );
        Assert.Equal(OperationProgress.Completed, mapped);
    }

    [Fact]
    public void MapUninstall_Uninstalling_KnownIsDeterminate()
    {
        var mapped = WinGetProgressMapper.MapUninstall(
            new UninstallProgress
            {
                State = PackageUninstallProgressState.Uninstalling,
                UninstallationProgress = 75,
            }
        );
        Assert.True(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Uninstalling, mapped.Stage);
    }

    [Fact]
    public void MapUninstall_Uninstalling_UnknownIsIndeterminate()
    {
        var mapped = WinGetProgressMapper.MapUninstall(
            new UninstallProgress
            {
                State = PackageUninstallProgressState.Uninstalling,
                UninstallationProgress = 0,
            }
        );
        Assert.False(mapped.IsDeterminate);
        Assert.Equal(OperationProgressStage.Uninstalling, mapped.Stage);
    }

    /// <summary>
    /// The mapper itself stays stateless and never synthesizes a speed; the
    /// generic operation layer attaches the measured throughput downstream
    /// from the mapped cumulative byte samples.
    /// </summary>
    [Fact]
    public void MappedDownload_ReceivesGenericCalculatedSpeedDownstream()
    {
        using var op = new WinGetSpeedProbeOperation();
        var clock = new WinGetManualClock();
        op.SetClockForTests(clock.Provider);

        var first = WinGetProgressMapper.MapInstall(
            new InstallProgress
            {
                State = PackageInstallProgressState.Downloading,
                BytesDownloaded = 0,
                BytesRequired = 10UL * 1024 * 1024,
                DownloadProgress = 0,
            }
        );
        Assert.Null(first.BytesPerSecond);

        op.ReportForTests(first);
        Assert.True(op.CurrentProgress.IsDeterminate);
        Assert.Null(op.CurrentProgress.BytesPerSecond);

        clock.Advance(TimeSpan.FromSeconds(2));
        op.ReportForTests(
            WinGetProgressMapper.MapInstall(
                new InstallProgress
                {
                    State = PackageInstallProgressState.Downloading,
                    BytesDownloaded = 2UL * 1024 * 1024,
                    BytesRequired = 10UL * 1024 * 1024,
                    DownloadProgress = 20,
                }
            )
        );

        // (2 MiB - 0) / 2 s = 1 MiB/s, calculated generically downstream.
        Assert.Equal(1024 * 1024.0, op.CurrentProgress.BytesPerSecond);
        Assert.True(op.CurrentProgress.HasThroughput);
        Assert.Contains(
            "MB/s",
            OperationProgressFormatter.Format(op.CurrentProgress)
        );
    }

    private sealed class WinGetManualClock
    {
        private DateTime _now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

        public Func<DateTime> Provider => () => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class WinGetSpeedProbeOperation : AbstractOperation
    {
        public WinGetSpeedProbeOperation()
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

        public void SetClockForTests(Func<DateTime> provider) =>
            SetUtcNowProviderForTests(provider);

        protected override void ApplyRetryAction(string retryMode) { }

        protected override Task<OperationVeredict> PerformOperation() =>
            Task.FromResult(OperationVeredict.Success);

        public override Task<Uri> GetOperationIcon() =>
            Task.FromResult(new Uri("avares://UniGetUI/Assets/package_color.png"));
    }
}
#endif
