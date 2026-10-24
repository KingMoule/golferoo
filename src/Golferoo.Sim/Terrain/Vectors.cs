namespace Golferoo.Sim.Terrain;

/// <summary>Point du plan du terrain, en mètres (x vers l'est, z vers le sud).</summary>
public readonly record struct Point2(double X, double Z);

/// <summary>Vecteur 3D en mètres (x est, y haut, z sud).</summary>
public readonly record struct Vec3(double X, double Y, double Z);
