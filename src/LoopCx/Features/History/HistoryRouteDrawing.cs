using System.Globalization;
using LoopCx.Features.ExportRun;

namespace LoopCx.Features.History;

internal sealed record HistoryRouteGeometry(IReadOnlyList<IReadOnlyList<ExportLocation>> Segments)
{
    public bool HasSeparateSegments => Segments.Count > 1;

    public static HistoryRouteGeometry From(StoredRouteScope scope)
    {
        var predecessors = scope.Links.ToDictionary(static link => link.ToOrder, static link => link.FromOrder);
        var segments = new List<IReadOnlyList<ExportLocation>>();
        List<ExportLocation>? segment = null;

        foreach (var point in scope.Points)
        {
            if (segment is null || !predecessors.TryGetValue(point.SnapshotOrder, out var previous)
                || segment[^1].SnapshotOrder != previous)
            {
                segment = [];
                segments.Add(segment);
            }

            segment.Add(point);
        }

        return new(segments.AsReadOnly());
    }
}

internal sealed class HistoryRouteDrawing
{
    private readonly record struct Point(double X, double Y)
    {
        public string XText => X.ToString("0.###", CultureInfo.InvariantCulture);
        public string YText => Y.ToString("0.###", CultureInfo.InvariantCulture);
        public string Position => $"{XText} {YText}";
    }

    private HistoryRouteDrawing(Point[][] segments, bool unknownQuality)
    {
        Line = string.Join(' ', segments.Where(static segment => segment.Length > 1)
            .Select(static segment => "M " + string.Join(' ', segment.Select(static point => point.Position))));
        Isolated = string.Join(' ', segments.Where(static segment => segment.Length == 1)
            .Select(static segment => $"M {segment[0].Position} h 0.01"));

        if (!unknownQuality)
        {
            StartX = segments[0][0].XText;
            StartY = segments[0][0].YText;
            var end = segments[^1][^1];
            EndX = (end.X - 4).ToString("0.###", CultureInfo.InvariantCulture);
            EndY = (end.Y - 4).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public string Line { get; }
    public string Isolated { get; }
    public string? StartX { get; }
    public string? StartY { get; }
    public string? EndX { get; }
    public string? EndY { get; }

    public static HistoryRouteDrawing? Create(HistoryRouteGeometry geometry, bool unknownQuality)
    {
        if (geometry.Segments.Count == 0)
        {
            return null;
        }

        const double width = 292;
        const double height = 192;
        var origin = geometry.Segments[0][0];
        var cosine = Math.Cos(origin.LatitudeDegrees * Math.PI / 180);
        var projected = geometry.Segments.Select(segment => segment.Select(point => new Point(
            Math.IEEERemainder(point.LongitudeDegrees - origin.LongitudeDegrees, 360) * cosine, -point.LatitudeDegrees)).ToArray()).ToArray();
        var points = projected.SelectMany(static segment => segment).ToArray();
        var left = points.Min(static point => point.X);
        var top = points.Min(static point => point.Y);
        var spanX = points.Max(static point => point.X) - left;
        var spanY = points.Max(static point => point.Y) - top;
        var scale = Math.Min(width / Math.Max(spanX, 0.000000001), height / Math.Max(spanY, 0.000000001));
        var offsetX = 24 + ((width - (spanX * scale)) / 2);
        var offsetY = 24 + ((height - (spanY * scale)) / 2);
        var fitted = projected.Select(segment => segment.Select(point => new Point(
            offsetX + ((point.X - left) * scale), offsetY + ((point.Y - top) * scale))).ToArray()).ToArray();

        return new(fitted, unknownQuality);
    }
}
