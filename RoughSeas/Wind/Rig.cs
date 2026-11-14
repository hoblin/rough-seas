using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class Rig(Rigging[] ends)
{
    public static Rig? Of(JsonObject rig, double camber, double clothKilograms)
    {
        if (!rig.Exists) return null;

        var widths = new RigWidths(rig["sparMetres"].AsDouble(), rig["poleMetres"].AsDouble(), rig["shoreMetres"].AsDouble(), rig["lineMetres"].AsDouble(),
            rig["rollMetres"].AsDouble());
        return rig["kind"].AsString() switch
        {
            SquareRig.Kind => new Rig([SquareRig.Of(rig, camber, clothKilograms, widths)]),
            CrabClaw.Kind => new Rig(CrabClaw.Of(rig, camber, clothKilograms, widths)),
            var kind => throw new InvalidOperationException($"no rig of kind '{kind}'")
        };
    }

    public int Tacked
    {
        get => tacked;
        set => tacked = Math.Clamp(value, 0, ends.Length - 1);
    }

    private int tacked;

    public Rigging End => ends[Tacked];

    public (double Reach, double Span) Set
    {
        set
        {
            foreach (var end in ends) end.Set = value;
        }
    }

    public Rigging EndAt(int end) => ends[end];
}
