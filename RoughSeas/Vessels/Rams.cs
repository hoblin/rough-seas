using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

/// <summary>
/// A hull's rams into other hulls: a contact that begins at a closing speed above the hull's give. Motion is in
/// blocks per 1/60 s, as <c>Pos.Motion</c> holds it; speeds are in m/s.
/// </summary>
public class Rams(double give)
{
    private const double TicksPerSecond = 60;

    private HashSet<long> touching = [];

    public static double ReducedKg(double kilograms, double theirKilograms) => kilograms * theirKilograms / (kilograms + theirKilograms);

    public List<Knocked> Step(IReadOnlyCollection<Contact> contacts)
    {
        var rams = contacts
            .Where(contact => !touching.Contains(contact.Against))
            .Select(Closing)
            .Where(knocked => knocked.SpeedLost > give)
            .ToList();
        touching = contacts.Select(contact => contact.Against).ToHashSet();
        return rams;
    }

    public void Forget() => touching.Clear();

    private static Knocked Closing(Contact contact)
    {
        double apart = Math.Max(1e-9, Math.Sqrt(contact.TowardX * contact.TowardX + contact.TowardZ * contact.TowardZ));
        double towardX = contact.TowardX / apart, towardZ = contact.TowardZ / apart;
        double speed = (contact.ClosingX * towardX + contact.ClosingZ * towardZ) * TicksPerSecond;
        return new Knocked(speed, towardX * speed, towardZ * speed, contact.Against);
    }
}

/// <param name="TowardX">From this hull's centre to the other's.</param>
/// <param name="ClosingX">This hull's motion less the other's.</param>
public readonly record struct Contact(long Against, double TowardX, double TowardZ, double ClosingX, double ClosingZ);
