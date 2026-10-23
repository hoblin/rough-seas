using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;

namespace AgentBridge;

public sealed class SoundsPlaying(ICoreClientAPI capi)
{
    private static readonly AccessTools.FieldRef<ClientMain, Queue<ILoadedSound>> Active =
        AccessTools.FieldRefAccess<ClientMain, Queue<ILoadedSound>>("ActiveSounds");

    public object[] Now()
    {
        var pos = capi.World.Player.Entity.Pos;
        return Active((ClientMain)capi.World)
            .Where(sound => sound is { IsDisposed: false, IsPlaying: true })
            .Select(sound => sound.Params)
            .Select(sound => (object)new
            {
                location = sound.Location.ToString(),
                volume = Math.Round(sound.Volume, 3),
                type = sound.SoundType.ToString(),
                metres = Distance(sound, pos) is { } metres ? Math.Round(metres, 1) : (double?)null
            })
            .ToArray();
    }

    public static double? Distance(SoundParams sound, EntityPos? listener) =>
        sound.Position == null ? null
        : sound.RelativePosition ? sound.Position.Length()
        : listener == null ? null
        : Math.Sqrt(listener.SquareDistanceTo(sound.Position.X, sound.Position.Y, sound.Position.Z));
}
