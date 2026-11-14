using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace AgentBridge;

/// <summary>
/// The game reads jump, sneak and sprint only while the mouse is grabbed, which an unfocused client never has; this
/// reads them there as a focused client does, so the bench's held keys jump, sneak and sprint.
/// </summary>
public sealed class UnfocusedKeys : IDisposable
{
    private readonly Harmony harmony = new("agentbridge.keys");
    private static readonly MethodInfo Grabbed = AccessTools.PropertyGetter("Vintagestory.Client.NoObf.ClientMain:MouseGrabbed");

    public UnfocusedKeys()
    {
        harmony.Patch(AccessTools.Method("Vintagestory.Client.NoObf.SystemPlayerControl:OnGameTick"),
            transpiler: new HarmonyMethod(typeof(UnfocusedKeys), nameof(Transpile)));
    }

    public void Dispose() => harmony.UnpatchAll(harmony.Id);

    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(Grabbed))
            {
                replaced++;
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(UnfocusedKeys), nameof(GrabbedOrUnfocused)));
                continue;
            }
            yield return instruction;
        }
        if (replaced == 0) throw new InvalidOperationException("SystemPlayerControl.OnGameTick no longer asks ClientMain.MouseGrabbed");
    }

    private static bool GrabbedOrUnfocused(object game)
    {
        var client = Traverse.Create(game);
        return !client.Field("Platform").Property("IsFocused").GetValue<bool>() || client.Property("MouseGrabbed").GetValue<bool>();
    }
}
