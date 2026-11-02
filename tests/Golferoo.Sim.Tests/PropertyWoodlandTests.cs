using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>
/// C24 lot 2 : boisement, arbres remarquables, surface constructible et couloirs de 400 m de la propriété générée. Chaque garantie est mesurée sur le terrain rendu
/// (jamais sur les compteurs du générateur), et chaque borne est resserrée par un test qui échoue si la garantie ne la suit plus.
/// </summary>
public class PropertyWoodlandTests
{
    private const int Seeds = 100;

    private static readonly Lazy<PropertyGenerator.Result[]> All = new(() => Enumerable.Range(1, Seeds).Select(s => PropertyGenerator.Generate((ulong)s)).ToArray());

    /// <summary>Les 100 propriétés des graines 1 à 100, générées une fois pour toute la suite.</summary>
    internal static PropertyGenerator.Result[] AllProperties => All.Value;

    private static double DistanceToCenter(double x, double z) => Math.Sqrt(Math.Pow(x - PropertyGenerator.Center.X, 2) + Math.Pow(z - PropertyGenerator.Center.Z, 2));

    /// <summary>Part de cellules Trees parmi les cellules sans eau à plus (ou, avec <paramref name="inside"/>, à moins) de <paramref name="radius"/> du pavillon.</summary>
    private static double WoodShare(TerrainGrid t, double radius, bool inside)
    {
        int wood = 0, dry = 0;
        for (var cz = 0; cz < t.CellsZ; cz++)
        {
            for (var cx = 0; cx < t.CellsX; cx++)
            {
                var c = t.CellCenter(cx, cz);
                var surface = t.GetCellSurface(cx, cz);
                if (surface != Surface.Water && (DistanceToCenter(c.X, c.Z) <= radius) == inside)
                {
                    dry++;
                    wood += surface == Surface.Trees ? 1 : 0;
                }
            }
        }

        return (double)wood / dry;
    }

    private static double Slope(TerrainGrid t, int cx, int cz)
    {
        var h = t.Heights;
        double h00 = h.GetCornerHeight(cx, cz), h10 = h.GetCornerHeight(cx + 1, cz), h01 = h.GetCornerHeight(cx, cz + 1), h11 = h.GetCornerHeight(cx + 1, cz + 1);
        double gx = (h10 + h11 - h00 - h01) / (2 * t.CellSize), gz = (h01 + h11 - h00 - h10) / (2 * t.CellSize);
        return Math.Sqrt((gx * gx) + (gz * gz));
    }

    /// <summary>Hectares sans eau, de pente au plus <paramref name="maxSlope"/>, d'un seul tenant (4 voisins) avec la cellule du pavillon.</summary>
    private static double BuildableHectares(TerrainGrid t, double maxSlope)
    {
        bool Ok(int cx, int cz) => t.GetCellSurface(cx, cz) != Surface.Water && Slope(t, cx, cz) <= maxSlope;
        var (sx, sz) = (t.CellXOf(PropertyGenerator.Center.X), t.CellZOf(PropertyGenerator.Center.Z));
        if (!Ok(sx, sz))
        {
            return 0;
        }

        var seen = new HashSet<(int, int)> { (sx, sz) };
        var stack = new Stack<(int X, int Z)>();
        stack.Push((sx, sz));
        while (stack.Count > 0)
        {
            var (x, z) = stack.Pop();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (n.Item1 >= 0 && n.Item2 >= 0 && n.Item1 < t.CellsX && n.Item2 < t.CellsZ && !seen.Contains(n) && Ok(n.Item1, n.Item2))
                {
                    seen.Add(n);
                    stack.Push(n);
                }
            }
        }

