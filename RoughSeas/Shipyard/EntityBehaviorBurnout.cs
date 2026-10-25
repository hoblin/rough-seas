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
    private const int DataVersion = 1;
    private const float TickSeconds = 1;
    private const float ParticleSeconds = 0.1f;
    private const int FlamesPerPuff = 3;
    private const double MinDousingRain = 0.05;
    private const double FlameReach = 0.8;
    private const string Unfinished = "roughseas-unfinished";
    private static readonly string[] Stages = ["Blank", "Char", "Fire"];

    private static readonly AssetLocation FireBlock = new("game:fire");
    private static readonly AssetLocation FireSound = new("game:sounds/environment/fire");
    private static readonly AssetLocation IgniteSound = new("game:sounds/torch-ignite");
    private static readonly AssetLocation ClaySound = new("game:sounds/block/dirt1");

    private BurnoutConfig config = null!;
    private Burnout burnout = null!;
    private ShipyardModSystem shipyard = null!;
    private ISeaWeather sea = null!;
    private AssetLocation clay = null!;
    private float sinceTick;
    private float sinceFlames;
    private string? shown;
    private int shownLevel;
    private ILoadedSound? fire;

    public event Action? Reformed;

    public double Hollowed => burnout.Hollowed;

    public Burnout Burnout => burnout;

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        config = attributes.AsObject<BurnoutConfig>(new BurnoutConfig());
        burnout = new Burnout(() => Tree, MarkDirty, config);
        clay = new AssetLocation(config.Clay);
        shipyard = entity.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        if (entity.Api.Side == EnumAppSide.Client) entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnBuildChanged);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        if (seatable != null) seatable.CanSit += CanSit;
        (shown, shownLevel) = (Visible(), burnout.Level);
    }

    public static void Begin(Entity vessel, double hours)
    {
        var tree = new TreeAttribute();
        tree.SetInt(VersionKey, DataVersion);
        tree.SetDouble(CheckedAtKey, hours);
        Burnout.Begin(tree);
        vessel.WatchedAttributes.SetAttribute(TreeKey, tree);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements) =>
        willDeleteElements = (willDeleteElements ?? []).Append(Hidden().ToArray());

    public override void OnGameTick(float deltaTime)
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            Flicker(capi, deltaTime);
            return;
        }
        if ((sinceTick += deltaTime) < TickSeconds || !burnout.Lit) return;

        sinceTick = 0;
        double now = sea.Clock.Hours;
        double hours = now - Tree.GetDouble(CheckedAtKey, now);
        Tree.SetDouble(CheckedAtKey, now);
        bool clayed = burnout.Clayed;
        if (burnout.Burn(hours))
        {
            shipyard.Log(entity, "the fire burns out after {0:F0} h: hollow {1} of {2} charred{3}", burnout.BurnHours, burnout.Level + 1, burnout.Hollowings, clayed ? ", the clay spent" : "");
            return;
        }
        if (Rain() is var rain and > MinDousingRain && entity.World.Rand.NextDouble() < rain / 2)
        {
            burnout.Douse();
            shipyard.Log(entity, "rain puts the fire out at {0:F1} of {1:F0} h", burnout.BurntHours, burnout.BurnHours);
        }
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || burnout.Finished || StepWith(itemslot) is not { } step) return;

        handled = EnumHandling.PreventSubsequent;
        var refusal = step switch
        {
            Step.Clay => burnout.ClayRefusal(),
            Step.Light => Wet() ? "shipyard-bail-first" : burnout.LightRefusal(),
            _ => burnout.HewRefusal()
        };
        if (refusal != null)
        {
            (entity.Api as ICoreClientAPI)?.TriggerIngameError(this, refusal, Lang.Get("roughseas:" + refusal));
            return;
        }
        if (entity.World.Side != EnumAppSide.Server) return;

        switch (step)
        {
            case Step.Clay:
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
                burnout.Clay();
                entity.World.PlaySoundAt(ClaySound, entity, null, true, 16);
                shipyard.Log(entity, "{0} smears clay along the rim", byEntity.GetName());
                break;
            case Step.Light:
                if (itemslot.Itemstack!.Collectible is ItemFirestarter) itemslot.Itemstack.Collectible.DamageItem(entity.World, byEntity, itemslot);
                Tree.SetDouble(CheckedAtKey, sea.Clock.Hours);
                burnout.Light();
                entity.World.PlaySoundAt(IgniteSound, entity, null, true, 16);
                shipyard.Log(entity, "{0} lights a fire in hollow {1} of {2}: {3:F0} h to burn", byEntity.GetName(), burnout.Level + 1, burnout.Hollowings, burnout.BurnHours - burnout.BurntHours);
                break;
            default:
                itemslot.Itemstack!.Collectible.DamageItem(entity.World, byEntity, itemslot);
                byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(config.HewSaturation);
                burnout.Hew();
                entity.World.PlaySoundAt(new AssetLocation($"game:sounds/block/chop{1 + entity.World.Rand.Next(3)}"), entity, null, true, 16);
                shipyard.Log(entity, burnout.Finished ? "{0} hews out the last char: she is finished" : "{0} hews out the char: hollow {1} of {2} cut", byEntity.GetName(), burnout.Level, burnout.Hollowings);
                Reformed?.Invoke();
                break;
        }
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (burnout.Finished || burnout.Lit) return null;
        if (burnout.Charred) return [Help("roughseas:shipyard-hew", Axes(world))];
        if (burnout.NeedsClay && !burnout.Clayed) return [Help("roughseas:shipyard-clay", world.SearchItems(clay).Select(item => new ItemStack(item)).ToArray())];
        return [Help("roughseas:shipyard-light", BlockBehaviorCanIgnite.CanIgniteStacks(world.Api, true).ToArray())];
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        if (!burnout.Finished)
        {
            infotext.AppendLine(Lang.Get("roughseas:shipyard-hollowed", burnout.Level, burnout.Hollowings));
            if (burnout.Lit) infotext.AppendLine(Lang.Get("roughseas:shipyard-burns", Math.Ceiling(burnout.BurnHours - burnout.BurntHours)));
            else if (burnout.Charred) infotext.AppendLine(Lang.Get("roughseas:shipyard-charred"));
            else if (burnout.Clayed) infotext.AppendLine(Lang.Get("roughseas:shipyard-clayed"));
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

    private enum Step { Clay, Light, Hew }

    private Step? StepWith(ItemSlot slot) => slot.Itemstack?.Collectible switch
    {
        null => null,
        { Tool: EnumTool.Axe } => Step.Hew,
        ItemFirestarter => Step.Light,
        Block block when block.HasBehavior<BlockBehaviorCanIgnite>() => Step.Light,
        var collectible when WildcardUtil.Match(clay, collectible.Code) => Step.Clay,
        _ => null
    };

    private static WorldInteraction Help(string action, ItemStack[] stacks) =>
        new() { ActionLangCode = action, MouseButton = EnumMouseButton.Right, Itemstacks = stacks };

    private static ItemStack[] Axes(IWorldAccessor world) =>
        world.Items.Where(item => item.Tool == EnumTool.Axe && item.Code != null).Select(item => new ItemStack(item)).ToArray();

    private bool CanSit(EntityAgent byEntity, out string? errorMessage)
    {
        errorMessage = burnout.Finished ? null : Unfinished;
        return burnout.Finished;
    }

    private IEnumerable<string> Hidden()
    {
        if (burnout.Finished) return [.. Stages, "Clay"];

        var level = burnout.Level;
        var shown = new List<string> { $"Blank{level}" };
        if (burnout.Lit || burnout.Charred) shown.Add($"Char{level}");
        if (burnout.Lit) shown.Add($"Fire{level}");
        return Enumerable.Range(0, burnout.Hollowings)
            .SelectMany(n => Stages.Select(stage => $"{stage}{n}"))
            .Except(shown)
            .Concat(burnout.Clayed ? [] : ["Clay"]);
    }

    private string Visible() => $"{burnout.Level}/{burnout.Clayed}/{burnout.Lit}/{burnout.Charred}";

    private void OnBuildChanged()
    {
        if (burnout.Level != shownLevel)
        {
            shownLevel = burnout.Level;
            Reformed?.Invoke();
        }
        if (Visible() == shown) return;

        shown = Visible();
        entity.MarkShapeModified();
    }

    private void Flicker(ICoreClientAPI capi, float deltaTime)
    {
        if (!burnout.Lit)
        {
            fire?.Stop();
            return;
        }

        var at = entity.Pos.XYZ.Add(0, config.RimMetres - config.HollowDepthMetres * burnout.Hollowed, 0);
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

    private double Rain()
    {
        var pos = entity.Pos;
        if (entity.World.BlockAccessor.GetRainMapHeightAt((int)pos.X, (int)pos.Z) > pos.Y) return 0;

        return entity.Api.ModLoader.GetModSystem<WeatherSystemBase>()?.GetPrecipitation(pos.XYZ) ?? 0;
    }

    private bool Wet() => entity.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault()?.Litres >= 1;

    private void MarkDirty() => entity.WatchedAttributes.MarkPathDirty(TreeKey);
}
