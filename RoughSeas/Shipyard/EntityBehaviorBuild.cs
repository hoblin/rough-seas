using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
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
    private const string Unfinished = "roughseas-unfinished";

    private string? shownShape;
    private string? shownForm;

    public event Action? Reformed;

    public abstract bool Finished { get; }

    public abstract double Hollowed { get; }

    public abstract double Uncut { get; }

    public abstract double FloorMetres { get; }

    protected ShipyardModSystem Shipyard { get; private set; } = null!;

    protected ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public virtual VesselPart[] Built(VesselPart[] designed) => designed;

    public abstract Shell Formed(Shell designed);

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

    /// <summary>The element groups the stages not reached yet draw, and those the stages passed drew.</summary>
    protected abstract IEnumerable<string> Hidden();

    /// <summary>Changes whenever what she shows changes.</summary>
    protected abstract string ShapeKey();

    /// <summary>Changes whenever her hull's form, what it is built of or what its hold holds, changes.</summary>
    protected abstract string FormKey();

    protected void MarkDirty()
    {
        entity.WatchedAttributes.MarkPathDirty(TreeKey);
        if (entity.Api.Side == EnumAppSide.Server) Reform();
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
        if (ShapeKey() == shownShape) return;

        shownShape = ShapeKey();
        entity.MarkShapeModified();
    }

    private bool CanSit(EntityAgent byEntity, out string? errorMessage)
    {
        errorMessage = Finished ? null : Unfinished;
        return Finished;
    }
}
