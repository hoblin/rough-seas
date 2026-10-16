using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public static class HullReach
{
    private const string HarmonyId = "roughseas.vessels.hullreach";

    private static readonly string[] RangeChecks =
    [
        "Vintagestory.Client.NoObf.ClientMain:TryAttackEntity",
        "Vintagestory.Server.ServerSystemEntitySimulation:HandleEntityInteraction",
        "Vintagestory.Server.ServerPlayer:isInInteractionRangeOf"
    ];

    private static readonly FieldInfo SelectionBoxField = AccessTools.Field(typeof(Entity), nameof(Entity.SelectionBox));
    private static readonly object patchLock = new();
    private static readonly Matrixf transform = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            foreach (var check in RangeChecks)
            {
                harmony.Patch(AccessTools.Method(check), transpiler: new HarmonyMethod(typeof(HullReach), nameof(MeasureToHull)));
            }
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    public static Cuboidf Box(Entity entity)
    {
        if (!entity.HasBehavior<EntityBehaviorVesselIntegrity>() ||
            entity.GetBehavior<EntityBehaviorSelectionBoxes>() is not { selectionBoxes.Length: > 0 } hull)
        {
            return entity.SelectionBox;
        }

        if (entity.Api is ICoreClientAPI capi)
        {
            var selection = capi.World.Player?.CurrentEntitySelection;
            if (selection?.Entity != entity || selection.HitPosition == null) return entity.SelectionBox;

            var hit = selection.HitPosition;
            return new Cuboidf((float)hit.X, (float)hit.Y, (float)hit.Z, (float)hit.X, (float)hit.Y, (float)hit.Z);
        }

        return Bounds(entity, hull.selectionBoxes);
    }

    private static IEnumerable<CodeInstruction> MeasureToHull(IEnumerable<CodeInstruction> instructions) =>
        instructions.Select(instruction => instruction.LoadsField(SelectionBoxField)
            ? new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = AccessTools.Method(typeof(HullReach), nameof(Box)) }
            : instruction);

    private static Cuboidf Bounds(Entity entity, AttachmentPointAndPose[] boxes)
    {
        var bounds = new Cuboidf(float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue);
        lock (transform)
        {
            foreach (var box in boxes)
            {
                var element = box.AttachPoint.ParentElement;
                var size = element.To!.Zip(element.From!, (to, from) => (float)(to - from) / 16).ToArray();
                transform.Identity()
                    .RotateY(GameMath.PIHALF + entity.Pos.Yaw)
                    .Translate(-0.5f, 0, -0.5f)
                    .Mul(box.AnimModelMatrix)
                    .Scale(size[0], size[1], size[2]);

                for (int corner = 0; corner < 8; corner++)
                {
                    var at = transform.TransformVector(new Vec4d(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1, 1));
                    bounds.X1 = Math.Min(bounds.X1, (float)at.X);
                    bounds.Y1 = Math.Min(bounds.Y1, (float)at.Y);
                    bounds.Z1 = Math.Min(bounds.Z1, (float)at.Z);
                    bounds.X2 = Math.Max(bounds.X2, (float)at.X);
                    bounds.Y2 = Math.Max(bounds.Y2, (float)at.Y);
                    bounds.Z2 = Math.Max(bounds.Z2, (float)at.Z);
                }
            }
        }
        return bounds;
    }
}
