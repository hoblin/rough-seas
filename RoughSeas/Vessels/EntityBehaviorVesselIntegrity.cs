using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private static readonly AssetLocation BreakSound = new("game:sounds/block/planks");
    private static readonly string[] Conditions = ["sound", "scarred", "damaged", "breaking"];

    private readonly object impactLock = new();
    private readonly List<double> pendingImpacts = new();
    private ImpactSensor sensor = null!;
    private double impactCapacity;
    private BlockDropItemStack[] wreckage = [];
    private HullCracks? cracks;
    private int shownStage;

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

        var materials = entity.Api.ModLoader.GetModSystem<VesselsModSystem>().Materials;
        impactCapacity = attributes["impactCapacity"].AsDouble() * materials.StrengthFactor(attributes["parts"].AsObject<VesselPart[]>([]));
        sensor = new ImpactSensor(attributes["minImpactSpeed"].AsDouble(1));
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
            if (pendingImpacts.Count == 0) return;
            impacts = pendingImpacts.ToArray();
            pendingImpacts.Clear();
        }

        double mass = Mass();
        foreach (double speed in impacts)
        {
            Strike(0.5 * mass * speed * speed);
        }
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
        if (rand.NextDouble() >= 1 - Math.Exp(-severity)) return;

        Integrity = Math.Max(0, Integrity - (float)(severity * (0.5 + rand.NextDouble())));
        if (Integrity > 0) Creak();
        else Break();
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
