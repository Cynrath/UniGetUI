using UniGetUI.PackageEngine.Enums;

namespace UniGetUI.PackageOperations;

/// <summary>
/// Pure, UI-framework-agnostic mapping from operation status plus generic
/// <see cref="OperationProgress"/> to operation-card progress visuals.
/// Extracted from <c>OperationViewModel</c> so the determinate/indeterminate
/// contract is unit-testable without Avalonia. This type never touches the
/// dispatcher or any UI control; the ViewModel remains the only UI-thread owner
/// and simply copies <see cref="IsIndeterminate"/>, <see cref="Value"/> and
/// <see cref="LiveLine"/> onto its bindable properties.
/// </summary>
public sealed record OperationCardProgressState(
    bool IsIndeterminate,
    double Value,
    string LiveLine
)
{
    /// <summary>
    /// Mirrors <c>OperationViewModel.ApplyStatus</c> for the progress visuals only
    /// (brushes and button text stay in the ViewModel). Terminal statuses own the
    /// final visuals with a full bar; queue resets to zero; running keeps the
    /// current value and shows the indeterminate animation until real progress
    /// arrives.
    /// </summary>
    public OperationCardProgressState WithStatus(OperationStatus status) =>
        status switch
        {
            OperationStatus.InQueue => this with { IsIndeterminate = false, Value = 0 },
            OperationStatus.Running => this with { IsIndeterminate = true },
            OperationStatus.Succeeded
            or OperationStatus.Failed
            or OperationStatus.Canceled => this with { IsIndeterminate = false, Value = 100 },
            _ => this,
        };

    /// <summary>
    /// Mirrors <c>OperationViewModel.ApplyProgress</c>. Progress is only honored
    /// while <paramref name="status"/> is <see cref="OperationStatus.Running"/>;
    /// progress arriving after completion is ignored so terminal visuals win and
    /// stale reports never leak into a later retry (retries reset via
    /// <see cref="AbstractOperation.ResetProgress"/> plus an <c>InQueue</c>
    /// status, which clears the bar before the next <c>Running</c> phase).
    /// Unknown progress keeps the indeterminate animation; a real but unmeasured
    /// phase (for example downloading with no byte totals) still updates the
    /// status text, while a plain <c>Unknown</c> reset preserves the existing
    /// log-driven line.
    /// </summary>
    public OperationCardProgressState WithProgress(
        OperationStatus status,
        OperationProgress? progress
    )
    {
        if (status is not OperationStatus.Running)
            return this;

        if (progress is null || !progress.IsDeterminate)
        {
            string liveLine =
                progress is not null && progress.Stage is not OperationProgressStage.Unknown
                    ? OperationProgressFormatter.Format(progress)
                    : LiveLine;
            return this with { IsIndeterminate = true, LiveLine = liveLine };
        }

        return this with
        {
            IsIndeterminate = false,
            Value = Math.Clamp(progress.Percentage!.Value, 0, 100),
            LiveLine = OperationProgressFormatter.Format(progress),
        };
    }
}
