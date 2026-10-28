using System.Text;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Save;

/// <summary>Enveloppe mince sur <see cref="BinaryWriter"/> : le corps de la sauvegarde s'écrit en mémoire, <see cref="SaveFormat"/> le compresse. Doubles bit à bit, listes préfixées de leur longueur.</summary>
internal sealed class SaveWriter
{
    private readonly MemoryStream _buffer = new();
    private readonly BinaryWriter _out;

    internal SaveWriter()
    {
        _out = new BinaryWriter(_buffer, Encoding.UTF8, leaveOpen: true);
    }

    internal ReadOnlySpan<byte> Written => _buffer.GetBuffer().AsSpan(0, (int)_buffer.Length);

    internal void Bool(bool v) => _out.Write(v);

    internal void Byte(byte v) => _out.Write(v);

    internal void Int(int v) => _out.Write(v);

    internal void Long(long v) => _out.Write(v);

    internal void ULong(ulong v) => _out.Write(v);

    internal void Double(double v) => _out.Write(BitConverter.DoubleToInt64Bits(v));

    internal void Float(float v) => _out.Write(BitConverter.SingleToInt32Bits(v));

    internal void String(string v) => _out.Write(v);

    internal void Bytes(ReadOnlySpan<byte> v) => _out.Write(v);

    internal void Point(Point2 p)
    {
        Double(p.X);
        Double(p.Z);
    }

    internal void Vec(Vec3 v)
    {
        Double(v.X);
        Double(v.Y);
        Double(v.Z);
    }

    internal void Doubles(double[] values)
    {
        foreach (var v in values)
        {
            Double(v);
        }
    }

    /// <summary>Liste préfixée de sa longueur ; <paramref name="write"/> écrit un élément.</summary>
    internal void List<T>(IReadOnlyCollection<T> items, Action<T> write)
    {
        Int(items.Count);
        foreach (var item in items)
        {
            write(item);
        }
    }

    /// <summary>Un coup du pro (club compris, écrit en entier : le lecteur retrouve les clubs de la table).</summary>
    internal void ProShot(ProShot s)
    {
        String(s.Club.Name);
        Double(s.Club.Speed);
        Double(s.Club.LaunchDegrees);
        Double(s.Club.TargetRange);
        Double(s.Club.DirectionSigmaDegrees);
        Double(s.Club.SpeedSigma);
        Double(s.Club.Backspin);
        Point(s.Target);
        Byte((byte)s.Shape);
        Double(s.Force);
    }
}
