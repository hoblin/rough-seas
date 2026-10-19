using ProtoBuf;

namespace RoughSeas.Vessels;

[ProtoContract]
public class ImpactReport
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public double SpeedLost;
}
