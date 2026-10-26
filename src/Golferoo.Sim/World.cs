using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim;

/// <summary>
/// Monde simulé, avancé par ticks fixes. Le rendu lit son état et lui envoie des commandes ;
/// il ne connaît pas Godot.
/// </summary>
public sealed partial class World
{
    /// <summary>Fréquence fixe de la simulation (spec C01 : 20 Hz).</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Durée d'un tick en secondes.</summary>
    public const double TickSeconds = 1.0 / TicksPerSecond;

    /// <summary>Durée d'un coup (geste), en ticks.</summary>
    private const int SwingTicksCount = TicksPerSecond;

    /// <summary>Ramassage de la balle après ce nombre de coups : garde-fou contre une partie sans fin.</summary>
    public const int MaxStrokes = 12;

    /// <summary>Feuille de départs de la ligue : le groupe n d'une journée peut jouer n × cet intervalle après la création des groupes (environ 0,08 jour de jeu).</summary>
    public const int LeagueTeeInterval = 600;

    private const int WaypointPoolSize = 16;

    private readonly DeterministicRng _rng;
    private readonly IShotResolver _resolver;
    private readonly List<Group> _groups = [];
    private readonly List<Golfer> _roamers = [];
    private readonly List<Point2> _waypoints = [];
    private int _nextGolferId;
    private Point2? _exitPoint;
    private long _exitPointVersion = -1;

    public World(ulong seed, TerrainGrid? terrain = null, IShotResolver? resolver = null, bool league = false)
    {
        Seed = seed;
        Terrain = terrain;
        _rng = new DeterministicRng(seed);
        _resolver = resolver ?? (terrain is null ? new StubShotResolver() : new PhysicalShotResolver());
        Flow = terrain is null ? null : new FlowFieldCache(terrain);
        if (league && terrain is not null)
        {
            League = new LeagueSeason(seed, LeagueSeason.GenerateMembers(seed));
            Pro = new ProSkills();
            _proMemberId = League.Members.First(m => m.IsPro).Id;
        }

        if (terrain is not null)
        {
            Trees = new TreeStand(terrain.CellsX * terrain.CellSize, terrain.CellsZ * terrain.CellSize, terrain);
            foreach (var p in terrain.IsolatedTrees)
            {
                Trees.Add(p.X, p.Z, p.Species, p.AgeMonths);
            }

            WoodlandGenerator.Fill(Trees, terrain, new CellRect(0, 0, terrain.CellsX - 1, terrain.CellsZ - 1), NextWoodlandRng());
            terrain.Soil.Season = Season;
            RefreshSoil();
            if (_resolver is PhysicalShotResolver physical)
            {
                physical.Trees = Trees;
                physical.Season = Season;
            }
        }
        else
        {
            Trees = new TreeStand(1.0, 1.0);
        }
    }

    /// <summary>Ligue du club : nulle sauf si le monde est créé avec <c>league: true</c> et un terrain (le monde de C01 à C05 n'en a pas).</summary>
    public LeagueSeason? League { get; }

    /// <summary>Arbres du monde (nul coût de marche : seul le sous-bois, surface Trees, bloque les golfeurs).</summary>
    public TreeStand Trees { get; }

    private int _dayOffset;

    /// <summary>Date de jeu, lue depuis le tick (et le décalage de debug).</summary>
    public Date Date => Calendar.At(Tick, _dayOffset);

    /// <summary>Saison de la date : règle la densité des couronnes que traverse la balle.</summary>
    public Season Season => Date.Season;

    /// <summary>Climat qui tire la météo (tempéré par défaut ; <see cref="Climate.Calm"/> rend le comportement d'avant la météo).</summary>
    public Climate Climate
    {
        get => _climate;
        init
        {
            _climate = value;
            RefreshSoil();
        }
    }

    private readonly Climate _climate = Climate.Temperate;

    private int _skyOverrideDay = -1;
    private Sky _skyOverride;

    /// <summary>Jours écoulés depuis le début de la partie, décalage de debug compris.</summary>
    public int DayIndex => (int)(Tick / Calendar.TicksPerDay) + _dayOffset;

    /// <summary>Temps du jour, tiré de la graine et du numéro du jour (et du ciel imposé par <see cref="CycleSky"/>).</summary>
    public WeatherDay Weather => WeatherOn(DayIndex);

