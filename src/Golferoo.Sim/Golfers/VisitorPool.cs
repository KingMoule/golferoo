using Golferoo.Sim.Save;

namespace Golferoo.Sim.Golfers;

/// <summary>
/// Un visiteur nommé et persistant du vivier (C15) : son niveau et son style, la dernière journée où il est venu et son envie de revenir (0 à 1).
/// C16 : il gagne des points après chaque partie finie et devient membre (<see cref="Level"/>) ; une démission est définitive.
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

    /// <summary>Points d'adhésion gagnés (<see cref="MembershipParams"/>), sans plafond.</summary>
    public int Points { get; internal set; }

    /// <summary>Niveau d'adhésion ; <see cref="MemberLevel.Visitor"/> tant qu'il n'est pas membre, ou après sa démission.</summary>
    public MemberLevel Level { get; internal set; }

    /// <summary>A démissionné (il a abandonné une partie comme membre) : il ne revient jamais.</summary>
    public bool Resigned { get; internal set; }

    /// <summary>Parties finies (sans l'avoir abandonnée) depuis son arrivée dans le vivier.</summary>
    public int Games { get; internal set; }

    internal void Write(SaveWriter w)
    {
        w.Int(Id);
        w.String(Name);
        w.Double(Handicap);
        w.Byte((byte)Profile);
        w.Double(Desire);
        w.Int(LastVisitDay);
        w.Bool(InPlay);
        w.Int(Points);
        w.Byte((byte)Level);
        w.Bool(Resigned);
        w.Int(Games);
    }

    internal static Visitor Read(SaveReader r)
    {
        var v = new Visitor(r.Int(), r.String(), r.Double(), (Archetype)r.Byte(), r.Double()) { LastVisitDay = r.Int(), InPlay = r.Bool() };
        if (r.Version >= 15)
        {
            (v.Points, v.Level, v.Resigned) = (r.Int(), (MemberLevel)r.Byte(), r.Bool());
            if (v.Points < 0 || !Enum.IsDefined(v.Level) || (v.Resigned && v.Level != MemberLevel.Visitor))
            {
                throw new InvalidDataException("Sauvegarde incohérente : adhésion d'un visiteur.");
            }
        }

        if (r.Version >= 16 && (v.Games = r.Int()) < 0)
        {
            throw new InvalidDataException("Sauvegarde incohérente : parties jouées d'un visiteur.");
        }

        return v;
    }

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)(uint)Id | ((ulong)Profile << 32) | (InPlay ? 1UL << 40 : 0UL));
        World.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(Handicap));
        World.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(Desire));
        World.Mix(ref hash, (ulong)(uint)LastVisitDay);
        if (Points != 0 || Level != MemberLevel.Visitor || Resigned)
        {
            World.Mix(ref hash, (ulong)(uint)Points | ((ulong)Level << 32) | (Resigned ? 1UL << 40 : 0UL));
        }

        if (Games != 0)
        {
            World.Mix(ref hash, (ulong)(uint)Games);
        }
    }
}

/// <summary>Le vivier persistant des visiteurs, dans l'ordre d'arrivée de leur première visite.</summary>
public sealed class VisitorPool
{
    private readonly List<Visitor> _visitors = [];

    public IReadOnlyList<Visitor> Visitors => _visitors;

    /// <summary>Numéro du prochain visiteur.</summary>
    public int Next => _visitors.Count;

    internal Visitor Add(string name, double handicap, Archetype profile, double desire, MemberLevel level = MemberLevel.Visitor, int points = 0)
    {
        var v = new Visitor(_visitors.Count, name, handicap, profile, desire) { Level = level, Points = points };
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
        [.. _visitors.Where(v => !v.InPlay && !v.Resigned && v.LastVisitDay < day && v.Desire >= threshold)];

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
