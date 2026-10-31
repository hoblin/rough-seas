using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public static class SeaWeatherCommands
{
    private const double DefaultStormRange = 10000;
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
        var latitude = (api.World.Calendar.OnGetLatitude?.Invoke(pos.Z) ?? 0.5) * 90;
        return $"{api.Side} at {Coordinates(api, pos.X, pos.Z)}, latitude {latitude:0.0}°, hour {sea.Clock.Hours:0.00}: "
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

    private static float Rainfall(ICoreAPI api, Vec3d pos) =>
        api.World.BlockAccessor.GetClimateAt(pos.AsBlockPos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0;

    private static string FromCurrent(SeaCurrent current) =>
        current.WarmthCelsius is { } warmth ? $" ({Math.Round(warmth, 1) + 0.0:+0.0;-0.0} from the current)" : "";

    private static double Distance((double X, double Z) centre, Vec3d pos) =>
        Math.Sqrt((centre.X - pos.X) * (centre.X - pos.X) + (centre.Z - pos.Z) * (centre.Z - pos.Z));

    private static string Coordinates(ICoreAPI api, double x, double z) =>
        $"{x - api.World.BlockAccessor.MapSizeX / 2:0} {z - api.World.BlockAccessor.MapSizeZ / 2:0}";
}