    /// <summary>Vent du moment, en m/s.</summary>
    public Vec3 Wind => Weather.WindAt(Date.DayFraction);

    /// <summary>Ciel annoncé dans <paramref name="daysAhead"/> jours (parfois faux d'un cran).</summary>
    public Sky Forecast(int daysAhead) => WeatherGenerator.Forecast(Seed, Climate, DayIndex + daysAhead);

    /// <summary>Debug (F9) : impose pour la journée le ciel suivant (soleil, nuages, pluie, forte pluie, puis soleil).</summary>
    public void CycleSky()
    {
        _skyOverride = (Sky)(((int)Weather.Sky + 1) % 4);
        _skyOverrideDay = DayIndex;
        RefreshSoil();
    }

    /// <summary>Consignes d'entretien du gazon, en file comme les autres commandes (appliquées au début du tick suivant, dans la somme de contrôle) : prises en compte au sol alors et à la hauteur visée au prochain changement de jour. Sans effet sans terrain.</summary>
    public void SetTurfSettings(TurfSettings settings)
    {
        if (Terrain is not null)
        {
            _turfCommands.Enqueue(settings);
        }
    }

    private readonly Queue<TurfSettings> _turfCommands = new();

    /// <summary>Note du parcours reçue par <see cref="SetCourseRating"/>, nulle tant qu'aucune n'est arrivée.</summary>
    public CourseRating? CourseRating { get; private set; }

    /// <summary>Note du parcours calculée hors du monde (<see cref="CourseRater"/>), en file comme les autres commandes : appliquée au début du tick suivant, dans la somme de contrôle.</summary>
    public void SetCourseRating(CourseRating rating)
    {
        ArgumentNullException.ThrowIfNull(rating);
        _ratingCommands.Enqueue(rating);
    }

    private readonly Queue<CourseRating> _ratingCommands = new();

