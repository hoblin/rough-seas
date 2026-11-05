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

public class EntityBehaviorBurnout(Entity entity) : EntityBehaviorBuild(entity)
{
    public const string Code = "roughseas:burnout";

    private const string CheckedAtKey = "checkedAt";
    private const int DataVersion = 1;
    private const float TickSeconds = 1;
    private const int BurningPhases = 3;
    private const double CatchingHours = 1;
    private const double MinDousingRain = 0.05;
    private const double DousingPerRain = 0.5;
    private const string ShellGroup = "Shell";
    private static readonly string[] WoodTextures = ["inside", "bark", "rings"];
    private const string CharTexture = "char";
    private const string ClayTexture = "clay";
    private const string BarkGroup = "Bark";
    private static readonly string[] Stages = ["Blank", "Char", "Grass", "Wood", "Fire", "Clay"];
    private static readonly byte[][] FireLight = [[4, 7, 10], [4, 7, 14], [4, 7, 12], [4, 7, 9]];
    private static readonly double[][] ParticleChance = [[0.1, 0.15, 0.25], [0.15, 0.1, 0.2], [0.15, 0.03, 0.2], [0.08, 0, 0.12]];

    private static readonly AssetLocation FireBlock = new("game:fire");
    private static readonly AssetLocation FireSound = new("game:sounds/environment/fire");
    private static readonly AssetLocation IgniteSound = new("game:sounds/torch-ignite");
    private static readonly AssetLocation ClaySound = new("game:sounds/block/dirt1");
    private static readonly AssetLocation WoodSound = new("game:sounds/block/planks");

    private BurnoutConfig config = null!;
    private Burnout burnout = null!;
    private ISeaWeather sea = null!;
    private AssetLocation clay = null!;
    private float sinceTick;
    private ILoadedSound? fire;

    public override bool Finished => burnout.Finished;

    public override double HoldLitres(double designed) => designed * burnout.Hollowed;

    public override double Uncut => 1 - burnout.Hollowed;

    public override double FloorMetres => burnout.TopMetres - config.HollowDepthMetres * burnout.Hollowed;

    public override double UnhewnLitres => burnout.UnhewnLitres;

    public override double DepthMetres(double designed) => burnout.Toward(config.BlankDepthMetres, designed);

    public override double WaterplaneSquareMetres(double designed) => burnout.Toward(config.BlankWaterplaneSquareMetres, designed);

    private double UncutMetres => config.HollowDepthMetres * (1 - burnout.Hollowed);

    public bool CanHew => !burnout.Finished && burnout.HewRefusal() == null;

    public double SecondsPerStroke => config.SecondsPerStroke;

