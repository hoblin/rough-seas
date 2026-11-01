using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// The lashings that hold an outrigger's float under her booms. Her float stands out on its side, so a knock from that
/// side meets it before her hull, and works the lashings loose as a raft's: mended with twine. Once they give out, the
/// float comes away with her booms, and the mast they stay falls with it: she is left the hull she was rigged on.
/// </summary>
public class EntityBehaviorFloatLashing(Entity entity) : EntityBehavior(entity), IOutboard
{
    public const string Code = "roughseas:floatlashing";

    private const string TreeKey = "float";
    private const string FormsGroup = "FloatLashing";
    private const double ScatterSpeed = 0.3;
    private const double ScatterRise = 0.2;
    private const double FloatHeight = 0.5;

    private FloatLashingConfig config = null!;
    private LashedHull lashing = null!;
    private HullRepair[] repairs = [];
    private Dictionary<string, string[]> replaced = [];
    private string? shown;

    public HullPlace Side => config.Side;

    public float Condition => lashing.Condition;

    public event Action<Knock>? Struck;

    private EntityBehaviorRigging? Rigging => entity.GetBehavior<EntityBehaviorRigging>();

    private ShipyardModSystem Shipyard => entity.Api.ModLoader.GetModSystem<ShipyardModSystem>();

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        config = attributes.AsObject<FloatLashingConfig>(new FloatLashingConfig());
        repairs = config.Repairs.Where(repair => repair.Material.Resolve(entity.World, "float lashing repair")).ToArray();
        lashing = new LashedHull(() => entity.WatchedAttributes.GetOrAddTreeAttribute(EntityBehaviorVesselIntegrity.TreeKey).GetOrAddTreeAttribute(TreeKey),
            () => entity.WatchedAttributes.MarkPathDirty(EntityBehaviorVesselIntegrity.TreeKey), "roughseas:vessel-float-lashing");
        if (entity.Api.Side != EnumAppSide.Client) return;

