using System;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>
/// A camera placed around an entity while the player stays where they are, in the picture: the game's third-person
/// view brought to zero distance and placed each frame. `.cam` moves the player itself and films in spectator.
/// </summary>
public sealed class FreeCamera : IRenderer
{
    private const float EyeAhead = 0.2f;

    private static Vec3d? selfOffset;

    private readonly ICoreClientAPI capi;
    private readonly Harmony harmony = new("agentbridge.freecamera");
    private Shot? shot;
    private EnumCameraMode modeBefore;
    private (int Min, int Max) distanceBefore;

    public FreeCamera(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "agentbridge-freecamera");
        harmony.Patch(AccessTools.Method(typeof(EntityPlayerShapeRenderer), nameof(EntityPlayerShapeRenderer.loadModelMatrixForPlayer)),
            postfix: new HarmonyMethod(typeof(FreeCamera), nameof(PlaceSelf)));
        harmony.Patch(AccessTools.Method(typeof(EntityShapeRenderer), nameof(EntityShapeRenderer.loadModelMatrix)),
            postfix: new HarmonyMethod(typeof(FreeCamera), nameof(PlaceMount)));
    }

    public double RenderOrder => -1;
    public int RenderRange => 0;

    private PlayerCamera Camera => ((ClientMain)capi.World).MainCamera;

    public string Frame(Shot next)
    {
        next.Start(capi.World, capi.ElapsedMilliseconds);
        if (shot == null) Take();
        shot = next;
        return Describe();
    }

    public string Release()
    {
        if (shot == null) return "null\n";

        shot = null;
        selfOffset = null;
        var camera = Camera;
        camera.UpdateCameraPos = true;
        (camera.TppCameraDistanceMin, camera.TppCameraDistanceMax) = distanceBefore;
        camera.CameraOffset.Rotation.Set(0, 0, 0);
        while (capi.World.Player.CameraMode != modeBefore) camera.CycleMode();
        return "null\n";
    }

    public string Mode(EnumCameraMode mode)
    {
        Release();
        while (capi.World.Player.CameraMode != mode) Camera.CycleMode();
        return "null\n";
    }

    public string Describe() => shot == null ? "null\n" : JsonSerializer.Serialize(shot.Describe(capi.World, capi.ElapsedMilliseconds)) + "\n";

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (shot?.Frame(capi.World, capi.ElapsedMilliseconds) is not var (eye, target)) return;

        var player = capi.World.Player;
        var entity = player.Entity;
        var look = target.SubCopy(eye);
        var yaw = (float)Math.Atan2(look.X, look.Z);
        var pitch = (float)(Math.PI - Math.Atan2(look.Y, Math.Sqrt(look.X * look.X + look.Z * look.Z)));
        Camera.CameraOffset.Rotation.Set(pitch - player.CameraPitch, player.CameraYaw - yaw, 0);

        var forward = look.Normalize();
        entity.CameraPos.Set(eye.X - entity.LocalEyePos.X - forward.X * EyeAhead,
            eye.Y - entity.LocalEyePos.Y - forward.Y * EyeAhead,
            eye.Z - entity.LocalEyePos.Z - forward.Z * EyeAhead);
        var body = entity.MountedOn?.SeatPosition.XYZ ?? entity.Pos.XYZ;
        selfOffset = body.Sub(entity.CameraPos);
    }

    private void Take()
    {
        var camera = Camera;
        modeBefore = capi.World.Player.CameraMode;
        distanceBefore = (camera.TppCameraDistanceMin, camera.TppCameraDistanceMax);
        while (capi.World.Player.CameraMode != EnumCameraMode.ThirdPerson) camera.CycleMode();
        camera.TppCameraDistanceMin = camera.TppCameraDistanceMax = 0;
        camera.UpdateCameraPos = false;
    }

    // The game draws the player, the mount they sit on and its other riders where the camera is; these move them back.
    private static void PlaceSelf(EntityPlayerShapeRenderer __instance, bool isSelf)
    {
        if (isSelf) Move(__instance);
    }

    private static void PlaceMount(EntityShapeRenderer __instance, Entity entity)
    {
        var mount = __instance.entity.World.Api is ICoreClientAPI client ? client.World.Player.Entity.MountedOn?.Entity : null;
        if (mount != null && (entity == mount || (entity as EntityAgent)?.MountedOn?.Entity == mount)) Move(__instance);
    }

    private static void Move(EntityShapeRenderer __instance)
    {
        if (selfOffset is not { } offset) return;

        var moved = Mat4f.Create();
        Mat4f.Translate(moved, moved, (float)offset.X, (float)offset.Y, (float)offset.Z);
        __instance.ModelMat = Mat4f.Mul(moved, moved, __instance.ModelMat);
    }

    public void Dispose()
    {
        Release();
        harmony.UnpatchAll(harmony.Id);
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
    }
}

public sealed record Station(double Bearing, double Distance, double Height);

public sealed class Shot(long on, Station from, Station? to, double seconds, long? look, bool hold)
{
    private long startMs;
    private (Vec3d Pos, float Heading)? held;

    public void Start(IWorldAccessor world, long nowMs)
    {
        var subject = world.GetEntityById(on) ?? throw new FileNotFoundException($"No entity {on} near the player");
        if (look is { } watched && world.GetEntityById(watched) == null) throw new FileNotFoundException($"No entity {watched} near the player");

        startMs = nowMs;
        held = hold ? (subject.Pos.XYZ, Heading(subject)) : null;
    }

    public (Vec3d Eye, Vec3d Target)? Frame(IWorldAccessor world, long nowMs)
    {
        var subject = world.GetEntityById(on);
        var watched = world.GetEntityById(look ?? on);
        if (subject == null || watched == null) return null;

        var progress = Progress(nowMs);
        var station = to == null ? from : new Station(
            GameMath.Lerp(from.Bearing, to.Bearing, progress),
            GameMath.Lerp(from.Distance, to.Distance, progress),
            GameMath.Lerp(from.Height, to.Height, progress));
        var origin = held?.Pos ?? subject.Pos.XYZ;
        var angle = (held?.Heading ?? Heading(subject)) - station.Bearing * GameMath.DEG2RAD;
        var eye = origin.AddCopy(Math.Sin(angle) * station.Distance, station.Height, Math.Cos(angle) * station.Distance);
        return (eye, Centre(watched));
    }

    public object Describe(IWorldAccessor world, long nowMs) => new
    {
        on, look = look ?? on, hold, progress = Math.Round(Progress(nowMs), 3),
        frame = Frame(world, nowMs) is var (eye, target) ? new { eye = new { eye.X, eye.Y, eye.Z }, target = new { target.X, target.Y, target.Z } } : null
    };

    private double Progress(long nowMs)
    {
        if (to == null || seconds <= 0) return 1;

        var t = Math.Clamp((nowMs - startMs) / 1000.0 / seconds, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // A boat moves against its view vector; a creature faces along it.
    private static float Heading(Entity subject) => subject is EntityBoat ? subject.Pos.Yaw : subject.Pos.Yaw + GameMath.PI;

    public static Vec3d Centre(Entity entity)
    {
        var box = entity.SelectionBox;
        return entity.Pos.XYZ.AddCopy((box.X1 + box.X2) / 2, (box.Y1 + box.Y2) / 2, (box.Z1 + box.Z2) / 2);
    }
}
