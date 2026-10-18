using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherSystem : ModSystem, ISeaWeather
{
    public const string Channel = "roughseas:seamap";

    private const string CalibrationKey = "roughseas:oceancalibration";
    private static readonly (int X, int Z)[] CalibrationColumns = [(8, 8), (24, 8), (8, 24), (24, 24)];

    private ICoreAPI api = null!;
    private Lazy<SeaWeatherConfig> config = null!;
    private Lazy<SeaModel>? model;
    private SeaMapClient? map;
    private OceanCalibration? calibration;
    private WorldOceanMap? ocean;

    public TickBudget Budget { get; } = new("sea and weather");

    /// <summary>After vanilla's weather systems (0.1), before the Devastation's effects (2).</summary>
    public override double ExecuteOrder() => 1;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        config = new Lazy<SeaWeatherConfig>(() => api.Assets.Get<SeaWeatherConfig>(new AssetLocation("roughseas", "config/seaweather.json")));
        api.Network.RegisterChannel(Channel).RegisterMessageType<SeaMapSnapshot>();
        new VanillaWind(this, api).Follow();
        VanillaWeatherPatches.Attach(this, api.Side);
        api.RegisterEntityBehaviorClass(CurrentDrift.Code, typeof(CurrentDrift));
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        calibration = new OceanCalibration(Config.Ocean);
        model = new Lazy<SeaModel>(() => CreateModel(sapi));
        sapi.Event.SaveGameLoaded += () => calibration.Load(sapi.WorldManager.SaveGame.GetData(CalibrationKey));
        sapi.Event.GameWorldSave += () => sapi.WorldManager.SaveGame.StoreData(CalibrationKey, calibration.ToBytes());
        sapi.Event.ChunkColumnLoaded += (coord, _) => Calibrate(sapi, coord);
        new VanillaRain(this, sapi).Follow();
        new VanillaRegions(this, sapi).Drive();
        new SeaMapServer(this, sapi).Start();
        Budget.CountTicks(sapi);
        SeaWeatherCommands.Register(this, sapi);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        map = new SeaMapClient(this, capi);
        map.Start();
        new VanillaRain(this, capi).Follow();
        SeaWeatherCommands.Register(this, capi);
    }

    public override void Dispose()
    {
        VanillaWeatherPatches.Detach(api.Side);
        if (api.Side == EnumAppSide.Server) OceanGenLock.Detach();
    }

    public bool DangerousOceans => api.World.Config.GetBool("dangerousOceans", true);

    public SeaWeatherConfig Config => config.Value;

    public double NowHours => api.World.Calendar.TotalHours;

    /// <summary>The model, on the server; a client reads the map the server sends instead.</summary>
    public SeaModel Model => model?.Value ?? throw new InvalidOperationException("The sea model runs on the server alone");

    public ISeaSource Source => map ?? (ISeaSource)Model;

    public SeaState At(double x, double z) => Source.At(x, z);

    public SeaCurrent CurrentAt(double x, double z) => Source.CurrentAt(x, z);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[seas] " + string.Format(CultureInfo.InvariantCulture, format, args));

    private SeaModel CreateModel(ICoreServerAPI sapi)
    {
        var world = sapi.World;
        var calendar = world.Calendar;
        var basins = new OceanBasins(Ocean(sapi), calibration!, Config.Currents);
        var weather = new SeaWeatherModel(
            world.Seed,
            Config,
            new WorldClimate(world.Seed, world.Config, world.BlockAccessor.MapSizeZ),
            basins,
            z => calendar.OnGetLatitude?.Invoke(z) ?? 0.5,
            calendar.DaysPerYear * calendar.HoursPerDay);
        return new SeaModel(this, sapi, weather, new WaterBody(sapi, basins, Config.Waves, () => NowHours));
    }

    private WorldOceanMap Ocean(ICoreServerAPI sapi) => ocean ??= new WorldOceanMap(sapi, new GeneratedOcean(() => Generator(sapi)));

    private MapLayerBase? Generator(ICoreServerAPI sapi)
    {
        var generator = sapi.ModLoader.GetModSystem<GenMaps>()?.oceanGen;
        if (generator == null) return null;
        OceanGenLock.Attach(generator);
        Log("ocean map: built regions' own, and the world's {0} where not built", generator.GetType().Name);
        return generator;
    }

    private void Calibrate(ICoreServerAPI sapi, Vec2i chunk)
    {
        if (calibration is not { Settled: false }) return;
        var accessor = sapi.World.BlockAccessor;
        var seaLevel = sapi.World.SeaLevel - 1;
        var pos = new BlockPos(Dimensions.NormalWorld);
        foreach (var (dx, dz) in CalibrationColumns)
        {
            var x = chunk.X * GlobalConstants.ChunkSize + dx;
            var z = chunk.Y * GlobalConstants.ChunkSize + dz;
            var water = accessor.GetBlock(pos.Set(x, seaLevel, z), BlockLayersAccess.Fluid).LiquidCode == "saltwater";
            if (!calibration.Add(Ocean(sapi).At(x / WorldOceanMap.PixelSize, z / WorldOceanMap.PixelSize), water)) continue;
            Log("ocean map calibrated against the built world: open sea from {0} of 255", calibration.SeaFrom);
            if (model is { IsValueCreated: true }) model.Value.Weather.Storms.Forget();
            return;
        }
    }
}
