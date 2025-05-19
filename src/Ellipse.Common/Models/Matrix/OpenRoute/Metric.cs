namespace Ellipse.Common.Models.Matrix.OpenRoute;

public enum Metric
{
    Distance,
    Duration,
}

public static class MetricExtensions
{
    public static string ToMetricString(this Metric metric)
    {
        return metric switch
        {
            Metric.Distance => "distance",
            Metric.Duration => "duration",
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
        };
    }
}
