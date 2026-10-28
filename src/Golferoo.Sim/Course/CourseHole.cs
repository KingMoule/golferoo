using Golferoo.Sim.Save;

namespace Golferoo.Sim.Terrain;

/// <summary>Un trou du parcours : numéro, départ, centre du green, drapeau et par. Les positions sont en mètres sur la carte.</summary>
public sealed class CourseHole
{
    public CourseHole(int number, Point2 tee, Point2 green, Point2 hole, int par)
    {
        Number = number;
        Tee = tee;
        Green = green;
        Hole = hole;
        Par = par;
    }

    /// <summary>Numéro du trou, à partir de 1.</summary>
    public int Number { get; }

    /// <summary>Position de départ, en mètres.</summary>
    public Point2 Tee { get; private set; }

    /// <summary>Centre du green, en mètres.</summary>
    public Point2 Green { get; }

    /// <summary>Position du drapeau, en mètres.</summary>
    public Point2 Hole { get; private set; }

    /// <summary>Par : celui du fichier, sinon déduit de la distance départ-drapeau.</summary>
    public int Par { get; internal set; }

    /// <summary>Déplace le départ (pose du joueur, C07).</summary>
    internal void MoveTee(Point2 tee) => Tee = tee;

    /// <summary>Déplace le drapeau (changement de jour) ; la balle et les golfeurs lisent <see cref="Hole"/> à chaque coup.</summary>
    internal void MovePin(Point2 hole) => Hole = hole;

    internal void Write(SaveWriter w)
    {
        w.Int(Number);
        w.Point(Tee);
        w.Point(Green);
        w.Point(Hole);
        w.Int(Par);
    }

    internal static CourseHole Read(SaveReader r) => new(r.Int(), r.Point(), r.Point(), r.Point(), r.Int());
}
