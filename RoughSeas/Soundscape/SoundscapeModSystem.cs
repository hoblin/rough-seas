using System.Globalization;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace RoughSeas.Soundscape;

public class SoundscapeModSystem : ModSystem
{
    private const double LogEverySeconds = 10;
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/soundscape.json");

    private ICoreAPI api = null!;
    private SeaBed? bed;
    private VesselSounds? vessels;
    private double sinceLog;

    public SoundscapeConfig Config { get; private set; } = null!;

    public override void Start(ICoreAPI api) => this.api = api;

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<SoundscapeConfig>(ConfigAsset);

    public override void StartServerSide(ICoreServerAPI sapi) => new HullSounds(this, sapi).Start();

    public override void StartClientSide(ICoreClientAPI capi)
    {
        var sea = capi.ModLoader.GetModSystem<SeaWeatherSystem>();
        var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
        ShoreSound.Attach(Config.Shore, sea, storms.Waves);
        VanillaRowing.Attach(Config.VanillaRowingVolume, this);
        var crests = new Crests(capi, Config.Crests, this);
        var splashes = new Splashes(capi, Config.Water.Splash, Config.Water.Range, storms, this);
        var swimmer = new Swimmer(capi, splashes);
        vessels = new VesselSounds(capi, Config.Water, splashes, this);
        bed = new SeaBed(capi, Config.Beds);
        capi.ChatCommands.Create("sounds")
            .WithDescription("Rough Seas: what the sea sounds like here")
            .BeginSubCommand("shore")
                .WithDescription("How the water's edges around the player lap or break, by the shore's ambient sound")
                .WithArgs(capi.ChatCommands.Parsers.OptionalInt("radius", 24))
                .HandleWith(args => TextCommandResult.Success(ShoreSound.Survey(capi.World, capi.World.Player.Entity.Pos.AsBlockPos, (int)args[0])))
            .EndSubCommand()
            .BeginSubCommand("vessels")
                .WithDescription("What each vessel within hearing plays: bilge slosh, leak trickle, the waves splashing at her")
                .HandleWith(_ => TextCommandResult.Success(vessels.Describe()))
            .EndSubCommand();
        capi.Event.LevelFinalize += () =>
        {
            bed.Load();
            capi.Event.RegisterGameTickListener(dt =>
            {
                if (Listener.Of(capi, sea, storms, Config) is not { } listener) return;
                bed.Update(dt, listener);
                crests.Update(dt, listener);
                vessels.Update(listener);
                swimmer.Update();
                if ((sinceLog += dt) < LogEverySeconds) return;
                sinceLog = 0;
                Log("bed {0} in a {1:F1} m/s wind, waves {2:F1} m: {3}; whitecaps {4:F2}, spray {5:F2}", listener.AtSea ? "at sea" : "ashore",
                    listener.Sea.WindSpeed, listener.WaveHeight, bed.Describe(), listener.Foam.Caps, listener.Foam.Spray);
            }, (int)(Config.UpdateSeconds * 1000));
        };
    }

    public override void Dispose()
    {
        if (api?.Side != EnumAppSide.Client) return;
        ShoreSound.Detach();
        VanillaRowing.Detach();
        bed?.Dispose();
        vessels?.Dispose();
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Log($"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[sounds] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
