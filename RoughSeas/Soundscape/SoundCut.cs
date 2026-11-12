using ProtoBuf;

namespace RoughSeas.Soundscape;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SoundCut
{
    public string Location = "";
    public float UntilSeconds;
    public float Range = 32;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SoundCutPacket
{
    public SoundCut Cut = new();
    public double X;
    public double Y;
    public double Z;
}
