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
    public const string Code = "vesselintegrity";

    private const string TreeKey = "roughseas:vessel";
    private const int DataVersion = 1;
    private const double PhysicsTicksPerSecond = 60;
    private const double WreckageScatterSpeed = 0.3;
    private const int InvulnerableMs = 500;
    private const float BareHandDamage = 0.5f;
    private const float HazardSeconds = 1;
    private const double WearStepHours = 1;
    private const double HoursPerDay = 24;
    private const string WornAtKey = "wornAt";
    private const string AfloatKey = "afloatHours";
    private static readonly AssetLocation BreakSound = new("game:sounds/block/planks");
    private static readonly AssetLocation MendSound = new("game:sounds/player/build");
    private static readonly string[] Conditions = ["sound", "scarred", "damaged", "breaking"];

    private readonly object impactLock = new();
    private readonly List<double> pendingImpacts = new();
    private ImpactSensor sensor = null!;
    private double impactCapacity;
    private DamageConversion damageConversion = null!;
    private VesselsModSystem vessels = null!;
    private ISeaWeather seaWeather = null!;
    private HullRepair? repair;
    private BlockDropItemStack[] wreckage = [];
    private HullCracks? cracks;
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

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        repair = attributes["repair"].AsObject<HullRepair?>(null);
        if (repair?.Material.Resolve(entity.World, "vessel repair") != true) repair = null;

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
        wearPerDay = attributes["wearPerDay"].AsDouble(0);
        runsRapids = attributes["runsRapids"].AsBool(false);
        rapids = vessels.Rapids;
        wreckage = attributes["wreckage"].AsObject<BlockDropItemStack[]>([])
            .Where(drop => drop.Resolve(entity.World, "vessel wreckage", entity.Code))
            .ToArray();
        entity.PhysicsUpdateWatcher += OnPhysicsUpdate;
    }

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        if (cracks?.Visible != true) return null;

        handling = EnumHandling.PreventSubsequent;
        var capi = (ICoreClientAPI)entity.Api;
        return cracks.Over(capi.Tesselator.GetTextureSource(entity, null, entity.WatchedAttributes.GetInt("textureIndex")));
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        entity.PhysicsUpdateWatcher -= OnPhysicsUpdate;
        base.OnEntityDespawn(despawn);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server) return;

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
        if (!CanMendWith(itemslot)) return;

        handled = EnumHandling.PreventSubsequent;
        if (server) Mend(byEntity, itemslot);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (repair == null || Integrity >= 1) return null;

        return
        [
            new WorldInteraction
            {
                ActionLangCode = "roughseas:vessel-mend",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [repair.Material.ResolvedItemstack]
            }
        ];
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        infotext.AppendLine(Lang.Get($"roughseas:vessel-condition-{Conditions[Stage()]}"));
        base.GetInfoText(infotext);
    }

    private void InitializeClient(ICoreClientAPI capi, JsonObject attributes)
    {
        if (attributes["cracks"].AsObject<CrackConfig?>(null) is not { } config) return;

        cracks = new HullCracks(capi, entity, config);
        shownStage = Stage();
        cracks.Show(shownStage);
        entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnIntegrityChanged);
    }

    private void OnIntegrityChanged()
    {
        int stage = Stage();
        if (stage == shownStage) return;

        shownStage = stage;
        cracks!.Show(stage);
        entity.MarkShapeModified();
    }

    private void OnPhysicsUpdate(float accum, Vec3d prevPos)
    {
        var motion = entity.Pos.Motion;
        // Called from the physics threads for a drifting vessel and from the main thread for a steered one.
        lock (impactLock)
        {
            if (sensor.Sample(motion.X * PhysicsTicksPerSecond, motion.Z * PhysicsTicksPerSecond) is double speed)
            {
                pendingImpacts.Add(speed);
            }
        }
    }

    private void Wear(bool afloat)
    {
        if (wearPerDay == 0) return;

        double now = entity.World.Calendar.TotalHours;
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
        Integrity = Math.Max(0, Integrity - (float)(wearPerDay * hours / HoursPerDay));
        vessels.Log(entity, "wore {0:F1} h afloat: {1:F2} -> {2:F2}", hours, before, Integrity);
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

    private bool CanMendWith(ItemSlot slot)
    {
        var material = repair?.Material.ResolvedItemstack;
        return material != null && Integrity < 1 &&
               slot.Itemstack?.Collectible.Code == material.Collectible.Code && slot.StackSize >= material.StackSize;
    }

    private void Mend(EntityAgent byEntity, ItemSlot slot)
    {
        slot.TakeOut(repair!.Material.StackSize);
        slot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(repair.Saturation);
        float before = Integrity;
        Integrity = Math.Min(1, Integrity + repair.Restores);
        vessels.Log(entity, "mended by {0}: {1:F2} -> {2:F2}", byEntity.GetName(), before, Integrity);
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
        > 0.75f => 0,
        > 0.5f => 1,
        > 0.25f => 2,
        _ => 3
    };
}
