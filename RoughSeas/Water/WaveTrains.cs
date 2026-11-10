using System;
using System.Linq;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Water;

/// <summary>
/// Which waves are running: trains whose direction, length and speed stay fixed for their life, a generation in each of two banks with
/// the newer rising over the older, the short waves under them, and the one clock they all run on.
/// </summary>
public sealed class WaveTrains
{
    private const double FullTurn = 2 * Math.PI;
    private const double Faded = 0.001;

    private readonly SurfaceConfig config;
    private readonly int generation;
    private readonly int headings;
    private readonly double[] amplitudeShare;
    private readonly double[] slotShares;
    private readonly double[] numberX, numberZ, frequency, amplitude;
    private readonly (double Length, double Angle, double Amplitude)[] shortTrains;
    private readonly double shortHeight;
    private readonly double[] shortX, shortZ, shortFrequency, shortAmplitude;
    private readonly Bank?[] banks = new Bank?[2];
    private int active = -1;
    private double risingFrom = double.NegativeInfinity;

    public WaveTrains(SurfaceConfig config)
    {
        this.config = config;
        generation = config.Trains.Length;
        headings = (int)Math.Round(360 / config.TurnDegrees);
        var energy = config.Trains.Sum(train => train.Energy);
        amplitudeShare = config.Trains.Select(train => Math.Sqrt(train.Energy / energy) / (2 * Math.Sqrt(2))).ToArray();
        var slots = banks.Length * generation;
        slotShares = Enumerable.Range(0, slots).Select(slot => amplitudeShare[slot % generation]).ToArray();
        numberX = new double[slots];
        numberZ = new double[slots];
        frequency = new double[slots];
        amplitude = new double[slots];

        var bands = config.ShortWaves.Bands.Select(band => (band, variance: config.ShortWaves.PhillipsConstant / (4 * FullTurn * FullTurn) * (band.ToBlocks * band.ToBlocks - band.FromBlocks * band.FromBlocks))).ToArray();
        shortTrains = bands.SelectMany(entry => entry.band.AnglesDegrees.Select(angle => (Math.Sqrt(entry.band.FromBlocks * entry.band.ToBlocks), angle * Math.PI / 180, Math.Sqrt(2 * entry.variance / entry.band.AnglesDegrees.Length)))).ToArray();
        shortHeight = 4 * Math.Sqrt(bands.Sum(entry => entry.variance));
        var shortSlots = banks.Length * shortTrains.Length;
        shortX = new double[shortSlots];
        shortZ = new double[shortSlots];
        shortFrequency = new double[shortSlots];
        shortAmplitude = new double[shortSlots];
    }

    public int Slots => amplitude.Length;

    public int ShortSlots => shortAmplitude.Length;

    public double Seconds { get; private set; } = double.NaN;

    public long Frame { get; private set; }

    public double Steepness { get; private set; }

    /// <summary>The highest significant height around the point the waves are aimed at.</summary>
    public double SeaHeight { get; private set; }

    public double ShortSeaHeight { get; private set; }

    public bool Calm { get; private set; } = true;

    public int Dominant { get; private set; }

    public double DominantPeriod => frequency[Dominant] > 0 ? FullTurn / frequency[Dominant] : 0;

    public double DrawnHeight => WaveTheory.SignificantHeight(amplitude.Sum(value => value * value));

    /// <summary>Where the rising trains run, fixed for their life.</summary>
    public double TowardRadians => Rising is { } bank ? Toward(bank) : 0;

    public double? RisingLength => Rising is { } bank ? Length(bank) : null;

    public event Action<string>? Changed;

    public double NumberX(int slot) => numberX[slot];

    public double NumberZ(int slot) => numberZ[slot];

    public double Frequency(int slot) => frequency[slot];

    public double Amplitude(int slot) => amplitude[slot];

    public double Wavenumber(int slot) => Math.Sqrt(numberX[slot] * numberX[slot] + numberZ[slot] * numberZ[slot]);

    public double Phase(int slot, double x, double z) => (numberX[slot] * x + numberZ[slot] * z - frequency[slot] * Seconds) % FullTurn;

    public (double X, double Z)[] Vectors() => Enumerable.Range(0, Slots).Select(slot => (numberX[slot], numberZ[slot])).ToArray();

    public double[] AmplitudeShares() => slotShares;

    public void Aim(double height) => SeaHeight = height;

    public void Raise(WaveGeneration earlier, WaveGeneration latest, double bornSeconds, bool crossed = false)
    {
        var from = BankOf(earlier);
        if ((BankOf(latest) ?? from) is not { } next || Rising == next) return;

        if (Rising is { } drawn && drawn == from && !crossed) Rise(1 - active, next, bornSeconds);
        else if (active < 0 || !crossed)
        {
            if (from is { } first && first != next)
            {
                Load(0, first);
                Rise(1, next, bornSeconds);
            }
            else Rise(0, next, double.NegativeInfinity);
        }
        else
        {
            // The kept bank's weight carries on unbroken, and the new trains take the dropped bank's, so the drawn energy doesn't jump.
            var kept = banks[1 - active] == next || Handover >= 0.5 ? active : 1 - active;
            Rise(1 - kept, next, Seconds - Math.Acos(Weight(kept)) / (Math.PI / 2) * config.SettleSeconds);
        }
    }

