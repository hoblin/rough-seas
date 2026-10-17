using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

public class BlockBehaviorBailer(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:bailer";

    private const string ScoopsKey = "roughseas:scoops";
    private static readonly AssetLocation PourSound = new("game:sounds/effect/water-pour");

    private double secondsPerScoop;

    /// <summary>
    /// Plays one cycle a scoop only while, in <c>patches/player-poses.json</c>, its <c>animationSpeed</c> stays
    /// 1 / <see cref="secondsPerScoop"/> and <c>roughseas-bail</c> stays 30 frames, one second at speed 1.
    /// </summary>
    private string? animation;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        secondsPerScoop = properties["secondsPerScoop"].AsDouble(1);
        animation = properties["animation"].AsString();
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || !Empty(slot) || Bilge(byEntity, entitySel) is not { Litres: > 0 }) return;

        byEntity.Attributes.SetInt(ScoopsKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Bilge(byEntity, entitySel) is not { Litres: > 0 } bilge) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return true;

        var scoops = (int)(secondsUsed / secondsPerScoop);
        var done = byEntity.Attributes.GetInt(ScoopsKey);
        if (scoops <= done) return true;

        byEntity.Attributes.SetInt(ScoopsKey, scoops);
        bilge.Bail(((BlockLiquidContainerBase)block).CapacityLitres * (scoops - done));
        byEntity.World.PlaySoundAt(PourSound, byEntity, null, true, 16);
        Throw(byEntity);
        return bilge.Litres > 0;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Bilge(byEntity, entitySel) != null) handling = EnumHandling.PreventDefault;
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        if (forEntity is not EntityAgent agent || !Empty(activeHotbarSlot)) return null;
        if (Bilge(agent, (agent as EntityPlayer)?.Player?.CurrentEntitySelection) is not { Litres: > 0 }) return null;

        bhHandling = EnumHandling.PreventDefault;
        return animation;
    }

    private static void Throw(EntityAgent byEntity)
    {
        var pos = byEntity.Pos;
        var side = new Vec3d(Math.Cos(pos.Yaw), 0, -Math.Sin(pos.Yaw));
        var at = pos.XYZ.Add(side.X * 0.8, 1, side.Z * 0.8);
        var water = new SimpleParticleProperties(12, 20, ColorUtil.ToRgba(180, 140, 170, 190),
            at, at.AddCopy(0.2, 0.2, 0.2),
            new Vec3f((float)side.X * 1.5f, 1.2f, (float)side.Z * 1.5f), new Vec3f((float)side.X * 3f, 2.4f, (float)side.Z * 3f),
            1f, 1f, 0.3f, 0.6f, EnumParticleModel.Quad)
        {
            WindAffected = true
        };
        byEntity.World.SpawnParticles(water);
    }

    private bool Empty(ItemSlot slot) =>
        block is BlockLiquidContainerBase container && slot.Itemstack is { } stack && container.GetContent(stack) == null;

    private static EntityBehaviorBilge? Bilge(EntityAgent byEntity, EntitySelection? entitySel) =>
        entitySel?.Entity?.GetBehavior<EntityBehaviorBilge>() ?? byEntity.MountedOn?.Entity?.GetBehavior<EntityBehaviorBilge>();
}
