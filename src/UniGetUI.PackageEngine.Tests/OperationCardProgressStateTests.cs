using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

/// <summary>
/// Direct coverage for the exact mapping <c>OperationViewModel</c> uses for its
/// operation card (indeterminate vs determinate, percent, byte/status text,
/// retry/reset, terminal visuals). The mapping lives in
/// <see cref="OperationCardProgressState"/> so it runs without Avalonia; the
/// ViewModel only copies the result onto bindable properties on the UI thread
/// via <c>Dispatcher.UIThread.Post</c> (verified by inspection of
/// <c>OperationViewModel.cs</c>).
/// </summary>
public sealed class OperationCardProgressStateTests
{
    private static OperationCardProgressState FreshCard(
        string liveLine = "Please wait..."
    ) => new OperationCardProgressState(
        IsIndeterminate: false,
        Value: 0,
        LiveLine: liveLine
    );

    private static OperationCardProgressState RunningCard(
        string liveLine = "Please wait..."
    ) => FreshCard(liveLine).WithStatus(OperationStatus.Running);

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
        var card = RunningCard("Downloading installer...")
            .WithProgress(OperationStatus.Running, OperationProgress.Unknown);

        Assert.True(card.IsIndeterminate);
        // Plain Unknown resets must not overwrite the log-driven line.
        Assert.Equal("Downloading installer...", card.LiveLine);
    }

    [Fact]
    public void Running_WithKnownDownload_IsDeterminateWithPercentAndBytes()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(50, 100));

        Assert.False(card.IsIndeterminate);
        Assert.Equal(50, card.Value);
        Assert.Contains("50%", card.LiveLine);
        // Byte counters are shown when both sides are known.
        Assert.Contains("/", card.LiveLine);
    }

    [Fact]
    public void Running_UnknownAfterDeterminate_ReturnsToIndeterminate()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(50, 100));

        Assert.False(card.IsIndeterminate);

        card = card.WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(0, 0)
        );

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Downloading", card.LiveLine);
    }

    [Fact]
    public void DownloadToInstall_Transition_UpdatesStageAndValue()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(80, 100));

        Assert.Contains("Downloading", card.LiveLine);

        card = card.WithProgress(
            OperationStatus.Running,
            OperationProgress.FromInstall(30)
        );

        Assert.False(card.IsIndeterminate);
        Assert.Equal(30, card.Value);
        Assert.Contains("Installing", card.LiveLine);
        Assert.Contains("30%", card.LiveLine);
    }

    [Fact]
    public void Install_Known_IsDeterminate()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromInstall(34));

        Assert.False(card.IsIndeterminate);
        Assert.Equal(34, card.Value);
        Assert.Contains("Installing", card.LiveLine);
    }

    [Fact]
    public void Install_Null_StaysIndeterminateWithoutFakePercent()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromInstall(null));

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Installing", card.LiveLine);
        Assert.DoesNotContain("%", card.LiveLine);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Install_Unknown_StaysIndeterminateWithoutFakePercent(double reported)
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromInstall(reported));

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Installing", card.LiveLine);
        Assert.DoesNotContain("%", card.LiveLine);
    }

    [Fact]
    public void Uninstall_Known_IsDeterminate()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromUninstall(75));

        Assert.False(card.IsIndeterminate);
        Assert.Equal(75, card.Value);
        Assert.Contains("Uninstalling", card.LiveLine);
    }

    [Fact]
    public void Uninstall_Unknown_IsIndeterminate()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromUninstall(0));

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Uninstalling", card.LiveLine);
    }

    [Fact]
    public void InQueue_ResetsToZero()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(80, 100))
            .WithStatus(OperationStatus.InQueue);

        Assert.False(card.IsIndeterminate);
        Assert.Equal(0, card.Value);
    }

    [Fact]
    public void Running_Initial_IsIndeterminate()
    {
        var card = FreshCard().WithStatus(OperationStatus.Running);

        Assert.True(card.IsIndeterminate);
    }

    [Theory]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Canceled)]
    public void Terminal_Status_IsFullBar(OperationStatus status)
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(40, 100))
            .WithStatus(status);

        Assert.False(card.IsIndeterminate);
        Assert.Equal(100, card.Value);
    }

    [Theory]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Canceled)]
    [InlineData(OperationStatus.InQueue)]
    public void Progress_AfterTerminal_IsIgnored(OperationStatus status)
    {
        var card = FreshCard("done").WithStatus(status);
        var before = card;

        card = card.WithProgress(status, OperationProgress.FromDownload(90, 100));

        Assert.Equal(before, card);
    }

    [Fact]
    public void Retry_DoesNotLeakStaleProgress()
    {
        // First attempt showed 80%, then completed.
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.FromDownload(80, 100));
        Assert.Equal(80, card.Value);
        card = card.WithStatus(OperationStatus.Succeeded);
        Assert.Equal(100, card.Value);

        // Retry goes through InQueue (resets value to zero) then Running (indeterminate).
        card = card.WithStatus(OperationStatus.InQueue);
        Assert.Equal(0, card.Value);
        Assert.False(card.IsIndeterminate);
        card = card.WithStatus(OperationStatus.Running);
        Assert.True(card.IsIndeterminate);

        // The reset Unknown report stays indeterminate with a zero bar: the stale
        // 80% value never leaks into the new attempt. The text line itself is
        // log-driven and is refreshed by the next log/progress event.
        string lineBeforeReset = card.LiveLine;
        card = card.WithProgress(OperationStatus.Running, OperationProgress.Unknown);
        Assert.True(card.IsIndeterminate);
        Assert.Equal(0, card.Value);
        Assert.Equal(lineBeforeReset, card.LiveLine);

        // New determinate progress starts from zero, not from the stale 80%.
        card = card.WithProgress(
            OperationStatus.Running,
            OperationProgress.FromDownload(10, 100)
        );
        Assert.False(card.IsIndeterminate);
        Assert.Equal(10, card.Value);
        Assert.Contains("10%", card.LiveLine);
    }

    [Fact]
    public void Determinate_OutOfRange_IsClamped()
    {
        // The record constructor bypasses NormalizePercentage on purpose:
        // the card must still clamp like the ViewModel did.
        var over = new OperationProgress(150, null, null, OperationProgressStage.Downloading);
        var under = new OperationProgress(-20, null, null, OperationProgressStage.Downloading);

        Assert.Equal(100, RunningCard().WithProgress(OperationStatus.Running, over).Value);
        Assert.Equal(0, RunningCard().WithProgress(OperationStatus.Running, under).Value);
    }

    [Fact]
    public void QueuedStage_WithoutPercent_ShowsWaitingText()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.Queued);

        Assert.True(card.IsIndeterminate);
        Assert.False(string.IsNullOrWhiteSpace(card.LiveLine));
    }

    [Fact]
    public void Finalizing_WithoutPercent_StaysIndeterminate()
    {
        var card = RunningCard()
            .WithProgress(OperationStatus.Running, OperationProgress.Finalizing);

        Assert.True(card.IsIndeterminate);
        Assert.Contains("Finalizing", card.LiveLine);
    }

    private sealed class CardProbeOperation : AbstractOperation
    {
        public CardProbeOperation()
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

        protected override void ApplyRetryAction(string retryMode) { }

        protected override Task<OperationVeredict> PerformOperation() =>
            Task.FromResult(OperationVeredict.Success);

        public override Task<Uri> GetOperationIcon() =>
            Task.FromResult(new Uri("avares://UniGetUI/Assets/package_color.png"));
    }

    [Fact]
    public void ProgressChanged_Event_DrivesCardStateLikeViewModel()
    {
        using var op = new CardProbeOperation();
        var card = FreshCard().WithStatus(OperationStatus.Running);

        // The ViewModel subscribes to ProgressChanged and applies the mapping on
        // the UI thread; here we apply the same pure mapping directly.
        op.ProgressChanged += (_, progress) =>
        {
            card = card.WithProgress(OperationStatus.Running, progress);
        };

        op.ReportForTests(OperationProgress.FromDownload(25, 100));
        Assert.False(card.IsIndeterminate);
        Assert.Equal(25, card.Value);

        op.ReportForTests(OperationProgress.FromInstall(null));
        Assert.True(card.IsIndeterminate);
        Assert.Contains("Installing", card.LiveLine);
    }

    [Fact]
    public async Task ReportProgress_FromBackgroundThread_IsSafeForCardMapping()
    {
        using var op = new CardProbeOperation();
        var card = FreshCard().WithStatus(OperationStatus.Running);
        var gate = new object();

        op.ProgressChanged += (_, progress) =>
        {
            // The mapping itself is pure and thread-agnostic; only the property
            // assignment needs the UI thread in the real ViewModel.
            var next = card.WithProgress(OperationStatus.Running, progress);
            lock (gate)
                card = next;
        };

        await Task.Run(() =>
        {
            for (int i = 1; i <= 10; i++)
                op.ReportForTests(OperationProgress.FromDownload((ulong)(i * 10), 100));
        });

        lock (gate)
            Assert.False(card.IsIndeterminate);
    }
}
