using System;
using System.Text;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public class EntityLathBundle : Entity
{
    public const string ClassName = "roughseas:EntityLathBundle";

    private const string TreeKey = "roughseas:soak";
    private const string HoursKey = "hours";
    private const string SoakedKey = "soaked";
    private const string CheckedAtKey = "checkedAt";
    private const float TickSeconds = 1;
    private const string StoneTexture = "stone";
    private const string StoneMaterial = "material";

    private double soakHours;
    private ISeaWeather sea = null!;
    private float sinceTick;

    public bool Soaked => Tree.GetBool(SoakedKey);

    public double HoursSoaked => Tree.GetDouble(HoursKey);

    private ITreeAttribute Tree => WatchedAttributes.GetOrAddTreeAttribute(TreeKey);

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        soakHours = properties.Attributes["soakHours"].AsDouble();
        sea = api.ModLoader.GetModSystem<SeaWeatherSystem>();
    }

    public void Lay(bool soaked, ItemStack? weight)
    {
        Tree.SetBool(SoakedKey, soaked);
        if (weight != null) WatchedAttributes.SetItemstack(ItemLathBundle.WeightKey, weight);
    }

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);
        if (Api.Side != EnumAppSide.Server || (sinceTick += dt) < TickSeconds) return;

        sinceTick = 0;
        double now = sea.Clock.Hours;
        double hours = Math.Max(0, now - Tree.GetDouble(CheckedAtKey, now));
        Tree.SetDouble(CheckedAtKey, now);
        if (Soaked || !Waterside.IsWater(World.BlockAccessor, Pos.AsBlockPos)) return;

        double soaked = HoursSoaked + hours;
        Tree.SetDouble(HoursKey, soaked);
        if (soaked >= soakHours)
        {
            Tree.SetBool(SoakedKey, true);
            Shipyard.Log(this, "soaked through after {0:F1} h in the water: its laths are wet", soaked);
        }
        if (Math.Floor(soaked) > Math.Floor(soaked - hours) || Soaked) WatchedAttributes.MarkPathDirty(TreeKey);
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
    {
        base.OnInteract(byEntity, itemslot, hitPosition, mode);
        if (mode != EnumInteractMode.Interact || Api.Side != EnumAppSide.Server || !Alive) return;
        if (World.GetItem(new AssetLocation(Code.Domain, $"{Code.Path}-{(Soaked ? "soaked" : "dry")}")) is not { } item) return;

        var bundle = new ItemStack(item);
        if (Weight is { } weight) bundle.Attributes.SetItemstack(ItemLathBundle.WeightKey, weight);
        if (!byEntity.TryGiveItemStack(bundle)) World.SpawnItemEntity(bundle, Pos.XYZ);
        Shipyard.Log(this, "lifted by {0} after {1:F1} h in the water{2}", byEntity.GetName(), HoursSoaked, Soaked ? ", soaked" : "");
        Die(EnumDespawnReason.PickedUp);
    }

    public override ITexPositionSource GetTextureSource()
    {
        var source = base.GetTextureSource();
        if (Api is not ICoreClientAPI capi || Weight?.Item?.Textures?.TryGetValue(StoneMaterial, out var stone) != true) return source;

        capi.EntityTextureAtlas.GetOrInsertTexture(stone!, out _, out var position);
        return new StoneTextures(source, position);
    }

    public override string GetName() =>
        Wood.Of(Properties.Variant) is { } wood ? Lang.GetMatching($"{Code.Domain}:item-creature-{Code.Path}", wood.Name, Properties.Variant["length"]) : base.GetName();

    public override string GetInfoText()
    {
        var info = new StringBuilder(base.GetInfoText());
        info.AppendLine(Soaked ? Lang.Get("roughseas:lathbundle-soaked") : Lang.Get("roughseas:lathbundle-soaking", (int)HoursSoaked, (int)soakHours));
        return info.ToString();
    }

    private ItemStack? Weight => WatchedAttributes.GetItemstack(ItemLathBundle.WeightKey) is { } weight && weight.ResolveBlockOrItem(World) ? weight : null;

    private ShipyardModSystem Shipyard => Api.ModLoader.GetModSystem<ShipyardModSystem>();

    private sealed class StoneTextures(ITexPositionSource wood, TextureAtlasPosition stone) : ITexPositionSource
    {
        public TextureAtlasPosition? this[string textureCode] => textureCode == StoneTexture ? stone : wood[textureCode];

        public Size2i? AtlasSize => wood.AtlasSize;
    }
}