    private IVesselHold? Hold => entity.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        config = attributes.AsObject<BurnoutConfig>(new BurnoutConfig());
        burnout = new Burnout(() => Tree, MarkDirty, config, RimVoxels, () => Hold is { } hold ? hold.CapacityLitres + hold.UncutLitres : 0);
        clay = new AssetLocation(config.Clay);
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
    }

    public override Shell Formed(Shell designed) => designed.Uncut(UncutMetres, burnout.Hollowed);

    public static void Begin(Entity vessel, double hours) => Begin(vessel, DataVersion, tree =>
    {
        tree.SetDouble(CheckedAtKey, hours);
        Burnout.Begin(tree);
    });

    /// <returns>Whether char is left to hew after this stroke.</returns>
    public bool Hew(EntityAgent byEntity, ItemSlot axe)
    {
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(burnout.SaturationPerStroke);
        entity.World.PlaySoundAt(new AssetLocation($"game:sounds/block/chop{1 + entity.World.Rand.Next(3)}"), entity, null, true, 16);
        if (!burnout.Stroke()) return true;

        axe.Itemstack?.Collectible.DamageItem(entity.World, byEntity, axe);
        if (burnout.Level == 0)
        {
            Shipyard.Log(entity, "{0} strips the last of the bark: {1} pieces, the trunk bare for its first fire", byEntity.GetName(), burnout.BarkPieces);
            return false;
        }
        Shipyard.Log(entity, burnout.Finished ? "{0} hews out the last char: she is finished" : "{0} hews out the char in {3} strokes: hollow {1} of {2} cut",
            byEntity.GetName(), burnout.Level, burnout.Hollowings, burnout.StrokesPerHewing);
        return false;
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        if (burnout.ClayType is { } type && entity.Properties.Client.Textures.ContainsKey($"{ClayTexture}-{type}"))
        {
            ShapeFaces.Retexture(ref entityShape, ref shapeIsCloned, ClayTexture, $"{ClayTexture}-{type}");
        }
        if (!burnout.Spoiled) return;

        foreach (var wood in WoodTextures) ShapeFaces.Retexture(ref entityShape, ref shapeIsCloned, wood, CharTexture);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            Flicker(capi);
            return;
        }
        if ((sinceTick += deltaTime) < TickSeconds || !burnout.Lit) return;

        sinceTick = 0;
        if (Wet())
        {
            burnout.Douse();
            Shipyard.Log(entity, "water in the hollow puts the fire out at {0:F1} of {1:F0} h", burnout.HoursBurnt, burnout.HoursPerFire);
            return;
        }
        double now = sea.Clock.Hours;
        double hours = now - Tree.GetDouble(CheckedAtKey, now);
        Tree.SetDouble(CheckedAtKey, now);
        bool spoiled = burnout.Spoiled;
        bool burntOut = burnout.Burn(hours);
        if (burnout.Spoiled && !spoiled)
        {
            Shipyard.Log(entity, "the unguarded fire chars the rim {0:F0} mm deep, through her {1:F0} mm wall: the blank is spoiled",
                burnout.RimCharMetres * 1000, burnout.WallMetres * 1000);
        }
        if (burntOut)
        {
            Shipyard.Log(entity, "the fire burns out after {0:F0} h: hollow {1} of {2} charred, the rim charred {3:F0} mm", burnout.HoursPerFire, burnout.Level + 1,
                burnout.Hollowings, burnout.RimCharMetres * 1000);
            return;
        }
        if (VanillaRain.FallingOn(entity) is var rain and > MinDousingRain && entity.World.Rand.NextDouble() < rain * DousingPerRain)
        {
            burnout.Douse();
            Shipyard.Log(entity, "rain puts the fire out at {0:F1} of {1:F0} h", burnout.HoursBurnt, burnout.HoursPerFire);
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
                var type = itemslot.Itemstack!.Collectible.Variant["type"];
                Spend(itemslot, creative);
                burnout.Clay(type);
                entity.World.PlaySoundAt(ClaySound, entity, null, true, 16);
                Shipyard.Log(entity, "{0} smears clay along the rim: {1} of {2} voxels", byEntity.GetName(), burnout.ClayVoxels, burnout.RimVoxels);
                break;
            case Step.Firewood:
                Spend(itemslot, creative);
                burnout.Fire.AddFirewood();
                entity.World.PlaySoundAt(WoodSound, entity, null, true, 16);
                Shipyard.Log(entity, "{0} lays firewood on the fire in hollow {1}: {2} of {3}", byEntity.GetName(), burnout.Level + 1,
                    burnout.Fire.OnFirepit, LaidFire.FirepitFirewood);
                break;
            case Step.Kindle:
                Spend(itemslot, creative);
                burnout.Fire.Kindle();
                entity.World.PlaySoundAt(WoodSound, entity, null, true, 16);
                Shipyard.Log(entity, "{0} lays dry grass in hollow {1}", byEntity.GetName(), burnout.Level + 1);
                break;
            case Step.Light:
                if (itemslot.Itemstack!.Collectible is ItemFirestarter) itemslot.Itemstack.Collectible.DamageItem(entity.World, byEntity, itemslot);
                Tree.SetDouble(CheckedAtKey, sea.Clock.Hours);
                burnout.Light();
                entity.World.PlaySoundAt(IgniteSound, entity, null, true, 16);
                Shipyard.Log(entity, "{0} lights a fire in hollow {1} of {2}: {3:F0} h to burn, {4}", byEntity.GetName(), burnout.Level + 1, burnout.Hollowings,
                    burnout.HoursPerFire - burnout.HoursBurnt, burnout.Clayed ? "the rim guarded with clay" : $"the rim unguarded, {burnout.ClayVoxels} of {burnout.RimVoxels} voxels clayed");
                break;
        }
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (burnout.Finished || burnout.Spoiled || burnout.Lit) return null;
        if (!burnout.Debarked) return Help(world, "roughseas:shipyard-debark", Axes(world));
        if (burnout.Charred) return Help(world, "roughseas:shipyard-hew", Axes(world));
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
            else if (!burnout.Debarked) infotext.AppendLine(Lang.Get("roughseas:shipyard-bark", burnout.BarkStripped, burnout.BarkPieces));
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

    private static Func<IEnumerable<CollectibleObject>> Axes(IWorldAccessor world) => () => world.Items.Where(item => item.Tool == EnumTool.Axe && item.Code != null);

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

    private int RimVoxels(int level) => Burnout.TopVoxels(Element($"Clay{level}"));

    private ShapeElement Element(string name)
    {
        var path = entity.Properties.Client!.Shape!.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        return ObjectCacheUtil.GetOrCreate(entity.Api, $"{Code}/{path}/{name}", () => Shape.TryGet(entity.Api, path)!.GetElementByName(name));
    }

    protected override IEnumerable<string> Hidden()
    {
        var levels = Enumerable.Range(0, burnout.Hollowings).SelectMany(n => Stages.Select(stage => $"{stage}{n}")).Prepend(BarkGroup);
        if (burnout.Finished) return levels;

        var level = burnout.Level;
        int phase = burnout.BurningPhase(BurningPhases, CatchingHours);
        bool burning = (burnout.Lit || burnout.HoursBurnt > 0) && phase > 0;
        var showing = new List<string> { $"Blank{level}" };
        var pieces = new List<string>();
        if (!burnout.Debarked)
        {
            showing.Add(BarkGroup);
            pieces.AddRange(Enumerable.Range(1, burnout.BarkStripped).Select(n => $"{BarkGroup}{n}"));
        }
        if (burning || burnout.Charred || burnout.Spoiled) showing.Add($"Char{level}");
        if (burnout.Fire.Kindled && !burning) showing.Add($"Grass{level}");
        if (burnout.Fire.OnFirepit > 0 && !burning)
        {
            showing.Add($"Wood{level}");
            pieces.AddRange(Enumerable.Range(burnout.Fire.OnFirepit + 1, LaidFire.FirepitFirewood - burnout.Fire.OnFirepit).Select(n => $"Wood{level}x{n}"));
        }
        if (burning && !burnout.Charred)
        {
            showing.Add($"Fire{level}");
            pieces.AddRange(Enumerable.Range(1, BurningPhases).Where(p => p != phase).Select(p => $"Fire{level}At{p}"));
        }
        if (burnout.ClayVoxels > 0)
        {
            showing.Add($"Clay{level}");
            pieces.AddRange(Unclayed(level));
        }
        return levels.Except(showing).Concat(pieces).Append(ShellGroup);
    }

    private IEnumerable<string> Unclayed(int level)
    {
        double coated = 0;
        foreach (var piece in Element($"Clay{level}").Children ?? [])
        {
            if (coated >= burnout.ClayVoxels) yield return piece.Name;
            coated += Burnout.TopVoxels(piece);
        }
    }

    protected override string FormKey() => burnout.Level.ToString();

    protected override string ShapeKey() =>
        $"{burnout.Level}/{burnout.BarkStripped}/{burnout.ClayVoxels}/{burnout.ClayType}/{burnout.Fire.Kindled}/{burnout.Fire.OnFirepit}/{burnout.Lit}/{burnout.BurningPhase(BurningPhases, CatchingHours)}/{burnout.HoursBurnt > 0}/{burnout.Charred}/{burnout.Spoiled}";

    private void Flicker(ICoreClientAPI capi)
    {
        if (!burnout.Lit)
        {
            fire?.Stop();
            entity.LightHsv = null;
            return;
        }

        int phase = burnout.BurningPhase(BurningPhases, CatchingHours);
        entity.LightHsv = FireLight[phase];
        var at = entity.Pos.XYZ.Add(0, FloorMetres, 0);
        fire ??= capi.World.LoadSound(new SoundParams { Location = FireSound, ShouldLoop = true, DisposeOnFinish = false, Position = at.ToVec3f(), Range = 16 });
        fire.SetPosition(at.ToVec3f());
        if (!fire.IsPlaying) fire.Start();
        if (capi.World.GetBlock(FireBlock)?.ParticleProperties is not { Length: > 0 } kinds) return;

        var rand = capi.World.Rand;
        for (double spot = 0; spot < config.HollowLengthMetres; spot++)
        {
            int kind = Math.Min(kinds.Length - 1, rand.Next(kinds.Length + 1));
            if (rand.NextDouble() >= ParticleChance[phase][kind]) continue;

            var particles = kinds[kind];
            double along = (rand.NextDouble() - 0.5) * config.HollowLengthMetres;
            double across = (rand.NextDouble() - 0.5) * config.HollowBeamMetres;
            var (x, z) = HullShape.ToWorld(along, across, entity.Pos.Yaw);
            particles.basePos.Set(at.X + x, at.Y, at.Z + z);
            particles.Quantity.avg = 1;
            particles.TerrainCollision = false;
            capi.World.SpawnParticles(particles);
            particles.Quantity.avg = 0;
        }
    }

    private bool Wet() =>
        Hold?.Litres >= 1 || IHull.Of(entity)?.Afloat.Sunk == true;
}
