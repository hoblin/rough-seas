using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public static class SeaWeatherCommands
{
    private const double DefaultStormRange = 10000;
    private const double DefaultForecastDays = 30;
    private const double DefaultForecastRange = 60000;
    private const int ForecastLength = 10;
    private const double DefaultPassingSize = 1000;
    private const string AnyType = "any";
    private static readonly string[] StormTypes = ["squall", "gale", "hurricane", AnyType];
    private static readonly TextCommandResult NoDays = TextCommandResult.Error("Days must be more than 0");
    private const int ForecastBearings = 12;
    private static readonly double[] ForecastReaches = [0.08, 0.15, 0.25, 0.4, 0.6, 0.8];
    private const double DefaultCurrentRange = 4000;
    private const double MaxCurrentRange = 8000;
    private const double CurrentScanStep = 64;

    public static void Register(SeaWeatherSystem sea, ICoreServerAPI sapi)
    {
        sapi.ChatCommands.Create("seas")
            .WithDescription("Rough Seas: the sea and weather state as the server computes it")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("here")
                .WithDescription("Wind, sea state and storm at your position")
                .RequiresPlayer()
                .HandleWith(args => TextCommandResult.Success(Here(sea, sapi, args.Caller.Pos)))
            .EndSubCommand()
            .BeginSubCommand("ocean")
                .WithDescription("The world's ocean map at your position, how the built world calibrates it, and the fetch upwind")
                .RequiresPlayer()
                .HandleWith(args => TextCommandResult.Success(sea.OceanReport(sapi, args.Caller.Pos.X, args.Caller.Pos.Z)))
            .EndSubCommand()
            .BeginSubCommand("storms")
                .WithDescription("Active storms within a range of you, nearest first")
                .RequiresPlayer()
                .WithArgs(sapi.ChatCommands.Parsers.OptionalDouble("range", DefaultStormRange))
                .HandleWith(args => TextCommandResult.Success(Storms(sea, sapi, args.Caller.Pos, (double)args[0])))
            .EndSubCommand()
            .BeginSubCommand("forecast")
                .WithDescription("The strongest storms of a type, or of any, born within a range of a position over the coming days, with where and when each peaks; limit 0 lists them all")
                .RequiresPlayer()
                .WithArgs(sapi.ChatCommands.Parsers.Word("type", StormTypes),
                    sapi.ChatCommands.Parsers.OptionalDouble("days", DefaultForecastDays),
                    sapi.ChatCommands.Parsers.OptionalDouble("range", DefaultForecastRange),
                    sapi.ChatCommands.Parsers.OptionalInt("limit", ForecastLength),
                    sapi.ChatCommands.Parsers.OptionalWorldPosition("position"))
                .HandleWith(args =>
                {
                    var (pos, code, days, range, limit) = (((Vec3d)args[4]).Clone(), TypeCode(args[0]), (double)args[1], (double)args[2], (int)args[3]);
                    return days > 0 ? Later(sapi, args, () => Forecast(sea, sapi, pos, code, days, range, limit)) : NoDays;
                })
            .EndSubCommand()
            .BeginSubCommand("passing")
                .WithDescription("The storms of a type, or of any, that pass over a square of a size around a position over the coming days, in order of arrival, with how often they come and how long they stand over it; limit 0 lists them all")
                .RequiresPlayer()
                .WithArgs(sapi.ChatCommands.Parsers.Word("type", StormTypes),
                    sapi.ChatCommands.Parsers.OptionalDouble("days", DefaultForecastDays),
                    sapi.ChatCommands.Parsers.OptionalDouble("size", DefaultPassingSize),
                    sapi.ChatCommands.Parsers.OptionalInt("limit", ForecastLength),
                    sapi.ChatCommands.Parsers.OptionalWorldPosition("position"))
                .HandleWith(args => (double)args[1] > 0
                    ? TextCommandResult.Success(Passing(sea, sapi, (Vec3d)args[4], TypeCode(args[0]), (double)args[1], (double)args[2], (int)args[3]))
                    : NoDays)
            .EndSubCommand()
            .BeginSubCommand("stage")
                .WithDescription("Put a storm of a type over your position at its peak, held until a restart: a gale or hurricane standing still with its sea built, a squall travelling; 'clear' removes the staged ones")
                .RequiresPlayer()
                .WithArgs(sapi.ChatCommands.Parsers.Word("type", ["squall", "gale", "hurricane", "clear"]),
                    sapi.ChatCommands.Parsers.OptionalDouble("peak", double.NaN),
                    sapi.ChatCommands.Parsers.OptionalDouble("radius", double.NaN))
                .HandleWith(args => TextCommandResult.Success(Stage(sea, sapi, args.Caller.Pos, (string)args[0], (double)args[1], (double)args[2])))
            .EndSubCommand()
            .BeginSubCommand("currents")
                .WithDescription("The warmest, the coldest and the fastest current within a range of you, up to 8000 blocks")
                .RequiresPlayer()
                .WithArgs(sapi.ChatCommands.Parsers.OptionalDouble("range", DefaultCurrentRange))
                .HandleWith(args => TextCommandResult.Success(Currents(sea, sapi, args.Caller.Pos, (double)args[0])))
            .EndSubCommand()
            .BeginSubCommand("budget")
                .WithDescription("Server time spent in sea and weather since the last call")
                .HandleWith(_ => TextCommandResult.Success(sea.Budget.Report()))
            .EndSubCommand();
    }

    public static void Register(SeaWeatherSystem sea, ICoreClientAPI capi)
    {
        capi.ChatCommands.Create("seas")
            .WithDescription("Rough Seas: the sea and weather state as this client reads it from the server's map")
            .HandleWith(_ => TextCommandResult.Success(Here(sea, capi, capi.World.Player.Entity.Pos.XYZ)));
    }

    private static string Here(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos)
    {
        var state = sea.At(pos.X, pos.Z);
        var force = sea.Config.Beaufort.First(row => row.Force == state.Beaufort);
        var vanilla = api.World.BlockAccessor.GetWindSpeedAt(pos);
        var storm = state.StormPresence <= 0 ? "no storm" : $"{state.StormCode ?? "storm"} at {state.StormPresence:P0}";
        var current = sea.CurrentAt(pos.X, pos.Z);
        return $"{api.Side} at {Coordinates(api, pos.X, pos.Z)}, latitude {Latitude(api, pos.Z):0.0}°, hour {sea.Clock.Hours:0.00}: "
            + $"wind from {state.WindFromDegrees:0}° at {state.WindSpeed:0.0} m/s, Beaufort {state.Beaufort} ({force.Name}); "
            + $"waves {state.WaveHeight:0.0} m every {state.WavePeriod:0.0} s; {storm}, rain {Rainfall(api, pos):P0}; "
            + $"current toward {current.TowardDegrees:0}° at {current.Speed:0.00} m/s, water {current.WaterCelsius:0.0} °C{FromCurrent(current)}; "
            + $"vanilla wind ({vanilla.X:0.00}, {vanilla.Z:0.00})";
    }

    private static string Storms(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos, double range)
    {
        var now = sea.Clock.Hours;
        var storms = sea.Model.StormsWithin(pos.X, pos.Z, range)
            .Select(storm => (storm, centre: storm.CentreAt(now)))
            .OrderBy(entry => Distance(entry.centre, pos))
            .Select(entry => Describe(api, entry.storm, entry.centre, pos, now))
            .ToArray();
        return storms.Length == 0
            ? $"No storms within {range:0} blocks"
            : string.Join("\n", storms);
    }

    private static string Describe(ICoreAPI api, Storm storm, (double X, double Z) centre, Vec3d pos, double now)
    {
        var bearing = Compass.FromDegrees(pos.X - centre.X, pos.Z - centre.Z);
        var heading = (Compass.FromDegrees(storm.VelocityX, storm.VelocityZ) + 180) % 360;
        var speed = Math.Sqrt(storm.VelocityX * storm.VelocityX + storm.VelocityZ * storm.VelocityZ);
        return $"{storm.Code}: {Distance(centre, pos):0} blocks away bearing {bearing:0}°, centre {Coordinates(api, centre.X, centre.Z)}, "
            + $"radius {storm.Radius:0}, strength {storm.Strength(now):P0} of {storm.PeakMps:0} m/s, "
            + $"heading {heading:0}° at {speed:0} blocks/h, {storm.DiesHours - now:0.0} h left";
    }

    private static TextCommandResult Later(ICoreServerAPI sapi, TextCommandCallingArgs args, Func<string> answer)
    {
        void Reply(string message, EnumChatType type) => (args.Caller.Player as IServerPlayer)?.SendMessage(args.Caller.FromChatGroupId, message, type);

        Task.Run(answer).ContinueWith(task => sapi.Event.EnqueueMainThreadTask(() =>
        {
            if (task.Exception is not { } error)
            {
                Reply(task.Result, EnumChatType.CommandSuccess);
                return;
            }
            sapi.Logger.Error(error);
            Reply($"Failed: {error.InnerException?.Message}", EnumChatType.CommandError);
        }, "roughseas-seas-command"));
        return TextCommandResult.Deferred;
    }

    private static string? TypeCode(object type) => (string)type == AnyType ? null : (string)type;

    private static IEnumerable<T> Limited<T>(IEnumerable<T> entries, int limit) => limit > 0 ? entries.Take(limit) : entries;

    private static string Forecast(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos, string? code, double days, double range, int limit)
    {
        var now = sea.Clock.Hours;
        var brewed = sea.Model.Weather.Storms.Brewed(code, pos.X, pos.Z, now, now + days * api.World.Calendar.HoursPerDay, range)
            .OrderByDescending(storm => storm.PeakMps);
        var storms = Limited(brewed, limit)
            .Select(storm => (storm, peak: storm.BornHours + storm.LifetimeHours / 2))
            .Select(entry => (entry.storm, entry.peak, roughest: Roughest(sea.Model, entry.storm, entry.peak)))
            .Select(entry =>
            {
                var (storm, peak, roughest) = entry;
                var centre = storm.CentreAt(peak);
                var under = roughest is var (x, z, state)
                    ? $"its roughest open sea Beaufort {state.Beaufort}, waves {state.WaveHeight:0.0} m, at {Coordinates(api, x, z)}"
                    : "no open sea under it";
                return $"{storm.Code} of {storm.PeakMps:0} m/s, radius {storm.Radius:0}: peaks in {peak - now:0} h "
                    + $"(born in {storm.BornHours - now:0} h, lives {storm.LifetimeHours:0} h) at {Coordinates(api, centre.X, centre.Z)}, "
                    + $"{Distance(centre, pos):0} blocks away; {under}, water {sea.CurrentAt(centre.X, centre.Z).WaterCelsius:0} °C";
            })
            .ToArray();
        return storms.Length == 0
            ? $"No {code ?? "storm"} is born within {range:0} blocks in the next {days:0} days"
            : string.Join("\n", storms);
    }

    private static string Passing(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos, string? code, double days, double size, int limit)
    {
        var now = sea.Clock.Hours;
        var hoursPerDay = api.World.Calendar.HoursPerDay;
        var survey = new StormSurvey(sea.Model.Weather.Storms.Passing(code, pos.X, pos.Z, size / 2, now, now + days * hoursPerDay), now, now + days * hoursPerDay);
        var types = string.Join(", ", survey.ByType.Select(type => $"{type.Count} {type.Code}"));
        var summary = $"{survey.Passes.Count} {code ?? "storm"}s over the {size:0}-block square at {Coordinates(api, pos.X, pos.Z)}, latitude {Latitude(api, pos.Z):0.0}°, in {days:0} days"
            + (survey.Passes.Count == 0 ? ", " : $" ({types}): one every {days / survey.Passes.Count:0.0} days, ")
            + $"a storm over it {survey.CoveredHours / survey.Hours:P0} of the time, the longest calm {survey.LongestCalmHours / hoursPerDay:0.0} days";
        var passes = Limited(survey.Passes, limit).Select(pass =>
            $"{pass.Storm.Code} of {pass.Storm.PeakMps:0} m/s, radius {pass.Storm.Radius:0}: over it in {(pass.FromHours - now) / hoursPerDay:0.0} days for {pass.Hours:0.0} h, "
            + $"at most {pass.Presence:P0} of its strength there");
        return string.Join("\n", passes.Prepend(summary));
    }

    private static string Stage(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos, string code, double peak, double radius)
    {
        var storms = sea.Model.Weather.Storms;
        if (code == "clear")
        {
            storms.Unstage();
            sea.Model.Forget();
            return "Staged storms removed";
        }

        var storm = storms.Stage(code, pos.X, pos.Z, sea.Clock.Hours, double.IsNaN(peak) ? null : peak, double.IsNaN(radius) ? null : radius);
        sea.Model.Forget();
        return $"Staged {Describe(api, storm, storm.CentreAt(sea.Clock.Hours), pos, sea.Clock.Hours)}";
    }

    private static (double X, double Z, SeaState State)? Roughest(ISeaModel model, Storm storm, double hours)
    {
        var (centreX, centreZ) = storm.CentreAt(hours);
        return ForecastReaches
            .SelectMany(reach => Enumerable.Range(0, ForecastBearings).Select(i => (reach, angle: i * 2 * Math.PI / ForecastBearings)))
            .Select(probe => (X: centreX + Math.Sin(probe.angle) * probe.reach * storm.Radius, Z: centreZ + Math.Cos(probe.angle) * probe.reach * storm.Radius))
            .Where(point => model.IsSea(point.X, point.Z))
            .Select(point => ((double X, double Z, SeaState State)?)(point.X, point.Z, model.At(point.X, point.Z, hours)))
            .MaxBy(probe => probe!.Value.State.WaveHeight);
    }

    private static string Currents(SeaWeatherSystem sea, ICoreAPI api, Vec3d pos, double range)
    {
        range = Math.Clamp(range, 0, MaxCurrentRange);
        var reach = (int)(range / CurrentScanStep);
        var offsets = Enumerable.Range(-reach, 2 * reach + 1).Select(i => i * CurrentScanStep).ToArray();
        var samples = offsets
            .SelectMany(dx => offsets.Select(dz => (X: pos.X + dx, Z: pos.Z + dz)))
            .Where(at => Distance(at, pos) <= range)
            .Select(at => (at.X, at.Z, Current: sea.CurrentAt(at.X, at.Z)))
            .ToArray();
        var warm = samples.MaxBy(sample => sample.Current.WarmthCelsius);
        var cold = samples.MinBy(sample => sample.Current.WarmthCelsius);
        var fast = samples.MaxBy(sample => sample.Current.Speed);
        return $"Warmest: {Describe(api, warm.Current, warm.X, warm.Z, pos)}\n"
            + $"Coldest: {Describe(api, cold.Current, cold.X, cold.Z, pos)}\n"
            + $"Fastest: {Describe(api, fast.Current, fast.X, fast.Z, pos)}";
    }

    private static string Describe(ICoreAPI api, SeaCurrent current, double x, double z, Vec3d pos) =>
        $"toward {current.TowardDegrees:0}° at {current.Speed:0.00} m/s, water {current.WaterCelsius:0.0} °C{FromCurrent(current)}, "
        + $"at {Coordinates(api, x, z)}, {Distance((x, z), pos):0} blocks away bearing {Compass.FromDegrees(pos.X - x, pos.Z - z):0}°";

    private static double Latitude(ICoreAPI api, double z) => (api.World.Calendar.OnGetLatitude?.Invoke(z) ?? 0.5) * 90;

    private static float Rainfall(ICoreAPI api, Vec3d pos) =>
        api.World.BlockAccessor.GetClimateAt(pos.AsBlockPos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0;

    private static string FromCurrent(SeaCurrent current) =>
        current.WarmthCelsius is { } warmth ? $" ({Math.Round(warmth, 1) + 0.0:+0.0;-0.0} from the current)" : "";

    private static double Distance((double X, double Z) centre, Vec3d pos) =>
        Math.Sqrt((centre.X - pos.X) * (centre.X - pos.X) + (centre.Z - pos.Z) * (centre.Z - pos.Z));

    private static string Coordinates(ICoreAPI api, double x, double z) =>
        $"{x - api.World.BlockAccessor.MapSizeX / 2:0} {z - api.World.BlockAccessor.MapSizeZ / 2:0}";
}
