using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Vanilla's boat seat, whose rider faces the way its mount rotation turns it. Vanilla turns only the limits the
/// rider's body and head are held in, while the seat still faces the bow, so a rider of a seat turned astern sits
/// askew at the edge of those limits.
/// </summary>
public class VesselSeat(IMountable mountable, string seatId, SeatConfig config) : EntityBoatSeat(mountable, seatId, config)
{
    public override EntityPos SeatPosition
    {
        get
        {
            var position = base.SeatPosition;
            position.Yaw += config.MountRotation.Y * GameMath.DEG2RAD;
            return position;
        }
    }

    public override void DidMount(EntityAgent entityAgent)
    {
        base.DidMount(entityAgent);
        if (entityAgent.Api is ICoreClientAPI capi && capi.World.Player.Entity.EntityId == entityAgent.EntityId)
            capi.Input.MouseYaw = SeatPosition.Yaw;
    }
}
