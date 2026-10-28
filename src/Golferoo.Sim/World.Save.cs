using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Save;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim;

/// <summary>
/// Sauvegarde du monde (C11) : tout l'état entre deux ticks, à n'importe quel tick. Caches non écrits, recalculés au chargement :
/// champs de flux, cartes de jeu, sol, flaques, beauté, point de sortie. Tout nouvel état de la sim entre dans <see cref="Checksum"/> et ici.
/// </summary>
public sealed partial class World
{
    /// <summary>Écrit le monde (en-tête, somme de contrôle, corps compressé).</summary>
    public void Save(Stream output)
    {
        var body = new SaveWriter();
        Write(body);
        SaveFormat.Write(output, Checksum(), body);
    }

    /// <summary>
    /// Relit un monde sauvé par <see cref="Save"/> et vérifie sa somme de contrôle. <paramref name="resolver"/> : celui du monde d'origine
    /// s'il n'était pas celui par défaut. <see cref="InvalidDataException"/> si le fichier est faux, d'une autre version, tronqué ou corrompu.
    /// </summary>
    public static World Load(Stream input, IShotResolver? resolver = null)
    {
        var body = SaveFormat.Open(input, out var expected);
        World world;
        try
        {
            world = new World(body, resolver);
        }
        catch (Exception e) when (e is not InvalidDataException)
        {
            throw new InvalidDataException("Sauvegarde illisible.", e);
        }

        return world.Checksum() == expected ? world : throw new InvalidDataException("Somme de contrôle fausse : la sauvegarde est corrompue.");
    }

    private void Write(SaveWriter w)
    {
        w.ULong(Seed);
        foreach (var table in new[] { _climate.RainChance, _climate.HeavyRainChance, _climate.CloudChance, _climate.WindMean, _climate.WindSpread })
        {
            w.Int(table.Length);
            w.Doubles(table);
        }

        w.Long(Tick);
        w.Int(_dayOffset);
        w.Int(_skyOverrideDay);
        w.Byte((byte)_skyOverride);
        w.Int(_rainyDays);
        w.Int(_woodlandDraws);
        w.Int(_nextGolferId);
        w.List(_holesOpen, w.Bool);
        w.Long(_holeCheckedVersion);
        w.Long(TreeSpending);
        w.Int(TreeCommandsRefused);
        w.Int(TreesPlanted);
        w.Bool(CourseRating is not null);
        if (CourseRating is { } rating)
        {
            WriteRating(w, rating);
        }

        _rng.Write(w);
        w.List(_beautySources, w.Point);
        w.List(_waypoints, w.Point);
        w.Bool(Terrain is not null);
        Terrain?.Write(w);
        Trees.Write(w);
        w.List(_groups, g => g.Write(w));
        w.List(_roamers, g => g.Write(w));

        w.List(_edits, e =>
        {
            w.Point(e.Center);
            w.Double(e.Radius);
            w.Byte((byte)e.Surface);
            w.Double(e.Delta);
            w.Bool(e.IsPaint);
        });
        w.List(_treeCommands, c =>
        {
            w.Byte((byte)c.Kind);
            w.Int(c.Id);
            w.Double(c.X);
            w.Double(c.Z);
            w.Double(c.Radius);
            w.Bool(c.Flag);
            w.Byte((byte)c.Species);
            w.Byte((byte)c.Stage);
        });
        w.List(_holeCommands, c =>
        {
            w.Int(c.Hole);
            w.Bool(c.IsTee);
            w.Point(c.Point);
        });
        w.List(_turfCommands, s =>
        {
            w.Byte((byte)s.GreenSpeed);
            w.Byte((byte)s.FairwayFirmness);
            w.Byte((byte)s.RoughLength);
        });
        w.List(_ratingCommands, c => WriteRating(w, c));
        w.List(_proShots, w.ProShot);
        w.Bool(League is not null);
        League?.Write(w);
        Pro?.Write(w);
        w.Bool(_manualPro);
    }

    private static void WriteRating(SaveWriter w, CourseRating r)
    {
        w.Int(r.Holes);
        w.Int(r.Par);
        w.Double(r.ScratchStrokes);
        w.Double(r.BogeyStrokes);
    }

