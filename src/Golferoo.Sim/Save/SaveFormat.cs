using System.IO.Compression;
using System.Text;

namespace Golferoo.Sim.Save;

/// <summary>
/// Fichier de sauvegarde : « GOLF », numéro de version (int), somme de contrôle du monde (ulong), longueur du corps compressé (int),
/// empreinte du corps décompressé (ulong), puis le corps en gzip. Le gzip de .NET ne contrôle ni son CRC ni son pied : la longueur décèle
/// la troncature, l'empreinte décèle un octet changé même dans un état que la somme de contrôle du monde ne couvre pas. Le numéro de version change avec le format ; aucune migration tant qu'aucune version n'est publiée.
/// </summary>
internal static class SaveFormat
{
    internal const int Version = 1;

    /// <summary>Plafond du corps, compressé comme décompressé, à la lecture.</summary>
    internal const int MaxBodyBytes = 64 * 1024 * 1024;

    private static readonly byte[] Magic = "GOLF"u8.ToArray();

    internal static void Write(Stream output, ulong checksum, SaveWriter body)
    {
        var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(body.Written);
        }

        using var header = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        header.Write(Magic);
        header.Write(Version);
        header.Write(checksum);
        header.Write((int)packed.Length);
        header.Write(Hash(body.Written));
        header.Write(packed.GetBuffer().AsSpan(0, (int)packed.Length));
    }

    /// <summary>FNV-1a 64 bits du corps décompressé.</summary>
    internal static ulong Hash(ReadOnlySpan<byte> body)
    {
        var hash = 0xCBF29CE484222325UL;
        foreach (var b in body)
        {
            hash = (hash ^ b) * 0x100000001B3UL;
        }

        return hash;
    }

    /// <summary>Vérifie l'en-tête et décompresse le corps en entier (le CRC du gzip se contrôle ici, avant toute lecture du monde).</summary>
    internal static SaveReader Open(Stream input, out ulong checksum)
    {
        try
        {
            using var header = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
            if (!header.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
            {
                throw new InvalidDataException("Ce fichier n'est pas une sauvegarde Golferoo (magique faux).");
            }

            var version = header.ReadInt32();
            if (version != Version)
            {
                throw new InvalidDataException($"Version de sauvegarde inconnue : {version} (attendue : {Version}).");
            }

            checksum = header.ReadUInt64();
            var length = header.ReadInt32();
            var bodyHash = header.ReadUInt64();
            if (length < 0 || length > MaxBodyBytes || (input.CanSeek && length > input.Length - input.Position))
            {
                throw new InvalidDataException("Longueur impossible dans l'en-tête.");
            }

            var packed = header.ReadBytes(length);
            if (packed.Length != length)
            {
                throw new EndOfStreamException();
            }

            using var gzip = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress);
            var body = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = gzip.Read(chunk)) > 0)
            {
                body.Write(chunk, 0, read);
                if (body.Length > MaxBodyBytes)
                {
                    throw new InvalidDataException("Corps décompressé trop grand.");
                }
            }
            if (Hash(body.GetBuffer().AsSpan(0, (int)body.Length)) != bodyHash)
            {
                throw new InvalidDataException("Corps de sauvegarde corrompu (empreinte fausse).");
            }

            body.Position = 0;
            return new SaveReader(body);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("Sauvegarde tronquée.", e);
        }
    }
}
