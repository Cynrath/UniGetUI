namespace UniGetUI.PackageEngine.Enums;

/// <summary>
/// Coarse phase of a package operation, supplied by the package manager when
/// structured progress is available. The UI must never see manager-specific types.
/// </summary>
public enum OperationProgressStage
{
    Unknown,
    Queued,
    Downloading,
    Installing,
    Uninstalling,
    Finalizing,
}

/// <summary>
/// Generic, manager-agnostic progress report for a running operation.
/// <see cref="Percentage"/> is null when the real percentage is unknown, in
/// which case the UI must stay indeterminate. Byte counters are optional and
/// only set when the manager reports reliable values.
/// </summary>
public sealed record OperationProgress(
    double? Percentage,
    ulong? BytesDownloaded = null,
    ulong? BytesTotal = null,
    OperationProgressStage Stage = OperationProgressStage.Unknown
)
{
    public static readonly OperationProgress Unknown = new(
        null,
        null,
        null,
        OperationProgressStage.Unknown
    );

    /// <summary>
    /// True when <see cref="Percentage"/> holds a real 0-100 value.
    /// </summary>
    public bool IsDeterminate => Percentage.HasValue;

    /// <summary>
    /// Normalizes a raw percentage: NaN/Infinity become unknown (null),
    /// out-of-range values are clamped to 0-100.
    /// </summary>
    public static double? NormalizePercentage(double? value)
    {
        if (value is null)
            return null;
        double v = value.Value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return null;
        if (v < 0)
            return 0;
        if (v > 100)
            return 100;
        return v;
    }

    /// <summary>
    /// Builds a download progress report. Prefers byte-based percentage when
    /// <paramref name="bytesTotal"/> is greater than zero; otherwise falls back
    /// to <paramref name="reportedPercentage"/>. A zero/unknown total with no
    /// usable reported value yields an indeterminate report (no fake percent).
    /// </summary>
    public static OperationProgress FromDownload(
        ulong bytesDownloaded,
        ulong bytesTotal,
        double? reportedPercentage = null
    )
    {
        if (bytesTotal > 0)
        {
            double percent = bytesDownloaded / (double)bytesTotal * 100.0;
            return new OperationProgress(
                NormalizePercentage(percent),
                bytesDownloaded,
                bytesTotal,
                OperationProgressStage.Downloading
            );
        }

        double? normalized = NormalizePercentage(reportedPercentage);
        // A bare 0% with no byte totals carries no information; stay indeterminate.
        if (normalized is null or <= 0)
            return new OperationProgress(
                null,
                bytesDownloaded > 0 ? bytesDownloaded : null,
                null,
                OperationProgressStage.Downloading
            );

        return new OperationProgress(
            normalized,
            bytesDownloaded > 0 ? bytesDownloaded : null,
            null,
            OperationProgressStage.Downloading
        );
    }

    /// <summary>
    /// Builds an install progress report. Only a positive 0-100
    /// <paramref name="installationProgress"/> is treated as known; zero,
    /// negative, NaN and Infinity mean the installer supplied no usable
    /// progress and map to indeterminate (never synthesize a total).
    /// </summary>
    public static OperationProgress FromInstall(double? installationProgress)
    {
        double? normalized = NormalizePercentage(installationProgress);
        if (normalized is null or <= 0)
            return new OperationProgress(null, null, null, OperationProgressStage.Installing);
        return new OperationProgress(
            normalized,
            null,
            null,
            OperationProgressStage.Installing
        );
    }

    /// <summary>
    /// Builds an uninstall progress report. Same unknown semantics as
    /// <see cref="FromInstall"/>: only a positive value is determinate.
    /// </summary>
    public static OperationProgress FromUninstall(double? uninstallationProgress)
    {
        double? normalized = NormalizePercentage(uninstallationProgress);
        if (normalized is null or <= 0)
            return new OperationProgress(null, null, null, OperationProgressStage.Uninstalling);
        return new OperationProgress(
            normalized,
            null,
            null,
            OperationProgressStage.Uninstalling
        );
    }

    public static OperationProgress Queued =>
        new(null, null, null, OperationProgressStage.Queued);

    public static OperationProgress Finalizing =>
        new(null, null, null, OperationProgressStage.Finalizing);

    public static OperationProgress Completed =>
        new(100, null, null, OperationProgressStage.Finalizing);
}
