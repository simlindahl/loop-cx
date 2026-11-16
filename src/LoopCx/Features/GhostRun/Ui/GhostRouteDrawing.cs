using System.Globalization;

namespace LoopCx.Features.GhostRun.Ui;

public sealed record RouteDrawingPoint(double X, double Y)
{
    public string XText => X.ToString("0.###", CultureInfo.InvariantCulture);
    public string YText => Y.ToString("0.###", CultureInfo.InvariantCulture);
    public string Position => $"{XText} {YText}";
}

// Presentation-only projection of the frozen ordered geometry; never distance or route matching.
public sealed class GhostRouteDrawing
{
    private readonly GhostRoute route;
    private readonly RouteDrawingPoint[] points;

    public GhostRouteDrawing(GhostRoute route)
    {
        this.route = route;
        var origin = route.Geometry[0];
        var cosine = Math.Cos(origin.Latitude * Math.PI / 180);
        var projected = route.Geometry.Select(point => new RouteDrawingPoint(
            Math.IEEERemainder(point.Longitude - origin.Longitude, 360) * cosine, -point.Latitude)).ToArray();
        var left = projected.Min(point => point.X);
        var top = projected.Min(point => point.Y);
        var width = projected.Max(point => point.X) - left;
        var height = projected.Max(point => point.Y) - top;
        var scale = Math.Min(256 / Math.Max(width, .000000001), 176 / Math.Max(height, .000000001));
        points = projected.Select(point => new RouteDrawingPoint(32 + (256 - width * scale) / 2 + (point.X - left) * scale,
            32 + (176 - height * scale) / 2 + (point.Y - top) * scale)).ToArray();
    }

    public string Line => string.Join(" ", points.Select(point => point.Position.Replace(' ', ',')));
    public RouteDrawingPoint Start => points[0];
    public RouteDrawingPoint Finish => points[^1];
    public RouteDrawingPoint? PointAt(double? metres)
    {
        if (metres is not { } at || !double.IsFinite(at) || at < 0 || at > route.LengthMetres) return null;
        if (at == 0) return Start;
        for (var index = 1; index < points.Length; index++)
        {
            var from = route.Geometry[index - 1].Metres;
            var to = route.Geometry[index].Metres;
            if (at > to) continue;
            var fraction = (at - from) / (to - from);
            return new(points[index - 1].X + (points[index].X - points[index - 1].X) * fraction,
                points[index - 1].Y + (points[index].Y - points[index - 1].Y) * fraction);
        }
        return null;
    }
    public double HeadingAt(double metres)
    {
        var step = Math.Min(1, route.LengthMetres / 100);
        var a = PointAt(Math.Clamp(metres, 0, route.LengthMetres - step))!;
        var b = PointAt(Math.Clamp(metres + step, step, route.LengthMetres))!;
        return Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 90;
    }
}
