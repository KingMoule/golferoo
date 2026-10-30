using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
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
        var older = body.Version < SaveFormat.Version;
        World world;
        try
        {
            world = new World(body, resolver);
        }
        catch (Exception e) when (e is not InvalidDataException)
        {
            throw new InvalidDataException("Sauvegarde illisible.", e);
        }

        // Une v11 n'a ni pavillon, ni catégorie Buildings, ni clubhouseMove : sa somme de contrôle ne se refait plus (l'empreinte du corps garde l'intégrité).
        return older || world.Checksum() == expected ? world : throw new InvalidDataException("Somme de contrôle fausse : la sauvegarde est corrompue.");
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
        w.String(Economy.ToJson());
        w.String(Progression.ToJson());
        Ledger.Write(w);
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
            w.Byte((byte)e.Kind);
        });
        WriteStroke(w);
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
        w.List(_courseCommands, c =>
        {
            w.Bool(c.Add);
            w.Point(c.Tee);
            w.Point(c.Pin);
        });
        w.Bool(_ratingStale);
        w.Int(HoleCommandsRefused);
        w.List(_heldCards, h =>
        {
            w.Int(h.Round);
            w.Long(h.Tick);
            w.List(h.Cards, c =>
            {
                w.Int(c.MemberId);
                c.Card.Write(w);
            });
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
        w.List(Flow?.Pending ?? [], w.Int);
        w.List(Flow?.CachedKeys ?? [], w.Int);
        w.Bool(_manualPro);
        w.Bool(Clubhouse is not null);
        Clubhouse?.Write(w);
        WriteArrivals(w);
    }

    private static void WriteRating(SaveWriter w, CourseRating r)
    {
        w.Int(r.Holes);
        w.Int(r.Par);
        w.Double(r.ScratchStrokes);
        w.Double(r.BogeyStrokes);
        w.Int(r.ScratchByHole?.Count ?? -1);
        foreach (var s in r.ScratchByHole ?? [])
        {
            w.Double(s);
        }
    }

    private static CourseRating ReadRating(SaveReader r)
    {
        var (holes, par, scratch, bogey) = (r.Int(), r.Int(), r.Double(), r.Double());
        var count = r.Int();
        return new(holes, par, scratch, bogey, count < 0 ? null : r.Doubles(count));
    }

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
        Economy = EconomyParams.Parse(r.String(), olderSave: r.Version < 12);
        Progression = ProgressionParams.Parse(r.String());
        Ledger = Ledger.Read(r);
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

        foreach (var e in r.List(() => new TerrainEdit(r.Point(), r.Double(), (Surface)r.Byte(), r.Double(), (EditKind)r.Byte())))
        {
            _edits.Enqueue(e.Kind <= EditKind.UndoStroke ? e : throw new InvalidDataException("Sauvegarde incohérente : retouche de genre inconnu."));
        }

        ReadStroke(r);

        foreach (var c in r.List(() => new TreeCommand((TreeCommandKind)r.Byte(), r.Int(), r.Double(), r.Double(), r.Double(), r.Bool(), (TreeSpeciesId)r.Byte(), (TreePlantStage)r.Byte())))
        {
            _treeCommands.Enqueue(c);
        }

        foreach (var c in r.List(() => (Hole: r.Int(), IsTee: r.Bool(), Point: r.Point())))
        {
            _holeCommands.Enqueue(c);
        }

        foreach (var c in r.List(() => (Add: r.Bool(), Tee: r.Point(), Pin: r.Point())))
        {
            _courseCommands.Enqueue(c);
        }

        _ratingStale = r.Bool();
        HoleCommandsRefused = r.Int();
        foreach (var h in r.List(() => (Round: r.Int(), Tick: r.Long(), Cards: r.List(() => (MemberId: r.Int(), Card: Golferoo.Sim.Scoring.ScoreCard.Read(r))).ToArray())))
        {
            _heldCards.Add((h.Round, h.Cards, h.Tick));
        }

        var holeCount = Terrain?.Course.Count ?? 1;
        if (_holesOpen.Length != holeCount || _holeCommands.Any(c => c.Hole < 1 || c.Hole > holeCount))
        {
            throw new InvalidDataException("Sauvegarde incohérente : états ou commandes de trous hors du parcours.");
        }

        if (_groups.Any(g => g.Golfers.Any(o => o.Card is null) || (!g.Finished && (g.Hole < 1 || g.Hole > holeCount || g.Golfers.Any(o => o.Card!.HoleCount < g.Hole || o.Card.HoleCount > holeCount)))))
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
            Pro = ProSkills.Read(r, Progression);
            _proMemberId = League.Members.First(m => m.IsPro).Id;
        }

        var pendingFlow = r.List(r.Int);
        var cachedFlow = r.List(r.Int);
        _manualPro = r.Bool();
        if (_manualPro && League is null)
        {
            throw new InvalidDataException("Pro joué à la main sans ligue.");
        }

        if (r.Version >= 12 && r.Bool())
        {
            Clubhouse = Building.Read(r);
            if (Terrain is null || Clubhouse.Footprint.MinX < 0 || Clubhouse.Footprint.MinZ < 0 || Clubhouse.Footprint.MaxX >= Terrain.CellsX || Clubhouse.Footprint.MaxZ >= Terrain.CellsZ)
            {
                throw new InvalidDataException("Sauvegarde incohérente : pavillon hors de la carte.");
            }
        }

        Arrivals = ArrivalParams.Default;
        if (r.Version >= 13)
        {
            _arrivalRng = DeterministicRng.Read(r);
            Reputation = r.Double();
            Visitors = VisitorPool.Read(r);
            _nextGroupId = r.Int();
        }
        else
        {
            _arrivalRng = new DeterministicRng(Seed ^ ArrivalSeedMask);
            Reputation = Arrivals.ReputationStart;
            Visitors = new VisitorPool();
            _nextGroupId = _groups.Count;
        }

        if (!double.IsFinite(Reputation) || Reputation is < 0.0 or > 1.0 || _groups.Any(g => g.Golfers.Any(o => o.VisitorId >= Visitors.Next)))
        {
            throw new InvalidDataException("Sauvegarde incohérente : réputation ou visiteur hors du vivier.");
        }

        _resolver = resolver ?? (Terrain is null ? new StubShotResolver() : new PhysicalShotResolver());
        if (Terrain is not null)
        {
            Flow = new FlowFieldCache(Terrain, pending: pendingFlow, cached: cachedFlow, obstacles: Clubhouse is null ? null : [Clubhouse.Footprint]);
            Terrain.Soil.Season = Season;
            RefreshSoil();
            if (_resolver is PhysicalShotResolver physical)
            {
                physical.Trees = Trees;
                physical.Season = Season;
                physical.BeautySources = _beautySources;
                physical.MapCapacity = MapCapacityFor(Terrain);
            }

            if (r.Version < 12 && !Terrain.Blank)
            {
                PlaceClubhouseNearTee(); // sauvegarde v11 : le pavillon se place comme à une nouvelle partie
            }
        }
    }
}
