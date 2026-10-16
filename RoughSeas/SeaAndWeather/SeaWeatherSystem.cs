using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherSystem : ModSystem, ISeaWeather
{
    private const int SwellIntervalMs = 500;
    private const double SwellLogStepMetres = 0.5;

    private ICoreAPI api = null!;
    private Lazy<SeaWeatherModel> model = null!;
    private Lazy<SeaWeatherConfig> config = null!;
    private Lazy<SeaSurface> surface = null!;
    private Lazy<SwellField> swell = null!;
    private VanillaWeather vanilla = null!;

    public TickBudget Budget { get; } = new();

    /// <summary>After vanilla's weather systems (0.1), before the Devastation's effects (2).</summary>
    public override double ExecuteOrder() => 1;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        config = new Lazy<SeaWeatherConfig>(() => api.Assets.Get<SeaWeatherConfig>(new AssetLocation("roughseas", "config/seaweather.json")));
        model = new Lazy<SeaWeatherModel>(CreateModel);
        surface = new Lazy<SeaSurface>(() => new SeaSurface(Config.Swell));
        swell = new Lazy<SwellField>(() => Surface.Field());
        vanilla = new VanillaWeather(this, api);
        vanilla.FollowWind();
        VanillaWeatherPatches.Attach(this, api.Side);
        api.RegisterEntityBehaviorClass(CurrentDrift.Code, typeof(CurrentDrift));
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        vanilla.FollowRain();
        vanilla.FollowStormEvents(sapi);
        Budget.CountTicks(sapi);
        SeaWeatherCommands.Register(this, sapi);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        vanilla.FollowRain();
        SeaWeatherCommands.Register(this, capi);
        capi.Event.RegisterGameTickListener(_ => FollowSwell(capi), SwellIntervalMs);
        SwellShader.Attach(this, capi);
        SwellRide.Attach(this, capi);
    }

    public override void Dispose()
    {
        if (api.Side == EnumAppSide.Client)
        {
            SwellShader.Detach();
            SwellRide.Detach();
        }
        VanillaWeatherPatches.Detach(api.Side);
    }

    public bool DangerousOceans => api.World.Config.GetBool("dangerousOceans", true);

    public SeaWeatherConfig Config => config.Value;

    public SeaSurface Surface => surface.Value;

    public double NowHours => api.World.Calendar.TotalHours;

    /// <summary>The wave height at the client's player as it would be on open sea; the water shader scales it down near shore itself.</summary>
    public double SwellMetres { get; private set; }

    public SwellField Swell => swell.Value;

    public SeaState At(double x, double z) => At(x, z, NowHours);

    public SeaState At(double x, double z, double totalHours) => model.Value.At(x, z, totalHours, Oceanity(x, z));

    public SeaCurrent CurrentAt(double x, double z) => model.Value.Current(x, z);

    public bool IsSea(double x, double z) => model.Value.IsSea(x, z);

    public IEnumerable<Storm> StormsWithin(double x, double z, double range) => model.Value.Storms.Active(x, z, NowHours, range);

    public WindSample Wind(double x, double z) => model.Value.Wind(x, z, NowHours);

    public WindSample StormsAt(double x, double z, double totalHours) => model.Value.StormsAt(x, z, totalHours);

    /// <summary>How much of the open-sea swell the water shader shows here: none on land and lakes, all of it on open sea.</summary>
    public double OpenSea(double x, double z) => Math.Clamp((1 / Math.Max(0.2, 1 - Oceanity(x, z)) - 1) / 4, 0, 1);

    private void FollowSwell(ICoreClientAPI capi)
    {
        if (capi.World.Player?.Entity?.Pos is not { } pos) return;
        var state = At(pos.X, pos.Z);
        var height = state.Oceanity > 0 ? state.WaveHeight / state.Oceanity : 0;
        if (Math.Abs(height - SwellMetres) >= SwellLogStepMetres)
        {
            Mod.Logger.Debug("[swell] {0:F1} m, period {1:F1} s, wind toward {2:F0}°, at {3:F0} {4:F0}",
                height, Surface.Period(height), (Math.Atan2(state.WindZ, state.WindX) * 180 / Math.PI + 360) % 360, pos.X, pos.Z);
        }
        SwellMetres = height;
        Swell.Aim(height, state.WindX, state.WindZ);
        if (Swell.DistanceFromAnchor(pos.X, pos.Z) > Config.Swell.AnchorReachBlocks) Swell.Anchor(pos.X, pos.Z);
    }

    private double Oceanity(double x, double z)
    {
        var accessor = api.World.BlockAccessor;
        var regionSize = accessor.RegionSize;
        var map = accessor.GetMapRegion((int)x / regionSize, (int)z / regionSize)?.OceanMap;
        return map is { Data.Length: > 0 }
            ? map.GetUnpaddedIntLerpedForBlockPos((int)x, (int)z, regionSize) / 255.0
            : 1;
    }

    private SeaWeatherModel CreateModel()
    {
        var world = api.World;
        var calendar = world.Calendar;
        return new SeaWeatherModel(
            world.Seed,
            Config,
            new WorldClimate(world.Seed, world.Config, world.BlockAccessor.MapSizeZ),
            new OceanBasins(world.Seed, world.Config, world.BlockAccessor.MapSizeX, world.BlockAccessor.MapSizeZ, Config.Currents),
            z => calendar.OnGetLatitude?.Invoke(z) ?? 0.5,
            calendar.DaysPerYear * calendar.HoursPerDay);
    }
}
