#if WINDOWS
using Microsoft.Management.Deployment;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Managers.WingetManager;

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
}
#endif
