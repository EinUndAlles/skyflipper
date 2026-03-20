using Prometheus;

namespace SkyFlipperSolo.Services;

public static class FlipMetrics
{
    public static readonly Counter BinFlipsDetected = Metrics
        .CreateCounter("skyflipper_bin_flips_detected_total", "Total BIN flips detected.");

    public static readonly Counter BidFlipsDetected = Metrics
        .CreateCounter("skyflipper_bid_flips_detected_total", "Total BID flips detected.");

    public static readonly Counter ReferenceSelectionCalls = Metrics
        .CreateCounter("skyflipper_reference_selection_calls_total", "Reference selection calls.");

    public static readonly Counter ReferenceSelectionEmpty = Metrics
        .CreateCounter("skyflipper_reference_selection_empty_total", "Reference selection calls with zero references.");

    public static readonly Histogram ReferenceSelectionDuration = Metrics
        .CreateHistogram("skyflipper_reference_selection_seconds", "Reference selection duration in seconds.");

    public static readonly Histogram FlipDetectionDuration = Metrics
        .CreateHistogram("skyflipper_flip_detection_seconds", "Flip detection batch duration in seconds.");

    public static readonly Gauge ReferenceCount = Metrics
        .CreateGauge("skyflipper_reference_count", "Number of references selected for a flip.");

    public static IDisposable MeasureReferenceSelection() =>
        ReferenceSelectionDuration.NewTimer();

    public static IDisposable MeasureFlipDetection() =>
        FlipDetectionDuration.NewTimer();
}
