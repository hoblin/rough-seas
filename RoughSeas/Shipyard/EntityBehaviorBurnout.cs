using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

public class EntityBehaviorBurnout(Entity entity) : EntityBehavior(entity), IHullForm
{
    public const string Code = "roughseas:burnout";
    public const string TreeKey = "roughseas:build";

    private const string VersionKey = "version";
    private const string CheckedAtKey = "checkedAt";
    private const int DataVersion = 2;
    private const float TickSeconds = 1;
    private const float ParticleSeconds = 0.1f;
    private const int FlamesPerPuff = 3;
    private const double MinDousingRain = 0.05;
    private const double DousingPerRain = 0.5;
    private const double FlameReach = 0.8;
    private const string Unfinished = "roughseas-unfinished";
    private const string ClayGroup = "Clay";
    private const string WoodTexture = "inside";
    private const string CharTexture = "char";
    private static readonly string[] Stages = ["Blank", "Char", "Wood", "Fire"];

    private static readonly AssetLocation FireBlock = new("game:fire");
    private static readonly AssetLocation FireSound = new("game:sounds/environment/fire");
    private static readonly AssetLocation IgniteSound = new("game:sounds/torch-ignite");
    private static readonly AssetLocation ClaySound = new("game:sounds/block/dirt1");
    private static readonly AssetLocation WoodSound = new("game:sounds/block/planks");

    private BurnoutConfig config = null!;
    private Burnout burnout = null!;
    private ShipyardModSystem shipyard = null!;
    private ISeaWeather sea = null!;
    private AssetLocation clay = null!;
    private float sinceTick;
    private float sinceFlames;
    private string? shownKey;
    private int shownLevel;
    private ILoadedSound? fire;

    public event Action? Reformed;

    public double Hollowed => burnout.Hollowed;

    public double FloorMetres => config.RimMetres - config.HollowDepthMetres * burnout.Hollowed;

    public double UncutMetres => config.HollowDepthMetres * (1 - burnout.Hollowed);

    public bool CanHew => !burnout.Finished && burnout.HewRefusal() == null;

