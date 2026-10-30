using Golferoo.Sim.Save;

namespace Golferoo.Sim.Golfers;

/// <summary>
/// Un visiteur nommé et persistant du vivier (C15) : son niveau et son style, la dernière journée où il est venu et son envie de revenir (0 à 1).
/// C16 remplacera l'envie par l'humeur et fera des membres de certains visiteurs.
/// </summary>
public sealed class Visitor
{
    internal Visitor(int id, string name, double handicap, Archetype profile, double desire)
    {
        Id = id;
        Name = name;
        Handicap = handicap;
        Profile = profile;
        Desire = desire;
    }

    public int Id { get; }

    public string Name { get; }

    public double Handicap { get; }

    public Archetype Profile { get; }

    /// <summary>Envie de revenir, de 0 à 1 : monte avec une bonne sortie, baisse avec un refus à la file.</summary>
    public double Desire { get; internal set; }

    /// <summary>Numéro de la journée de sa dernière visite (−1 : jamais venu).</summary>
    public int LastVisitDay { get; internal set; } = -1;

    /// <summary>Il est sur le parcours ou dans la file, donc pas disponible.</summary>
    public bool InPlay { get; internal set; }

    internal void Write(SaveWriter w)
    {
        w.Int(Id);
        w.String(Name);
        w.Double(Handicap);
        w.Byte((byte)Profile);
        w.Double(Desire);
        w.Int(LastVisitDay);
        w.Bool(InPlay);
    }

    internal static Visitor Read(SaveReader r) =>
        new(r.Int(), r.String(), r.Double(), (Archetype)r.Byte(), r.Double()) { LastVisitDay = r.Int(), InPlay = r.Bool() };

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)(uint)Id | ((ulong)Profile << 32) | (InPlay ? 1UL << 40 : 0UL));
        World.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(Handicap));
        World.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(Desire));
        World.Mix(ref hash, (ulong)(uint)LastVisitDay);
    }
}

/// <summary>Le vivier persistant des visiteurs, dans l'ordre d'arrivée de leur première visite.</summary>
public sealed class VisitorPool
{
    private readonly List<Visitor> _visitors = [];

    public IReadOnlyList<Visitor> Visitors => _visitors;

    /// <summary>Numéro du prochain visiteur.</summary>
    public int Next => _visitors.Count;

    internal Visitor Add(string name, double handicap, Archetype profile, double desire)
    {
        var v = new Visitor(_visitors.Count, name, handicap, profile, desire);
        _visitors.Add(v);
        return v;
    }

    /// <summary>Retire les visiteurs arrivés depuis le numéro <paramref name="count"/> (un groupe refusé n'a pas laissé de trace).</summary>
    internal void TruncateTo(int count) => _visitors.RemoveRange(count, _visitors.Count - count);

    /// <summary>
    /// Les visiteurs qui peuvent revenir le jour <paramref name="day"/> : libres, pas venus ce jour-là, envie de revenir d'au moins
    /// <paramref name="threshold"/>. Dans l'ordre du vivier.
    /// </summary>
    internal List<Visitor> Returning(int day, double threshold) =>
        [.. _visitors.Where(v => !v.InPlay && v.LastVisitDay < day && v.Desire >= threshold)];

    internal void Write(SaveWriter w) => w.List(_visitors, v => v.Write(w));

    internal static VisitorPool Read(SaveReader r)
    {
        var pool = new VisitorPool();
        pool._visitors.AddRange(r.List(() => Visitor.Read(r)));
        for (var i = 0; i < pool._visitors.Count; i++)
        {
            if (pool._visitors[i].Id != i)
            {
                throw new InvalidDataException("Sauvegarde incohérente : numéro de visiteur hors de l'ordre.");
            }
        }

        return pool;
    }

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)_visitors.Count);
        foreach (var v in _visitors)
        {
            v.Mix(ref hash);
        }
    }
}
