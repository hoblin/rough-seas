using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed record RigPose(
    HullPoint Tack, HullPoint Masthead, HullPoint Heel, HullPoint HalyardBend, HullPoint Peak, HullPoint Clew, HullPoint Bridle,
    HullPoint Lead, HullPoint SternPin, HullPoint ShroudFoot, HullPoint ShoreTop, HullPoint ShoreFoot)
{
    public static RigPose Of(JsonObject pose)
    {
        HullPoint At(string name) => HullPoint.Of(pose[name + "Metres"]);
        return new RigPose(At("tack"), At("masthead"), At("heel"), At("halyardBend"), At("peak"), At("clew"), At("bridle"),
            At("lead"), At("sternPin"), At("shroudFoot"), At("shoreTop"), At("shoreFoot"));
    }
}