    public double SecondsPerStroke => config.SecondsPerStroke;

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    private IVesselHold? Hold => entity.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        config = attributes.AsObject<BurnoutConfig>(new BurnoutConfig());
        burnout = new Burnout(() => Tree, MarkDirty, config, RimVoxels(), () => Hold is { } hold ? hold.CapacityLitres + hold.UncutLitres : 0);
        clay = new AssetLocation(config.Clay);
        shipyard = entity.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        if (entity.Api.Side == EnumAppSide.Client) entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnBuildChanged);
        else if (Tree is { } tree && tree.GetInt(VersionKey) < DataVersion)
        {
            burnout.Migrate();
            tree.SetInt(VersionKey, DataVersion);
        }
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        if (seatable != null) seatable.CanSit += CanSit;
        (shownKey, shownLevel) = (ShapeKey(), burnout.Level);
    }

    public static void Begin(Entity vessel, double hours)
    {
        var tree = new TreeAttribute();
        tree.SetInt(VersionKey, DataVersion);
        tree.SetDouble(CheckedAtKey, hours);
        Burnout.Begin(tree);
        vessel.WatchedAttributes.SetAttribute(TreeKey, tree);
    }

    /// <returns>Whether char is left to hew after this stroke.</returns>
    public bool Hew(EntityAgent byEntity, ItemSlot axe)
    {
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(burnout.SaturationPerStroke);
        entity.World.PlaySoundAt(new AssetLocation($"game:sounds/block/chop{1 + entity.World.Rand.Next(3)}"), entity, null, true, 16);
        if (!burnout.Stroke()) return true;

        axe.Itemstack?.Collectible.DamageItem(entity.World, byEntity, axe);
        shipyard.Log(entity, burnout.Finished ? "{0} hews out the last char: she is finished" : "{0} hews out the char in {3} strokes: hollow {1} of {2} cut",
            byEntity.GetName(), burnout.Level, burnout.Hollowings, burnout.StrokesPerHewing);
        Reformed?.Invoke();
        return false;
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        willDeleteElements = (willDeleteElements ?? []).Append(Hidden().ToArray());
        if (burnout.Spoiled) CharAllOver(ref entityShape, ref shapeIsCloned);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            Flicker(capi, deltaTime);
            return;
        }
        if ((sinceTick += deltaTime) < TickSeconds || !burnout.Lit) return;

        sinceTick = 0;
        if (Wet())
        {
            burnout.Douse();
            shipyard.Log(entity, "water in the hollow puts the fire out at {0:F1} of {1:F0} h", burnout.HoursBurnt, burnout.HoursPerFire);
            return;
        }
        double now = sea.Clock.Hours;
        double hours = now - Tree.GetDouble(CheckedAtKey, now);
        Tree.SetDouble(CheckedAtKey, now);
        bool spoiled = burnout.Spoiled;
        bool burntOut = burnout.Burn(hours);
        if (burnout.Spoiled && !spoiled)
        {
            shipyard.Log(entity, "the unguarded fire chars the rim {0:F0} mm deep, through her {1:F0} mm wall: the blank is spoiled",
                burnout.RimCharMetres * 1000, burnout.WallMetres * 1000);
        }
        if (burntOut)
        {
            shipyard.Log(entity, "the fire burns out after {0:F0} h: hollow {1} of {2} charred, the rim charred {3:F0} mm", burnout.HoursPerFire, burnout.Level + 1,
                burnout.Hollowings, burnout.RimCharMetres * 1000);
            return;
        }
        if (VanillaRain.FallingOn(entity) is var rain and > MinDousingRain && entity.World.Rand.NextDouble() < rain * DousingPerRain)
        {
            burnout.Douse();
            shipyard.Log(entity, "rain puts the fire out at {0:F1} of {1:F0} h", burnout.HoursBurnt, burnout.HoursPerFire);
        }
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || burnout.Finished || StepWith(itemslot) is not { } step) return;

        handled = EnumHandling.PreventSubsequent;
        var refusal = step switch
        {
            Step.Clay => burnout.ClayRefusal(),
            Step.Firewood => burnout.FirewoodRefusal(),
            Step.Kindle => burnout.KindlingRefusal(),
            Step.Light => Wet() ? "shipyard-bail-first" : burnout.LightRefusal(),
            _ => burnout.HewRefusal()
        };
        if (refusal != null)
        {
            (entity.Api as ICoreClientAPI)?.TriggerIngameError(this, refusal, Lang.Get("roughseas:" + refusal));
            return;
        }
        if (entity.World.Side != EnumAppSide.Server) return;

        bool creative = (byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;
        switch (step)
        {
            case Step.Clay:
                Spend(itemslot, creative);
                burnout.Clay();
                entity.World.PlaySoundAt(ClaySound, entity, null, true, 16);
                shipyard.Log(entity, "{0} smears clay along the rim: {1} of {2} voxels", byEntity.GetName(), burnout.ClayVoxels, burnout.RimVoxels);
                break;
            case Step.Firewood:
                Spend(itemslot, creative);
                burnout.Fire.AddFirewood();
                entity.World.PlaySoundAt(WoodSound, entity, null, true, 16);
                shipyard.Log(entity, "{0} lays firewood on the fire in hollow {1}: {2} of {3}", byEntity.GetName(), burnout.Level + 1,
                    burnout.Fire.OnFirepit, LaidFire.FirepitFirewood);
                break;
            case Step.Kindle:
                Spend(itemslot, creative);
                burnout.Fire.Kindle();
                entity.World.PlaySoundAt(WoodSound, entity, null, true, 16);
                shipyard.Log(entity, "{0} lays dry grass in hollow {1}", byEntity.GetName(), burnout.Level + 1);
                break;
            case Step.Light:
                if (itemslot.Itemstack!.Collectible is ItemFirestarter) itemslot.Itemstack.Collectible.DamageItem(entity.World, byEntity, itemslot);
                Tree.SetDouble(CheckedAtKey, sea.Clock.Hours);
                burnout.Light();
                entity.World.PlaySoundAt(IgniteSound, entity, null, true, 16);
                shipyard.Log(entity, "{0} lights a fire in hollow {1} of {2}: {3:F0} h to burn, {4}", byEntity.GetName(), burnout.Level + 1, burnout.Hollowings,
                    burnout.HoursPerFire - burnout.HoursBurnt, burnout.Clayed ? "the rim guarded with clay" : $"the rim unguarded, {burnout.ClayVoxels} of {burnout.RimVoxels} voxels clayed");
                break;
        }
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (burnout.Finished || burnout.Spoiled || burnout.Lit) return null;
        if (burnout.Charred) return Help(world, "roughseas:shipyard-hew", () => world.Items.Where(item => item.Tool == EnumTool.Axe && item.Code != null));
        if (!burnout.Clayed) return Help(world, "roughseas:shipyard-clay", () => world.SearchItems(clay));
        if (!burnout.Fire.Kindled) return Help(world, "roughseas:shipyard-kindle", () => world.Items.Where(item => item is ItemDryGrass));
        if (!burnout.Fire.Laid) return Help(world, "roughseas:shipyard-firewood", () => world.Items.Where(Constructs));
        return Help(world, "roughseas:shipyard-light", () => BlockBehaviorCanIgnite.CanIgniteStacks(world.Api, true).Select(stack => stack.Collectible));
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        if (!burnout.Finished)
        {
            infotext.AppendLine(Lang.Get("roughseas:shipyard-hollowed", burnout.Level, burnout.Hollowings));
            if (burnout.Lit) infotext.AppendLine(Lang.Get("roughseas:shipyard-burns", Math.Ceiling(burnout.HoursPerFire - burnout.HoursBurnt)));
            else if (burnout.Spoiled) infotext.AppendLine(Lang.Get("roughseas:shipyard-spoiled"));
            else if (burnout.Charred) infotext.AppendLine(Lang.Get("roughseas:shipyard-charred", burnout.Strokes, burnout.StrokesPerHewing));
            else
            {
                infotext.AppendLine(Lang.Get("roughseas:shipyard-rim-clay", burnout.ClayBallsOn, burnout.ClayBalls));
                if (burnout.Fire.Laid) infotext.AppendLine(Lang.Get("roughseas:shipyard-laid"));
                else if (burnout.Fire.Kindled) infotext.AppendLine(Lang.Get("roughseas:shipyard-firepit", burnout.Fire.OnFirepit, LaidFire.FirepitFirewood));
            }
        }
        base.GetInfoText(infotext);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        if (seatable != null) seatable.CanSit -= CanSit;
        fire?.Stop();
        fire?.Dispose();
        base.OnEntityDespawn(despawn);
    }

    private enum Step { Clay, Firewood, Kindle, Light, Hew }

    private Step? StepWith(ItemSlot slot) => slot.Itemstack?.Collectible switch
    {
        null => null,
        { Tool: EnumTool.Axe } => Step.Hew,
        ItemFirestarter => Step.Light,
        ItemDryGrass => Step.Kindle,
        var collectible when Constructs(collectible) => Step.Firewood,
        Block block when block.HasBehavior<BlockBehaviorCanIgnite>() => Step.Light,
        var collectible when WildcardUtil.Match(clay, collectible.Code) => Step.Clay,
        _ => null
    };

    private static bool Constructs(CollectibleObject collectible) => collectible.Attributes?.IsTrue("firepitConstructable") == true;

    private static void Spend(ItemSlot slot, bool creative)
    {
        if (creative) return;

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    private WorldInteraction[] Help(IWorldAccessor world, string action, Func<IEnumerable<CollectibleObject>> with) =>
        ObjectCacheUtil.GetOrCreate(world.Api, $"{action}/{clay}", () => new[]
        {
            new WorldInteraction { ActionLangCode = action, MouseButton = EnumMouseButton.Right, Itemstacks = with().Select(collectible => new ItemStack(collectible)).ToArray() }
        });

    private bool CanSit(EntityAgent byEntity, out string? errorMessage)
    {
        errorMessage = burnout.Finished ? null : Unfinished;
        return burnout.Finished;
    }

    private int RimVoxels()
    {
        if (entity.Properties.Client?.Shape?.Base is not { } shape) return 0;

        var path = shape.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        return ObjectCacheUtil.GetOrCreate(entity.Api, $"{Code}/rim/{path}",
            () => Shape.TryGet(entity.Api, path)?.GetElementByName(ClayGroup) is { } rim ? Burnout.TopVoxels(rim) : 0);
    }

    private IEnumerable<string> Hidden()
    {
        if (burnout.Finished) return [.. Stages, ClayGroup];

        var level = burnout.Level;
        var showing = new List<string> { $"Blank{level}" };
        if (burnout.Lit || burnout.Charred || burnout.Spoiled) showing.Add($"Char{level}");
        if (burnout.Fire.Laid) showing.Add($"Wood{level}");
        if (burnout.Lit) showing.Add($"Fire{level}");
        return Enumerable.Range(0, burnout.Hollowings)
            .SelectMany(n => Stages.Select(stage => $"{stage}{n}"))
            .Except(showing)
            .Concat(burnout.ClayVoxels > 0 ? [] : [ClayGroup]);
    }

    private string ShapeKey() => $"{burnout.Level}/{burnout.ClayVoxels > 0}/{burnout.Fire.Laid}/{burnout.Lit}/{burnout.Charred}/{burnout.Spoiled}";

    private static void CharAllOver(ref Shape shape, ref bool shapeIsCloned)
    {
        if (!shapeIsCloned)
        {
            shape = shape.Clone();
            shapeIsCloned = true;
        }
        foreach (var element in shape.Elements) Char(element);
    }

    private static void Char(ShapeElement element)
    {
        var faces = element.FacesResolved ?? [];
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i] is not { Texture: WoodTexture } face) continue;

            faces[i] = new ShapeElementFace
            {
                Texture = CharTexture,
                Uv = face.Uv,
                ReflectiveMode = face.ReflectiveMode,
                WindMode = face.WindMode,
                WindData = face.WindData,
                Rotation = face.Rotation,
                Glow = face.Glow,
                Enabled = face.Enabled
            };
        }
        foreach (var child in element.Children ?? []) Char(child);
    }

    private void OnBuildChanged()
    {
        if (burnout.Level != shownLevel)
        {
            shownLevel = burnout.Level;
            Reformed?.Invoke();
        }
        if (ShapeKey() == shownKey) return;

        shownKey = ShapeKey();
        entity.MarkShapeModified();
    }

    private void Flicker(ICoreClientAPI capi, float deltaTime)
    {
        if (!burnout.Lit)
        {
            fire?.Stop();
            return;
        }

        var at = entity.Pos.XYZ.Add(0, FloorMetres, 0);
        fire ??= capi.World.LoadSound(new SoundParams { Location = FireSound, ShouldLoop = true, DisposeOnFinish = false, Position = at.ToVec3f(), Range = 16 });
        fire.SetPosition(at.ToVec3f());
        if (!fire.IsPlaying) fire.Start();
        if ((sinceFlames += deltaTime) < ParticleSeconds || IHull.Of(entity)?.Shape is not { } hull) return;

        sinceFlames = 0;
        var rand = capi.World.Rand;
        foreach (var props in capi.World.GetBlock(FireBlock)?.ParticleProperties ?? [])
        {
            for (int i = 0; i < FlamesPerPuff; i++)
            {
                double along = hull.CentreAlong + (rand.NextDouble() * 2 - 1) * hull.HalfLength * FlameReach;
                double across = hull.CentreAcross + (rand.NextDouble() * 2 - 1) * hull.HalfBeam / 2;
                var (x, z) = HullShape.ToWorld(along, across, entity.Pos.Yaw);
                var flame = props.Clone();
                flame.basePos = at.AddCopy(x - 0.5, 0, z - 0.5);
                capi.World.SpawnParticles(flame);
            }
        }
    }

    private bool Wet() =>
        Hold?.Litres >= 1 || IHull.Of(entity)?.Afloat.Sunk == true;

    private void MarkDirty() => entity.WatchedAttributes.MarkPathDirty(TreeKey);
}
