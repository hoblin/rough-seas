using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;
using RoughSeas.Shore;
using RoughSeas.Surf;

namespace RoughSeas.Storms;

public class StormsModSystem : ModSystem
{
    private const string Channel = "roughseas:storms";
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/storms.json");

    private ICoreAPI api = null!;
    private Seabeds? seabeds;
    private SurfaceClock? clock;
    private IClientNetworkChannel? reports;

    public StormsConfig Config { get; private set; } = null!;

    public Waves Waves { get; private set; } = null!;

    public TickBudget Budget { get; } = new("storms");

    /// <summary>The sea surface this client draws; null on the server.</summary>
    public IDrawnSea? DrawnSea { get; private set; }

    /// <summary>The water a point is in by <see cref="DrawnSea"/>; null on the server.</summary>
    public DrawnWater? DrawnWater { get; private set; }

    /// <summary>The drawn sea around the vessels nobody steers; null on a client.</summary>
    public RegionSwells? Swells { get; private set; }

    /// <summary>The surface behind <see cref="DrawnSea"/>, foam included; null on the server.</summary>
    internal SeaSurface? Surface { get; private set; }

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        api.RegisterEntityBehaviorClass(EntityBehaviorSeaForces.Code, typeof(EntityBehaviorSeaForces));
        api.RegisterEntityBehaviorClass(EntityBehaviorBilge.Code, typeof(EntityBehaviorBilge));
        api.RegisterEntityBehaviorClass(EntityBehaviorMooring.Code, typeof(EntityBehaviorMooring));
        api.RegisterEntityBehaviorClass(EntityBehaviorCurrentDrift.Code, typeof(EntityBehaviorCurrentDrift));
        api.RegisterBlockBehaviorClass(BlockBehaviorBailer.Code, typeof(BlockBehaviorBailer));
        api.Network.RegisterChannel(Channel).RegisterMessageType<MooringReport>();
        VesselDraught.Attach();
        DriftPhysics.Attach();
        WaveCarry.Attach();
        OneRope.Attach();
    }

    public void Report(MooringReport report) => reports?.SendPacket(report);

    public override void AssetsLoaded(ICoreAPI api)
    {
        Config = api.Assets.Get<StormsConfig>(ConfigAsset);
        Waves = new Waves(Config.Waves);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        reports = capi.Network.GetChannel(Channel);
        var sea = capi.ModLoader.GetModSystem<SeaWeatherSystem>();
        var surface = Surface = new SeaSurface(Config.Surface, Waves);
        var field = new SurfaceField();
        surface.Changed += change => Log("surface: " + change);
        DrawnSea = new DrawnSea(surface, field);
        DrawnWater = new DrawnWater(DrawnSea);
        var shoreConfig = capi.ModLoader.GetModSystem<ShoreModSystem>().Config;
        if (SurfaceShader.Attach(surface, field, shoreConfig.Sheet.LensDepthRatio, Config.Surface.Shore.WetReflectance, shoreConfig.Faces.FoamDecaySeconds, shoreConfig.RunUp.WallSlope, capi))
        {
            clock = new SurfaceClock(this, sea, surface, field, Surf(capi), capi);
            EyeUnderSurface.Attach(this, capi, DrawnWater, Config.Eye);
            WatchStrikes(capi, sea, surface, field);
        }
        else Mod.Logger.Error("The sea surface is not drawn: its shaders failed to compile or vanilla's water shader changed; see the client log");
        VesselMotion.Attach(this, capi, DrawnSea, surface);
        FloatMotion.Attach(DrawnSea);
        RegisterSurfaceCommands(capi);
    }

    internal SwellAround Swell(ICoreServerAPI sapi)
    {
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>();
        seabeds ??= new Seabeds(() => Surf(sapi), Config.Surface.Shore, sapi.World);
        return new SwellAround(Config.Surface, Waves, sea.Config, sea.Clock, seabeds);
    }

    private void WatchStrikes(ICoreClientAPI capi, SeaWeatherSystem sea, SeaSurface surface, SurfaceField field)
    {
        var shore = capi.ModLoader.GetModSystem<ShoreModSystem>().Config;
        var particles = new SprayParticles(shore.Faces);
        var strikes = new FaceStrikes(surface, field, Waves, new WallSpray(shore.Faces, shore.RunUp.SpectralPeriodRatio), shore.Faces, particles.Add, (format, args) => Log(format, args));
        var watch = sea.Clock.StartWatch();
        capi.Event.RegisterAsyncParticleSpawner(particles.Spawn);
        capi.Event.RegisterGameTickListener(_ =>
        {
            var lived = watch.Lap().LivedSeconds;
            if (capi.World.Player?.Entity?.Pos is { } pos) strikes.Update(pos.X, pos.Z, lived);
        }, shore.Faces.TickMilliseconds);
    }

    private SurfMaker Surf(ICoreAPI api)
    {
        var shore = api.ModLoader.GetModSystem<ShoreModSystem>().Config;
        var band = new Band(new RunUp(shore.RunUp), shore.Sheet, shore.RunUp.SlopeSearchBlocks);
        var faces = new FaceBuilder(new WallReflection(shore.Faces, shore.RunUp.SpectralPeriodRatio), shore.Faces, Waves.BreakingDepthRatio);
        return new SurfMaker(new SurfBuilder(band, faces), new Beds(shore.Beds), Config.Surface.Shore, shore.Band.HighestRiseBlocks, (format, args) => Log(format, args));
    }

    private static void RegisterSurfaceCommands(ICoreClientAPI capi)
    {
        var parsers = capi.ChatCommands.Parsers;
        capi.ChatCommands.Create("surface")
            .WithDescription("Rough Seas: the sea's surface as this client draws it")
            .BeginSubCommand("waves")
                .WithDescription("Draw the waves, or leave them out to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("waves", SurfaceShader.DrawWaves = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("foam")
                .WithDescription("Draw the foam, or leave it out to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("foam", SurfaceShader.DrawFoam = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("shore")
                .WithDescription("Draw the waves over the seabed and the swash on the shore, or the sea as if it were deep everywhere, to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("the shore", SurfaceShader.DrawShore = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("bed")
                .WithDescription("The seabed and the swash read under the player, and N blocks along X")
                .WithArgs(parsers.OptionalInt("blocks", 0))
                .HandleWith(args =>
                {
                    var pos = capi.World.Player.Entity.Pos;
                    var field = capi.ModLoader.GetModSystem<StormsModSystem>().Surface?.Seabed;
                    if (field == null) return TextCommandResult.Error("No seabed read yet");
                    var blocks = (int)args[0];
                    return TextCommandResult.Success(string.Join("\n", Enumerable.Range(0, blocks + 1)
                        .Select(i => $"{(int)Math.Floor(pos.X) + i - (int)capi.World.DefaultSpawnPosition.X} {(int)Math.Floor(pos.Z) - (int)capi.World.DefaultSpawnPosition.Z}: "
                            + field.Describe(Math.Floor(pos.X) + i + 0.5, Math.Floor(pos.Z) + 0.5))));
                })
            .EndSubCommand()
            .BeginSubCommand("trace")
                .WithDescription("Log every vessel's pose on the waves every frame, for a number of seconds")
                .WithArgs(parsers.OptionalDouble("seconds", 3))
                .HandleWith(args =>
                {
                    VesselMotion.Trace((double)args[0]);
                    return TextCommandResult.Success($"Tracing vessels' poses for {args[0]} s, tagged 'surface trace' in the client's debug log");
                })
            .EndSubCommand()
            .BeginSubCommand("samplers")
                .WithDescription("List the texture units the engine numbers each shader program's samplers on")
                .HandleWith(_ => TextCommandResult.Success(SurfaceShader.Samplers()))
            .EndSubCommand()
            .BeginSubCommand("reload")
                .WithDescription("Read the surface's shader includes again from the mod's assets and recompile")
                .HandleWith(_ => SurfaceShader.Reload()
                    ? TextCommandResult.Success("Sea surface shaders reloaded")
                    : TextCommandResult.Error("Sea surface shaders failed to compile; see the client log"))
            .EndSubCommand();
    }

    public override void Dispose()
    {
        VesselDraught.Detach();
        DriftPhysics.Detach();
        WaveCarry.Detach();
        OneRope.Detach();
        if (api?.Side == EnumAppSide.Server)
        {
            BreathUnderSurface.Detach();
            RopeIds.Detach();
        }
        if (api?.Side != EnumAppSide.Client) return;
        VesselMotion.Detach();
        FloatMotion.Detach();
        EyeUnderSurface.Detach();
        SurfaceShader.Detach();
        clock?.Dispose();
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>();
        new StormLightning(this, sea, sapi).Start();
        RopeIds.Attach();
        sapi.Network.GetChannel(Channel).SetMessageHandler<MooringReport>((player, report) =>
            sapi.World.GetEntityById(report.EntityId)?.GetBehavior<EntityBehaviorMooring>()?.Reported(player, report));
        BreathUnderSurface.Attach(this, sea, sapi);
        Swells = new RegionSwells(this, sea, sapi);
        Budget.CountTicks(sapi);
        sapi.ChatCommands.Create("storms")
            .WithDescription("Rough Seas: storms acting on vessels")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("budget")
                .WithDescription("Server time the storms spent on vessels since the last report")
                .HandleWith(_ => TextCommandResult.Success(Budget.Report()))
            .EndSubCommand();
    }

    private static TextCommandResult Drawn(string part, bool on) => TextCommandResult.Success($"Sea {part} {(on ? "drawn" : "left out")}");

    public void Log(Entity entity, string format, params object[] args) =>
        Log($"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[storms] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
