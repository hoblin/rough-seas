using System;
using System.Collections.Concurrent;
using RoughSeas.Water;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

[ReliesOn("Vintagestory.Client.NoObf.ParticlePoolQuads", "SpawnParticles", "multiplies a particle's life by 5 and its gravity by 0.3·40")]
[ReliesOn("Vintagestory.Client.NoObf.ParticlePoolQuads", "LoadModel", "draws a quad 0.25 wide")]
public sealed class SprayParticles(FacesConfig config) : ParticlesProviderBase
{
    /// <summary><c>ParticlePoolQuads.SpawnParticles</c> multiplies a particle's life by this at the game's normal speed.</summary>
    private const float EngineLifeFactor = 5;

    /// <summary>The width <c>ParticlePoolQuads</c> draws a particle of <see cref="Size"/> 1.</summary>
    private const float EngineQuadWidth = 0.25f;

    /// <summary><c>ParticlePoolQuads.SpawnParticles</c> multiplies <see cref="GravityEffect"/> and <see cref="GlobalConstants.GravityStrengthParticle"/> by this into blocks per second squared.</summary>
    private const float EngineGravityFactor = 40;

    private static readonly EvolvingNatFloat Fading = new(EnumTransformFunction.QUADRATIC, -16);

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

    public override float GravityEffect => (float)(WaveTheory.Gravity / (GlobalConstants.GravityStrengthParticle * EngineGravityFactor));

    public override float LifeLength
    {
        get
        {
            var gravity = (float)WaveTheory.Gravity;
            var flight = (speed + MathF.Sqrt(speed * speed + 2 * gravity * (float)burst.FallMetres)) / gravity;
            return (flight + (float)config.SprayLandingSeconds) / EngineLifeFactor;
        }
    }

    public override float Quantity => burst.Count;

    public override float Size => (float)burst.Side / EngineQuadWidth;

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

    public override EvolvingNatFloat OpacityEvolve => Fading;

    private float Scatter() => (float)((rand.NextDouble() * 2 - 1) * config.SprayScatterMetresPerSecond);
}
