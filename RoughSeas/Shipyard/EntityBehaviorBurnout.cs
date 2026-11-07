using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoughSeas.SeaAndWeather;
using RoughSeas.Units;
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

public class EntityBehaviorBurnout(Entity entity) : EntityBehaviorBuild(entity), IStruck
{
    public const string Code = "roughseas:burnout";

    private const string CheckedAtKey = "checkedAt";
    private const string HewerKey = "hewer";
    private const int DataVersion = 1;
    private const float TickSeconds = 1;
    private const int BurningPhases = 3;
    private const double CatchingHours = 1;
    private const double MinDousingRain = 0.05;
    private const double DousingPerRain = 0.5;
    private const string ShellGroup = "Shell";
    private static readonly string[] WoodTextures = ["inside", "hewn", "bark", "rings"];
    private const string HullElement = "Hull";
    private const string StepPart = "Step";
    private const string CharTexture = "char";
    private const string ClayTexture = "clay";
    private const string BarkGroup = "Bark";
    private static readonly string[] Stages = ["Blank", "Hewn", "Char", "Grass", "Wood", "Fire", "Clay"];
    private static readonly string[] SectionedStages = ["Blank", "Hewn", "Char"];
    private const string SternPart = "Stern";
    private const string BowPart = "Bow";
    private static readonly byte[][] FireLight = [[4, 7, 10], [4, 7, 14], [4, 7, 12], [4, 7, 9]];
    private static readonly double[][] ParticleChance = [[0.1, 0.15, 0.25], [0.15, 0.1, 0.2], [0.15, 0.03, 0.2], [0.08, 0, 0.12]];

    private static readonly AssetLocation FireBlock = new("game:fire");
    private static readonly AssetLocation FireSound = new("game:sounds/environment/fire");
    private static readonly AssetLocation IgniteSound = new("game:sounds/torch-ignite");
    private static readonly AssetLocation ClaySound = new("game:sounds/block/dirt1");
    private static readonly AssetLocation WoodSound = new("game:sounds/block/planks");
    private static readonly AssetLocation Charcoal = new("game:charcoal");
    private const double ChipReachMetres = 5;
    private const float ChipSpreadMetres = 0.2f;
    private const int ChipsPerBlow = 6;
    private const float ChipScale = 0.8f;
    private const float ChipThrow = 2;
    /// <summary>BlockFirepit.OnEntityInside, each server tick.</summary>
    private const double FirepitBurnChance = 0.05;
    /// <summary>BlockFirepit.OnEntityInside.</summary>
    private const float FirepitBurnDamage = 0.5f;
    private static readonly AssetLocation LitFirepit = new("game:firepit-lit");

    private BurnoutConfig config = null!;
    private Burnout burnout = null!;
    private ISeaWeather sea = null!;
    private AssetLocation clay = null!;
    private float sinceTick;
    private ILoadedSound? fire;
    private readonly Dictionary<long, float> swings = [];

    public override bool Finished => burnout.Finished;

    public override double HoldLitres(double designed) => designed * burnout.Hollowed;

    public override double Uncut => 1 - burnout.Hollowed;

    public override double FloorMetres => burnout.FloorMetres;

    public override double UnhewnLitres => burnout.UnhewnLitres;

    public override double DepthMetres(double designed) => burnout.Toward(config.BlankDepthMetres, designed);

    public override double WaterplaneSquareMetres(double designed) => burnout.Toward(config.BlankWaterplaneSquareMetres, designed);

    private double UncutMetres => config.HollowDepthMetres * (1 - burnout.Hollowed);

    public bool CanHew => !burnout.Finished && burnout.HewRefusal() == null;

    public bool CanLight => !burnout.Finished && !Wet() && burnout.LightRefusal() == null;

    public double SecondsPerBlow => config.SecondsPerStroke;

