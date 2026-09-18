using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

/// <summary>
/// Direct coverage for the exact mapping <see cref="OperationViewModel"/> uses for its
/// operation card (indeterminate vs determinate, percent, byte/status text, retry/reset,
/// terminal visuals). The mapping lives in <see cref="OperationCardProgressState"/> so
/// it runs without Avalonia; the ViewModel only copies the result onto bindable
/// properties on the UI thread via <c>Dispatcher.UIThread.Post</c>.
/// </summary>
public sealed class OperationCardProgressStateTests
{
    private const ulong OneMiB = 1024UL * 1024;

    private static OperationCardProgressState FreshCard(string liveLine = "Please wait...") =>
        new(IsIndeterminate: false, Value: 0, LiveLine: liveLine);

    private static OperationCardProgressState RunningCard(string liveLine = "Please wait...") =>
        FreshCard(liveLine).WithStatus(OperationStatus.Running);

    [Fact]
    public void Running_WithNoProgress_StaysIndeterminate()
    {
        var card = RunningCard().WithProgress(OperationStatus.Running, null);

        Assert.True(card.IsIndeterminate);
        Assert.Equal("Please wait...", card.LiveLine);
    }

    [Fact]
    public void Running_WithPlainUnknown_PreservesLogDrivenLine()
    {
        var card = RunningCard("Downloading installer...").WithProgress(
            OperationStatus.Running,
            OperationProgress.Unknown
        );

        Assert.True(card.IsIndeterminate);
        // Plain Unknown resets must not overwrite the log-driven line.
        Assert.Equal("Downloading installer...", card.LiveLine);
    }

    [Fact]
    public void Running_WithKnownDownload_IsDeterminateWithPercentAndBytes()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(50, 100)
        );

        Assert.False(card.IsIndeterminate);
        Assert.Equal(50, card.Value);
        Assert.Contains("50%", card.LiveLine);
        // Byte counters are shown when both sides are known.
        Assert.Contains("/", card.LiveLine);
    }

    [Fact]
    public void Running_WithZeroPercent_IsDeterminate()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(0, 100)
        );

        Assert.False(card.IsIndeterminate);
        Assert.Equal(0, card.Value);
        Assert.Contains("0%", card.LiveLine);
    }

    [Fact]
    public void Running_DownloadWithSpeed_ShowsThroughputInLiveLine()
    {
        var progress = OperationProgress.FromDownload(21 * OneMiB, 100 * OneMiB)
        with
        {
            BytesPerSecond = 1.2 * OneMiB,
        };
        var card = RunningCard().WithProgress(OperationStatus.Running, progress);

        Assert.False(card.IsIndeterminate);
        Assert.Contains("21%", card.LiveLine);
        Assert.Contains("/s", card.LiveLine);
    }

    [Fact]
    public void Running_UnknownDownloadStage_ShowsDownloadingIndeterminate()
    {
        var card = RunningCard("Starting operation...").WithProgress(
            OperationStatus.Running,
            OperationProgress.ForStage(OperationProgressStage.Downloading)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Downloading", card.LiveLine);
    }

    [Fact]
    public void Running_UnknownInstallStage_ShowsInstallingIndeterminate()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.ForStage(OperationProgressStage.Installing)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Installing", card.LiveLine);
        Assert.DoesNotContain("%", card.LiveLine);
    }

    [Fact]
    public void Running_UnknownUpdateStage_ShowsUpdatingIndeterminate()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.ForStage(OperationProgressStage.Updating)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Updating", card.LiveLine);
    }

    [Fact]
    public void Running_UnknownUninstallStage_ShowsUninstallingIndeterminate()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.ForStage(OperationProgressStage.Uninstalling)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Uninstalling", card.LiveLine);
    }

    [Fact]
    public void DownloadingToInstalling_RemovesSpeed()
    {
        var progress = OperationProgress.FromDownload(21 * OneMiB, 100 * OneMiB)
        with
        {
            BytesPerSecond = 1.2 * OneMiB,
        };
        var card = RunningCard().WithProgress(OperationStatus.Running, progress);

        Assert.Contains("/s", card.LiveLine);

        // Stage change away from Downloading: speed must not survive.
        card = card.WithProgress(
            OperationStatus.Running,
            OperationProgress.ForStage(OperationProgressStage.Installing)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Installing", card.LiveLine);
        Assert.DoesNotContain("/s", card.LiveLine);
    }

    [Fact]
    public void RetryReset_ReturnsToIndeterminate_KeepingLineForVmLogRestore()
    {
        var card = RunningCard("Starting operation...").WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(40, 100) with { BytesPerSecond = 1024 }
        );
        Assert.False(card.IsIndeterminate);
        string determinateLine = card.LiveLine;

        var reset = card.WithProgress(OperationStatus.Running, OperationProgress.Unknown);

        Assert.True(reset.IsIndeterminate);
        // The mapping never invents text: it keeps the line it holds. The ViewModel
        // swaps this for its separately-tracked last log line, so the stale
        // speed-bearing text never survives a retry reset on the real card.
        Assert.Equal(determinateLine, reset.LiveLine);
    }

    [Theory]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Canceled)]
    public void Progress_AfterTerminal_IsIgnored_StaleSpeedCannotSurvive(OperationStatus status)
    {
        // Terminal visuals own the card: a stale speed-bearing report arriving after
        // completion must not leak back into the visuals.
        var card = RunningCard()
            .WithProgress(
                OperationStatus.Running,
                OperationProgress.FromDownload(40, 100) with { BytesPerSecond = 1024 }
            )
            .WithStatus(status);
        var before = card;

        card = card.WithProgress(
            status,
            OperationProgress.FromDownload(90, 100) with { BytesPerSecond = 999_999 }
        );

        Assert.Equal(before, card);
        Assert.False(card.IsIndeterminate);
        Assert.Equal(100, card.Value);
    }

    [Theory]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Canceled)]
    public void TerminalStatus_OwnsFullBar(OperationStatus status)
    {
        var card = RunningCard().WithStatus(status);

        Assert.False(card.IsIndeterminate);
        Assert.Equal(100, card.Value);
    }

    [Fact]
    public void InQueue_ResetsToZero()
    {
        var card = RunningCard()
            .WithProgress(
                OperationStatus.Running,
                OperationProgress.FromDownload(40, 100)
            )
            .WithStatus(OperationStatus.InQueue);

        Assert.False(card.IsIndeterminate);
        Assert.Equal(0, card.Value);
    }

    [Fact]
    public void OvershootPercentage_IsClampedOnCard()
    {
        var card = RunningCard().WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(150, 100)
        );

        Assert.False(card.IsIndeterminate);
        Assert.Equal(100, card.Value);
    }
}
