using System;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AgentBridge;

public sealed class SwellReading(ICoreClientAPI capi)
{
    private IDrawnSea? Sea => capi.ModLoader.GetModSystem<StormsModSystem>()?.DrawnSea;

    public object? At(Entity entity) =>
        Sea is { } sea ? new { sea.Swell, height = sea.HeightAt(entity.Pos.X, entity.Pos.Z) } : null;

    public object? Of(Entity entity)
    {
        if (Sea is not { Swell: true } sea) return null;
        var side = Math.Max(entity.CollisionBox.XSize, entity.CollisionBox.ZSize);
        var yaw = entity.Pos.Yaw + (entity.Properties.Client.Shape?.rotateY ?? 0) * GameMath.DEG2RAD;
        var pose = sea.PoseOf(entity.Pos.X, entity.Pos.Z, yaw, side, side);
        return new { pose.Hold, pose.Lift, roll = pose.Roll * GameMath.RAD2DEG, pitch = pose.Pitch * GameMath.RAD2DEG };
    }
}
