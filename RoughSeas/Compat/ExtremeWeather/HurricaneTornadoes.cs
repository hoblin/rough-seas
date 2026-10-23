using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Compat.ExtremeWeather;

public class HurricaneTornadoes(ExtremeWeatherCompat compat, SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private TornadoConfig Config => compat.Config.Tornadoes;

    public void Start() => sapi.Event.RegisterGameTickListener(_ => Roll(), (int)(Config.CheckSeconds * 1000));

    private void Roll()
    {
        if (!sea.DangerousOceans) return;

        var inside = sapi.World.AllOnlinePlayers.Select(player => player.Entity?.Pos.XYZ).OfType<Vec3d>().Where(InHurricane).ToArray();
        var rand = sapi.World.Rand;
        if (inside.Length == 0 || rand.NextDouble() >= ExtremeWeatherMod.TornadoChance) return;

        var pos = inside[rand.Next(inside.Length)];
        compat.Log("a tornado in our hurricane near {0:F0} {1:F0}: {2}", pos.X, pos.Z, ExtremeWeatherMod.SpawnTornadoNear(pos));
    }

    private bool InHurricane(Vec3d pos) =>
        sea.Model.StormsWithin(pos.X, pos.Z, 0).Any(storm =>
            storm.Code == Config.StormCode && storm.InfluenceAt(pos.X, pos.Z, sea.Clock.Hours).Presence >= Config.MinPresence);
}
