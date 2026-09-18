using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Enums;

namespace UniGetUI.PackageOperations;

/// <summary>
/// Formats a generic <see cref="OperationProgress"/> for operation cards, log lines,
/// and screen-reader status. Unknown progress maps to a short stage label
/// (indeterminate); determinate progress appends percent and, when available,
/// human-readable byte counters plus the measured download throughput
/// (e.g. "Downloading · 21% · 10.0 MB / 46.7 MB · 1.2 MB/s"). No ETA is shown.
/// Size units reuse <see cref="CoreTools.FormatAsSize"/> conventions; stage labels go
/// through <see cref="CoreTools.Translate"/> like every other user-facing string.
/// </summary>
public static class OperationProgressFormatter
{
    public static string Format(OperationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (!progress.IsDeterminate)
            return IndeterminateLabel(progress.Stage);

        int percent = (int)Math.Round(progress.Percentage!.Value);
        string label = StageLabel(progress.Stage);
        if (
            progress.BytesDownloaded.HasValue
            && progress.BytesTotal.HasValue
            && progress.BytesTotal.Value > 0
        )
        {
            string text =
                $"{label} · {percent}% · {FormatBytes(progress.BytesDownloaded.Value)} / {FormatBytes(progress.BytesTotal.Value)}";
            string? throughput = FormatThroughput(progress.BytesPerSecond);
            return throughput is null ? text : $"{text} · {throughput}";
        }

        return $"{label} · {percent}%";
    }

    public static string StageLabel(OperationProgressStage stage) =>
        stage switch
        {
            OperationProgressStage.Downloading => CoreTools.Translate("Downloading"),
            OperationProgressStage.Installing => CoreTools.Translate("Installing"),
            OperationProgressStage.Updating => CoreTools.Translate("Updating"),
            OperationProgressStage.Uninstalling => CoreTools.Translate("Uninstalling"),
            _ => CoreTools.Translate("Please wait..."),
        };

    private static string IndeterminateLabel(OperationProgressStage stage) =>
        stage switch
        {
            OperationProgressStage.Downloading => CoreTools.Translate("Downloading..."),
            OperationProgressStage.Installing => CoreTools.Translate("Installing..."),
            OperationProgressStage.Updating => CoreTools.Translate("Updating..."),
            OperationProgressStage.Uninstalling => CoreTools.Translate("Uninstalling..."),
            _ => CoreTools.Translate("Please wait..."),
        };

    private static string FormatBytes(ulong value) =>
        value > (ulong)long.MaxValue
            ? $"{value / 1099511627776.0:F1} TB"
            : CoreTools.FormatAsSize((long)value);

    /// <summary>
    /// Formats a measured throughput reusing <see cref="CoreTools.FormatAsSize"/> units
    /// with a "/s" suffix. Returns null when there is no usable speed, in which case the
    /// caller keeps the established speed-less format.
    /// </summary>
    private static string? FormatThroughput(double? bytesPerSecond)
    {
        if (OperationProgress.NormalizeBytesPerSecond(bytesPerSecond) is not { } value)
            return null;

        return $"{CoreTools.FormatAsSize((long)value)}/s";
    }
}