        return seen.Count * t.CellSize * t.CellSize / 10000.0;
    }

    private static int Separation(int a, int b)
    {
        var d = Math.Abs(a - b) % 360;
        return Math.Min(d, 360 - d);
    }

    /// <summary>Le couloir est libre : rectangle dans la carte dont aucune cellule (centre dans le rectangle) n'est d'eau ni trop pentue, mesuré sur le terrain.</summary>
    private static bool StripIsFree(TerrainGrid t, PropertyGenerator.Corridor c, PropertyParams p)
    {
        var angle = c.Bearing * Math.PI / 180;
        double ux = Math.Cos(angle), uz = Math.Sin(angle);
        for (var cz = 0; cz < t.CellsZ; cz++)
        {
            for (var cx = 0; cx < t.CellsX; cx++)
            {
                var center = t.CellCenter(cx, cz);
                double dx = center.X - c.Start.X, dz = center.Z - c.Start.Z;
                double along = (dx * ux) + (dz * uz), across = (-dx * uz) + (dz * ux);
                if (along >= 0 && along <= p.CorridorLength && Math.Abs(across) <= p.CorridorHalfWidth && (t.GetCellSurface(cx, cz) == Surface.Water || Slope(t, cx, cz) > p.CorridorMaxSlope))
                {
                    return false;
                }
            }
        }

        // Le rectangle tient dans la carte.
        foreach (var (along, across) in new[] { (0.0, -p.CorridorHalfWidth), (0.0, p.CorridorHalfWidth), (p.CorridorLength, -p.CorridorHalfWidth), (p.CorridorLength, p.CorridorHalfWidth) })
        {
            double x = c.Start.X + (along * ux) - (across * uz), z = c.Start.Z + (along * uz) + (across * ux);
            if (x < 0 || z < 0 || x >= t.CellsX * t.CellSize || z >= t.CellsZ * t.CellSize)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong TreesChecksum(IEnumerable<Tree> trees)
    {
        var hash = 1469598103934665603UL;
        foreach (var t in trees)
        {
            foreach (var v in new[] { BitConverter.DoubleToUInt64Bits(t.X), BitConverter.DoubleToUInt64Bits(t.Z), (ulong)t.Species, (ulong)t.AgeMonths, t.Remarkable ? 1UL : 0UL })
            {
                hash = (hash ^ v) * 1099511628211UL;
            }
        }

        return hash;
    }

    // ---------- reproductibilité ----------

    [Fact]
    public void SameSeed_SameTrees_DifferentSeeds_DifferentTrees()
    {
        var a = PropertyGenerator.CreateWithTrees(7);
        var b = PropertyGenerator.CreateWithTrees(7);

        Assert.Equal(TreesChecksum(a.Trees), TreesChecksum(b.Trees));
        Assert.Equal(a.Terrain.Checksum(), b.Terrain.Checksum());
        Assert.Equal(PropertyGenerator.Create(7).Checksum(), a.Terrain.Checksum());
        Assert.Equal(Seeds, All.Value.Select(r => TreesChecksum(r.Trees)).ToHashSet().Count);
    }

    // ---------- boisement ----------

    [Fact]
    public void Woodland_IsBetween20And30Percent_OfDryGroundOutsideTheClearedZone()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in All.Value)
        {
            Assert.InRange(WoodShare(r.Terrain, p.ClearedRadius, inside: false), 0.20, 0.30);
        }
    }

    [Theory]
    [InlineData(0.10)]
    [InlineData(0.40)]
    public void Woodland_FollowsWoodShareMinAndMax(double share)
    {
        var p = PropertyParams.Parkland with { WoodShareMin = share, WoodShareMax = share };
        for (ulong seed = 1; seed <= 15; seed++)
        {
            Assert.InRange(WoodShare(PropertyGenerator.Generate(seed, p).Terrain, p.ClearedRadius, inside: false), share - 0.005, share + 0.005);
        }
    }

    [Theory]
    [InlineData(150.0, 0.05)]
    [InlineData(150.0, 0.30)]
    [InlineData(250.0, 0.05)]
    public void ClearedZone_HoldsAtMost10PercentTrees_AndFollowsItsParameters(double radius, double share)
    {
        var p = PropertyParams.Parkland with { ClearedRadius = radius, ClearedWoodShare = share };
        for (ulong seed = 1; seed <= 15; seed++)
        {
            var measured = WoodShare(PropertyGenerator.Generate(seed, p).Terrain, radius, inside: true);

            // La clairière du pavillon, sans arbre, compte dans le dénominateur : la part mesurée est au plus celle demandée.
            Assert.InRange(measured, share * 0.8, share);
        }
    }

    [Fact]
    public void ClearedZone_OfTheShippedFile_HoldsAtMost10PercentTrees()
    {
        var p = PropertyParams.Parkland;
        Assert.Equal(150.0, p.ClearedRadius);
        foreach (var r in All.Value)
        {
            Assert.InRange(WoodShare(r.Terrain, 150.0, inside: true), 0.0, 0.10);
        }
    }

    [Fact]
    public void Clearing_HasNoTreeAndNoTreeCell()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in All.Value)
        {
            Assert.DoesNotContain(r.Trees, t => DistanceToCenter(t.X, t.Z) < p.ClearingRadius);
            for (var cz = 0; cz < r.Terrain.CellsZ; cz++)
            {
                for (var cx = 0; cx < r.Terrain.CellsX; cx++)
                {
                    var c = r.Terrain.CellCenter(cx, cz);
                    Assert.False(DistanceToCenter(c.X, c.Z) <= p.ClearingRadius && r.Terrain.GetCellSurface(cx, cz) == Surface.Trees);
                }
            }
        }
    }

    [Fact]
    public void Trees_StandOnTreeCells_NeverOnWater_AndKeepTheirSpacing()
    {
        for (var i = 0; i < 12; i++)
        {
            var r = All.Value[i];
            var massif = r.Trees.Where(t => !t.Remarkable).ToArray();
            Assert.All(massif, t => Assert.Equal(Surface.Trees, r.Terrain.SurfaceAt(t.X, t.Z)));
            Assert.All(r.Trees, t => Assert.NotEqual(Surface.Water, r.Terrain.SurfaceAt(t.X, t.Z)));
            for (var a = 0; a < r.Trees.Count; a++)
            {
                for (var b = a + 1; b < r.Trees.Count; b++)
                {
                    var (ta, tb) = (r.Trees[a], r.Trees[b]);
                    var min = WoodlandGenerator.MinSpacingFactor * (ta.CrownRadius() + tb.CrownRadius());
                    Assert.True(Math.Pow(ta.X - tb.X, 2) + Math.Pow(ta.Z - tb.Z, 2) >= min * min, $"graine {i + 1} : arbres {ta.Id} et {tb.Id} trop proches");
                }
            }
        }
    }

    [Fact]
    public void Trees_StayUnderTheBudget_AndThreeSpeciesOfVariedAgesGrow()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in All.Value)
        {
            Assert.InRange(r.Trees.Count, 200, p.MaxTrees);
            Assert.Equal(3, r.Trees.Select(t => t.Species).Distinct().Count());
            var ratios = r.Trees.Where(t => !t.Remarkable).Select(t => (double)t.AgeMonths / TreeSpeciesTable.Get(t.Species).AdultAgeMonths).ToArray();
            Assert.True(ratios.Min() < 0.6 && ratios.Max() > 1.2, $"âges de {ratios.Min():F2} à {ratios.Max():F2} fois l'âge adulte");
            Assert.True(ratios.Count(a => a < 1.0) > ratios.Length / 5 && ratios.Count(a => a >= 1.0) > ratios.Length / 5, "il manque des jeunes ou des adultes");
        }
    }

    [Fact]
    public void TreeBudget_IsEnforced_ByRetryThenAClearException()
    {
        var p = PropertyParams.Parkland with { MaxTrees = 100 };

        var e = Assert.Throws<InvalidOperationException>(() => PropertyGenerator.Generate(3, p));

        Assert.Contains("8 essais", e.Message);
        Assert.Contains("graine 3", e.Message);
        Assert.Contains("arbres", e.Message);
    }

    // ---------- arbres remarquables ----------

    [Fact]
    public void Remarkables_AreTenToTwenty_VeryOld_IsolatedAndNearTheCenter()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in All.Value)
        {
            var remarkables = r.Trees.Where(t => t.Remarkable).ToArray();
            Assert.InRange(remarkables.Length, 10, 20);
            foreach (var t in remarkables)
            {
                Assert.True(t.AgeMonths >= Tree.RemarkableAgeFactor * TreeSpeciesTable.Get(t.Species).AdultAgeMonths);
                Assert.InRange(DistanceToCenter(t.X, t.Z), p.ClearingRadius, p.RemarkableMaxDistance);
                for (var cz = 0; cz < r.Terrain.CellsZ; cz++)
                {
                    for (var cx = 0; cx < r.Terrain.CellsX; cx++)
                    {
                        var c = r.Terrain.CellCenter(cx, cz);
                        var near = Math.Sqrt(Math.Pow(c.X - t.X, 2) + Math.Pow(c.Z - t.Z, 2)) <= p.RemarkableIsolation - r.Terrain.CellSize;
                        Assert.False(near && r.Terrain.GetCellSurface(cx, cz) is Surface.Trees or Surface.Water, $"remarquable {t.Id} contre un massif ou l'eau");
                    }
                }
            }

            Assert.All(r.Trees.Where(t => !t.Remarkable), t => Assert.True(t.AgeMonths < Tree.RemarkableAgeFactor * TreeSpeciesTable.Get(t.Species).AdultAgeMonths));
        }
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(25, 25)]
    public void Remarkables_FollowTheirCountBounds(int min, int max)
    {
        var p = PropertyParams.Parkland with { RemarkablesMin = min, RemarkablesMax = max };
        for (ulong seed = 1; seed <= 10; seed++)
        {
            Assert.Equal(min, PropertyGenerator.Generate(seed, p).Remarkables);
        }
    }

    [Fact]
    public void Remarkables_FollowTheirDistanceAndIsolationBounds()
    {
        var p = PropertyParams.Parkland with { RemarkableMaxDistance = 100.0, RemarkableIsolation = 30.0 };
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var r = PropertyGenerator.Generate(seed, p);
            foreach (var t in r.Trees.Where(t => t.Remarkable))
            {
                Assert.InRange(DistanceToCenter(t.X, t.Z), p.ClearingRadius + p.RemarkableIsolation, 100.0);
            }
        }
    }

    [Fact]
    public void Remarkables_ThatCannotBePlaced_FailTheAttempt()
    {
        var p = PropertyParams.Parkland with { RemarkablesMin = 20, RemarkablesMax = 20, RemarkableSpacing = 400.0 };

        var e = Assert.Throws<InvalidOperationException>(() => PropertyGenerator.Generate(1, p));

        Assert.Contains("remarquable", e.Message);
    }

    // ---------- surface constructible ----------

    [Fact]
    public void BuildableGround_IsAtLeast55Hectares_InOnePieceAroundTheClubhouse()
    {
        foreach (var r in All.Value)
        {
            Assert.True(BuildableHectares(r.Terrain, 0.08) >= 55.0, $"{BuildableHectares(r.Terrain, 0.08):F1} ha");
        }
    }

    [Theory]
    [InlineData(0.08, 94.0)]
    [InlineData(0.04, 70.0)]
    public void BuildableGround_FollowsItsBounds_AndRetriesWhenTooSmall(double slope, double hectares)
    {
        var p = PropertyParams.Parkland with { BuildableMinHa = hectares, BuildableMaxSlope = slope };
        var retried = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            try
            {
                var r = PropertyGenerator.Generate(seed, p);
                Assert.True(BuildableHectares(r.Terrain, slope) >= hectares);
                retried += r.Attempts > 1 ? 1 : 0;
            }
            catch (InvalidOperationException e)
            {
                Assert.Contains("surface constructible", e.Message);
            }
        }

        Assert.True(retried > 0, "la borne serrée n'a jamais provoqué de nouvel essai");
    }

    [Fact]
    public void BuildableGround_ThatCannotBeFound_Throws()
    {
        var p = PropertyParams.Parkland with { BuildableMinHa = 96.0 };

        var e = Assert.Throws<InvalidOperationException>(() => PropertyGenerator.Generate(2, p));

        Assert.Contains("surface constructible", e.Message);
    }

    // ---------- couloirs de 400 m ----------

    [Fact]
    public void Corridors_AtLeastThree_FreeOfWater_Gentle_AndTwoDirectionsSixtyDegreesApart()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in All.Value)
        {
            Assert.True(r.Corridors.Count >= 3);
            Assert.True(r.Corridors.Select(c => c.Bearing).Distinct().Count() >= 3);
            Assert.True(r.Corridors.Max(a => r.Corridors.Max(b => Separation(a.Bearing, b.Bearing))) >= 60);
            foreach (var c in r.Corridors)
            {
                Assert.True(DistanceToCenter(c.Start.X, c.Start.Z) <= p.CorridorStartRadius);
                Assert.True(StripIsFree(r.Terrain, c, p), $"couloir de cap {c.Bearing}° depuis ({c.Start.X:F0} ; {c.Start.Z:F0})");
            }
        }
    }

    [Theory]
    [InlineData("length", 1500.0)]
    [InlineData("halfWidth", 600.0)]
    [InlineData("slope", 0.001)]
    [InlineData("count", 25.0)]
    public void Corridors_ThatCannotBeFound_Throw(string bound, double value)
    {
        var p = bound switch
        {
            "length" => PropertyParams.Parkland with { CorridorLength = value },
            "halfWidth" => PropertyParams.Parkland with { CorridorHalfWidth = value },
            "slope" => PropertyParams.Parkland with { CorridorMaxSlope = value },
            _ => PropertyParams.Parkland with { CorridorsMin = (int)value },
        };

        var e = Assert.Throws<InvalidOperationException>(() => PropertyGenerator.Generate(4, p));

        Assert.Contains("couloirs", e.Message);
    }

    [Fact]
    public void Corridors_FollowTheirTightenedBounds_OnWhatTheyReturn()
    {
        var p = PropertyParams.Parkland with { CorridorMaxSlope = 0.05, CorridorHalfWidth = 40.0, CorridorSpreadDegrees = 120, CorridorStartRadius = 100.0 };
        var retried = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            try
            {
                var r = PropertyGenerator.Generate(seed, p);
                retried += r.Attempts > 1 ? 1 : 0;
                Assert.True(r.Corridors.Max(a => r.Corridors.Max(b => Separation(a.Bearing, b.Bearing))) >= 120);
                Assert.All(r.Corridors, c =>
                {
                    Assert.True(DistanceToCenter(c.Start.X, c.Start.Z) <= 100.0);
                    Assert.True(StripIsFree(r.Terrain, c, p));
                });
            }
            catch (InvalidOperationException e)
            {
                Assert.Contains("couloirs", e.Message);
            }
        }

        Assert.True(retried > 0, "la borne serrée n'a jamais provoqué de nouvel essai");
    }

    // ---------- reprise de graine ----------

    [Fact]
    public void Retry_IsDeterministic_AtMostEightAttempts_AndSometimesNeeded()
    {
        var attempts = All.Value.Select(r => r.Attempts).ToArray();

        Assert.InRange(attempts.Max(), 1, 8);
        Assert.Contains(attempts, a => a > 1);
        Assert.Equal(attempts.Take(20), Enumerable.Range(1, 20).Select(s => PropertyGenerator.Generate((ulong)s).Attempts));
    }

    [Fact]
    public void ImpossibleBounds_ThrowAClearMessage_NamingTheSeedAndEachFailure()
    {
        var p = PropertyParams.Parkland with { CorridorLength = 5000.0 };

        var e = Assert.Throws<InvalidOperationException>(() => PropertyGenerator.Generate(11, p));

        Assert.Contains("graine 11", e.Message);
        Assert.Contains("parkland.json", e.Message);
        Assert.Contains("couloirs", e.Message);
        Assert.Equal(8, e.Message.Split(" ; ").Length);
    }

    // ---------- monde ----------

    [Fact]
    public void GeneratedProperty_SaveRoundTrip_KeepsTerrainAndTrees()
    {
        foreach (ulong seed in new ulong[] { 3, 42 })
        {
            var property = PropertyGenerator.CreateWithTrees(seed);
            var world = new World(seed, property.Terrain, plantedTrees: property.Trees);

            using var stream = new MemoryStream();
            world.Save(stream);
            var loaded = World.Load(new MemoryStream(stream.ToArray()));

            Assert.Equal(world.Checksum(), loaded.Checksum());
            Assert.Equal(property.Trees.Count, loaded.Trees.Count);
            Assert.Equal(property.Trees.Count(t => t.Remarkable), loaded.Trees.Trees.Count(t => t.Remarkable));
            Assert.Equal(property.Terrain.Checksum(), loaded.Terrain!.Checksum());
        }
    }

    [Fact]
    public void NewGame_OnGeneratedProperties_HoldsThirtyDays_AndReloadsIdentically()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var property = PropertyGenerator.CreateWithTrees(seed);
            var world = new World(seed, property.Terrain, plantedTrees: property.Trees);

            world.Step(30 * Time.Calendar.TicksPerDay);
            using var stream = new MemoryStream();
            world.Save(stream);

            Assert.Equal(world.Checksum(), World.Load(new MemoryStream(stream.ToArray())).Checksum());
        }
    }

    [Fact]
    public void Facile_CanAffordClearingTheTreesOfAFirstThreeHoles()
    {
        // Trois trous = trois couloirs de 400 m (marge large : deux trous tiennent dans un couloir) ; abattre chaque arbre au tarif de Facile, remarquables au double.
        var economy = Economy.EconomyParams.Facile;
        foreach (var r in All.Value)
        {
            var costs = r.Corridors.Select(c =>
            {
                var angle = c.Bearing * Math.PI / 180;
                double ux = Math.Cos(angle), uz = Math.Sin(angle);
                return r.Trees.Where(t =>
                {
                    double dx = t.X - c.Start.X, dz = t.Z - c.Start.Z;
                    var along = (dx * ux) + (dz * uz);
                    return along >= 0 && along <= 400 && Math.Abs((-dx * uz) + (dz * ux)) <= 15;
                }).Sum(economy.RazeCost);
            }).Order().Take(3).Sum();

            Assert.True(costs * 4 < economy.StartingMoney, $"{costs / 100} $ de défrichage pour 3 couloirs");
            Assert.True(r.Trees.Where(t => DistanceToCenter(t.X, t.Z) <= 150.0).Sum(economy.RazeCost) * 4 < economy.StartingMoney, "zone dégagée trop chère à raser");
        }
    }
}
