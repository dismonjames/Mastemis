namespace Mastemis.VisualReview;

internal sealed record CaptureMetadata(
    string Route,
    string State,
    string? Role,
    string Theme,
    int RequestedWidth,
    int RequestedHeight,
    int ActualWidth,
    int ActualHeight,
    double TextScale,
    bool ReducedMotion,
    int ProcessId,
    string WindowId,
    bool WindowActivated,
    bool CaptureSuccess,
    int? ProcessExitCode,
    DateTimeOffset CapturedAtUtc,
    string? Error);
