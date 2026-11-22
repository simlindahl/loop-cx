using System.Globalization;

namespace LoopCx.Features.GhostRun.Ui;

public sealed record RouteDrawingPoint(double X, double Y)
{
    public string XText => X.ToString("0.###", CultureInfo.InvariantCulture);
    public string YText => Y.ToString("0.###", CultureInfo.InvariantCulture);
    public string Position => $"{XText} {YText}";
}

internal readonly record struct GhostRouteCoordinate(double Latitude, double Longitude);
internal readonly record struct GhostRouteMarkers(double? Runner, double? Opponent)
{
    public static GhostRouteMarkers From(GhostRoute route, GhostRaceSnapshot? race)
    {
        if (race is null || race.Route != route.Key || race.Match.Route != route.Key || race.Match.RuleVersion != route.RuleVersion)
        {
            return default;
        }

        var runner = race.Match.State is RouteMatchState.Tracking or RouteMatchState.Finished && race.Match.Problem is null
            ? Supported(route, race.Match.Metres) : null;
        var opponent = race.ReferenceId is not null ? Supported(route, race.GhostMetres) : null;

        return new(runner, opponent);
    }

    private static double? Supported(GhostRoute route, double? metres)
    {
        return metres is { } value && double.IsFinite(value) && value >= 0 && value <= route.LengthMetres ? value : null;
    }
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
        if (SegmentAt(metres) is not { } segment)
        {
            return null;
        }

        var from = points[segment.End - 1];
        var to = points[segment.End];
        return new(from.X + ((to.X - from.X) * segment.Fraction), from.Y + ((to.Y - from.Y) * segment.Fraction));
    }

    internal GhostRouteCoordinate? CoordinateAt(double? metres)
    {
        if (SegmentAt(metres) is not { } segment)
        {
            return null;
        }

        var from = route.Geometry[segment.End - 1];
        var to = route.Geometry[segment.End];
        var longitudeDelta = Math.IEEERemainder(to.Longitude - from.Longitude, 360);

        return new(from.Latitude + ((to.Latitude - from.Latitude) * segment.Fraction),
            Math.IEEERemainder(from.Longitude + (longitudeDelta * segment.Fraction), 360));
    }

    private (int End, double Fraction)? SegmentAt(double? metres)
    {
        if (metres is not { } at || !double.IsFinite(at) || at < 0 || at > route.LengthMetres)
        {
            return null;
        }

        for (var index = 1; index < route.Geometry.Count; index++)
        {
            var to = route.Geometry[index].Metres;
            if (at > to) continue;

            var from = route.Geometry[index - 1].Metres;
            return (index, (at - from) / (to - from));
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