    private static void MixRating(ref ulong hash, CourseRating r)
    {
        Mix(ref hash, ((ulong)(uint)r.Holes << 32) | (uint)r.Par);
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(r.ScratchStrokes));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(r.BogeyStrokes));
    }

    private void ApplyTurfCommands()
    {
        while (_turfCommands.Count > 0 && Terrain is not null)
        {
            Terrain.Turf.Settings = _turfCommands.Dequeue();
            RefreshSoil();
        }
    }

    /// <summary>Chance qu'un arbre adulte tombe pendant une journée de tempête (forte pluie, ou vent de base d'au moins <see cref="StormWind"/>).</summary>
    public const double StormFallChance = 1.0 / 2000.0;

    /// <summary>Vent de base d'une tempête, en m/s (50 km/h).</summary>
    public const double StormWind = 14.0;

    /// <summary>Jours de pluie qui, dans un mois de printemps, font pousser les jeunes arbres d'un mois de plus (M9).</summary>
    public const int SpringRainDays = 3;

    /// <summary>Temps du jour <paramref name="day"/>, avec le ciel imposé par <see cref="CycleSky"/>.</summary>
    private WeatherDay WeatherOn(int day)
    {
        var w = WeatherGenerator.For(Seed, Climate, day);
        return _skyOverrideDay == day ? w with { Sky = _skyOverride } : w;
    }

    /// <summary>Humidité du sol, sans état : 1 s'il pleut aujourd'hui, 0,5 s'il a fortement plu hier, 0 sinon.</summary>
    public double SoilWetness => Weather.Sky >= Sky.Rain ? 1.0 : WeatherOn(DayIndex - 1).Sky == Sky.HeavyRain ? 0.5 : 0.0;

    private void RefreshSoil()
    {
        if (Terrain is not null)
        {
            Terrain.Soil.Wetness = SoilWetness;
        }
    }

    /// <summary>Fin de la journée <paramref name="day"/> : une tempête peut abattre quelques arbres adultes, couchés dans le sens du vent à ±30°.</summary>
    private void EndOfDay(int day)
    {
        var w = WeatherOn(day);
        _rainyDays += w.Sky >= Sky.Rain ? 1 : 0;
        if (Terrain is null || (w.Sky != Sky.HeavyRain && w.WindBase < StormWind))
        {
            return;
        }

        var rng = new DeterministicRng(Seed ^ (0xA0761D6478BD642FUL * (ulong)(uint)(day + 1)));
        var yaw = Math.Atan2(w.WindDirZ, w.WindDirX);
        var victims = new List<(int Id, double Direction)>();
        foreach (var t in Trees.Trees)
        {
            if (t.State == TreeState.Standing && t.Height() >= Tree.YoungMaxHeight && rng.NextDouble() < StormFallChance)
            {
                victims.Add((t.Id, yaw + ((rng.NextDouble() - 0.5) * Math.PI / 3.0)));
            }
        }

        foreach (var (id, direction) in victims)
        {
            ApplyFell(Terrain, id, direction);
        }
    }

    /// <summary>Debug : saute au 1er du mois suivant et fait pousser les arbres.</summary>
    public void SkipToNextMonth()
    {
        _dayOffset += Calendar.DaysPerMonth - Date.Day + 1;
        _rainyDays = 0;
        GrowTrees();
    }

    private void GrowTrees()
    {
        Trees.Grow(Calendar.TreeMonthsPerMonth);
        RefreshSoil();
        if (Terrain is not null)
        {
            Terrain.Soil.Season = Season;
        }

        if (_resolver is PhysicalShotResolver physical)
        {
            physical.Season = Season;
        }
    }

    /// <summary>Jours de pluie du mois en cours (comptés à la fin de chaque journée, ciel imposé compris).</summary>
    private int _rainyDays;

    /// <summary>M9 : un mois de printemps (qui finit le jour <paramref name="lastDay"/>) assez pluvieux fait pousser les jeunes arbres d'un mois de plus.</summary>
    private void SpringRain(int lastDay)
    {
        if (_rainyDays >= SpringRainDays && Calendar.At((long)lastDay * Calendar.TicksPerDay, 0).Season == Season.Spring)
        {
            Trees.Grow(Calendar.TreeMonthsPerMonth, youngOnly: true);
        }

        _rainyDays = 0;
    }

    private int _woodlandDraws;

    /// <summary>RNG propre au boisement, dérivé de la graine et du rang du tirage : ne touche pas au RNG du tick.</summary>
    private DeterministicRng NextWoodlandRng() => new(Seed ^ (0x7EE5BA5E00000000UL + (ulong)_woodlandDraws++));

    public ulong Seed { get; }

    /// <summary>Terrain du trou, s'il y en a un (son état entre dans la somme de contrôle).</summary>
    public TerrainGrid? Terrain { get; }

    /// <summary>Cache de champs de flux du terrain, s'il y en a un.</summary>
    public FlowFieldCache? Flow { get; }

    /// <summary>Nombre de ticks écoulés depuis la création.</summary>
    public long Tick { get; private set; }

    public IReadOnlyList<Group> Groups => _groups;

    /// <summary>Golfeurs du mode charge.</summary>
    public IReadOnlyList<Golfer> Roamers => _roamers;

    /// <summary>Point où les golfeurs sortent du green (près du green, hors green et hors bunker).</summary>
    public Point2 ExitPoint
    {
        get
        {
            var terrain = RequireTerrain();
            if (_exitPoint is null || _exitPointVersion != terrain.SurfaceVersion)
            {
                _exitPoint = ComputeExitPoint(terrain);
                _exitPointVersion = terrain.SurfaceVersion;
            }

            return _exitPoint.Value;
        }
    }

    /// <summary>Ajoute une paire de golfeurs à l'entrée du tee. Elle joue quand la précédente a quitté le green.</summary>
    public Group SpawnGroup() => AddGroup(2, null, -1);

    /// <summary>Partie d'entraînement : votre pro seul, hors ligue (sa carte ne compte pas pour son handicap). Exige la ligue.</summary>
    public Group StartProRound() =>
        AddGroup(1, [(League ?? throw new InvalidOperationException("Pas de ligue, donc pas de pro.")).Members.First(m => m.IsPro)], -1);

    /// <summary>Groupe de <paramref name="count"/> golfeurs ; ceux d'une journée de ligue portent leur membre et son niveau.</summary>
    private Group AddGroup(int count, Member[]? members, int leagueRound)
    {
        var terrain = RequireTerrain();
        var spawn = new Point2(terrain.Tee.X - 10.0, terrain.Tee.Z);
        var golfers = new List<Golfer>(count);
        for (var i = 0; i < count; i++)
        {
            var p = GridQueries.NearestWalkablePoint(terrain, new Point2(spawn.X, spawn.Z + ((i * 3.0) - 1.5)));
            var golfer = new Golfer(_nextGolferId++, p, GolferState.Arriving) { Ball = terrain.Tee };
            if (members is not null)
            {
                golfer.MemberId = members[i].Id;
                golfer.Skill = members[i].Skill;
            }

            golfers.Add(golfer);
        }

        foreach (var g in golfers)
        {
            g.InLeague = leagueRound >= 0;
        }

        var group = new Group(_groups.Count, golfers) { LeagueRound = leagueRound };
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Mode charge : remplace les golfeurs du mode charge par <paramref name="golfers"/> marcheurs qui vont en boucle
    /// entre des points tirés au RNG sur des cellules franchissables, sans jouer.
    /// </summary>
    public void SetLoadMode(int golfers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(golfers);
        var terrain = RequireTerrain();
        _roamers.Clear();
        _waypoints.Clear();
        if (golfers == 0)
        {
            return;
        }

        // Seules les cellules reliées au tee comptent : un golfeur ne vise jamais une poche isolée.
        var reference = Flow!.Get(terrain.Tee);
        for (var tries = 0; _waypoints.Count < WaypointPoolSize && tries < 2000; tries++)
        {
            if (TryRandomReachableCell(terrain, reference, out var p))
            {
                _waypoints.Add(p);
            }
        }

        if (_waypoints.Count == 0)
        {
            _waypoints.Add(terrain.CellCenter(reference.DestX, reference.DestZ));
        }

        for (var i = 0; i < golfers; i++)
        {
            var start = TryRandomReachableCell(terrain, reference, out var p) ? p : _waypoints[0];
            _roamers.Add(new Golfer(_nextGolferId++, start, GolferState.Roaming)
            {
                WaypointIndex = (int)(_rng.NextUInt64() % (ulong)_waypoints.Count),
            });
        }
    }

    /// <summary>Une paire de ligue a fini : la carte de chaque joueur (balle ramassée = 12 coups) revient à la ligue.</summary>
    private void ReturnLeagueCards(TerrainGrid terrain, Group group)
    {
        var cards = new List<(int, ScoreCard)>(group.Golfers.Count);
        foreach (var g in group.Golfers)
        {
            var card = new ScoreCard([terrain.Par]);
            if (g.PickedUp)
            {
                card.PickUp(0);
            }
            else
            {
                card.Record(0, g.Strokes);
            }

            cards.Add((g.MemberId, card));
        }

        League!.GroupFinished(group.LeagueRound, cards, CourseRating, Tick);
    }

    public void Step()
    {
        // Le tirage garde l'état du RNG lié au temps : la somme de contrôle prouve le déterminisme même à vide.
        _rng.NextUInt64();
        ApplyPendingEdits();
        ApplyTreeCommands();
        ApplyTurfCommands();
        ApplyProShots();
        while (_ratingCommands.Count > 0)
        {
            CourseRating = _ratingCommands.Dequeue();
        }

        if (Terrain is not null)
        {
            if (_resolver is PhysicalShotResolver physical)
            {
                physical.Wind = Wind;
            }

            for (var i = 0; i < _groups.Count; i++)
            {
                UpdateGroup(Terrain, i);
            }

            for (var i = 0; i < _roamers.Count; i++)
            {
                UpdateRoamer(Terrain, _roamers[i]);
            }
        }

        var month = Calendar.MonthIndex(Tick, _dayOffset);
        var day = DayIndex;
        Tick++;
        if (DayIndex != day)
        {
            EndOfDay(day);
            Terrain?.Turf.NextDay(Season, WeatherOn(day).Sky >= Sky.Rain, DayIndex);
            if (Terrain is not null && PinPicker.Pick(Terrain, Seed, DayIndex) is { } pin)
            {
                Terrain.MovePin(pin);
            }

            RefreshSoil();
            if (League?.OnNewDay(DayIndex, WeatherOn(DayIndex).Sky == Sky.HeavyRain, Tick) is { } round)
            {
                for (var n = 0; n < round.Pairs.Count; n++)
                {
                    var pair = round.Pairs[n];
                    AddGroup(pair.Length, [.. pair.Select(id => League.Members[id])], round.Number).ReleaseTick = Tick + ((long)n * LeagueTeeInterval);
                }
            }
        }

        if (Calendar.MonthIndex(Tick, _dayOffset) != month)
        {
            SpringRain(day);
            GrowTrees();
        }
    }

    public void Step(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step();
        }
    }

    /// <summary>Instantané en lecture seule pour le rendu. Réutilise <paramref name="reuse"/> si fourni.</summary>
    public WorldSnapshot Snapshot(WorldSnapshot? reuse = null)
    {
        var snap = reuse ?? new WorldSnapshot();
        snap.Tick = Tick;
        if (snap.TreesVersion != Trees.Version)
        {
            snap.TreeItems.Clear();
            snap.TreeItems.AddRange(Trees.Trees);
            snap.TreesVersion = Trees.Version;
        }

        var items = snap.Items;
        items.Clear();
        foreach (var group in _groups)
        {
            foreach (var g in group.Golfers)
            {
                items.Add(new GolferView(g.Id, group.Id, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g)));
            }
        }

        foreach (var g in _roamers)
        {
            items.Add(new GolferView(g.Id, -1, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g)));
        }

        return snap;
    }

    private Vec3 BallPosition(Golfer g)
    {
        if (g.State == GolferState.BallInFlight && g.FlightPath is { } path && path.Count > 0)
        {
            return path[Math.Min(g.FlightIndex, path.Count - 1)];
        }

        return new Vec3(g.Ball.X, Terrain?.Sample(g.Ball.X, g.Ball.Z) ?? 0.0, g.Ball.Z);
    }

    /// <summary>Somme de contrôle de l'état (FNV-1a 64 bits) : même graine et mêmes commandes, même valeur.</summary>
    public ulong Checksum()
    {
        var hash = 0xCBF29CE484222325UL;
        Mix(ref hash, Seed);
        Mix(ref hash, (ulong)Tick);
        Mix(ref hash, (ulong)_dayOffset);
        Mix(ref hash, ((ulong)(uint)_skyOverrideDay << 16) ^ ((ulong)_skyOverride << 8) ^ (ulong)_rainyDays);
        Mix(ref hash, _rng.StateHash());
        if (Terrain is not null)
        {
            Mix(ref hash, Terrain.Checksum());
            Mix(ref hash, Terrain.Turf.Checksum());
        }

        MixPendingEdits(ref hash);
        MixTreeCommands(ref hash);
        foreach (var c in _turfCommands)
        {
            Mix(ref hash, (byte)c.GreenSpeed | ((ulong)(byte)c.FairwayFirmness << 8) | ((ulong)(byte)c.RoughLength << 16));
        }

        if (CourseRating is { } rating)
        {
            MixRating(ref hash, rating);
        }

        League?.MixInto(ref hash);
        MixPro(ref hash);

        foreach (var c in _ratingCommands)
        {
            Mix(ref hash, 1UL);
            MixRating(ref hash, c);
        }

        Mix(ref hash, (ulong)Trees.Count);
        foreach (var t in Trees.Trees)
        {
            Mix(ref hash, (ulong)t.Id);
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(t.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(t.Z));
            Mix(ref hash, ((ulong)t.Species << 32) ^ (uint)t.AgeMonths ^ ((ulong)t.State << 48) ^ (t.Remarkable ? 1UL << 56 : 0UL));
            Mix(ref hash, (ulong)BitConverter.SingleToInt32Bits(t.FallYaw));
        }

        Mix(ref hash, (ulong)_groups.Count);
        foreach (var group in _groups)
        {
            Mix(ref hash, group.Started ? 1UL : 0UL);
            foreach (var g in group.Golfers)
            {
                MixGolfer(ref hash, g);
            }
        }

        Mix(ref hash, (ulong)_roamers.Count);
        foreach (var g in _roamers)
        {
            MixGolfer(ref hash, g);
        }

        return hash;
    }

    private static void MixGolfer(ref ulong hash, Golfer g)
    {
        Mix(ref hash, (ulong)g.Id);
        if (g.MemberId >= 0)
        {
            Mix(ref hash, (ulong)g.MemberId);
        }

        Mix(ref hash, (ulong)g.State);
        Mix(ref hash, (ulong)g.Strokes);
        Mix(ref hash, (ulong)g.SwingTicks);
        Mix(ref hash, (ulong)g.WaypointIndex);
        Mix(ref hash, g.Holed ? 1UL : 0UL);
        Mix(ref hash, g.PickedUp ? 1UL : 0UL);
        Mix(ref hash, (ulong)g.FlightIndex);
        if (g.State == GolferState.BallInFlight && g.FlightPath is { } path && path.Count > 0)
        {
            var p = path[Math.Min(g.FlightIndex, path.Count - 1)];
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Y));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Z));
        }

        if (g.PlannedShot is { } shot)
        {
            MixShot(ref hash, shot);
        }

        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.Z));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.Z));
    }

    internal static void Mix(ref ulong hash, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= 0x100000001B3UL;
        }
    }

    private static Point2 ComputeExitPoint(TerrainGrid terrain)
    {
        var want = new Point2(terrain.Green.X, terrain.Green.Z + 20.0);
        var best = double.MaxValue;
        var found = false;
        var result = want;
        for (var cz = 0; cz < terrain.CellsZ; cz++)
        {
            for (var cx = 0; cx < terrain.CellsX; cx++)
            {
                var s = terrain.GetCellSurface(cx, cz);
                if (s is Surface.Green or Surface.Bunker || !terrain.IsWalkable(cx, cz))
                {
                    continue;
                }

                var c = terrain.CellCenter(cx, cz);
                var d = ((c.X - want.X) * (c.X - want.X)) + ((c.Z - want.Z) * (c.Z - want.Z));
                if (d < best)
                {
                    best = d;
                    result = c;
                    found = true;
                }
            }
        }

        return found ? result : GridQueries.NearestWalkablePoint(terrain, want);
    }

    private TerrainGrid RequireTerrain() =>
        Terrain ?? throw new InvalidOperationException("Ce monde n'a pas de terrain.");

    private bool TryRandomReachableCell(TerrainGrid terrain, FlowField reference, out Point2 p)
    {
        var cx = (int)(_rng.NextUInt64() % (ulong)terrain.CellsX);
        var cz = (int)(_rng.NextUInt64() % (ulong)terrain.CellsZ);
        if (terrain.IsWalkable(cx, cz) && reference.IsReachable(cx, cz))
        {
            p = terrain.CellCenter(cx, cz);
            return true;
        }

        p = default;
        return false;
    }

    private void UpdateRoamer(TerrainGrid terrain, Golfer g)
    {
        if (_waypoints.Count == 0)
        {
            return;
        }

        var target = _waypoints[g.WaypointIndex];
        var field = Flow!.Get(target);
        var pos = g.Position;
        var result = FlowWalker.Advance(terrain, field, ref pos, target, TickSeconds);
        g.Position = pos;
        if (result != WalkResult.Moving)
        {
            g.WaypointIndex = (int)(_rng.NextUInt64() % (ulong)_waypoints.Count);
        }
    }

    private void UpdateGroup(TerrainGrid terrain, int index)
    {
        var group = _groups[index];
        foreach (var g in group.Golfers)
        {
            switch (g.State)
            {
                case GolferState.Arriving:
                case GolferState.WalkingToBall:
                    if (Walk(terrain, g, g.Ball))
                    {
                        g.State = GolferState.WaitingTurn;
                    }

                    break;
                case GolferState.Exiting:
                    if (Walk(terrain, g, ExitPoint))
                    {
                        g.State = GolferState.Done;
                        if (group.LeagueRound >= 0 && group.Finished)
                        {
                            ReturnLeagueCards(terrain, group);
                        }
                    }

                    break;
                case GolferState.Swinging:
                case GolferState.Putting:
                    if (--g.SwingTicks <= 0)
                    {
                        Hit(terrain, g);
                    }

                    break;
                case GolferState.BallInFlight:
                    if (++g.FlightIndex >= g.FlightPath!.Count - 1)
                    {
                        Land(g);
                    }

                    break;
                default:
                    break;
            }
        }

        // Un coup à la fois dans le groupe ; le plus loin du trou joue.
        if (!group.Started)
        {
            if (group.LeagueRound >= 0 ? Tick < group.ReleaseTick : index > 0 && !_groups[index - 1].Finished)
            {
                return;
            }
        }

        Golfer? shooter = null;
        var farthest = -1.0;
        foreach (var g in group.Golfers)
        {
            if (g.State is GolferState.Swinging or GolferState.Putting or GolferState.BallInFlight or GolferState.AwaitingShot)
            {
                return;
            }

            if (g.Holed || g.PickedUp || g.State == GolferState.Exiting || g.State == GolferState.Done)
            {
                continue;
            }

            var dx = terrain.Hole.X - g.Ball.X;
            var dz = terrain.Hole.Z - g.Ball.Z;
            var d2 = (dx * dx) + (dz * dz);
            if (d2 > farthest)
            {
                farthest = d2;
                shooter = g;
            }
        }

        if (shooter is not null && shooter.State == GolferState.WaitingTurn)
        {
            group.Started = true;
            shooter.State = terrain.SurfaceAt(shooter.Ball.X, shooter.Ball.Z) == Surface.Green ? GolferState.Putting : GolferState.Swinging;
            shooter.SwingTicks = SwingTicksCount;
            if (IsManualPro(shooter) && !Gimme(terrain, shooter))
            {
                shooter.State = GolferState.AwaitingShot;
            }
        }
    }

    private bool Walk(TerrainGrid terrain, Golfer g, Point2 target)
    {
        var field = Flow!.Get(target);
        var pos = g.Position;
        var (cx, cz) = (terrain.CellXOf(pos.X), terrain.CellZOf(pos.Z));
        var result = FlowWalker.Advance(terrain, field, ref pos, target, IsManualPro(g) ? TickSeconds * ProWalkFactor : TickSeconds);
        g.Position = pos;
        var (nx, nz) = (terrain.CellXOf(pos.X), terrain.CellZOf(pos.Z));
        if ((nx, nz) != (cx, cz))
        {
            terrain.Turf.AddWear(nx, nz, TurfGrid.StepWear);
        }

        if (result == WalkResult.Stuck)
        {
            // Poche inatteignable : on repose le golfeur sur la cellule franchissable la plus proche de sa destination.
            g.Position = GridQueries.NearestWalkablePoint(terrain, target);
            return true;
        }

        return result == WalkResult.Arrived;
    }

    private void Hit(TerrainGrid terrain, Golfer g)
    {
        if (terrain.SurfaceAt(g.Ball.X, g.Ball.Z) is Surface.Fairway or Surface.Tee)
        {
            terrain.Turf.AddWear(terrain.CellXOf(g.Ball.X), terrain.CellZOf(g.Ball.Z), TurfGrid.DivotWear);
        }

        var request = new ShotRequest(g.Ball, terrain.Hole, g.Strokes, g.Skill);
        var result = g.PlannedShot is { } shot ? ExecuteProShot(terrain, g, shot) : _resolver.Resolve(request, terrain, _rng);
        g.Pending = result;
        g.FlightPath = result.Trajectory;
        g.FlightIndex = 0;
        if (result.Trajectory is null || result.Trajectory.Count <= 1)
        {
            Land(g);
            return;
        }

        // La balle part : les coups et pénalités ne sont comptés qu'à son arrêt.
        g.State = GolferState.BallInFlight;
    }

    private void Land(Golfer g)
    {
        var result = g.Pending;
        if (g.PlannedShot is { } shot && Terrain is { } played)
        {
            Pro!.Record(played, g.Ball, shot, result, g.InLeague, Date.Year);
            g.PlannedShot = null;
        }

        g.Strokes += 1 + result.PenaltyStrokes;
        g.Ball = result.Ball;

        // La trajectoire a été calculée avant une éventuelle retouche : si la balle retombe dans une cellule
        // repeinte en eau ou en arbres, on la repose comme les balles au repos (sans pénalité).
        if (Terrain is { } terrain && !result.Holed && !terrain.IsWalkable(terrain.CellXOf(g.Ball.X), terrain.CellZOf(g.Ball.Z)))
        {
            g.Ball = GridQueries.NearestWalkablePoint(terrain, g.Ball);
        }

        if (Terrain is { } landed && landed.SurfaceAt(g.Ball.X, g.Ball.Z) == Surface.Green)
        {
            landed.Turf.AddWear(landed.CellXOf(g.Ball.X), landed.CellZOf(g.Ball.Z), TurfGrid.MarkWear);
        }

        g.FlightPath = null;
        g.FlightIndex = 0;
        g.Pending = default;
        if (result.Holed || g.Strokes >= MaxStrokes)
        {
            g.Holed = result.Holed;
            g.PickedUp = !result.Holed;
            g.State = GolferState.Exiting;
        }
        else
        {
            g.State = GolferState.WalkingToBall;
        }
    }
}
