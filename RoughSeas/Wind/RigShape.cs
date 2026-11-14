using ProtoBuf;

namespace RoughSeas.Wind;

[ProtoContract]
public class RigShape
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public int Tacked;
    [ProtoMember(3)] public double Yard;
    [ProtoMember(4)] public double Boom;
    [ProtoMember(5)] public double Fill;
    [ProtoMember(6)] public double Hoisted;
    [ProtoMember(7)] public double Reach;
    [ProtoMember(8)] public double Span;
    [ProtoMember(9)] public double Slack;
    [ProtoMember(10)] public bool Aback;
    [ProtoMember(11)] public int Toward = -1;
    [ProtoMember(12)] public double StartYard;
    [ProtoMember(13)] public double StartBoom;
    [ProtoMember(14)] public bool StartRolled;
    [ProtoMember(15)] public double ShuntSeconds;
    [ProtoMember(16)] public double FromYard;
    [ProtoMember(17)] public double FromBoom;
    [ProtoMember(18)] public bool Hoisting;
}
