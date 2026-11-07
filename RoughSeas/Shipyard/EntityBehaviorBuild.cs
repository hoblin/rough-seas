using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
/// A vessel built where she lies, stage by stage, each stage drawn on her as it is done. Her build lives in her
/// watched <c>roughseas:build</c> tree; a vessel with none is finished, as is every vessel from before the build.
/// She seats nobody until she is finished.
/// </summary>
public abstract class EntityBehaviorBuild(Entity entity) : EntityBehavior(entity), IHullForm, ICarriedState
{
    public const string TreeKey = "roughseas:build";

    private const string VersionKey = "version";
    private const string StepError = "roughseas-build-step";

    private string? shownShape;
    private string? shownForm;

    public event Action? Reformed;

    public abstract bool Finished { get; }

    /// <summary>The step her build stands at, which her interaction help, her hover line and a click that does no work all name; null once she is finished.</summary>
    protected abstract BuildStep? Next { get; }

    public abstract double HoldLitres(double designed);

    public abstract double Uncut { get; }

    public abstract double FloorMetres { get; }

    protected ShipyardModSystem Shipyard { get; private set; } = null!;

    protected ITreeAttribute? Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public virtual VesselPart[] Built(VesselPart[] designed) => designed;

    public abstract Shell Formed(Shell designed);

    public virtual double UnhewnLitres => 0;

    public virtual double DepthMetres(double designed) => designed;

    public virtual double WaterplaneSquareMetres(double designed) => designed;

    public virtual BlockDropItemStack[] Wrecked(BlockDropItemStack[] designed) => designed;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        Shipyard = entity.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        if (entity.Api.Side == EnumAppSide.Client) entity.WatchedAttributes.RegisterModifiedListener(TreeKey, OnBuildChanged);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        if (seatable != null) seatable.CanSit += CanSit;
        (shownShape, shownForm) = (ShapeKey(), FormKey());
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements) =>
        willDeleteElements = (willDeleteElements ?? []).Append(Hidden().ToArray());

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        if (seatable != null) seatable.CanSit -= CanSit;
        base.OnEntityDespawn(despawn);
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || Finished || byEntity.Controls.ShiftKey) return;

        handled = EnumHandling.PreventSubsequent;
        if (entity.Api is not ICoreClientAPI capi || Next is not { } step) return;

        Shipyard.Log(entity, "{0} clicks her unfinished, holding {1}: {2}", byEntity.GetName(), itemslot.Itemstack?.Collectible.Code.ToString() ?? "nothing", step.Hint);
        capi.TriggerIngameError(this, StepError, step.Hint);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) =>
        Next is { Action: { } action } step
            ? ObjectCacheUtil.GetOrCreate(world.Api, $"{TreeKey}/help/{entity.Code}/{action}", () => new[]
            {
                new WorldInteraction { ActionLangCode = action, MouseButton = EnumMouseButton.Right, Itemstacks = step.With?.Invoke().ToArray() }
            })
            : null;

    public override void GetInfoText(StringBuilder infotext)
    {
        if (Next?.Progress is { } progress) infotext.AppendLine(progress);
    }

    public void Carry(ItemStack stack)
    {
        if (Tree != null) stack.Attributes[TreeKey] = Tree.Clone();
    }

    public void TakeFrom(ItemStack stack)
    {
        if (stack.Attributes.GetTreeAttribute(TreeKey) is not { } carried) return;

        entity.WatchedAttributes.SetAttribute(TreeKey, carried.Clone());
        MarkDirty();
    }

    protected static void Begin(Entity vessel, int version, Action<ITreeAttribute> begin)
    {
        var tree = new TreeAttribute();
        tree.SetInt(VersionKey, version);
        begin(tree);
        vessel.WatchedAttributes.SetAttribute(TreeKey, tree);
    }

    /// <summary>Brings a build saved under an older <paramref name="version"/> of her data up to it, on the server.</summary>
    protected void Upgrade(int version, Action<ITreeAttribute> upgrade)
    {
        if (entity.Api.Side != EnumAppSide.Server || Tree is not { } tree || tree.GetInt(VersionKey) >= version) return;

        Shipyard.Log(entity, "her build saved at data version {0} is brought up to {1}", tree.GetInt(VersionKey), version);
        upgrade(tree);
        tree.SetInt(VersionKey, version);
        entity.WatchedAttributes.MarkPathDirty(TreeKey);
    }

    /// <summary>The element groups the stages not reached yet draw, and those the stages passed drew.</summary>
    protected abstract IEnumerable<string> Hidden();

    /// <summary>Changes whenever what she shows changes.</summary>
    protected abstract string ShapeKey();

    /// <summary>Changes whenever what her hull is built of, or how much of her hold holds water, changes.</summary>
    protected abstract string FormKey();

    protected void MarkDirty()
    {
        entity.WatchedAttributes.MarkPathDirty(TreeKey);
        if (entity.Api.Side != EnumAppSide.Server) return;

        Reform();
        Reshape();
    }

    private void Reform()
    {
        if (FormKey() == shownForm) return;

        shownForm = FormKey();
        Reformed?.Invoke();
    }

    private void OnBuildChanged()
    {
        Reform();
        Reshape();
    }

    private void Reshape()
    {
        if (ShapeKey() == shownShape) return;

        shownShape = ShapeKey();
        entity.MarkShapeModified();
    }

    private bool CanSit(EntityAgent byEntity, out string? errorMessage)
    {
        errorMessage = null;
        return Finished;
    }
}