    public void Advance(double seconds)
    {
        Seconds = seconds;
        Frame++;
        var handover = Handover;
        var (rising, fading) = (Math.Sin(handover * Math.PI / 2), Math.Cos(handover * Math.PI / 2));

        var slopes = 0.0;
        var calm = true;
        var dominant = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            amplitude[slot] = Wavenumber(slot) > 0 ? amplitudeShare[slot % generation] * SeaHeight * (slot / generation == active ? rising : fading) : 0;
            slopes += amplitude[slot] * Wavenumber(slot);
            calm &= amplitude[slot] <= Faded;
            if (amplitude[slot] > amplitude[dominant]) dominant = slot;
        }
        var developed = shortHeight > 0 ? Math.Min(1, SeaHeight / shortHeight) : 0;
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            var bank = slot / shortTrains.Length;
            var share = banks[0]?.Heading == banks[1]?.Heading ? (bank == active ? 1 : 0) : bank == active ? rising : fading;
            shortAmplitude[slot] = banks[bank] != null ? shortTrains[slot % shortTrains.Length].Amplitude * developed * share : 0;
        }
        ShortSeaHeight = WaveTheory.SignificantHeight(shortAmplitude.Sum(value => value * value));
        Dominant = dominant;
        Calm = calm;
        Steepness = slopes > 0 ? Math.Min(config.Steepness, 1 / slopes) : config.Steepness;
    }

    public double ShortRise(double x, double z, double kept)
    {
        var rise = 0.0;
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            if (shortAmplitude[slot] <= 0) continue;
            rise += kept * shortAmplitude[slot] * Math.Sin(shortX[slot] * x + shortZ[slot] * z - shortFrequency[slot] * Seconds);
        }
        return rise;
    }

    public void WriteUniforms(double cameraX, double cameraZ, float[] into)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            into[slot * 4] = (float)numberX[slot];
            into[slot * 4 + 1] = (float)numberZ[slot];
            into[slot * 4 + 2] = (float)amplitude[slot];
            into[slot * 4 + 3] = (float)Phase(slot, cameraX, cameraZ);
        }
    }

    public void WriteShortUniforms(double cameraX, double cameraZ, float[] into)
    {
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            into[slot * 4] = (float)shortX[slot];
            into[slot * 4 + 1] = (float)shortZ[slot];
            into[slot * 4 + 2] = (float)shortAmplitude[slot];
            into[slot * 4 + 3] = (float)((shortX[slot] * cameraX + shortZ[slot] * cameraZ - shortFrequency[slot] * Seconds) % FullTurn);
        }
    }

    private Bank? Rising => active >= 0 ? banks[active] : null;

    private double Handover => Math.Clamp((Seconds - risingFrom) / config.SettleSeconds, 0, 1);

    private double Weight(int bank) => bank == active ? Math.Sin(Handover * Math.PI / 2) : Math.Cos(Handover * Math.PI / 2);

    private Bank? BankOf(WaveGeneration raised)
    {
        if (!raised.Raised) return null;
        var length = WaveTheory.DeepLength(raised.Period);
        var heading = (int)Math.Round(raised.TowardRadians / FullTurn * headings);
        return new Bank((heading % headings + headings) % headings, (int)Math.Round(Math.Log(length) / Math.Log(1 + config.Stretch)));
    }

    private double Toward(Bank bank) => bank.Heading * FullTurn / headings;

    private double Length(Bank bank) => Math.Pow(1 + config.Stretch, bank.Stretch);

    private void Rise(int bank, Bank trains, double from)
    {
        if (banks[bank] != trains) Load(bank, trains);
        (active, risingFrom) = (bank, from);
    }

    private void Load(int bank, Bank trains)
    {
        banks[bank] = trains;
        var (towardRadians, length) = (Toward(trains), Length(trains));
        for (var i = 0; i < generation; i++)
        {
            var train = config.Trains[i];
            var angle = towardRadians + train.AngleDegrees * Math.PI / 180;
            var number = FullTurn / Math.Max(config.MinLengthBlocks, length * train.LengthFactor);
            var slot = bank * generation + i;
            (numberX[slot], numberZ[slot], frequency[slot]) = (Math.Cos(angle) * number, Math.Sin(angle) * number, WaveTheory.DeepFrequency(number));
            amplitude[slot] = 0;
        }
        for (var i = 0; i < shortTrains.Length; i++)
        {
            var angle = towardRadians + shortTrains[i].Angle;
            var number = FullTurn / shortTrains[i].Length;
            var slot = bank * shortTrains.Length + i;
            (shortX[slot], shortZ[slot], shortFrequency[slot]) = (Math.Cos(angle) * number, Math.Sin(angle) * number, WaveTheory.DeepFrequency(number));
            shortAmplitude[slot] = 0;
        }
        Changed?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "new waves toward {0:F0}°, {1:F0} m long", (towardRadians * 180 / Math.PI + 360) % 360, length));
    }

    private readonly record struct Bank(int Heading, int Stretch);
}