    private static CourseRating ReadRating(SaveReader r) => new(r.Int(), r.Int(), r.Double(), r.Double());

    /// <summary>Chargement : relit l'état dans le même ordre que <see cref="Write"/> et ne génère rien (ni boisement, ni membres).</summary>
    private World(SaveReader r, IShotResolver? resolver)
    {
        Seed = r.ULong();
        double[] Table() => r.Doubles(r.Int());
        _climate = new Climate(Table(), Table(), Table(), Table(), Table());
        Tick = r.Long();
        _dayOffset = r.Int();
        _skyOverrideDay = r.Int();
        _skyOverride = (Sky)r.Byte();
        _rainyDays = r.Int();
        _woodlandDraws = r.Int();
        _nextGolferId = r.Int();
        _holesOpen = [.. r.List(r.Bool)];
        _holeCheckedVersion = r.Long();
        TreeSpending = r.Long();
        TreeCommandsRefused = r.Int();
        TreesPlanted = r.Int();
        CourseRating = r.Bool() ? ReadRating(r) : null;
        _rng = DeterministicRng.Read(r);
        _beautySources.AddRange(r.List(r.Point));
        _waypoints.AddRange(r.List(r.Point));
        Terrain = r.Bool() ? TerrainGrid.Read(r) : null;
        Trees = Terrain is null ? TreeStand.Read(r, 1.0, 1.0, null) : TreeStand.Read(r, Terrain.CellsX * Terrain.CellSize, Terrain.CellsZ * Terrain.CellSize, Terrain);
        _groups.AddRange(r.List(() => Group.Read(r)));
        _roamers.AddRange(r.List(() => Golfer.Read(r)));

        foreach (var e in r.List(() => new TerrainEdit(r.Point(), r.Double(), (Surface)r.Byte(), r.Double(), r.Bool())))
        {
            _edits.Enqueue(e);
        }

        foreach (var c in r.List(() => new TreeCommand((TreeCommandKind)r.Byte(), r.Int(), r.Double(), r.Double(), r.Double(), r.Bool(), (TreeSpeciesId)r.Byte(), (TreePlantStage)r.Byte())))
        {
            _treeCommands.Enqueue(c);
        }

        foreach (var c in r.List(() => (Hole: r.Int(), IsTee: r.Bool(), Point: r.Point())))
        {
            _holeCommands.Enqueue(c);
        }

        var holeCount = Terrain?.Course.Count ?? 1;
        if (_holesOpen.Length != holeCount || _holeCommands.Any(c => c.Hole < 1 || c.Hole > holeCount))
        {
            throw new InvalidDataException("Sauvegarde incohérente : états ou commandes de trous hors du parcours.");
        }

        if (_groups.Any(g => g.Hole < 1 || g.Hole > holeCount || g.Golfers.Any(o => o.Card is null || o.Card.HoleCount != holeCount)))
        {
            throw new InvalidDataException("Sauvegarde incohérente : trou ou carte de score d'un groupe hors du parcours.");
        }

        foreach (var s in r.List(() => new TurfSettings((GreenSpeed)r.Byte(), (FairwayFirmness)r.Byte(), (RoughLength)r.Byte())))
        {
            _turfCommands.Enqueue(s);
        }

        foreach (var c in r.List(() => ReadRating(r)))
        {
            _ratingCommands.Enqueue(c);
        }

        foreach (var s in r.List(r.ProShot))
        {
            _proShots.Enqueue(s);
        }

        if (r.Bool())
        {
            League = LeagueSeason.Read(r, Seed);
            Pro = ProSkills.Read(r);
            _proMemberId = League.Members.First(m => m.IsPro).Id;
        }

        _manualPro = r.Bool();
        if (_manualPro && League is null)
        {
            throw new InvalidDataException("Pro joué à la main sans ligue.");
        }

        _resolver = resolver ?? (Terrain is null ? new StubShotResolver() : new PhysicalShotResolver());
        if (Terrain is not null)
        {
            Flow = new FlowFieldCache(Terrain);
            Terrain.Soil.Season = Season;
            RefreshSoil();
            if (_resolver is PhysicalShotResolver physical)
            {
                physical.Trees = Trees;
                physical.Season = Season;
                physical.BeautySources = _beautySources;
            }
        }
    }
}
