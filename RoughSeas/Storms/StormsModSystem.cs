using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;
using RoughSeas.Shore;
using RoughSeas.Water;

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
        surface.Trains.Changed += change => Log("surface: " + change);
        DrawnSea = new DrawnSea(surface, field);
        DrawnWater = new DrawnWater(DrawnSea);
        var shoreConfig = capi.ModLoader.GetModSystem<ShoreModSystem>().Config;
        if (SurfaceShader.Attach(surface, field, shoreConfig.Sheet.LensDepthRatio, Config.Surface.Shore.WetReflectance, Config.Surface.Shore.WaterAttenuation, shoreConfig.Faces.FoamDecaySeconds, shoreConfig.RunUp.WallSlope, Config.Surface.Tessellation, message => Log(message), capi))
        {
            clock = new SurfaceClock(this, sea, surface, field, Surf(capi), capi);
            EyeUnderSurface.Attach(this, capi, DrawnWater, Config.Eye);
            WatchStrikes(capi, sea, surface, field);
        }
        else Mod.Logger.Error("The sea surface is not drawn: its shaders failed to compile or vanilla's water shader changed; see the client log");
        VesselMotion.Attach(this, capi, DrawnSea, surface);
        FloatMotion.Attach(DrawnSea);
        RegisterSurfaceCommands(capi, field);
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
        var faces = new FaceBuilder(new WallReflection(shore.Faces, shore.RunUp.SpectralPeriodRatio), shore.Faces);
        var transformation = new WaveTransformation(Waves.BreakingDepthRatio, faces.SmoothingBlocks, Config.Surface.Shore.BreakingDissipation, Config.Surface.Shore.BoreCelerityShare);
        return new SurfMaker(new SurfBuilder(new SwashSheets(band), faces, transformation), new Beds(shore.Beds), Config.Surface.Shore, shore.Band.HighestRiseBlocks, (format, args) => Log(format, args));
    }

    private static void RegisterSurfaceCommands(ICoreClientAPI capi, SurfaceField field)
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
            .BeginSubCommand("reflection")
                .WithDescription("Draw the waves faces send back, or leave them out to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("reflection", SurfaceShader.DrawReflection = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("lag")
                .WithDescription("Draw the crests as the bed turns them, or straight, to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("the bed's lag", SurfaceShader.DrawLag = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("energy")
                .WithDescription("Draw each train as high as its energy over the bed makes it, or as over deep water, to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("the waves' energy over the bed", SurfaceShader.DrawEnergy = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("tessellation")
                .WithDescription("Divide the water's blocks near the player, or draw one quad a block, to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args =>
                {
                    SurfaceTessellation.On = (bool)args[0];
                    capi.Shader.ReloadShaders();
                    return Drawn("tessellation", SurfaceTessellation.Drawn);
                })
            .EndSubCommand()
            .BeginSubCommand("colours")
                .WithDescription("Paint the water by its height, from purple at the zone's lowest to red at its highest, or over a fixed range in metres above still water, and outline each block's top face")
                .WithArgs(parsers.Bool("draw"), parsers.OptionalDouble("lowest", double.NaN), parsers.OptionalDouble("highest", double.NaN))
                .HandleWith(args =>
                {
                    var (lowest, highest) = ((double)args[1], (double)args[2]);
                    SurfaceShader.Colours.Range = double.IsNaN(lowest) || double.IsNaN(highest) ? null : (lowest, highest);
                    return Drawn("height colours", SurfaceShader.Colours.On = (bool)args[0]);
                })
            .EndSubCommand()
            .BeginSubCommand("map")
                .WithDescription("Paint the water as a map of height, the dominant train's phase (crests as white lines), the share of waves breaking, the foam, or which wave's sheet stands over the beach (purple the latest, red four periods old)")
                .WithArgs(parsers.WordRange("view", HeightColours.Views))
                .HandleWith(args =>
                {
                    SurfaceShader.Colours.View = Array.IndexOf(HeightColours.Views, (string)args[0]);
                    return Drawn("map of " + args[0], SurfaceShader.Colours.On = true);
                })
            .EndSubCommand()
            .BeginSubCommand("profile")
                .WithDescription("The drawn sea's height every block for N blocks along Z from the player, X blocks ahead along X")
                .WithArgs(parsers.OptionalInt("blocks", 40), parsers.OptionalInt("ahead", 0))
                .HandleWith(args =>
                {
                    var pos = capi.World.Player.Entity.Pos;
                    var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
                    var (blocks, ahead) = ((int)args[0], (int)args[1]);
                    return TextCommandResult.Success(string.Join(" ", Enumerable.Range(-blocks / 2, blocks + 1)
                        .Select(i => $"{Math.Floor(pos.Z) + i - (int)capi.World.DefaultSpawnPosition.Z:F0}:{storms.DrawnSea!.HeightAt(pos.X + ahead, Math.Floor(pos.Z) + i + 0.5):F2}")));
                })
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
            .BeginSubCommand("probe")
                .WithDescription("Why the drawn sea stands as it does at a point this many blocks from the player, along X and Z: its share of the waves, its height, and the seabed read there")
                .WithArgs(parsers.Int("x"), parsers.Int("z"))
                .HandleWith(args =>
                {
                    var pos = capi.World.Player.Entity.Pos;
                    var (x, z) = (Math.Floor(pos.X) + (int)args[0] + 0.5, Math.Floor(pos.Z) + (int)args[1] + 0.5);
                    var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
                    var seabed = storms.Surface?.Seabed;
                    return TextCommandResult.Success($"{(int)Math.Floor(x) - (int)capi.World.DefaultSpawnPosition.X} {(int)Math.Floor(z) - (int)capi.World.DefaultSpawnPosition.Z}: "
                        + $"share {field.Factor(x, z):F3}, height {storms.DrawnSea!.HeightAt(x, z):F2}; crests {storms.Surface?.CrestAt(x, z)}; field from {seabed?.OriginX - (int)capi.World.DefaultSpawnPosition.X} {seabed?.OriginZ - (int)capi.World.DefaultSpawnPosition.Z}; {seabed?.Describe(x, z) ?? "no seabed read yet"}");
                })
            .EndSubCommand()
            .BeginSubCommand("watch")
                .WithDescription("Log what the swash reads at a point this many blocks from the player, along X and Z, ten times a second for a number of seconds")
                .WithArgs(parsers.Int("x"), parsers.Int("z"), parsers.OptionalDouble("seconds", 30))
                .HandleWith(args =>
                {
                    var pos = capi.World.Player.Entity.Pos;
                    var (x, z) = (Math.Floor(pos.X) + (int)args[0] + 0.5, Math.Floor(pos.Z) + (int)args[1] + 0.5);
                    var (spawnX, spawnZ) = ((int)capi.World.DefaultSpawnPosition.X, (int)capi.World.DefaultSpawnPosition.Z);
                    var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
                    var until = capi.ElapsedMilliseconds + (long)((double)args[2] * 1000);
                    long listener = 0;
                    listener = capi.Event.RegisterGameTickListener(_ =>
                    {
                        if (capi.ElapsedMilliseconds > until || storms.Surface is not { } surface)
                        {
                            capi.Event.UnregisterGameTickListener(listener);
                            return;
                        }
                        var seabed = surface.Seabed;
                        var dominant = surface.Trains.Dominant;
                        var since = surface.SinceCrest(x, z);
                        var (height, twoPercent) = surface.SheetWaveAt(x, z, since);
                        storms.Log("watch {0} {1}: field {2}, dominant {3}, lag {4:F3}, since crest {5:F2} s, wave at its arrival {6:F2} m of 2% {7:F2} m, share {8:F3}; {9}",
                            (int)Math.Floor(x) - spawnX, (int)Math.Floor(z) - spawnZ, seabed is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(seabed), dominant,
                            seabed?.LagAt(dominant, x, z) ?? 0, since, height, twoPercent, field.Factor(x, z), seabed?.Describe(x, z) ?? "no seabed read yet");
                    }, 100);
                    return TextCommandResult.Success($"Watching the swash at {(int)Math.Floor(x) - spawnX} {(int)Math.Floor(z) - spawnZ} for {args[2]} s, tagged 'watch' in the client's debug log");
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
