using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class EntityBehaviorVesselIntegrity : EntityBehavior
{
    public const string Code = "roughseas:vesselintegrity";

    private const string TreeKey = "roughseas:vessel";
    private const int DataVersion = 1;
    private const double WreckageScatterSpeed = 0.3;
    private const int InvulnerableMs = 500;
    private const float BareHandDamage = 0.5f;
    private const float HazardSeconds = 1;
    private const double WearStepHours = 1;
    private const string WornAtKey = "wornAt";
    private const string AfloatKey = "afloatHours";
    private const string WornKey = "worn";
    private const string CarriedWornKey = "roughseas:worn";
    private static readonly AssetLocation BreakSound = new("game:sounds/block/planks");
    private static readonly AssetLocation MendSound = new("game:sounds/player/build");
    private static readonly string[] Conditions = ["sound", "scratched", "scarred", "damaged", "breaking"];

    private readonly object impactLock = new();
    private readonly List<double> pendingImpacts = new();
    private ImpactSensor sensor = null!;
    private double impactCapacity;
    private DamageConversion damageConversion = null!;
    private VesselsModSystem vessels = null!;
    private ISeaWeather seaWeather = null!;
    private HullRepair[] repairs = [];
    private BlockDropItemStack[] wreckage = [];
    private HullDamage? damage;
    private int shownStage;
    private double wearPerDay;
    private bool runsRapids;
    private Rapids rapids = null!;
    private float sinceHazards;

    public EntityBehaviorVesselIntegrity(Entity entity) : base(entity) { }

    /// <summary>What is left of the hull, from 1 (sound) to 0 (broken). Synced to clients.</summary>
    public float Integrity
    {
        get => Tree?.GetFloat("integrity", 1) ?? 1;
        private set
        {
            Tree.SetFloat("integrity", value);
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    private float Worn
    {
        get => Tree?.GetFloat(WornKey) ?? 0;
        set
        {
            Tree.SetFloat(WornKey, Math.Clamp(value, 0, 1 - Integrity));
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        repairs = attributes["repairs"].AsObject<HullRepair[]>([])
            .Where(repair => repair.Material.Resolve(entity.World, "vessel repair"))
            .ToArray();
        wearPerDay = attributes["wearPerDay"].AsDouble(0);

        if (entity.Api is ICoreClientAPI capi)
        {
            InitializeClient(capi, attributes);
            return;
        }

        if (Tree == null)
        {
            var tree = new TreeAttribute();
            tree.SetInt("version", DataVersion);
            tree.SetFloat("integrity", 1);
            entity.WatchedAttributes.SetAttribute(TreeKey, tree);
        }

        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        impactCapacity = attributes["impactCapacity"].AsDouble() * vessels.Materials.StrengthFactor(attributes["parts"].AsObject<VesselPart[]>([]));
        damageConversion = vessels.DamageConversion;
        seaWeather = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        sensor = new ImpactSensor(attributes["minImpactSpeed"].AsDouble(1));
        runsRapids = attributes["runsRapids"].AsBool(false);
        rapids = vessels.Rapids;
        wreckage = attributes["wreckage"].AsObject<BlockDropItemStack[]>([])
            .Where(drop => drop.Resolve(entity.World, "vessel wreckage", entity.Code))
            .ToArray();
        entity.PhysicsUpdateWatcher += OnPhysicsUpdate;
    }

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        if (damage?.Visible != true) return null;

        handling = EnumHandling.PreventSubsequent;
        var capi = (ICoreClientAPI)entity.Api;
        return damage.Over(capi.Tesselator.GetTextureSource(entity, null, entity.WatchedAttributes.GetInt("textureIndex")));
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        entity.PhysicsUpdateWatcher -= OnPhysicsUpdate;
        base.OnEntityDespawn(despawn);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        using var _ = vessels.Budget.Measure();

        double[] impacts;
        lock (impactLock)
        {
            impacts = pendingImpacts.ToArray();
            pendingImpacts.Clear();
        }

        if (impacts.Length > 0)
        {
            double mass = Mass();
            foreach (double speed in impacts)
            {
                vessels.Log(entity, "impact at {0:F1} m/s", speed);
                Strike(0.5 * mass * speed * speed);
            }
        }

        if ((sinceHazards += deltaTime) < HazardSeconds || !entity.Alive) return;

        var fluid = entity.World.BlockAccessor.GetBlock(entity.Pos.AsBlockPos, BlockLayersAccess.Fluid);
        Wear(fluid.IsLiquid());
        if (!runsRapids && rapids.Holds(fluid))
        {
            vessels.Log(entity, "in rapids");
            Strike(rapids.JoulesPerSecond * sinceHazards);
        }
        sinceHazards = 0;
    }

    /// <summary>
    /// Strikes the hull with an impact of this energy. Server only. Whether it harms the hull, and how much, is
    /// rolled against the vessel's impact capacity; at zero integrity the vessel breaks into wreckage.
    /// </summary>
    public void Strike(double joules)
    {
        if (!entity.Alive) return;

        double severity = joules / impactCapacity;
        var rand = entity.World.Rand;
        var harmed = rand.NextDouble() < 1 - Math.Exp(-severity);
        if (harmed) Integrity = Math.Max(0, Integrity - (float)(severity * (0.5 + rand.NextDouble())));
        vessels.Log(entity, "struck with {0:F0} J against a capacity of {1:F0}: {2}, hull now {3:F2}",
            joules, impactCapacity, harmed ? "harmed" : "unharmed", Integrity);
        if (!harmed) return;

        if (Integrity > 0) Creak();
        else Break();
    }

    public void TakeConditionFrom(ItemStack stack)
    {
        var item = stack.Collectible;
        Integrity = CarriedCondition.Integrity(item.GetRemainingDurability(stack), item.GetMaxDurability(stack));
        Worn = stack.Attributes.GetFloat(CarriedWornKey);
    }

    /// <summary>Vanilla's shift pickup, server only, with the hull's condition carried on the item as its durability.</summary>
    public bool TryPickUp(EntityAgent byEntity)
    {
        if (!byEntity.Controls.ShiftKey || entity.Properties.Attributes?["rightClickPickup"].AsBool() != true || Occupied()) return false;

        var stack = new ItemStack(entity.World.GetItem(entity.Code));
        if (Integrity < 1)
        {
            stack.Collectible.SetDurability(stack, CarriedCondition.Durability(Integrity, stack.Collectible.GetMaxDurability(stack)));
        }
        if (Worn > 0) stack.Attributes.SetFloat(CarriedWornKey, Worn);
        if (!byEntity.TryGiveItemStack(stack)) entity.World.SpawnItemEntity(stack, entity.Pos.XYZ);

        entity.World.Logger.Audit("{0} Picked up 1x{1} at {2}.", byEntity.GetName(), stack.Collectible.Code, entity.Pos);
        entity.Die();
        return true;
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (entity.World.Side != EnumAppSide.Server || damageSource.Type == EnumDamageType.Heal) return;
        if (damageSource.Source == EnumDamageSource.Weather && !seaWeather.DangerousOceans) return;

        entity.SetActivityRunning("invulnerable", InvulnerableMs);
        vessels.Log(entity, "takes {0:F1} {1} damage from {2}", damage, damageSource.Type, damageSource.Source);
        Strike(damageConversion.Joules(damageSource, damage));
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        bool server = entity.World.Side == EnumAppSide.Server;
        if (mode == EnumInteractMode.Attack)
        {
            if (server) Swing(byEntity, itemslot, hitPosition);
            return;
        }
        if (RepairWith(itemslot) is not { } repair) return;

        handled = EnumHandling.PreventSubsequent;
        if (Refusal(repair, itemslot) is { } refusal)
        {
            if (entity.Api is ICoreClientAPI capi) capi.TriggerIngameError(this, "roughseas-vessel-mend", refusal);
            return;
        }
        if (server) Mend(byEntity, itemslot, repair);
    }

    /// <summary>Whether the held item is one this hull is mended or greased with, whether or not it would help now.</summary>
    public bool MendsWith(ItemSlot slot) => RepairWith(slot) != null;

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        var mending = repairs.Where(Helps).ToArray();
        if (mending.Length == 0) return null;

        return
        [
            new WorldInteraction
            {
                ActionLangCode = "roughseas:vessel-mend",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = mending.Select(repair => repair.Material.ResolvedItemstack).ToArray()
            }
        ];
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        infotext.AppendLine(Lang.Get($"roughseas:vessel-condition-{Conditions[Stage()]}", (int)Math.Round(Integrity * 100)));
        if (Greasing is { } grease)
        {
            double days = (grease.Restores - Worn) / wearPerDay;
            infotext.AppendLine(days > 0 ? Lang.Get("roughseas:vessel-grease-in", (int)Math.Ceiling(days)) : Lang.Get("roughseas:vessel-grease-due"));
        }
        base.GetInfoText(infotext);
    }

    private HullRepair? Greasing => wearPerDay > 0 ? repairs.FirstOrDefault(repair => repair.Greases) : null;

    private void InitializeClient(ICoreClientAPI capi, JsonObject attributes)
    {
        if (attributes["damage"].AsObject<HullDamageConfig?>(null) is not { } config) return;

        damage = new HullDamage(capi, entity, config);
        shownStage = Stage();
        damage.Show(shownStage);
        entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnIntegrityChanged);
    }

    private void OnIntegrityChanged()
    {
        int stage = Stage();
        if (stage == shownStage) return;

        shownStage = stage;
        damage!.Show(stage);
        entity.MarkShapeModified();
    }

    private void OnPhysicsUpdate(float accum, Vec3d prevPos)
    {
        var pos = entity.Pos;
        // Called from the physics threads for a drifting vessel and from the main thread for a steered one.
        lock (impactLock)
        {
            if (sensor.Sample(pos.X, pos.Z, entity.World.ElapsedMilliseconds) is double speed)
            {
                pendingImpacts.Add(speed);
            }
        }
    }

    private void Wear(bool afloat)
    {
        if (wearPerDay == 0) return;

        var calendar = entity.World.Calendar;
        double now = calendar.TotalHours;
        double last = Tree.GetDouble(WornAtKey, now);
        Tree.SetDouble(WornAtKey, now);
        if (!afloat) return;

        double hours = Tree.GetDouble(AfloatKey) + now - last;
        if (hours < WearStepHours)
        {
            Tree.SetDouble(AfloatKey, hours);
            return;
        }

        Tree.SetDouble(AfloatKey, 0);
        float before = Integrity;
        Integrity = Math.Max(0, Integrity - (float)(wearPerDay * hours / calendar.HoursPerDay));
        Worn += before - Integrity;
        vessels.Log(entity, "wore {0:F1} h afloat: {1:F2} -> {2:F2}{3}", hours, before, Integrity, Ungreased());
        if (Integrity == 0) Break();
    }

    private void Swing(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition)
    {
        if (entity.GetInterface<IMountable>()?.Controller == byEntity) return;

        var stack = slot.Itemstack;
        float damage = (stack?.Collectible.GetAttackPower(stack) ?? BareHandDamage) * byEntity.Stats.GetBlended("meleeWeaponsDamage");
        bool critical = false;
        damage = stack?.Collectible.GetDamageToEntity(damage, entity, stack, ref critical) ?? damage;

        if (!entity.IsActivityRunning("invulnerable"))
        {
            entity.World.PlaySoundAt(new AssetLocation($"game:sounds/block/chop{1 + entity.World.Rand.Next(3)}"), entity, null, true, 16);
            stack?.Collectible.OnAttackingWith(entity.World, byEntity, entity, slot);
        }

        var source = new DamageSource
        {
            Source = byEntity is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = byEntity,
            Type = EnumDamageType.BluntAttack,
            HitPosition = hitPosition,
            DamageTier = stack?.Collectible.GetToolTier(slot) ?? 0
        };
        entity.ReceiveDamage(source, damage);
    }

    private HullRepair? RepairWith(ItemSlot slot) =>
        repairs.FirstOrDefault(repair => slot.Itemstack?.Collectible.Code == repair.Material.Code);

    private string? Refusal(HullRepair repair, ItemSlot slot)
    {
        if (!Helps(repair)) return Lang.Get(repair.Greases ? "roughseas:vessel-nothing-to-grease" : "roughseas:vessel-nothing-to-mend");
        return slot.StackSize < repair.Material.StackSize
            ? Lang.Get("roughseas:vessel-mend-takes", repair.Material.StackSize, slot.GetStackName())
            : null;
    }

    private bool Helps(HullRepair repair) => repair.Greases ? Worn > 0 : Integrity < 1;

    private string Ungreased() => Greasing != null ? $", {Worn:F2} worn ungreased" : "";

    private void Mend(EntityAgent byEntity, ItemSlot slot, HullRepair repair)
    {
        slot.TakeOut(repair.Material.StackSize);
        slot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(repair.Saturation);
        float before = Integrity;
        float worn = Worn;
        float restores = repair.Greases ? Math.Min(repair.Restores, worn) : repair.Restores;
        Integrity = Math.Min(1, Integrity + restores);
        Worn = repair.Greases ? worn - restores : worn;
        vessels.Log(entity, "{0} by {1}: {2:F2} -> {3:F2}{4}",
            repair.Greases ? "greased" : "mended", byEntity.GetName(), before, Integrity, Ungreased());
        entity.World.PlaySoundAt(MendSound, entity, null, true, 16);
    }

    private bool Occupied() =>
        entity.GetBehavior<EntityBehaviorSeatable>()?.AnyMounted() == true ||
        entity.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory.Empty == false;

    private double Mass()
    {
        var seats = entity.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        return entity.Properties.Weight + seats.Sum(seat => seat.Passenger?.Properties.Weight ?? 0);
    }

    private void Creak()
    {
        var sound = new AssetLocation($"game:sounds/block/woodcreak_{1 + entity.World.Rand.Next(4)}");
        entity.World.PlaySoundAt(sound, entity, null, true, 24);
    }

    private void Break()
    {
        vessels.Log(entity, "broke up");
        var world = entity.World;
        var box = entity.SelectionBox;
        foreach (var drop in wreckage)
        {
            var stack = drop.GetNextItemStack();
            if (stack == null) continue;

            var piece = stack.GetEmptyClone();
            piece.StackSize = 1;
            for (int i = 0; i < stack.StackSize; i++)
            {
                var at = entity.Pos.XYZ.Add((world.Rand.NextDouble() - 0.5) * box.XSize, 0.5, (world.Rand.NextDouble() - 0.5) * box.ZSize);
                var scatter = new Vec3d(world.Rand.NextDouble() - 0.5, 0.2, world.Rand.NextDouble() - 0.5).Mul(WreckageScatterSpeed);
                world.SpawnItemEntity(piece.Clone(), at, scatter);
            }
            world.SpawnCubeParticles(entity.Pos.XYZ.Add(0, 0.5, 0), stack, 1.5f, 24, 0.8f);
        }

        world.PlaySoundAt(BreakSound, entity, null, true, 32);
        entity.Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Block, Type = EnumDamageType.BluntAttack });
    }

    private int Stage() => Integrity switch
    {
        > 0.9f => 0,
        > 0.75f => 1,
        > 0.5f => 2,
        > 0.25f => 3,
        _ => 4
    };
}
