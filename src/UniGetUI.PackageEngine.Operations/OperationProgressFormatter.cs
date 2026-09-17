using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Enums;

namespace UniGetUI.PackageOperations;

/// <summary>
/// Formats a generic <see cref="OperationProgress"/> for operation cards,
/// log lines and screen-reader status. Unknown progress maps to a short
/// stage label (indeterminate); determinate progress appends percent and,
/// when available, human-readable byte counters plus the measured download
/// throughput (e.g. "Downloading · 21% · 10.0 MB / 46.7 MB · 3.8 MB/s").
/// No ETA is shown.
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
                $"{label} \u00b7 {percent}% \u00b7 {FormatBytes(progress.BytesDownloaded.Value)} / {FormatBytes(progress.BytesTotal.Value)}";
            string? throughput = FormatThroughput(progress.BytesPerSecond);
            return throughput is null ? text : $"{text} \u00b7 {throughput}";
        }

        return $"{label} \u00b7 {percent}%";
    }

    public static string StageLabel(OperationProgressStage stage) =>
        stage switch
        {
            OperationProgressStage.Downloading => CoreTools.Translate("Downloading"),
            OperationProgressStage.Installing => CoreTools.Translate("Installing"),
            OperationProgressStage.Uninstalling => CoreTools.Translate("Uninstalling"),
            OperationProgressStage.Queued => CoreTools.Translate("Please wait..."),
            OperationProgressStage.Finalizing => CoreTools.Translate("Finalizing"),
            _ => CoreTools.Translate("Please wait..."),
        };

    private static string IndeterminateLabel(OperationProgressStage stage) =>
        stage switch
        {
            OperationProgressStage.Downloading => CoreTools.Translate("Downloading..."),
            OperationProgressStage.Installing => CoreTools.Translate("Installing..."),
            OperationProgressStage.Uninstalling => CoreTools.Translate("Uninstalling..."),
            OperationProgressStage.Queued => CoreTools.Translate("Please wait..."),
            OperationProgressStage.Finalizing => CoreTools.Translate("Finalizing..."),
            _ => CoreTools.Translate("Please wait..."),
        };

    private static string FormatBytes(ulong value) =>
        value > (ulong)long.MaxValue
            ? $"{value / 1099511627776.0:F1} TB"
            : CoreTools.FormatAsSize((long)value);

    /// <summary>
    /// Formats a measured throughput using the same binary-unit conventions as
    /// <see cref="CoreTools.FormatAsSize"/> (one decimal, current culture;
    /// unit suffixes stay invariant like the existing size strings). Returns
    /// null when there is no usable speed, in which case the caller keeps the
    /// established speed-less format. NaN/Infinity/non-positive values never
    /// produce output.
    /// </summary>
    private static string? FormatThroughput(double? bytesPerSecond)
    {
        double? normalized = OperationProgress.NormalizeBytesPerSecond(bytesPerSecond);
        if (normalized is null)
            return null;

        double value = normalized.Value;
        const double KiloByte = 1024d;
        const double MegaByte = 1024d * 1024d;
        const double GigaByte = 1024d * 1024d * 1024d;

        if (value >= GigaByte)
            return $"{value / GigaByte:F1} GB/s";
        if (value >= MegaByte)
            return $"{value / MegaByte:F1} MB/s";
        if (value >= KiloByte)
            return $"{value / KiloByte:F1} KB/s";
        return $"{value:F1} B/s";
    }
}
