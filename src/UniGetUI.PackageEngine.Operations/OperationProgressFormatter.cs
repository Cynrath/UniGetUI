using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Enums;

namespace UniGetUI.PackageOperations;

/// <summary>
/// Formats a generic <see cref="OperationProgress"/> for operation cards,
/// log lines and screen-reader status. Unknown progress maps to a short
/// stage label (indeterminate); determinate progress appends percent and,
/// when available, human-readable byte counters.
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
            return $"{label} \u00b7 {percent}% \u00b7 {FormatBytes(progress.BytesDownloaded.Value)} / {FormatBytes(progress.BytesTotal.Value)}";
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
}
