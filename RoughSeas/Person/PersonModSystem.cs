using System.Globalization;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace RoughSeas.Person;

/// <summary>
/// The body in the water and what it carries, behind <see cref="IPerson"/>. It reads the sea through <see cref="IDrawnSea"/>,
/// after <see cref="StormsModSystem"/> has built it.
/// </summary>
public class PersonModSystem : ModSystem
{
    private const string Channel = "roughseas:person";
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/person.json");

    private ICoreAPI api = null!;
    private PersonConfig? config;
    private IPerson? person;

    public override double ExecuteOrder() => 0.2;

    public PersonConfig Config => config ??= api.Assets.Get<PersonConfig>(ConfigAsset);

    public IPerson Person => person ??= new PlayerPerson(Body.Of(Config.Body), Config.Carried, text => Log(text.Replace("{", "{{").Replace("}", "}}")));

    public override void StartPre(ICoreAPI api) => this.api = api;

    public override void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(Channel).RegisterMessageType<BreakerPacket>();
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
        BodyUnderSurface.AttachClient(this, storms.DrawnWater);
        BodyAfloat.Attach(this, capi, storms.DrawnSea!, storms.DrawnWater!, Person, Config, storms.Waves);
        capi.Network.GetChannel(Channel).SetMessageHandler<BreakerPacket>(BodyAfloat.Struck);
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        var storms = sapi.ModLoader.GetModSystem<StormsModSystem>();
        BodyUnderSurface.AttachServer(this, storms.Swells);
        new SwimmerBreakers(this, storms, sapi.ModLoader.GetModSystem<SeaWeatherSystem>(), sapi, sapi.Network.GetChannel(Channel)).Start();
    }

    public override void Dispose()
    {
        BodyUnderSurface.Detach();
        if (api?.Side == EnumAppSide.Client) BodyAfloat.Detach();
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Log($"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[person] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
