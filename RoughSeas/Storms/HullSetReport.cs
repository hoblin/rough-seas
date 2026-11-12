using ProtoBuf;

namespace RoughSeas.Storms;

[ProtoContract]
public class HullSetReport
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public double X;
    [ProtoMember(3)] public double Z;
}
