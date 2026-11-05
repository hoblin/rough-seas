using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace RoughSeas.Units;

public static class Voxels
{
    public static readonly double AMetre = Read();

    public static double ToMetres(double voxels, int power = 1) => voxels / Math.Pow(AMetre, power);

    public static double FromMetres(double metres, int power = 1) => metres * Math.Pow(AMetre, power);

    private static double Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RoughSeas.Units.voxel.json")!;
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd()).Value<double>("voxelsAMetre");
    }
}
