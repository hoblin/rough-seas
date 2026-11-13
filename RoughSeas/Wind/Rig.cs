using System.Linq;
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
            _ => new Rig(rig["ends"].AsArray().Select((end, n) => (Rigging)new CrabClaw(RigPose.Of(end["hoisted"]), RigPose.Of(end["lowered"]), n == 0 ? -1 : 1,
                camber, (rig["yardKilograms"].AsDouble(), rig["boomKilograms"].AsDouble(), clothKilograms), widths)).ToArray())
        };
    }

    public int Tacked { get; set; }

    public Rigging End => ends[Tacked];

    public Rigging EndAt(int tacked) => ends[tacked];

    public int Ends => ends.Length;
}
