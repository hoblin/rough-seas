using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// The one vanilla rope a vessel is tied by. Vanilla keeps the tying, the length the player sets, the sag, the sync and
/// the save; while the rope holds her to a post, our mooring holds and parts it. See docs/vanilla-at-sea.md.
/// </summary>
[ReliesOn("Vintagestory.GameContent.ClothManager", "tickPhysics", "a rope stretched past StretchRip for a moment tears")]
[ReliesOn("Vintagestory.GameContent.ClothPoint", "update", "CanPull drives the rope's pull")]
public sealed class VesselRope(Entity vessel, ClothManager cloths)
{
    private const float VanillaStretchRip = 0.75f;
    private const float PinHeight = 0.5f;
    private static readonly AssetLocation RopeItem = new("game:rope");

    private readonly EntityBehaviorRopeTieable? tieable = vessel.GetBehavior<EntityBehaviorRopeTieable>();

    /// <summary>The rope tied to her, if any: by her rope ids on the server, by the rope's pins on a client, which is never sent her ids.</summary>
    public ClothSystem? Tied =>
        vessel.World.Side == EnumAppSide.Server
            ? tieable?.ClothIds?.value.Select(cloths.GetClothSystem).FirstOrDefault(Holds)
            : cloths.GetClothSystemAttachedToEntity(vessel.EntityId);

    /// <summary>
    /// Server only: forgets the ids that now name another vessel's or animal's rope. Vanilla leaves a torn rope's id
    /// behind, and its interaction hands whatever rope later takes that id to the player who clicks her.
    /// </summary>
    public void Prune()
    {
        if (tieable?.ClothIds is not { } ids) return;

        foreach (var id in ids.value.Where(id => cloths.GetClothSystem(id) is { } rope && !Holds(rope)).ToArray()) ids.RemoveInt(id);
    }

    private bool Holds(ClothSystem? rope) => rope != null && rope.Ends.Any(end => end.pinnedToEntityId == vessel.EntityId);

    /// <summary>The block it is tied to; vanilla keeps an end's block when the block is broken and the end comes loose.</summary>
    public static BlockPos? Post(ClothSystem rope) => rope.Ends.FirstOrDefault(end => end.Pinned && end.PinnedToBlockPos != null)?.PinnedToBlockPos;

    public static double RestMetres(ClothSystem rope)
    {
        var points = 0;
        rope.WalkPoints(_ => points++);
        return (points - 1) / rope.Resolution;
    }

    /// <summary>
    /// Vanilla stops pulling and tearing it; not <see cref="ClothSystem.CanRip"/>, which also stops vanilla sending its
    /// pins to clients. On the server, given her bow, its end is made fast there, where the mooring holds her.
    /// </summary>
    public void TakeOver(ClothSystem rope, (double X, double Z)? bow)
    {
        rope.CanPull = false;
        rope.StretchRip = float.MaxValue;
        if (bow is { } at && vessel.World.Side == EnumAppSide.Server && rope.Ends.FirstOrDefault(end => end.pinnedToEntityId == vessel.EntityId) is { } end)
        {
            end.PinTo(vessel, new Vec3f((float)at.X, PinHeight, (float)at.Z));
        }
    }

    public static void GiveBack(ClothSystem rope)
    {
        rope.CanPull = true;
        rope.StretchRip = VanillaStretchRip;
    }

    /// <summary>Server only: the rope parts, leaves her, and falls by its post.</summary>
    public void Part(ClothSystem rope)
    {
        var post = Post(rope);
        tieable?.Detach(rope);
        cloths.UnregisterCloth(rope.ClothId);
        if (post != null && vessel.World.GetItem(RopeItem) is { } item) vessel.World.SpawnItemEntity(new ItemStack(item), post.ToVec3d().Add(0.5, 1, 0.5));
    }
}
