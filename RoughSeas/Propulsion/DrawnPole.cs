using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public static class DrawnPole
{
    private const string HarmonyId = "roughseas.propulsion.drawnpole";
    private static readonly Vec4f AlongThePole = new(1, 0, 0, 0);

    private static Harmony? harmony;
    private static PropulsionModSystem? propulsion;

    public static void Attach(PropulsionModSystem system)
    {
        propulsion = system;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityShapeRenderer), "RenderItem"), prefix: new HarmonyMethod(typeof(DrawnPole), nameof(Slid)));
    }

    public static void Detach(PropulsionModSystem system)
    {
        if (propulsion != system) return;
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        propulsion = null;
    }

    private static void Slid(EntityShapeRenderer __instance, ItemStack stack, AttachmentPointAndPose apap, ItemRenderInfo renderInfo)
    {
        if (propulsion == null || renderInfo?.Transform == null || apap?.AttachPoint is not { } point ||
            __instance.entity is not EntityAgent { MountedOn.Entity: { } vessel } rower || rower.RightHandItemSlot?.Itemstack != stack ||
            vessel.GetBehavior<EntityBehaviorPropulsion>() is not { } propelled ||
            Tool.Of(stack, propulsion.Config, _ => 0) is not Pole pole)
        {
            return;
        }

        var held = renderInfo.Transform;
        var turning = new Matrixf().Rotate(
            (float)(point.RotationX + held.Rotation.X) * GameMath.DEG2RAD,
            (float)(point.RotationY + held.Rotation.Y) * GameMath.DEG2RAD,
            (float)(point.RotationZ + held.Rotation.Z) * GameMath.DEG2RAD);
        var along = turning.TransformVector(AlongThePole);
        var drawn = new Matrixf().Set(__instance.ModelMat).Mul(apap.AnimModelMatrix).TransformVector(along);
        var length = GameMath.Sqrt(drawn.X * drawn.X + drawn.Y * drawn.Y + drawn.Z * drawn.Z);
        if (length <= 0 || propelled.PoleSlide(rower, pole.Metres, -drawn.Y / length) is not { } slide) return;

        var slid = held.Clone();
        slid.Translation = new FastVec3f(held.Translation.X + (float)slide * along.X, held.Translation.Y + (float)slide * along.Y,
            held.Translation.Z + (float)slide * along.Z);
        renderInfo.Transform = slid;
    }
}
