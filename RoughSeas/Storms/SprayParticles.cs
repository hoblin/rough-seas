using System;
using System.Collections.Concurrent;
using RoughSeas.Shore;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

public sealed class SprayParticles(FacesConfig config) : ParticlesProviderBase
{
    /// <summary><c>ParticlePoolQuads.SpawnParticles</c> multiplies a particle's life by this at the game's normal speed.</summary>
    private const float EngineLifeFactor = 5;

    private readonly ConcurrentQueue<SprayBurst> bursts = new();
    private readonly Random rand = new();
    private SprayBurst burst;
    private float speed;

    public void Add(SprayBurst strike) => bursts.Enqueue(strike);

    public bool Spawn(float dt, IAsyncParticleManager manager)
    {
        while (bursts.TryDequeue(out var next))
        {
            burst = next;
            manager.Spawn(this);
        }
        return true;
    }

    public override bool DieInLiquid => true;

    public override float GravityEffect => (float)(RunUp.Gravity / (GlobalConstants.GravityStrengthParticle * 40));

    public override float LifeLength => (2 * speed / (float)RunUp.Gravity + 0.2f) / EngineLifeFactor;

    public override float Quantity => burst.Count;

    public override float Size => (float)burst.Side;

    public override Vec3d Pos
    {
        get
        {
            var along = rand.NextDouble() - 0.5;
            return new Vec3d(burst.X + along * burst.AlongX, burst.Y, burst.Z + along * burst.AlongZ);
        }
    }

    public override Vec3f GetVelocity(Vec3d pos)
    {
        speed = (float)burst.Impact.Speed(rand.NextDouble());
        return new Vec3f(Scatter(), speed, Scatter());
    }

    public override int GetRgbaColor(ICoreClientAPI capi) => ColorUtil.HsvToRgba(110, 20 + rand.Next(30), 220 + rand.Next(30), 110 + rand.Next(40));

    public override EvolvingNatFloat OpacityEvolve => new(EnumTransformFunction.QUADRATIC, -16);

    private float Scatter() => (float)((rand.NextDouble() * 2 - 1) * config.SprayScatterMetresPerSecond);
}
