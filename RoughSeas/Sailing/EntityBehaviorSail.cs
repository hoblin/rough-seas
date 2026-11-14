using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using RoughSeas.Wind;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Sailing;

public class EntityBehaviorSail(Entity entity) : EntityBehavior(entity), IRigged
{
    public const string Code = "roughseas:sail";
    public const string TackedKey = "roughseas:tacked";

    private const string SailKey = "sailPosition";
    private const string ShapeKey = "roughseas:rig";

    private SailingModSystem sailing = null!;
    private EntityBehaviorWindage? windage;
    private RigRenderer? drawn;
    private string[] drawnGroups = [];
    private string[] steps = [];
    private string cloth = "";
    private Dictionary<string, string> parts = new();
    private IHullForm[] forms = [];
    private bool trimmed;
    private RigState? was;
    private bool wasShunting;
    private byte[]? shownBytes;
    private byte[]? published;
    private long loggedAt;

    public Sail Sail { get; private set; } = null!;

    public IAloft Aloft => Sail;

    public bool Rigged => forms.All(form => form.Finished);

    public (double X, double Z) Blowing => windage?.Blowing ?? default;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        sailing = entity.Api.ModLoader.GetModSystem<SailingModSystem>();
        Sail = Sail.Of(attributes, sailing.Config, entity.Api.ModLoader.GetModSystem<WindModSystem>().Config)
            ?? throw new InvalidOperationException($"{entity.Code}'s {Code} names no rig");
        drawnGroups = attributes["drawn"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        steps = attributes["steps"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        parts = attributes["parts"].AsObject<Dictionary<string, string>?>(null) ?? new();
        cloth = attributes["cloth"].AsString() ?? throw new InvalidOperationException($"{entity.Code}'s sail names no cloth texture: {Code} needs cloth");
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        windage = entity.GetBehavior<EntityBehaviorWindage>();
        forms = entity.SidedProperties.Behaviors.OfType<IHullForm>().ToArray();
        if (entity.WatchedAttributes.GetBytes(ShapeKey) is { } saved) Sail.Show(SerializerUtil.Deserialize<RigShape>(saved));
        if (entity.Api is ICoreClientAPI capi) drawn = new RigRenderer(capi, entity, this, cloth, parts);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (drawn == null || !Rigged) return;

        drawn.Shaped(entityShape.Clone());
        willDeleteElements = (willDeleteElements ?? []).Concat(drawnGroups).ToArray();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || entity.Api.Side != EnumAppSide.Server || !Rigged
            || byEntity is not EntityPlayer player || entity.GetBehavior<EntityBehaviorSelectionBoxes>() is not { } boxes) return;

        var step = Array.FindIndex(steps, code => boxes.IsAPCode(player.EntitySelection, code));
        if (step < 0 || step == entity.WatchedAttributes.GetInt(TackedKey) || Sail.Shunting) return;

        entity.WatchedAttributes.SetInt(TackedKey, step);
        handled = EnumHandling.PreventSubsequent;
        sailing.Log(entity, "{0} shunts her: her tack goes to the step at her {1}, her mast rakes toward it", player.GetName(), step == 0 ? "bow" : "stern");
    }

    public override void OnGameTick(float deltaTime)
    {
        if (drawn == null) return;

        var helm = entity.GetInterface<IMountable>()?.Controller;
        if (!trimmed && (helm == null || helm != ((ICoreClientAPI)entity.Api).World.Player.Entity)
            && entity.WatchedAttributes.GetBytes(ShapeKey) is { } bytes && !ReferenceEquals(bytes, shownBytes))
        {
            Sail.Show(SerializerUtil.Deserialize<RigShape>(bytes));
            shownBytes = bytes;
        }
        trimmed = false;
    }

    public void Trim()
    {
        (was, wasShunting) = (Sail.State, Sail.Shunting);
        Sail.Hoist(entity.WatchedAttributes.GetInt(SailKey), entity.WatchedAttributes.GetInt(TackedKey));
        trimmed = true;
    }

    public void Trimmed(double seconds)
    {
        if (Sail.Shunting != wasShunting)
            sailing.Log(entity, "{0} her shunt to her {1} end: {2}", Sail.Shunting ? "begins" : "ends", entity.WatchedAttributes.GetInt(TackedKey) == 0 ? "bow" : "stern",
                Sail.Rig.End.Describe(Sail.State));
        if (was != null && Sail.Rig.End.Marks(Sail.State).Zip(Sail.Rig.End.Marks(was)).Max(pair => (pair.First - pair.Second).Length) is var moved
            && moved > sailing.Config.JumpMetresPerSecond * seconds)
            sailing.Log(entity, "her rig moved {0:F2} m in one {1:F3} s tick: {2}", moved, seconds, Sail.Rig.End.Describe(Sail.State));
        Note();
        var shape = Sail.Shape(entity.EntityId);
        if (!Changed(shape)) return;

        if (entity.Api.Side == EnumAppSide.Server) Published(published!);
        else sailing.Report(shape);
    }

    private void Note()
    {
        var now = entity.World.ElapsedMilliseconds;
        if (now - loggedAt < sailing.Config.LogSeconds * 1000) return;

        loggedAt = now;
        if (Sail.Pressure > 0) sailing.Log(entity, "her sail {0} on tack {1}", Sail.Aback ? "aback" : "drawing", Sail.Rig.Tacked);
        if (Sail is { Pressure: > 0, Load: { } rig })
            sailing.Log(entity, "her rig holds the sail: sheet {0:F0} N, halyard {1:F0} N, {2}", rig.SheetNewtons, rig.HalyardNewtons,
                string.Join(", ", rig.Stays.Select(stay => $"{stay.Line} {stay.Newtons:F0} N")));
        if (Sail.SquareMetres > 0) sailing.Log(entity, "her rig stands: {0}", Sail.Rig.End.Describe(Sail.State));
    }

    public void Reported(IServerPlayer player, RigShape shape)
    {
        if (entity.GetInterface<IMountable>()?.Controller != player.Entity) return;

        Sail.Show(shape);
        Published(SerializerUtil.Serialize(shape));
    }

    private void Published(byte[] bytes)
    {
        entity.WatchedAttributes.SetBytes(ShapeKey, bytes);
        entity.WatchedAttributes.MarkPathDirty(ShapeKey);
    }

    private bool Changed(RigShape shape)
    {
        var bytes = SerializerUtil.Serialize(shape);
        if (published != null && bytes.AsSpan().SequenceEqual(published)) return false;

        published = bytes;
        return true;
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        drawn?.Dispose();
        drawn = null;
    }
}
