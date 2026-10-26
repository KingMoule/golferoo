using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>Trajectoire voulue d'un coup choisi (votre pro). Les valeurs entrent dans la somme de contrôle : ne pas réordonner.</summary>
public enum ShotShape : byte
{
    Straight = 0,

    /// <summary>Courbe de droite à gauche (droitier), roule un peu plus.</summary>
    Draw = 1,

    /// <summary>Courbe de gauche à droite (droitier), roule un peu moins.</summary>
    Fade = 2,

    /// <summary>Lancement deux fois plus bas, peu de spin : la balle roule.</summary>
    Chip = 3,
}

/// <summary>Ce qu'une trajectoire change au départ de la balle : angle de lancement, effet latéral et spin.</summary>
public static class ShotShapes
{
    /// <summary>Effet latéral d'un draw ou d'un fade plein au driver, en rad/s, proportionnel à la vitesse de balle du club (calibré : un driver courbe d'environ 15 m, un fer 7 d'environ 7).</summary>
    public const double CurveRate = 0.06;

    /// <summary>Spin d'un draw (roule plus) et d'un fade (roule moins), en part du spin du club.</summary>
    private const double DrawSpin = 0.7;

    private const double FadeSpin = 1.3;

    /// <summary>Spin d'un chip, en part du spin du club.</summary>
    private const double ChipSpin = 0.3;

    /// <summary>
    /// Vitesse de départ, effet et spin d'un coup <paramref name="shape"/> joué avec <paramref name="club"/> vers (dirX, dirZ).
    /// <paramref name="amount"/> multiplie l'effet voulu (1 : exécuté comme voulu) : courbure du draw et du fade, abaissement du chip.
    /// </summary>
    public static (Vec3 Velocity, double Curve, double Backspin) Launch(Club club, double dirX, double dirZ, double speed, ShotShape shape, double amount = 1.0)
    {
        var cos = club.LaunchCos;
        var sin = club.LaunchSin;
        if (shape == ShotShape.Chip)
        {
            // Angle multiplié par 1 − amount / 2 : la moitié de celui du club pour un chip exécuté comme voulu.
            var a = club.LaunchDegrees * (1.0 - (amount / 2.0)) * Math.PI / 180.0;
            cos = Math.Cos(a);
            sin = Math.Sin(a);
        }

        var velocity = new Vec3(dirX * cos * speed, sin * speed, dirZ * cos * speed);
        var curve = CurveRate * amount * club.Speed / Clubs.Driver.Speed;
        return shape switch
        {
            ShotShape.Draw => (velocity, curve, club.Backspin * DrawSpin),
            ShotShape.Fade => (velocity, -curve, club.Backspin * FadeSpin),
            ShotShape.Chip => (velocity, 0.0, club.Backspin * ChipSpin),
            _ => (velocity, 0.0, club.Backspin),
        };
    }
}
