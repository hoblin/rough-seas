using ProtoBuf;

namespace RoughSeas.Storms;

[ProtoContract]
public class MooringReport
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public double Newtons;
    [ProtoMember(3)] public int Line;
}
