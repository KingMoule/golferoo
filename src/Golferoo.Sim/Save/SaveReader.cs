using System.Text;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Save;

/// <summary>Lecture symétrique de <see cref="SaveWriter"/>. Une longueur de liste impossible (plus grande que ce qui reste à lire) lève <see cref="InvalidDataException"/>.</summary>
internal sealed class SaveReader
{
    private readonly MemoryStream _body;
    private readonly BinaryReader _in;

    internal SaveReader(MemoryStream body)
    {
        _body = body;
        _in = new BinaryReader(body, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>Octets restant à lire.</summary>
    internal long Remaining => _body.Length - _body.Position;

    internal bool Bool() => _in.ReadBoolean();

    internal byte Byte() => _in.ReadByte();

    internal int Int() => _in.ReadInt32();

    internal long Long() => _in.ReadInt64();

    internal ulong ULong() => _in.ReadUInt64();

    internal double Double() => BitConverter.Int64BitsToDouble(_in.ReadInt64());

    internal float Float() => BitConverter.Int32BitsToSingle(_in.ReadInt32());

    internal string String() => _in.ReadString();

    internal byte[] Bytes(int count)
    {
        var bytes = _in.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new EndOfStreamException();
    }

    internal Point2 Point() => new(Double(), Double());

    internal Vec3 Vec() => new(Double(), Double(), Double());

    internal double[] Doubles(int count)
    {
        if (count < 0 || (long)count * sizeof(double) > _body.Length - _body.Position)
        {
            throw new InvalidDataException("Longueur impossible dans la sauvegarde.");
        }

        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = Double();
        }

        return values;
    }

    /// <summary>Liste préfixée de sa longueur ; <paramref name="read"/> lit un élément (un élément pèse au moins un octet).</summary>
    internal List<T> List<T>(Func<T> read)
    {
        var count = Int();
        if (count < 0 || count > _body.Length - _body.Position)
        {
            throw new InvalidDataException("Longueur impossible dans la sauvegarde.");
        }

        var items = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(read());
        }

        return items;
    }

    internal ProShot ProShot()
    {
        var club = new Club(String(), Double(), Double(), Double(), Double(), Double(), Double());
        var shot = new ProShot(club, Point(), (ShotShape)Byte(), Double());
        // Les clubs de la table reprennent leur instance, comme dans une partie jamais interrompue.
        var known = Clubs.Full.Append(Clubs.Putter).FirstOrDefault(c => c == club);
        return known is null ? shot : shot with { Club = known };
    }
}