        replaced = Replaced(properties);
        entity.WatchedAttributes.RegisterModifiedListener(EntityBehaviorVesselIntegrity.TreeKey, OnLashingChanged);
    }

    public override void AfterInitialized(bool onFirstSpawn) => shown = Form();

    public void Strike(double joules)
    {
        double holds = config.ImpactCapacity * (Rigging?.LashedShare ?? 1);
        double loss = Loss(joules, holds);
        if (loss <= 0) return;

        int stage = lashing.Stage(Side);
        var knock = lashing.Take(Side, loss);
        Shipyard.Log(entity, "a knock of {0:F0} J meets her float, its lashings holding {1:F0} J: {2}, lashings now {3:F2}",
            joules, holds, knock.ToString().ToLowerInvariant(), lashing.Condition);
        if (knock == Knock.BrokenUp || lashing.Stage(Side) > stage) Struck?.Invoke(knock);
        if (knock == Knock.BrokenUp) ComeApart();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || repairs.FirstOrDefault(repair => itemslot.Itemstack?.Collectible.Code == repair.Material.Code) is not { } repair) return;
        if (!lashing.Needs(repair, Side) || itemslot.StackSize < repair.Material.StackSize) return;

        handled = EnumHandling.PreventSubsequent;
        if (entity.World.Side != EnumAppSide.Server) return;

        itemslot.TakeOut(repair.Material.StackSize);
        itemslot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(repair.Saturation);
        Shipyard.Log(entity, "float {0} by {1}", lashing.Mend(repair, Side), byEntity.GetName());
        entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), entity, null, true, 16);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        var mending = repairs.Where(repair => lashing.Needs(repair, Side)).ToArray();
        return mending.Length == 0 ? null :
        [
            new WorldInteraction
            {
                ActionLangCode = "roughseas:vessel-mend-float",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = mending.Select(repair => repair.Material.ResolvedItemstack).ToArray()
            }
        ];
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        if (lashing.Condition < 1) lashing.Describe(infotext);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        shown = Form();
        var hidden = replaced.Keys.Where(form => form != shown).Select(form => FormsGroup + form);
        if (shown != null) hidden = hidden.Concat(replaced.GetValueOrDefault(shown) ?? []);
        willDeleteElements = (willDeleteElements ?? []).Append(hidden.ToArray());
    }

    public static double Loss(double joules, double holds) => joules <= 0 ? 0 : holds > 0 ? joules / holds : 1;

    public static string? FormOf(int stage) => stage switch
    {
        0 => null,
        1 or 2 => "Loose",
        3 => "Torn",
        _ => "Shifted"
    };

    private string? Form() => Rigging?.LashedShare < 1 ? null : FormOf(lashing.Stage(Side));

    private void OnLashingChanged()
    {
        if (Form() == shown) return;

        shown = Form();
        entity.MarkShapeModified();
    }

    private void ComeApart()
    {
        var world = entity.World;
        if (world.GetEntityType(new AssetLocation($"{config.Hull}-{Wood.Of(entity.Properties.Variant)?.Code}")) is not { } type)
        {
            Shipyard.Log(entity, "her lashings gave out, but she knows no {0} to be left", config.Hull);
            return;
        }

        var hull = world.ClassRegistry.CreateEntity(type);
        hull.Pos.SetFrom(entity.Pos);
        foreach (var state in entity.SidedProperties.Behaviors.OfType<IHandedOver>()) state.HandOver(hull, false);
        hull.WatchedAttributes.GetTreeAttribute(EntityBehaviorVesselIntegrity.TreeKey)?.RemoveAttribute(TreeKey);

        var off = (Side.IsPort() ? -1 : 1) * (Rigging?.FloatOffMetres ?? 0);
        var (x, z) = HullShape.ToWorld(0, off, entity.Pos.Yaw);
        var floatAt = entity.Pos.XYZ.Add(x, FloatHeight, z);
        var stripped = Rigging?.Stripped(world, config.Keeps, config.FirewoodLitres).ToArray() ?? [];
        foreach (var stack in stripped)
        {
            var scatter = new Vec3d(world.Rand.NextDouble() - 0.5, ScatterRise, world.Rand.NextDouble() - 0.5).Mul(ScatterSpeed);
            world.SpawnItemEntity(stack, floatAt, scatter);
        }
        var seated = (entity.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? []).Select(seat => seat.Passenger as EntityAgent).ToArray();
        foreach (var rider in seated) rider?.TryUnmount();
        entity.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory.DropAll(entity.Pos.XYZ);

        world.SpawnEntity(hull);
        var seats = hull.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        foreach (var (rider, seat) in seated.Zip(seats)) rider?.TryMount(seat);
        entity.Die(EnumDespawnReason.Removed);
        Shipyard.Log(hull, "her float's lashings gave out: float, booms and rig came away from {0} {1} as {2}",
            entity.Code, entity.EntityId, string.Join(", ", stripped.Select(stack => $"{stack.StackSize} {stack.Collectible.Code}")));
    }

    private Dictionary<string, string[]> Replaced(EntityProperties properties)
    {
        var shape = properties.Client?.Shape?.Base is { } path ? FramingConfig.ShapeAt(entity.Api, path) : null;
        return shape?.SelectTokens($"$.elements[?(@.name == '{FormsGroup}')].children[*]")
            .ToDictionary(form => form.Value<string>("name")![FormsGroup.Length..], form => form["replaces"]?.ToObject<string[]>() ?? []) ?? [];
    }
}

public class FloatLashingConfig
{
    /// <summary>Where on her hull her float stands out: a knock toward it meets the float.</summary>
    public HullPlace Side = HullPlace.StarboardSide;

    /// <summary>The knock her lashings take, all laid, before they give out, in joules.</summary>
    public double ImpactCapacity;

    /// <summary>The vessel she was rigged on, which is left once her float comes away.</summary>
    public string Hull = "";

    /// <summary>By stage, the item each piece of it comes away as whole.</summary>
    public Dictionary<string, string> Keeps = [];

    /// <summary>The wood a piece of firewood holds, which her float breaks up into.</summary>
    public double FirewoodLitres;

    public HullRepair[] Repairs = [];
}
