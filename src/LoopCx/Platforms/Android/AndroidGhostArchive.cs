using Android.Content;
using Android.Database.Sqlite;
using System.Globalization;
using System.Text.Json;
using LoopCx.Features.GhostRun;

namespace LoopCx;

/// <summary>Isolated append-only archive in the existing journal; no service or UI orchestration.</summary>
public sealed class AndroidGhostArchive
{
    private readonly SQLiteDatabase db;

    public AndroidGhostArchive(RunJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        db = journal.WritableDatabase!;
    }

    private void EnsureSchema()
    {
        using var existing = db.RawQuery("SELECT name FROM sqlite_master WHERE type='table' AND name='ghost_schema'", null)!;
        if (existing.MoveToFirst())
        {
            using var version = db.RawQuery("SELECT version FROM ghost_schema WHERE singleton=1 AND typeof(version)='integer'", null)!;
            if (!version.MoveToFirst() || version.GetInt(0) != 1)
                throw new InvalidDataException("The local Ghost archive revision is unsupported.");
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "table:ghost_schema", "table:ghost_routes", "table:ghost_attempts", "table:ghost_race_starts",
                "index:ghost_attempt_records", "trigger:ghost_route_frozen", "trigger:ghost_attempt_frozen", "trigger:ghost_race_start_frozen"
            };
            using var objects = db.RawQuery("SELECT type,name FROM sqlite_master WHERE name GLOB 'ghost_*'", null)!;
            while (objects.MoveToNext())
                if (!expected.Remove(objects.GetString(0) + ":" + objects.GetString(1)))
                    throw new InvalidDataException("The Ghost archive contains an unsupported object.");
            if (expected.Count != 0) throw new InvalidDataException("The Ghost archive namespace is incomplete.");
        }
        else
        {
            using var partial = db.RawQuery("SELECT name FROM sqlite_master WHERE name GLOB 'ghost_*'", null)!;
            if (partial.MoveToFirst()) throw new InvalidDataException("An unknown partial Ghost archive cannot be migrated.");
            using var stream = typeof(AndroidGhostArchive).Assembly.GetManifestResourceStream(GhostArchiveSchema.ResourceName)
                ?? throw new InvalidDataException("The canonical Ghost archive DDL is missing.");
            using var reader = new StreamReader(stream);
            foreach (var statement in GhostArchiveSchema.Statements(reader.ReadToEnd())) db.ExecSQL(statement);
        }
    }

    private T InArchive<T>(Func<T> operation)
    {
        db.BeginTransaction();
        try
        {
            EnsureSchema();
            var result = operation();
            db.SetTransactionSuccessful();

            return result;
        }
        finally { db.EndTransaction(); }
    }

    public GhostArchiveWrite SaveCapturedRoute(CapturedMainRoute captured, GhostAttempt sourceAttempt, string? qualityReason)
    {
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentNullException.ThrowIfNull(sourceAttempt);
        if (sourceAttempt.SourceRunId != captured.Route.SourceRunId || sourceAttempt.Route != captured.Route.Key
            || sourceAttempt.RuleVersion != captured.Policy.RuleVersion || sourceAttempt.Elapsed != captured.ObservedSpan
            || !sourceAttempt.Points.SequenceEqual(captured.Progress))
            throw new ArgumentException("The source attempt must preserve the captured route's identity and evidence.");
        ValidateReason(sourceAttempt, qualityReason);
        var routeValues = RouteValues(captured.Route, captured.Policy);
        var attemptValues = AttemptValues(sourceAttempt, qualityReason, captured.Raw);
        return InArchive(() =>
        {
            var insertedRoute = InsertFrozen("ghost_routes", routeValues, "route_id=? AND version_id=?",
                [captured.Route.Key.Id.ToString(), captured.Route.Key.Version.ToString()]);
            var insertedAttempt = InsertAttempt(sourceAttempt, attemptValues);
            return insertedRoute || insertedAttempt ? GhostArchiveWrite.Inserted : GhostArchiveWrite.AlreadyStored;
        });
    }

    public GhostArchiveWrite SaveAttempt(GhostAttempt attempt, string? qualityReason, IReadOnlyList<RecordedRouteFix> raw)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(raw);
        ValidateReason(attempt, qualityReason);
        var values = AttemptValues(attempt, qualityReason, raw);
        return InArchive(() =>
        {
            var route = ReadRouteCore(attempt.Route) ?? throw new KeyNotFoundException("The exact archived route version does not exist.");
            if (route.Route.RuleVersion != attempt.RuleVersion) throw new ArgumentException("The attempt uses another route rule version.");
            var inserted = InsertAttempt(attempt, values);
            return inserted ? GhostArchiveWrite.Inserted : GhostArchiveWrite.AlreadyStored;
        });
    }

    private static void ValidateReason(GhostAttempt attempt, string? reason)
    {
        if (attempt.Quality != AttemptQuality.Verified && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An unverified attempt needs its actual quality reason.");
    }

    private bool InsertAttempt(GhostAttempt attempt, Dictionary<string, object?> values) =>
        InsertFrozen("ghost_attempts", values, "attempt_id=? OR (route_id=? AND version_id=? AND source_run_id=?)",
            [attempt.Id.ToString(), attempt.Route.Id.ToString(), attempt.Route.Version.ToString(), attempt.SourceRunId.ToString()]);

    private bool InsertFrozen(string table, Dictionary<string, object?> values, string where, string[] arguments)
    {
        using var existing = db.RawQuery($"SELECT {string.Join(',', values.Keys)} FROM {table} WHERE {where}", arguments)!;
        if (existing.MoveToFirst())
        {
            var column = 0;
            foreach (var value in values.Values)
            {
                var saved = existing.IsNull(column) ? null : existing.GetString(column);
                if (saved != Convert.ToString(value, CultureInfo.InvariantCulture) && !(value is null && saved is null))
                    throw new InvalidDataException("Conflicting Ghost archive identity or frozen evidence; nothing was replaced.");
                column++;
            }
            if (existing.MoveToNext()) throw new InvalidDataException("Conflicting copies of one Ghost source recording.");

            return false;
        }
        using var row = new ContentValues();
        foreach (var pair in values)
        {
            if (pair.Value is null) row.PutNull(pair.Key);
            else if (pair.Value is long number) row.Put(pair.Key, number);
            else if (pair.Value is int integer) row.Put(pair.Key, integer);
            else row.Put(pair.Key, (string)pair.Value);
        }
        db.InsertOrThrow(table, null, row);

        return true;
    }

    private static Dictionary<string, object?> RouteValues(GhostRoute route, RouteCapturePolicy policy) => new()
    {
        ["route_id"] = route.Key.Id.ToString(),
        ["version_id"] = route.Key.Version.ToString(),
        ["source_run_id"] = route.SourceRunId.ToString(),
        ["name"] = route.Name,
        ["rule_version"] = route.RuleVersion,
        ["geometry_json"] = JsonSerializer.Serialize(route.Geometry),
        ["policy_json"] = JsonSerializer.Serialize(policy)
    };

    private static Dictionary<string, object?> AttemptValues(GhostAttempt attempt, string? reason, IReadOnlyList<RecordedRouteFix> raw) => new()
    {
        ["attempt_id"] = attempt.Id.ToString(),
        ["source_run_id"] = attempt.SourceRunId.ToString(),
        ["route_id"] = attempt.Route.Id.ToString(),
        ["version_id"] = attempt.Route.Version.ToString(),
        ["rule_version"] = attempt.RuleVersion,
        ["started_local"] = attempt.StartedLocal.ToString("O", CultureInfo.InvariantCulture),
        ["local_start_year"] = attempt.SavedLocalYear,
        ["time_zone_id"] = attempt.TimeZoneId,
        ["started_utc_ticks"] = attempt.StartedLocal.UtcDateTime.Ticks,
        ["elapsed_ticks"] = attempt.Elapsed.Ticks,
        ["quality"] = attempt.Quality.ToString(),
        ["quality_reason"] = reason,
        ["progress_json"] = JsonSerializer.Serialize(attempt.Points),
        ["raw_json"] = JsonSerializer.Serialize(raw)
    };

    public IReadOnlyList<ArchivedGhostRoute> ReadRoutes() => InArchive(() =>
    {
        using var keys = db.RawQuery("SELECT route_id,version_id FROM ghost_routes ORDER BY name,route_id,version_id", null)!;
        var routes = new List<ArchivedGhostRoute>();
        while (keys.MoveToNext())
            routes.Add(ReadRouteCore(new(Guid.Parse(keys.GetString(0)!), Guid.Parse(keys.GetString(1)!)))
                ?? throw new InvalidDataException("An archived route disappeared inside its transaction."));

        return routes.AsReadOnly();
    });

    public ArchivedGhostRoute? ReadRoute(RouteKey key) => InArchive(() => ReadRouteCore(key));

    private ArchivedGhostRoute? ReadRouteCore(RouteKey key)
    {
        using var row = db.RawQuery("SELECT source_run_id,name,rule_version,geometry_json,policy_json FROM ghost_routes WHERE route_id=? AND version_id=?",
            [key.Id.ToString(), key.Version.ToString()])!;
        if (!row.MoveToFirst()) return null;
        var geometry = JsonSerializer.Deserialize<RouteVertex[]>(row.GetString(3)!) ?? throw new InvalidDataException("Missing archived geometry.");
        var policy = JsonSerializer.Deserialize<RouteCapturePolicy>(row.GetString(4)!) ?? throw new InvalidDataException("Missing archived policy.");
        var route = new GhostRoute(key, row.GetString(1)!, Guid.Parse(row.GetString(0)!), row.GetString(2)!, geometry);
        if (route.RuleVersion != policy.RuleVersion) throw new InvalidDataException("Archived route policy identity disagrees.");

        return new(route, policy);
    }

    public IReadOnlyList<ArchivedGhostAttempt> ReadAttempts(RouteKey key) => InArchive(() => ReadAttemptsCore(key));

    private System.Collections.ObjectModel.ReadOnlyCollection<ArchivedGhostAttempt> ReadAttemptsCore(RouteKey key)
    {
        using var rows = db.RawQuery("SELECT attempt_id,source_run_id,rule_version,started_local,local_start_year,time_zone_id,started_utc_ticks,elapsed_ticks,quality,quality_reason,progress_json,raw_json FROM ghost_attempts WHERE route_id=? AND version_id=? ORDER BY started_utc_ticks,attempt_id",
            [key.Id.ToString(), key.Version.ToString()])!;
        var result = new List<ArchivedGhostAttempt>();
        while (rows.MoveToNext())
        {
            var startedLocal = DateTimeOffset.Parse(rows.GetString(3)!, CultureInfo.InvariantCulture);
            var points = JsonSerializer.Deserialize<GhostPoint[]>(rows.GetString(10)!) ?? throw new InvalidDataException("Missing archived progress.");
            var raw = JsonSerializer.Deserialize<RecordedRouteFix[]>(rows.GetString(11)!) ?? throw new InvalidDataException("Missing archived raw evidence.");
            var attempt = new GhostAttempt(Guid.Parse(rows.GetString(0)!), Guid.Parse(rows.GetString(1)!), key, startedLocal,
                rows.GetString(5)!, TimeSpan.FromTicks(rows.GetLong(7)), Enum.Parse<AttemptQuality>(rows.GetString(8)!), rows.GetString(2)!, points);
            if (attempt.SavedLocalYear != rows.GetInt(4) || startedLocal.UtcDateTime.Ticks != rows.GetLong(6))
                throw new InvalidDataException("Archived start-zone provenance disagrees.");
            result.Add(new(attempt, rows.IsNull(9) ? null : rows.GetString(9), Array.AsReadOnly(raw)));
        }

        return result.AsReadOnly();
    }

    public GhostRecords ReadRecords(RouteKey key, int frozenYear) => InArchive(() =>
    {
        var route = ReadRouteCore(key) ?? throw new KeyNotFoundException("The exact route version does not exist.");

        return GhostReferences.Select(route.Route, ReadAttemptsCore(key).Select(static entry => entry.Attempt), frozenYear);
    });
}
