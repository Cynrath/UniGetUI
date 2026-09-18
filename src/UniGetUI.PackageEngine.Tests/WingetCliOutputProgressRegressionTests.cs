using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;
using LineType = UniGetUI.PackageOperations.AbstractOperation.LineType;

namespace UniGetUI.PackageEngine.Tests;

/// <summary>
/// Regression evidence for the §5 determination: piped WinGet CLI output carries no
/// reliable progress frames, so it must flow through the normal log/history path only
/// and must never produce structured (determinate) progress.
///
/// Fixtures are the sanitized lines actually captured from
/// <c>winget download --id 7zip.7zip --exact --accept-source-agreements
/// --disable-interactivity --accept-package-agreements</c> (winget v1.29.290) with
/// stdout redirected: six plain CR LF lines over an ~8.5&nbsp;s download, empty stderr,
/// no ANSI escapes, no byte counters, no percentages. The test replays them exactly as
/// <see cref="AbstractProcessOperation"/> splits them (CR-terminated text becomes a
/// <see cref="LineType.ProgressIndicator"/> line, promoted to
/// <see cref="LineType.Information"/> by the bare LF that follows) and asserts history
/// preservation plus the absence of invented progress.
/// </summary>
public sealed class WingetCliOutputProgressRegressionTests
{
    private sealed class ProgressProbeOperation : AbstractOperation
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

        public void EmitForTests(string line, LineType type) => Line(line, type);

        protected override void ApplyRetryAction(string retryMode) { }

        protected override Task<OperationVeredict> PerformOperation() =>
            Task.FromResult(OperationVeredict.Success);

        public override Task<Uri> GetOperationIcon() =>
            Task.FromResult(new Uri("avares://UniGetUI/Assets/package_color.png"));
    }

    /// <summary>
    /// Sanitized raw capture: only the user-specific download target path was replaced.
    /// </summary>
    private static readonly string[] RealCapturedWingetDownloadLines =
    [
        "Found 7-Zip [7zip.7zip] Version 26.03",
        "This application is licensed to you by its owner.",
        "Microsoft is not responsible for, nor does it grant any licenses to, third-party packages.",
        "Downloading https://www.7-zip.org/a/7z2603-x64.msi",
        "Successfully verified installer hash",
        "Installer downloaded: <download-dir>\\7-Zip_26.03_Machine_X64_wix_en-US.msi",
    ];

    private static void ReplayAsProcessReaderWouldSplit(
        ProgressProbeOperation op,
        string rawLine
    )
    {
        // AbstractProcessOperation: text terminated by CR is emitted as a progress
        // indicator; the bare LF that follows promotes it to a regular line.
        op.EmitForTests(rawLine, LineType.ProgressIndicator);
        op.EmitForTests(rawLine, LineType.Information);
    }

    [Fact]
    public void RealWingetDownloadOutput_PreservedInHistory_CreatesNoStructuredProgress()
    {
        using var op = new ProgressProbeOperation();
        int progressEvents = 0;
        op.ProgressChanged += (_, _) => progressEvents++;

        foreach (string line in RealCapturedWingetDownloadLines)
            ReplayAsProcessReaderWouldSplit(op, line);

        // Detailed CLI output is preserved for troubleshooting/history: progress
        // indicator frames stay out of the stored output, regular lines stay in.
        var stored = op.GetOutput();
        Assert.Equal(RealCapturedWingetDownloadLines.Length, stored.Count);
        Assert.Equal(
            RealCapturedWingetDownloadLines,
            stored.Select(entry => entry.Item1).ToArray()
        );
        Assert.All(stored, static entry => Assert.Equal(LineType.Information, entry.Item2));

        // And no determinate progress is invented from lines that carry no numbers.
        Assert.Equal(0, progressEvents);
        Assert.Equal(OperationProgress.Unknown, op.CurrentProgress);
    }

    [Fact]
    public void RealWingetDownloadOutput_ContainsNoParsableProgressSignals()
    {
        // Pins the §5 evidence: if a future winget version adds byte counters or
        // percentages to piped output, this documents the exact previously-observed
        // shape that justified staying indeterminate.
        foreach (string line in RealCapturedWingetDownloadLines)
        {
            Assert.DoesNotContain("%", line, StringComparison.Ordinal);
            Assert.DoesNotContain("MB", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("KB", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GB", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("B/s", line, StringComparison.OrdinalIgnoreCase);
        }
    }
}
