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
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class EntityBehaviorVesselIntegrity : EntityBehavior, IBuoyant
{
    public const string Code = "roughseas:vesselintegrity";

    private const string TreeKey = "roughseas:vessel";
    private const string CarriedKey = "roughseas:vessel";
    private const string VersionKey = "version";
    private const string CheckedAtKey = "checkedAt";
    private const int DataVersion = 2;
    private const double WreckageScatterSpeed = 0.3;
    private const int InvulnerableMs = 500;
    private const float BareHandDamage = 0.5f;
    private const float HazardSeconds = 1;
    private const float UpdateSeconds = 0.5f;
    private const double AfloatStepHours = 1;
    private const double WaterlineProbeMetres = 0.25;
    private const double FounderedMetres = 100;
    private const float MinSunlight = 0.5f;
    private const float MaxDryingRain = 0.05f;
    private const string HullBox = "HullAP";
    private static readonly AssetLocation BreakSound = new("game:sounds/block/planks");
    private static readonly AssetLocation MendSound = new("game:sounds/player/build");

    private readonly object impactLock = new();
    private readonly List<Knocked> pendingImpacts = new();
    private Impact impact = null!;
    private double impactCapacity;
    private DamageConversion damageConversion = null!;
    private VesselsModSystem vessels = null!;
    private ISeaWeather seaWeather = null!;
    private HullModel model = null!;
    private Flotation flotation = null!;
    private HullBuild build;
    private double referenceDraft;
    private HullRepair[] repairs = [];
    private BlockDropItemStack[] wreckage = [];
    private HullDamage? damage;
    private HullShape? shape;
    private bool runsRapids;
    private Rapids rapids = null!;
    private float sinceHazards;
    private float sinceUpdate = UpdateSeconds;
    private bool wasSunk;
    private bool wasAwash;
    private bool inWater;
    private double leaked;
    private double[]? hullBox;

    public EntityBehaviorVesselIntegrity(Entity entity) : base(entity) { }

    public float Integrity => model.Condition;

    public HullModel Model => model;

    public bool HasDraft => flotation.DepthMetres > 0;

    public double DepthMetres => flotation.DepthMetres;

    public Afloat Afloat { get; private set; }

    /// <summary>How far below vanilla's drawn waterline she floats, that line being her reference build with one rider aboard.</summary>
    public double SettlingMetres { get; private set; }

    public Aboard Aboard { get; private set; }

    public double ReferenceKg { get; private set; }

    public HullShape Shape => shape ??= ShapeOf(entity, hullBox);

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    private IVesselHold? Hold => entity.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();

    private static JsonItemStack OfWood(JsonItemStack stack, Wood? wood)
    {
        if (wood != null) stack.Code = wood.Locate(stack.Code);
        return stack;
    }

    private static BlockDropItemStack OfWood(BlockDropItemStack drop, Wood? wood)
    {
        if (wood != null && drop.Code != null) drop.Code = wood.Locate(drop.Code);
        return drop;
    }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        var wood = Wood.Of(properties.Variant);
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        repairs = attributes["repairs"].AsObject<HullRepair[]>([])
            .Where(repair => OfWood(repair.Material, wood).Resolve(entity.World, "vessel repair"))
            .ToArray();
        model = HullModel.For(attributes["model"].AsString("cracking"), () => Tree, MarkDirty, attributes, vessels.HullConfig);
        hullBox = attributes["hullBox"].AsArray<double>(null);
        var parts = attributes["parts"].AsObject<VesselPart[]>([]);
        build = vessels.Materials.Build(parts, properties.Weight);
        flotation = new Flotation(attributes["depthMetres"].AsDouble(), attributes["waterplaneSquareMetres"].AsDouble(), build.Litres);
        ReferenceKg = properties.Weight + vessels.Mass.CrewKg;
        referenceDraft = flotation.Float(new Aboard(ReferenceKg, 0, 0, 0, 0, 0), false).DraftMetres;
        impact = new Impact(attributes["minImpactSpeed"].AsDouble(1), InvulnerableMs);
        entity.PhysicsUpdateWatcher += OnPhysicsUpdate;

        if (entity.Api is ICoreClientAPI capi)
        {
            InitializeClient(capi, attributes);
            return;
        }

        if (Tree == null)
        {
            var tree = new TreeAttribute();
            tree.SetInt(VersionKey, DataVersion);
            entity.WatchedAttributes.SetAttribute(TreeKey, tree);
        }
        else if (Tree.GetInt(VersionKey) < DataVersion) Migrate(Tree);

        impactCapacity = attributes["impactCapacity"].AsDouble() * vessels.Materials.StrengthFactor(parts);
        damageConversion = vessels.DamageConversion;
        seaWeather = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        runsRapids = attributes["runsRapids"].AsBool(false);
        rapids = vessels.Rapids;
        wreckage = attributes["wreckage"].AsObject<BlockDropItemStack[]>([])
            .Where(drop => OfWood(drop, wood).Resolve(entity.World, "vessel wreckage", entity.Code))
            .ToArray();
    }

    public override void AfterInitialized(bool onFirstSpawn) => Float();

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        if (damage?.Visible != true) return null;

        handling = EnumHandling.PreventSubsequent;
        var capi = (ICoreClientAPI)entity.Api;
        return damage.Over(capi.Tesselator.GetTextureSource(entity, null, entity.WatchedAttributes.GetInt("textureIndex")));
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements) =>
        damage?.Retexture(ref entityShape, ref shapeIsCloned, Shape);

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        entity.PhysicsUpdateWatcher -= OnPhysicsUpdate;
        base.OnEntityDespawn(despawn);
    }

    public override void OnGameTick(float deltaTime)
    {
        if ((sinceUpdate += deltaTime) >= UpdateSeconds)
        {
            Float();
            if (entity.World.Side == EnumAppSide.Server && entity.Alive)
            {
                using var _ = vessels.Budget.Measure();
                Flood(sinceUpdate);
            }
            sinceUpdate = 0;
        }
        if (entity.World.Side != EnumAppSide.Server) return;
        using var budget = vessels.Budget.Measure();

        Knocked[] impacts;
        lock (impactLock)
        {
            impacts = pendingImpacts.ToArray();
            pendingImpacts.Clear();
        }

        if (impacts.Length > 0)
        {
            double mass = Aboard.Impact;
            foreach (var knocked in impacts)
            {
                double joules = impact.Joules(mass, knocked.SpeedLost);
                var place = Toward(knocked.TowardX, knocked.TowardZ);
                vessels.Log(entity, "impact at {0:F1} m/s into the {1} with {2:F0} kg aboard: {3:F0} J", knocked.SpeedLost, place.Code(), mass, joules);
                Strike(joules, place);
            }
        }

        if ((sinceHazards += deltaTime) < HazardSeconds || !entity.Alive) return;

        var fluid = FluidBelow();
        PassTime(entity.Swimming || fluid.IsLiquid());
        if (!runsRapids && rapids.Holds(fluid))
        {
            vessels.Log(entity, "in rapids");
            Strike(rapids.JoulesPerSecond * sinceHazards, HullPlace.Bottom);
        }
        sinceHazards = 0;
    }

    /// <summary>
    /// Strikes the hull with an impact of this energy at this place. Server only. Whether it harms the hull, and how
    /// much, is rolled against the vessel's impact capacity; what the harm does is the hull model's.
    /// </summary>
    public void Strike(double joules, HullPlace place)
    {
        if (!entity.Alive) return;
        if (model is not IKnockable knockable)
        {
            vessels.Log(entity, "struck at the {0} with {1:F0} J: the hull gives, unharmed", place.Code(), joules);
            return;
        }

        double severity = joules / impactCapacity;
        var rand = entity.World.Rand;
        var harmed = rand.NextDouble() < 1 - Math.Exp(-severity);
        var knock = harmed ? knockable.Take(place, severity * (0.5 + rand.NextDouble())) : (Knock?)null;
        vessels.Log(entity, "struck at the {0} with {1:F0} J against a capacity of {2:F0}: {3}, hull now {4:F2}",
            place.Code(), joules, impactCapacity, knock?.ToString().ToLowerInvariant() ?? "unharmed", Integrity);
        if (knock == Knock.Harmed) Creak();
        else if (knock == Knock.BrokenUp) Break();
    }

    public HullPlace Toward(double worldX, double worldZ)
    {
        var (along, across) = HullShape.ToHull(worldX, worldZ, entity.Pos.Yaw);
        return Shape.Toward(along, across);
    }

    public HullPlace At(Vec3d relative)
    {
        var (along, across) = HullShape.ToHull(relative.X, relative.Z, entity.Pos.Yaw);
        return Shape.At(along, relative.Y, across);
    }

    public Vec3d PointAt(HullPlace place)
    {
        var (along, height, across) = Shape.PointOf(place);
        var (x, z) = HullShape.ToWorld(along, across, entity.Pos.Yaw);
        return new Vec3d(x, height, z);
    }

    public void TakeConditionFrom(ItemStack stack)
    {
        if (stack.Attributes.GetTreeAttribute(CarriedKey) is { } carried)
        {
            entity.WatchedAttributes.SetAttribute(TreeKey, carried.Clone());
            if (Tree.GetInt(VersionKey) < DataVersion) Migrate(Tree);
            MarkDirty();
            return;
        }

        var item = stack.Collectible;
        model.Adopt(CarriedCondition.Integrity(item.GetRemainingDurability(stack), item.GetMaxDurability(stack)));
    }

    /// <summary>Vanilla's shift pickup, server only, with the hull's condition carried on the item.</summary>
    public bool TryPickUp(EntityAgent byEntity)
    {
        if (!byEntity.Controls.ShiftKey || entity.Properties.Attributes?["rightClickPickup"].AsBool() != true) return false;
        if (Occupied()) return false;
        if (Afloat.Sunk)
        {
            vessels.Log(entity, "{0} cannot lift her off the bottom", byEntity.GetName());
            return true;
        }

        var stack = new ItemStack(entity.World.GetItem(entity.Code));
        if (Integrity < 1) stack.Collectible.SetDurability(stack, CarriedCondition.Durability(Integrity, stack.Collectible.GetMaxDurability(stack)));
        var carried = (ITreeAttribute)Tree.Clone();
        carried.RemoveAttribute(CheckedAtKey);
        stack.Attributes[CarriedKey] = carried;
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
        var place = PlaceOf(damageSource);
        vessels.Log(entity, "takes {0:F1} {1} damage from {2} at the {3}", damage, damageSource.Type, damageSource.Source, place.Code());
        Strike(damageConversion.Joules(damageSource, damage), place);
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
        var place = At(hitPosition);
        var refusal = Refusal(repair, itemslot, place);
        if (refusal != null && !OnHull(byEntity)) return;

        handled = EnumHandling.PreventSubsequent;
        if (refusal != null)
        {
            if (entity.Api is ICoreClientAPI capi) capi.TriggerIngameError(this, "roughseas-vessel-mend", refusal);
            return;
        }
        if (server) Mend(byEntity, itemslot, repair, place);
    }

    /// <summary>Whether the held item is one this hull is mended or greased with, whether or not it would help now.</summary>
    public bool MendsWith(ItemSlot slot) => RepairWith(slot) != null;

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        var place = At(es.HitPosition);
        var mending = model is IMendable mendable ? repairs.Where(repair => mendable.Needs(repair, place)).ToArray() : [];
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
        if (Afloat.Sunk) infotext.AppendLine(Lang.Get("roughseas:vessel-sunk"));
        model.Describe(infotext);
        if (inWater && model.LitresPerSecond(Afloat, flotation.DepthMetres) is var leaking and >= 0.01)
        {
            infotext.AppendLine(Lang.Get("roughseas:vessel-leaking", Math.Round(leaking * 60)));
        }
        if (!Afloat.Sunk && inWater && HasDraft)
        {
            infotext.AppendLine(Afloat.FreeboardMetres > 0.01
                ? Lang.Get("roughseas:vessel-freeboard", Math.Round(Afloat.FreeboardMetres * 100))
                : Lang.Get("roughseas:vessel-awash"));
        }
        base.GetInfoText(infotext);
    }

    private void InitializeClient(ICoreClientAPI capi, JsonObject attributes)
    {
        if (attributes["damage"].AsObject<HullDamageConfig?>(null) is not { } config) return;

        damage = new HullDamage(capi, entity, config);
        damage.Show(model.Stage);
        entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnConditionChanged);
    }

    private void OnConditionChanged()
    {
        if (damage!.Shows(model.Stage)) return;

        damage.Show(model.Stage);
        entity.MarkShapeModified();
    }

    /// <summary>Server only: an impact the controller's client measured on the vessel it steers.</summary>
    public void Reported(IServerPlayer player, ImpactReport report)
    {
        if (entity.GetInterface<IMountable>()?.Controller != player.Entity)
        {
            vessels.Log(entity, "ignored an impact reported by {0}, who does not steer it", player.PlayerName);
            return;
        }
        lock (impactLock) pendingImpacts.Add(new Knocked(report.SpeedLost, report.TowardX, report.TowardZ));
    }

    private void OnPhysicsUpdate(float accum, Vec3d prevPos)
    {
        // Physics steps pass 0; the rider's frame-end call carries motion EntityBoat rewrote, never a collision. See docs/modding.md.
        if (accum != 0) return;
        var mount = entity.GetInterface<IMountable>();
        var client = entity.Api as ICoreClientAPI;
        if (client != null ? mount?.Controller != client.World.Player.Entity : mount?.IsBeingControlled() == true)
        {
            lock (impactLock) impact.Forget();
            return;
        }

        var motion = entity.Pos.Motion;
        Knocked? knocked;
        lock (impactLock)
        {
            knocked = impact.Step(motion.X, motion.Z, entity.World.ElapsedMilliseconds);
            if (knocked is { } lost && client == null) pendingImpacts.Add(lost);
        }
        if (knocked is { } reported && client != null) vessels.Report(entity, reported);
    }

    private void Float()
    {
        var hold = Hold;
        inWater = entity.Swimming || FluidBelow().IsLiquid();
        Aboard = vessels.Mass.Of(entity, build.Kilograms + model.SoakedKg);
        var afloat = flotation.Float(Aboard, hold != null && hold.Litres >= hold.CapacityLitres);
        Afloat = inWater ? afloat : afloat with { Sunk = false };
        SettlingMetres = Afloat.Sunk ? FounderedMetres : Afloat.DraftMetres - referenceDraft;
        if (entity.World.Side != EnumAppSide.Server || !HasDraft || !inWater) return;

        if (Afloat.Sunk != wasSunk)
        {
            vessels.Log(entity, Afloat.Sunk
                ? "sinks: {0:F0} kg that the water does not hold up against the {1:F0} her own material keeps afloat"
                : "floats again: {0:F0} kg that the water does not hold up against the {1:F0} her own material keeps afloat",
                Aboard.Sinking, flotation.BuoyantKg);
        }
        bool awash = !Afloat.Sunk && Afloat.FreeboardMetres <= 0;
        if (awash && !wasAwash) vessels.Log(entity, "awash at {0:F2} m draft with {1:F0} kg aboard", Afloat.DraftMetres, Aboard.Total);
        (wasSunk, wasAwash) = (Afloat.Sunk, awash);
    }

    private void Flood(double seconds)
    {
        if (!HasDraft || !inWater || Hold is not { } hold || hold.Litres >= hold.CapacityLitres) return;

        if (Afloat.Sunk || Afloat.FreeboardMetres <= 0)
        {
            hold.Ship(hold.CapacityLitres - hold.Litres, Afloat.Sunk ? "on the bottom" : "over the gunwale");
            return;
        }
        double litres = model.Inflow(seconds, vessels.GameHours(seconds), Afloat, flotation.DepthMetres);
        if (litres <= 0) return;

        hold.Ship(litres, "through her leaks");
        if ((leaked += litres) < 1) return;
        vessels.Log(entity, "leaked {0:F1} L at {1:F2} m draft: bilge {2:F0} of {3:F0} L", leaked, Afloat.DraftMetres, hold.Litres, hold.CapacityLitres);
        leaked = 0;
    }

    private void PassTime(bool afloat)
    {
        if (model is not IAfloat timed) return;

        var calendar = entity.World.Calendar;
        double now = calendar.TotalHours;
        double last = Tree.GetDouble(CheckedAtKey, now);
        if (now - last < AfloatStepHours && now != last) return;

        Tree.SetDouble(CheckedAtKey, now);
        if (now == last) return;
        double sun = afloat ? 0 : Sunlight();
        timed.Pass((now - last) / calendar.HoursPerDay, afloat, sun);
        vessels.Log(entity, "{0:F1} h {1}: hull {2:F2}, {3:F0} kg soaked", now - last,
            afloat ? "afloat" : sun > 0 ? "drying in the sun" : "ashore", Integrity, model.SoakedKg);
    }

    private double Sunlight()
    {
        var accessor = entity.World.BlockAccessor;
        var pos = entity.Pos.AsBlockPos;
        if (accessor.GetRainMapHeightAt(pos) > pos.Y) return 0;
        if ((accessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0) > MaxDryingRain) return 0;

        float daylight = entity.World.Calendar.GetDayLightStrength(pos);
        return daylight >= MinSunlight ? daylight : 0;
    }

    private void Migrate(ITreeAttribute tree)
    {
        float integrity = tree.GetFloat("integrity", 1);
        foreach (var legacy in new[] { "integrity", "worn", "wornAt", "afloatHours" }) tree.RemoveAttribute(legacy);
        tree.SetInt(VersionKey, DataVersion);
        entity.WatchedAttributes.SetAttribute(TreeKey, tree);
        model.Adopt(integrity);
        vessels.Log(entity, "hull data from version 1: integrity {0:F2} taken as {1:F2}", integrity, Integrity);
    }

    private HullPlace PlaceOf(DamageSource source)
    {
        if (source.HitPosition is { } hit) return At(hit);
        if (source.GetSourcePosition() is { } from) return Toward(from.X - entity.Pos.X, from.Z - entity.Pos.Z);
        return HullShape.Places[entity.World.Rand.Next(HullShape.Places.Length)];
    }

    private void Swing(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition)
    {
        if (entity.GetInterface<IMountable>()?.Controller == byEntity) return;

        var stack = slot.Itemstack;
        if (Afloat.Sunk && stack?.Collectible.Tool == EnumTool.Axe)
        {
            vessels.Log(entity, "broken up on the bottom by {0}", byEntity.GetName());
            stack.Collectible.DamageItem(entity.World, byEntity, slot);
            Break();
            return;
        }

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

    private string? Refusal(HullRepair repair, ItemSlot slot, HullPlace place)
    {
        if (model is not IMendable mendable || !mendable.Needs(repair, place))
        {
            return Lang.Get(repair.Greases ? "roughseas:vessel-nothing-to-grease" : "roughseas:vessel-nothing-to-mend");
        }
        return slot.StackSize < repair.Material.StackSize
            ? Lang.Get("roughseas:vessel-mend-takes", repair.Material.StackSize, slot.GetStackName())
            : null;
    }

    private bool OnHull(EntityAgent byEntity)
    {
        var index = (byEntity as EntityPlayer)?.EntitySelection?.SelectionBoxIndex ?? 0;
        var boxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes ?? [];
        return index <= 0 || index > boxes.Length || boxes[index - 1].AttachPoint.Code == HullBox;
    }

    private void Mend(EntityAgent byEntity, ItemSlot slot, HullRepair repair, HullPlace place)
    {
        slot.TakeOut(repair.Material.StackSize);
        slot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(repair.Saturation);
        vessels.Log(entity, "{0} by {1}", ((IMendable)model).Mend(repair, place), byEntity.GetName());
        entity.World.PlaySoundAt(MendSound, entity, null, true, 16);
    }

    private bool Occupied() =>
        entity.GetBehavior<EntityBehaviorSeatable>()?.AnyMounted() == true ||
        entity.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory.Empty == false;

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

    private void MarkDirty() => entity.WatchedAttributes.MarkPathDirty(TreeKey);

    private Block FluidBelow() =>
        entity.World.BlockAccessor.GetBlock(entity.Pos.XYZ.Add(0, -WaterlineProbeMetres, 0).AsBlockPos, BlockLayersAccess.Fluid);

    private static HullShape ShapeOf(Entity entity, double[]? hullBox)
    {
        if (hullBox is { Length: 6 } box) return HullShape.FromVoxels(box[..3], box[3..]);

        var boxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes ?? [];
        if (boxes.FirstOrDefault(box => box.AttachPoint.Code == HullBox)?.AttachPoint.ParentElement is { From: { } from, To: { } to })
        {
            return HullShape.FromVoxels(from, to);
        }

        var hitbox = entity.SelectionBox;
        return new HullShape(Math.Max(hitbox.XSize, hitbox.ZSize) / 2, Math.Min(hitbox.XSize, hitbox.ZSize) / 2, hitbox.YSize, 0, 0, 0);
    }
}
