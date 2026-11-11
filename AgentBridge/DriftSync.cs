using ProtoBuf;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AgentBridge;

/// <summary>
/// A steered vessel's drift runs on her rider's client, a process of its own beside a dedicated server, so
/// <c>/bridge drift</c> and <c>/bridge current</c> reach it over this channel. On one process the switches are the same
/// statics, and nothing needs sending.
/// </summary>
public class DriftSync : ModSystem
{
    private const string Channel = "agentbridge:drift";

    private IServerNetworkChannel? serverChannel;
    private DriftSwitch? clientDrift;
    private CurrentOverride? clientCurrent;

    public override void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(Channel).RegisterMessageType<DriftState>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Network.GetChannel(Channel).SetMessageHandler<DriftState>(Received);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverChannel = api.Network.GetChannel(Channel);
    }

    /// <summary>Tells one player, or every player, what the server's switches say now.</summary>
    public void Send(bool drifting, bool wavesOnly, (double X, double Z)? current, IServerPlayer? only = null)
    {
        var state = new DriftState { Drifting = drifting, WavesOnly = wavesOnly, HasCurrent = current != null, X = current?.X ?? 0, Z = current?.Z ?? 0 };
        if (only != null) serverChannel?.SendPacket(state, only);
        else serverChannel?.BroadcastPacket(state);
    }

    private void Received(DriftState state)
    {
        if (api is ICoreClientAPI { IsSinglePlayer: true }) return;

        clientDrift ??= new DriftSwitch();
        clientCurrent ??= new CurrentOverride();
        clientDrift.Drifting = state.Drifting;
        clientDrift.WavesOnly = state.WavesOnly;
        clientCurrent.Set(state.X, state.Z, state.HasCurrent);
        Mod.Logger.Debug($"[bridge] drift {state.Drifting}, current {(state.HasCurrent ? $"{state.X:F2} {state.Z:F2}" : "the sea's")}");
    }

    public override void Dispose()
    {
        clientDrift?.Dispose();
        clientCurrent?.Dispose();
    }

    private ICoreAPI? api;

    public override void StartPre(ICoreAPI api) => this.api = api;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class DriftState
{
    public bool Drifting;
    public bool WavesOnly;
    public bool HasCurrent;
    public double X;
    public double Z;
}
