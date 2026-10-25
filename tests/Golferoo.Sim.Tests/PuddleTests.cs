using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class PuddleTests
{
    /// <summary>Fairway de 60 x 60 cellules de 4 m avec une cuvette ronde de <paramref name="bowlDepth"/> m au centre (rayon 20 m), sur une pente si demandé.</summary>
    private static TerrainGrid Bowl(double bowlDepth, double slope = 0.0)
    {
        const int cells = 60;
        const double cell = 4.0;
        var field = new HeightField(cells, cells, cell);
        for (var j = 0; j <= cells; j++)
        {
            for (var i = 0; i <= cells; i++)
            {
                var dx = (i * cell) - 120.0;
                var dz = (j * cell) - 120.0;
                var r = Math.Sqrt((dx * dx) + (dz * dz));
                var dip = r < 20.0 ? bowlDepth * (1.0 - (r * r / 400.0)) : 0.0;
                field.SetCornerHeight(i, j, (slope * i * cell) - dip);
            }
        }

        var grid = new TerrainGrid(field, "cuvette", new Point2(8, 120), new Point2(232, 120), new Point2(232, 120));
        for (var cz = 0; cz < cells; cz++)
        {
            for (var cx = 0; cx < cells; cx++)
            {
                grid.PaintCell(cx, cz, Surface.Fairway);
            }
        }

        return grid;
    }

    [Fact]
    public void Hollow_FillsAfterHeavyRain_SlopeNever()
    {
        var bowl = Bowl(1.0);
        Assert.False(bowl.Puddles.IsPuddle(30, 30));
        bowl.Soil.Wetness = 1.0;
        Assert.True(bowl.Puddles.IsPuddle(30, 30));
        Assert.False(bowl.Puddles.IsPuddle(5, 5));

        var slope = Bowl(0.0, slope: 0.05);
        slope.Soil.Wetness = 1.0;
        for (var cz = 0; cz < slope.CellsZ; cz++)
        {
            for (var cx = 0; cx < slope.CellsX; cx++)
            {
                Assert.False(slope.Puddles.IsPuddle(cx, cz));
            }
        }
    }

    [Fact]
    public void DeepHollow_StillWetNextDay_ShallowOneDry()
    {
        var deep = Bowl(1.0);
        var shallow = Bowl(0.2);
        deep.Soil.Wetness = 0.5;
        shallow.Soil.Wetness = 0.5;
        Assert.True(deep.Puddles.IsPuddle(30, 30));
        Assert.False(shallow.Puddles.IsPuddle(30, 30));
        shallow.Soil.Wetness = 1.0;
        Assert.True(shallow.Puddles.IsPuddle(30, 30));
    }

    [Fact]
    public void BallStoppedInPuddle_DropsDryWithoutPenalty_NotNearerTheHole()
    {
        var terrain = Bowl(1.0);
        terrain.Soil.Wetness = 1.0;
        var start = terrain.CellCenter(30, 30);
        var shot = BallSimulator.Run(terrain, start, new Vec3(0.01, 0, 0), rolling: true, BallSimulator.DefaultDrag, recordPath: false).Shot;
        Assert.Equal(0, shot.PenaltyStrokes);
        Assert.False(terrain.Puddles.IsPuddle(terrain.CellXOf(shot.Ball.X), terrain.CellZOf(shot.Ball.Z)));
        double D(Point2 p) => Math.Sqrt(((p.X - terrain.Hole.X) * (p.X - terrain.Hole.X)) + ((p.Z - terrain.Hole.Z) * (p.Z - terrain.Hole.Z)));
        Assert.True(D(shot.Ball) >= D(start) - 0.5);
        Assert.True(Math.Abs(shot.Ball.X - start.X) + Math.Abs(shot.Ball.Z - start.Z) < 40.0);
    }

    [Fact]
    public void Dry_NoPuddleAnywhere()
    {
        var bowl = Bowl(1.0);
        bowl.Soil.Wetness = 0.0;
        Assert.False(bowl.Puddles.IsPuddle(30, 30));
        Assert.True(bowl.Puddles.DepthAt(30, 30) > 0.9);
    }
}
