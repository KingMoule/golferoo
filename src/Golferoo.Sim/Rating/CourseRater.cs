using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Rating;

/// <summary>Note un parcours en faisant jouer tous ses trous, dans l'ordre, à un golfeur scratch et à un golfeur bogey.</summary>
public static class CourseRater
{
    public const int DefaultGames = 50;

    /// <summary>
    /// Fonction pure : même terrain, même graine, même saison, même note. Sans vent, sol sec et gazon de référence, avec les
    /// arbres <paramref name="worldTrees"/> (copie des arbres du monde, debout seulement ; par défaut les arbres isolés du terrain). Le terrain passé n'est jamais modifié : le calcul joue sur une copie (hauteurs, surfaces, repères),
    /// car le sol (<see cref="SoilGrid"/>) se recalcule à la lecture. Peut donc tourner sur un autre fil que le monde ; si le terrain
    /// est retouché pendant le calcul, la note peut être incohérente et le jeu doit la refaire.
    /// </summary>
    public static CourseRating Rate(TerrainGrid terrain, ulong seed, int games = DefaultGames, Season season = Season.Summer, IReadOnlyList<Tree>? worldTrees = null)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentOutOfRangeException.ThrowIfLessThan(games, 1);
        var copy = Copy(terrain);
        copy.Soil.Season = season;
        var trees = new TreeStand(copy.CellsX * copy.CellSize, copy.CellsZ * copy.CellSize, copy);
        if (worldTrees is null)
        {
            foreach (var p in terrain.IsolatedTrees)
            {
                trees.Add(p.X, p.Z, p.Species, p.AgeMonths);
            }
        }
        else
        {
            foreach (var t in worldTrees.Where(t => t.State == TreeState.Standing))
            {
                trees.Add(t.X, t.Z, t.Species, t.AgeMonths);
            }
        }

        var resolver = new PhysicalShotResolver { Trees = trees, Season = season };
        var scratch = TotalStrokes(resolver, copy, seed, SkillLevels.Scratch, games);
        var bogey = TotalStrokes(resolver, copy, seed ^ 0x9E3779B97F4A7C15UL, SkillLevels.Bogey, games);
        return new CourseRating(terrain.Course.Count, terrain.Course.Holes.Sum(h => h.Par), (double)scratch / games, (double)bogey / games);
    }

    private static int TotalStrokes(PhysicalShotResolver resolver, TerrainGrid terrain, ulong seed, double skill, int games)
    {
        var total = 0;
        for (var game = 0; game < games; game++)
        {
            var rng = new DeterministicRng(seed + ((ulong)game * 0x2545F4914F6CDD1DUL));
            foreach (var hole in terrain.Course.Holes)
            {
                var ball = hole.Tee;
                var strokes = 0;
                while (strokes < World.MaxStrokes)
                {
                    var r = resolver.Resolve(new ShotRequest(ball, hole.Hole, strokes, skill), terrain, rng);
                    strokes += 1 + r.PenaltyStrokes;
                    ball = r.Ball;
                    if (r.Holed)
                    {
                        break;
                    }
                }

                total += Math.Min(strokes, World.MaxStrokes);
            }
        }

        return total;
    }

    private static TerrainGrid Copy(TerrainGrid source)
    {
        var heights = new HeightField(source.CellsX, source.CellsZ, source.CellSize);
        for (var j = 0; j <= source.CellsZ; j++)
        {
            for (var i = 0; i <= source.CellsX; i++)
            {
                heights.SetCornerHeight(i, j, source.Heights.GetCornerHeight(i, j));
            }
        }

        var copy = new TerrainGrid(heights, source.Name, new Course(source.Course.Holes.Select(h => new CourseHole(h.Number, h.Tee, h.Green, h.Hole, h.Par))));
        for (var cz = 0; cz < source.CellsZ; cz++)
        {
            for (var cx = 0; cx < source.CellsX; cx++)
            {
                copy.PaintCell(cx, cz, source.GetCellSurface(cx, cz));
            }
        }

        return copy;
    }
}
