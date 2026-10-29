using System.Text.Json;
using System.Text.Json.Nodes;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;

namespace Golferoo.Sim.Tests;

/// <summary>C14 lot 1 : paramètres de Facile, grand livre, coûts de construction, limite de découvert, clôture de mois.</summary>
public class EconomyTests
{
    internal static string FacilePath() => Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "economy", "facile.json");

    private static JsonObject FacileJson() =>
        JsonNode.Parse(File.ReadAllText(FacilePath()), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();

    /// <summary>Facile avec quelques valeurs changées (nom JSON, valeur).</summary>
    internal static EconomyParams With(params (string Name, long Value)[] changes)
    {
        var json = FacileJson();
        foreach (var (name, value) in changes)
        {
            Assert.True(json.ContainsKey(name), name);
            json[name] = value;
        }

        return EconomyParams.Parse(json.ToJsonString());
    }

    /// <summary>Carte plate de rough, cellules de 10 m (100 m² chacune), un trou ouvert.</summary>
    private static TerrainGrid RoughField()
    {
        var t = PathingTests.Flat(20, 20, 10.0);
        for (var z = 0; z < 20; z++)
        {
            for (var x = 0; x < 20; x++)
            {
                t.PaintCell(x, z, Surface.Rough);
            }
        }

        return t;
    }

    private static Point2 CellCentre(int x, int z) => new((x + 0.5) * 10.0, (z + 0.5) * 10.0);

    [Fact]
    public void Facile_HasTheSpecValues_AndIsTheDefaultOfAWorld()
    {
        var e = EconomyParams.Load(FacilePath());

        Assert.Equal(EconomyParams.Facile, e);
        Assert.Equal(15_000_000, e.StartingMoney);
        Assert.Equal(100, e.OverdraftInterestBasisPoints);
        Assert.Equal(10_000_000, e.OverdraftLimit);
        Assert.Equal(3, e.OverdraftFreeHoles);
        Assert.Equal(100, e.PaintCost(Surface.Fairway));
        Assert.Equal(600, e.PaintCost(Surface.Green));
        Assert.Equal(20, e.PaintCost(Surface.Rough));
        Assert.Equal(200, e.ReliefPerM3);
        Assert.Equal(200_000, e.NewHole);
        Assert.Equal(15_000, e.TreeRaze);
        Assert.Equal(50, e.TreeClearPerM2);
        Assert.Equal(110_000, e.MaintenancePerHole);
        Assert.Equal(1_500, e.GreenFeeVisitor);
        Assert.Equal(144_000, e.DuesPlatinum);
        Assert.Equal(12_000, e.MoodFactorMaxBasisPoints);

        var world = new World(1, RoughField());
        Assert.Equal(EconomyParams.Facile, world.Economy);
        Assert.Equal(15_000_000, world.Ledger.Balance);
    }

    [Fact]
    public void Parse_AnyMissingParameter_IsRefused()
    {
        var names = FacileJson().Select(p => p.Key).ToList();
        Assert.Equal(typeof(EconomyParams).GetProperties().Count(p => p.PropertyType == typeof(long)), names.Count);
        foreach (var name in names)
        {
            var json = FacileJson();
            json.Remove(name);
            var error = Assert.Throws<InvalidDataException>(() => EconomyParams.Parse(json.ToJsonString()));
            Assert.Contains(name, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Parse_AnyNegativeParameter_IsRefused()
    {
        foreach (var name in FacileJson().Select(p => p.Key))
        {
            var error = Assert.Throws<InvalidDataException>(() => With((name, -1)));
            Assert.Contains(name, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Parse_UnknownParameter_IsRefused()
    {
        var json = FacileJson();
        json["paintFairwayPerM3"] = 5;

        Assert.Throws<InvalidDataException>(() => EconomyParams.Parse(json.ToJsonString()));
    }

    [Fact]
    public void PaintingFairwayOnRough_Debits100DollarsPer100m2_AndRepaintingIsFree()
    {
        var world = new World(1, RoughField());
        long? seen = null;
        world.Ledger.MoneyChanged += b => seen = b;

        world.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway); // une cellule de 100 m²
        world.Step();

        Assert.Equal(15_000_000 - 10_000, world.Ledger.Balance);
        Assert.Equal(-10_000, world.Ledger.Current(LedgerCategory.Surfaces));
        Assert.Equal(world.Ledger.Balance, seen);

        world.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway);
        world.Step();
        Assert.Equal(15_000_000 - 10_000, world.Ledger.Balance);
    }

    [Fact]
    public void ChangingAValueOfTheFile_ChangesTheDebit()
    {
        var world = new World(1, RoughField(), economy: With(("paintFairwayPerM2", 300), ("paintGreenPerM2", 7)));

        world.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway);
        world.PaintSurface(CellCentre(8, 8), 1.0, Surface.Green);
        world.Step();

        Assert.Equal(-(30_000 + 700), world.Ledger.Current(LedgerCategory.Surfaces));
    }

    [Fact]
    public void RaisingTheRelief_DebitsTheVolumeReallyMoved()
    {
        var world = new World(1, RoughField());
        var heights = world.Terrain!.Heights;
        var before = Corners(heights);

        world.RaiseTerrain(new Point2(100, 100), 35, 1.5);
        world.Step();
        var after = Corners(heights);
        var volume = before.Zip(after, (a, b) => Math.Abs(b - a)).Sum() * 10.0 * 10.0;

        Assert.True(volume > 100);
        Assert.Equal(-(long)Math.Round(volume * 200), world.Ledger.Current(LedgerCategory.Relief));

        // Au plafond de hauteur, rien ne bouge : rien n'est débité.
        var flat = new World(1, RoughField());
        for (var i = 0; i < 12; i++)
        {
            flat.RaiseTerrain(new Point2(100, 100), 35, 2.0);
            flat.Step();
        }

        var spent = flat.Ledger.Current(LedgerCategory.Relief);
        flat.RaiseTerrain(new Point2(100, 100), 1.0, 2.0);
        flat.Step();
        Assert.Equal(spent, flat.Ledger.Current(LedgerCategory.Relief));
    }

    private static double[] Corners(HeightField h)
    {
        var list = new List<double>();
        for (var j = 0; j <= h.CellsZ; j++)
        {
            for (var i = 0; i <= h.CellsX; i++)
            {
                list.Add(h.GetCornerHeight(i, j));
            }
        }

        return [.. list];
    }

    [Fact]
    public void Trees_AreDebitedInTheTreesCategory_AndARemarkableCostsDouble()
    {
        var world = new World(1, RoughField());
        world.Trees.RemoveWhere(_ => true);
        var plain = world.Trees.Add(30, 30, TreeSpeciesId.Oak, 600);
        var remarkable = world.Trees.Add(150, 150, TreeSpeciesId.Oak, 1200, remarkable: true);

        world.RazeTree(plain.Id);
        world.Step();
        Assert.Equal(-15_000, world.Ledger.Current(LedgerCategory.Trees));

        world.RazeTree(remarkable.Id);
        world.PlantTree(60, 150, TreeSpeciesId.Birch, TreePlantStage.Mound);
        world.Step();
        Assert.Equal(-(15_000 + 30_000 + 80_000), world.Ledger.Current(LedgerCategory.Trees));
        Assert.Equal(15_000_000 - 125_000, world.Ledger.Balance);
    }

    [Fact]
    public void NewHole_Costs2000Dollars()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        var t = world.Terrain!;
        Assert.True(world.AddHole(t.Course[2].Tee, t.Course[2].Hole));
        world.Step();

        Assert.Equal(4, t.Course.Count);
        Assert.Equal(-200_000, world.Ledger.Current(LedgerCategory.Holes));
    }

    /// <summary>Parcours de quatre trous ouverts, sans argent : la limite de découvert s'applique.</summary>
    private static World ThreeHolesBroke(EconomyParams e)
    {
        var world = new World(1, CourseTests.ThreeHoles(), economy: e);
        world.Step();
        Assert.Equal(3, world.OpenHoles);
        return world;
    }

    [Fact]
    public void FromThreeOpenHoles_ACommandBelowTheLimit_IsRefusedWhole()
    {
        // Rough à 400 $ le m² : une petite peinture coûte plus de 100 000 $.
        var world = ThreeHolesBroke(With(("startingMoney", 0), ("paintRoughPerM2", 40_000), ("treeRaze", 20_000_000)));
        var t = world.Terrain!;
        var balance = world.Ledger.Balance;
        var refused = new List<(LedgerCategory, long)>();
        world.Ledger.PurchaseRefused += (c, cost) => refused.Add((c, cost));
        var centre = t.Course[1].Tee;
        var surfaceVersion = t.SurfaceVersion;
        var terrainSum = t.Checksum();

        world.PaintSurface(centre, 30, Surface.Rough);
        world.Step();

        Assert.Equal(balance, world.Ledger.Balance);
        Assert.Equal(surfaceVersion, t.SurfaceVersion);
        Assert.Equal(terrainSum, t.Checksum());
        Assert.Single(refused);
        Assert.Equal(LedgerCategory.Surfaces, refused[0].Item1);
        Assert.True(refused[0].Item2 > 10_000_000 + balance);
        Assert.Equal(1, world.Ledger.Refused);

        // Un arbre trop cher reste debout ; une commande sous la limite passe.
        var tree = world.Trees.Add(centre.X + 40, centre.Z + 40, TreeSpeciesId.Oak, 600);
        world.RazeTree(tree.Id);
        world.PaintSurface(centre, 3.0, Surface.Fairway);
        world.Step();
        Assert.True(world.Trees.TryGet(tree.Id, out _));
        Assert.Equal(2, world.Ledger.Refused);
        Assert.True(world.Ledger.Balance < balance);
    }

    [Fact]
    public void FromThreeOpenHoles_ReliefAndNewHoleBelowTheLimit_AreRefusedWhole()
    {
        var world = ThreeHolesBroke(With(("startingMoney", 0), ("reliefPerM3", 20_000_000), ("newHole", 20_000_000)));
        var t = world.Terrain!;
        var terrainSum = t.Checksum();
        var heightVersion = t.HeightVersion;

        world.RaiseTerrain(t.Course[1].Tee, 15, 1.5);
        Assert.True(world.AddHole(t.Course[2].Tee, t.Course[2].Hole));
        world.Step();

        Assert.Equal(0, world.Ledger.Balance);
        Assert.Equal(heightVersion, t.HeightVersion);
        Assert.Equal(terrainSum, t.Checksum());
        Assert.Equal(3, t.Course.Count);
        Assert.Equal(3, world.OpenHoles);
        Assert.Equal(2, world.Ledger.Refused);
    }

    [Fact]
    public void UnderThreeOpenHoles_TheOverdraftHasNoLimit()
    {
        var world = new World(1, CourseTests.ThreeHoles(), economy: With(("startingMoney", 0), ("paintRoughPerM2", 40_000)));
        Assert.True(world.RemoveLastHole());
        world.Step();
        Assert.Equal(2, world.OpenHoles);

        world.PaintSurface(world.Terrain!.Course[1].Tee, 30, Surface.Rough);
        world.Step();

        Assert.True(world.Ledger.Balance < -10_000_000);
        Assert.Equal(0, world.Ledger.Refused);
    }

    [Fact]
    public void MonthEnd_DebitsMaintenancePerOpenHole_AndAddsAMonthLine()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        LedgerMonth? closed = null;
        world.Ledger.MonthClosed += m => closed = m;
        world.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway);
        world.Step();
        var surfaces = world.Ledger.Current(LedgerCategory.Surfaces);

        world.Step((int)(Time.Calendar.TicksPerDay * Time.Calendar.DaysPerMonth));

        Assert.NotNull(closed);
        Assert.Single(world.Ledger.Months);
        var line = world.Ledger.Months[0];
        Assert.Equal(0, line.Month);
        Assert.Equal(-330_000, line.Total(LedgerCategory.Maintenance));
        Assert.Equal(surfaces, line.Total(LedgerCategory.Surfaces));
        Assert.Equal(0, line.Total(LedgerCategory.Interest));
        Assert.Equal(0, world.Ledger.Current(LedgerCategory.Surfaces));
        Assert.Equal(15_000_000 + surfaces - 330_000, world.Ledger.Balance);
    }

    [Theory]
    [InlineData(GreenSpeed.Fast, RoughLength.Normal, true)]
    [InlineData(GreenSpeed.Normal, RoughLength.Short, true)]
    [InlineData(GreenSpeed.Normal, RoughLength.Normal, false)]
    [InlineData(GreenSpeed.Slow, RoughLength.Thick, false)]
    public void DemandingTurf_AddsTwentyPercentToMaintenance(GreenSpeed green, RoughLength rough, bool demanding)
    {
        var world = new World(1, CourseTests.ThreeHoles());
        world.SetTurfSettings(new TurfSettings(green, FairwayFirmness.Firm, rough));
        world.Step();
        Assert.Equal(demanding, world.Terrain!.Turf.Settings.Demanding);

        world.SkipToNextMonth();

        Assert.Equal(demanding ? -396_000 : -330_000, world.Ledger.Months[0].Total(LedgerCategory.Maintenance));
    }

    [Fact]
    public void NegativeBalance_PaysOnePercentInterestAtMonthEnd()
    {
        // Carte plate d'un trou : 1 100 $ d'entretien ; peinture de 2 000 $ sur 0 $.
        var world = new World(1, RoughField(), economy: With(("startingMoney", 0), ("paintFairwayPerM2", 2_000)));
        world.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway);
        world.Step();
        Assert.Equal(-200_000, world.Ledger.Balance);

        world.SkipToNextMonth();

        var line = world.Ledger.Months[0];
        Assert.Equal(-110_000, line.Total(LedgerCategory.Maintenance));
        Assert.Equal(-3_100, line.Total(LedgerCategory.Interest));
        Assert.Equal(-313_100, world.Ledger.Balance);

        // 1 % d'un solde qui ne tombe pas juste : tronqué au cent (−309 950 ¢ donne −3 099 ¢, pas −3 100).
        var odd = new World(1, RoughField(), economy: With(("startingMoney", 50), ("paintFairwayPerM2", 2_000)));
        odd.PaintSurface(CellCentre(5, 5), 1.0, Surface.Fairway);
        odd.Step();
        odd.SkipToNextMonth();
        Assert.Equal(-3_099, odd.Ledger.Months[0].Total(LedgerCategory.Interest));

        // Solde positif : pas d'intérêts.
        var rich = new World(1, RoughField());
        rich.SkipToNextMonth();
        Assert.Equal(0, rich.Ledger.Months[0].Total(LedgerCategory.Interest));
    }

    [Fact]
    public void Ledger_KeepsTheLast24Months()
    {
        var world = new World(1, RoughField());
        for (var i = 0; i < 30; i++)
        {
            world.SkipToNextMonth();
        }

        Assert.Equal(24, world.Ledger.Months.Count);
        Assert.Equal(6, world.Ledger.Months[0].Month);
        Assert.Equal(29, world.Ledger.Months[^1].Month);
        Assert.Equal(15_000_000 - (30 * 110_000), world.Ledger.Balance);
    }

    [Fact]
    public void SameSeedSameCommands_SameBalanceAndChecksum()
    {
        World Run()
        {
            var w = new World(5, CourseTests.ThreeHoles());
            w.PaintSurface(new Point2(150, 70), 12, Surface.Bunker);
            w.RaiseTerrain(new Point2(100, 40), 15, 1.5);
            w.Step(10);
            w.SkipToNextMonth();
            w.Step(10);
            return w;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.Ledger.Balance, b.Ledger.Balance);
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.NotEqual(new World(5, CourseTests.ThreeHoles()).Checksum(), a.Checksum());
    }
}