    private IVesselHold? Hold => entity.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        config = attributes.AsObject<BurnoutConfig>(new BurnoutConfig());
        burnout = new Burnout(() => Tree, MarkDirty, config, RimVoxels, () => Hold?.DesignedLitres ?? 0);
        clay = new AssetLocation(config.Clay);
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
    }

    public override Shell Formed(Shell designed) => designed.Uncut(UncutMetres, burnout.Hollowed);

    public static void Begin(Entity vessel, double hours) => Begin(vessel, DataVersion, tree =>
    {
        tree.SetDouble(CheckedAtKey, hours);
        Burnout.Begin(tree);
    });

    public bool TakeUp(EntityAgent hewer)
    {
        if (entity.World.Side != EnumAppSide.Server) return true;

        Tree.SetLong(HewerKey, hewer.EntityId);
        MarkDirty();
        return true;
    }

    public bool Strike(EntityAgent byEntity, ItemSlot axe)
    {
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(burnout.SaturationPerStroke);
        int stage = burnout.Stage;
        if (!burnout.Stroke())
        {
            if (burnout.Stage != stage)
            {
                Shipyard.Log(entity, burnout.SectionsTrimmed == 0 ? "{0} cuts the char out of section {1} of {2} from the bow: its floor down to hollow {3}'s"
                    : "{0} trims the outside of section {1} of {2} from the stern to hollow {3}'s", byEntity.GetName(),
                    burnout.SectionsTrimmed == 0 ? burnout.SectionsCut : burnout.Sections - burnout.SectionsTrimmed + 1, burnout.Sections, burnout.Level + 1);
            }
            return true;
        }

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
        if (burnout.ClayType is { } type && entity.Properties.Client.Textures?.ContainsKey($"{ClayTexture}-{type}") == true)
        {
            ShapeFaces.Retexture(ref entityShape, ref shapeIsCloned, ClayTexture, $"{ClayTexture}-{type}");
        }
        if (!burnout.Finished && config.HullBoxesMetres.ElementAtOrDefault(burnout.Level) is { } box) FitHull(ref entityShape, ref shapeIsCloned, box);
        if (!burnout.Spoiled) return;

        foreach (var wood in WoodTextures) ShapeFaces.Retexture(ref entityShape, ref shapeIsCloned, wood, CharTexture);
    }

    private static void FitHull(ref Shape entityShape, ref bool shapeIsCloned, BoxMetres box)
    {
        if (!shapeIsCloned)
        {
            entityShape = entityShape.Clone();
            shapeIsCloned = true;
        }
        if (entityShape.GetElementByName(HullElement) is not { } hull) return;

        hull.From = HullShape.ToModel(box.From);
        hull.To = HullShape.ToModel(box.To);
        // A shape's clone shares its attachment points, and each point reads its size from its ParentElement: one
        // blank's box would size every dugout's.
        hull.AttachmentPoints = (hull.AttachmentPoints ?? []).Select(point => new AttachmentPoint
        {
            ParentElement = hull, Code = point.Code, PosX = (hull.To[0] - hull.From[0]) / 2, PosY = point.PosY,
            PosZ = (hull.To[2] - hull.From[2]) / 2, RotationX = point.RotationX, RotationY = point.RotationY, RotationZ = point.RotationZ
        }).ToArray();
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            Flicker(capi);
            Chip(capi);
            return;
        }
        if (!burnout.Lit) return;

        Scorch();
        if ((sinceTick += deltaTime) < TickSeconds) return;

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
        if (mode != EnumInteractMode.Interact || burnout.Finished || StepWith(itemslot) is not { } step)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            return;
        }

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
        }
    }

    public void Light(EntityAgent byEntity)
    {
        Tree.SetDouble(CheckedAtKey, sea.Clock.Hours);
        burnout.Light();
        entity.World.PlaySoundAt(IgniteSound, entity, null, true, 16);
        Shipyard.Log(entity, "{0} lights a fire in hollow {1} of {2}: {3:F0} h to burn, {4}", byEntity.GetName(), burnout.Level + 1, burnout.Hollowings,
            burnout.HoursPerFire - burnout.HoursBurnt, burnout.Clayed ? "the rim guarded with clay" : $"the rim unguarded, {burnout.ClayVoxels} of {burnout.RimVoxels} voxels clayed");
    }

    public void LogMisfire(EntityAgent byEntity, float secondsUsed) =>
        Shipyard.Log(entity, "{0}'s firestarter fails to light hollow {1} after {2:F1} s", byEntity.GetName(), burnout.Level + 1, secondsUsed);

    protected override BuildStep? Next
    {
        get
        {
            if (burnout.Finished) return null;

            var world = entity.World;
            var hollowed = Lang.Get("roughseas:shipyard-hollowed", burnout.Level, burnout.Hollowings);
            if (burnout.Lit) return new($"{hollowed}\n{Lang.Get("roughseas:shipyard-burns", Math.Ceiling(burnout.HoursPerFire - burnout.HoursBurnt))}");
            if (burnout.Spoiled) return new($"{hollowed}\n{Lang.Get("roughseas:shipyard-spoiled")}");
            if (!burnout.Debarked) return new($"{hollowed}\n{Lang.Get("roughseas:shipyard-bark", burnout.BarkStripped, burnout.BarkPieces)}", "roughseas:shipyard-debark", Stacks(Axes(world)));
            if (burnout.Charred) return new($"{hollowed}\n{Lang.Get("roughseas:shipyard-charred", burnout.Strokes, burnout.StrokesPerHewing)}", "roughseas:shipyard-hew", Stacks(Axes(world)));

            var rim = $"{hollowed}\n{Lang.Get("roughseas:shipyard-rim-clay", burnout.ClayBallsOn, burnout.ClayBalls)}";
            if (!burnout.Clayed) return new(rim, "roughseas:shipyard-clay", Stacks(() => world.SearchItems(clay)));
            if (!burnout.Fire.Kindled) return new(rim, "roughseas:shipyard-kindle", Stacks(() => world.Items.Where(item => item is ItemDryGrass)));
            if (!burnout.Fire.Laid)
            {
                return new($"{rim}\n{Lang.Get("roughseas:shipyard-firepit", burnout.Fire.OnFirepit, LaidFire.FirepitFirewood)}", "roughseas:shipyard-firewood", Stacks(() => world.Items.Where(Constructs)));
            }
            return new($"{rim}\n{Lang.Get("roughseas:shipyard-laid")}", "roughseas:shipyard-light",
                Stacks(() => BlockBehaviorCanIgnite.CanIgniteStacks(world.Api, true).Select(stack => stack.Collectible)));
        }
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

    private static Func<IEnumerable<ItemStack>> Stacks(Func<IEnumerable<CollectibleObject>> with) => () => with().Select(collectible => new ItemStack(collectible));

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
        bool charred = burning || burnout.Charred || burnout.Spoiled;
        var showing = new List<string>();
        var pieces = new List<string>();
        var parts = Hewing(level, charred, pieces).ToHashSet();
        if (!burnout.Debarked)
        {
            showing.Add(BarkGroup);
            pieces.AddRange(Enumerable.Range(1, burnout.BarkStripped).Select(n => $"{BarkGroup}{n}"));
        }
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
        var sectioned = Enumerable.Range(0, burnout.Hollowings).SelectMany(n => SectionedStages.Select(stage => $"{stage}{n}")).Append(ShellGroup);
        var hidden = sectioned.SelectMany(family => Parts(family).Any(parts.Contains) ? Parts(family).Where(part => !parts.Contains(part)) : [family]);
        return levels.Except(sectioned).Except(showing).Concat(hidden).Concat(pieces);
    }

    private IEnumerable<string> Hewing(int level, bool charred, List<string> pieces)
    {
        string before = $"Blank{level}", after = level + 1 == burnout.Hollowings ? ShellGroup : $"Blank{level + 1}";
        for (int section = 1; section <= burnout.Sections; section++)
        {
            if (burnout.Trimmed(section))
            {
                yield return $"{after}x{section}";
                continue;
            }
            yield return $"{before}x{section}";
            if (burnout.Cut(section))
            {
                pieces.Add($"{before}x{section}Inside");
                if (section < burnout.Sections && burnout.Cut(section + 1)) pieces.Add($"Hewn{level}x{section}{StepPart}");
                yield return $"Hewn{level}x{section}";
            }
            else if (charred) yield return $"Char{level}x{section}";
        }
        yield return $"{(burnout.Trimmed(1) ? after : before)}{SternPart}";
        yield return $"{(burnout.Trimmed(burnout.Sections) ? after : before)}{BowPart}";
    }

    private IEnumerable<string> Parts(string family) =>
        Enumerable.Range(1, burnout.Sections).Select(section => $"{family}x{section}").Append(family + SternPart).Append(family + BowPart);

    private IEnumerable<string> Unclayed(int level)
    {
        double coated = 0;
        foreach (var piece in Element($"Clay{level}").Children ?? [])
        {
            if (coated >= burnout.ClayVoxels || burnout.Trimmed(burnout.SectionAt(Voxels.ToMetres(piece.From[0]) - HullShape.ModelOriginMetres))) yield return piece.Name;
            coated += Burnout.TopVoxels(piece);
        }
    }

    protected override string FormKey() => $"{burnout.Level}/{burnout.Stage}";

    protected override string ShapeKey() =>
        $"{burnout.Level}/{burnout.Stage}/{burnout.BarkStripped}/{burnout.ClayVoxels}/{burnout.ClayType}/{burnout.Fire.Kindled}/{burnout.Fire.OnFirepit}/{burnout.Lit}/{burnout.BurningPhase(BurningPhases, CatchingHours)}/{burnout.HoursBurnt > 0}/{burnout.Charred}/{burnout.Spoiled}";

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

    private void Chip(ICoreClientAPI capi)
    {
        if (!burnout.Charred)
        {
            swings.Clear();
            return;
        }
        foreach (var hewer in capi.World.AllOnlinePlayers.Select(player => player.Entity).OfType<EntityPlayer>())
        {
            if (hewer.Properties.Client.AnimationsByMetaCode.GetValueOrDefault(CollectibleBehaviorHew.Swing) is not { } hewing
                || hewer.AnimManager?.GetAnimationState(CollectibleBehaviorHew.Swing) is not { Running: true } swing
                || Tree?.GetLong(HewerKey) != hewer.EntityId
                || hewer.Pos.DistanceTo(entity.Pos) > ChipReachMetres)
            {
                swings.Remove(hewer.EntityId);
                continue;
            }
            float blow = hewing.AnimationSounds?.FirstOrDefault()?.Frame ?? 0;
            bool struck = swings.TryGetValue(hewer.EntityId, out var was)
                && (was < blow ? swing.CurrentFrame >= blow || swing.CurrentFrame < was : swing.CurrentFrame < was && swing.CurrentFrame >= blow);
            swings[hewer.EntityId] = swing.CurrentFrame;
            if (struck) Chips(capi, hewer);
        }
    }

    private void Chips(ICoreClientAPI capi, Entity hewer)
    {
        var rand = capi.World.Rand;
        bool inside = burnout.SectionsCut < burnout.Sections;
        var span = config.HewnSectionsMetres[inside ? burnout.SectionsCut : burnout.Sections - burnout.SectionsTrimmed - 1];
        double along = span[0] + rand.NextDouble() * (span[1] - span[0]);
        var (_, hewerAcross) = HullShape.ToHull(hewer.Pos.X - entity.Pos.X, hewer.Pos.Z - entity.Pos.Z, entity.Pos.Yaw);
        double across = inside ? (rand.NextDouble() - 0.5) * config.HollowBeamMetres : Math.Sign(hewerAcross) * (config.HollowBeamMetres / 2 + config.WallMetres);
        double height = inside ? burnout.FloorMetres : burnout.TopMetres / 2;
        var (x, z) = HullShape.ToWorld(along, across, entity.Pos.Yaw);
        var chip = inside ? capi.World.GetItem(Charcoal) as CollectibleObject : Wood.Of(entity.Properties.Variant) is { } wood ? capi.World.GetBlock(wood.Plank) : null;
        if (chip == null) return;

        var (outX, outZ) = HullShape.ToWorld(0, inside ? 0 : Math.Sign(across) * ChipThrow, entity.Pos.Yaw);
        Shipyard.Log(entity, "{0}'s blow throws {1} chips of {2} {3:F2} m along her", hewer.GetName(), ChipsPerBlow, chip.Code, along);
        capi.World.SpawnCubeParticles(entity.Pos.XYZ.Add(x, height, z), new ItemStack(chip), ChipSpreadMetres, ChipsPerBlow, ChipScale, null,
            new Vec3f((float)outX, ChipThrow, (float)outZ));
    }

    private void Scorch()
    {
        var rand = entity.World.Rand;
        var middle = entity.Pos.XYZ.Add(0, FloorMetres, 0);
        double floor = entity.Pos.Y + FloorMetres, top = entity.Pos.Y + burnout.TopMetres;
        foreach (var victim in entity.World.GetEntitiesAround(middle, (float)config.HollowLengthMetres, (float)(top - floor) + 1, other => other != entity && other.Alive))
        {
            if (!InFire(victim, floor, top) || rand.NextDouble() >= FirepitBurnChance) continue;

            bool burnt = victim.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Block, SourceBlock = entity.World.GetBlock(LitFirepit), SourcePos = middle,
                Type = EnumDamageType.Fire }, FirepitBurnDamage);
            if (burnt) Shipyard.Log(entity, "the fire in hollow {0} burns {1} standing in it: {2:F1} damage", burnout.Level + 1, victim.GetName(), FirepitBurnDamage);
        }
    }

    private bool InFire(Entity victim, double floor, double top)
    {
        var box = victim.CollisionBox;
        if (victim.Pos.Y + box.Y2 < floor || victim.Pos.Y + box.Y1 > top) return false;

        var (along, across) = HullShape.ToHull(victim.Pos.X - entity.Pos.X, victim.Pos.Z - entity.Pos.Z, entity.Pos.Yaw);
        double reach = box.XSize / 2;
        return Math.Abs(along) < config.HollowLengthMetres / 2 + reach && Math.Abs(across) < config.HollowBeamMetres / 2 + reach;
    }

    private bool Wet() =>
        Hold?.Litres >= 1 || IHull.Of(entity)?.Afloat.Sunk == true;
}
