using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class EntityBehaviorLaunch(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:launch";

    private const double Gravity = 9.81;
    private static readonly AssetLocation ShoveSound = new("game:sounds/block/planks");

    private VesselsModSystem vessels = null!;
    private double pushJoules;
    private double friction;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        pushJoules = attributes["pushNewtons"].AsDouble() * attributes["strideMetres"].AsDouble();
        friction = attributes["friction"].AsDouble();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || !byEntity.Controls.ShiftKey || !itemslot.Empty || !Ashore()) return;

        handled = EnumHandling.PreventSubsequent;
        if (entity.World.Side == EnumAppSide.Server) Shove(byEntity);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) =>
        Ashore()
            ? [new WorldInteraction { ActionLangCode = "roughseas:vessel-shove", HotKeyCode = "shift", MouseButton = EnumMouseButton.Right, RequireFreeHand = true }]
            : null;

    private bool Ashore() =>
        !entity.Swimming && entity.GetBehavior<EntityBehaviorSeatable>()?.AnyMounted() != true;

    private void Shove(EntityAgent byEntity)
    {
        double kg = entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.Aboard.Total ?? entity.Properties.Weight;
        double metres = pushJoules / (friction * kg * Gravity);
        double dx = entity.Pos.X - byEntity.Pos.X, dz = entity.Pos.Z - byEntity.Pos.Z;
        double reach = Math.Sqrt(dx * dx + dz * dz);
        if (reach <= 0 || !double.IsFinite(metres) || metres <= 0 || entity.GetBehavior<EntityBehaviorPassivePhysics>() is not { } physics) return;

        double speed = metres * physics.GroundDragValue / reach;
        entity.Pos.Motion.Add(dx * speed, 0, dz * speed);
        entity.World.PlaySoundAt(ShoveSound, entity, null, true, 16);
        vessels.Log(entity, "shoved by {0}: {1:F0} kg slides {2:F2} m", byEntity.GetName(), kg, metres);
    }
}
