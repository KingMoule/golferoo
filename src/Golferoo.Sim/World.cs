using Golferoo.Sim.Economy;
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

    /// <summary>
    /// Durée d'un coup jusqu'à l'impact, en ticks : l'image d'impact du swing animé (<c>impact_golf_swing_2s</c> dans les extras de
    /// game/assets/golfers/golfer.glb, 1,133 s) arrondie au tick. Une constante plutôt qu'une lecture du fichier : la sim reste pure et
    /// déterministe sans dépendre d'un asset ; la fumée Godot échoue si le .glb et cette valeur divergent (spec C23).
    /// </summary>
    public const int SwingTicksCount = 23;

    /// <summary>Budget de planification par tick, tous golfeurs confondus, en pas de simulation de la balle (<see cref="Shots.BallSimulator.Step"/>) : environ 0,3 µs chacun, soit 1,5 à 2 ms par tick. Un drive complet en compte des dizaines de milliers.</summary>
    public const int PlanBudgetPerTick = 6500;

    /// <summary>Ramassage de la balle après ce nombre de coups : garde-fou contre une partie sans fin.</summary>
    public const int MaxStrokes = 12;

    /// <summary>Feuille de départs de la ligue : le groupe n d'une journée peut jouer n × cet intervalle après la création des groupes (environ 0,08 jour de jeu).</summary>
    public const int LeagueTeeInterval = 600;

    private const int WaypointPoolSize = 16;

    /// <summary>Un groupe part quand le précédent a quitté cette zone autour du départ du trou 1 (rayon, en mètres)…</summary>
    public const double TeeZoneRadius = 20.0;

    /// <summary>… et que ses balles sont à plus de cette distance du départ (en mètres), ou sur le green.</summary>
    public const double DepartureGap = 150.0;

    /// <summary>Portée supposée d'un coup, en mètres : le point visé est le drapeau, ou le point à cette distance sur la ligne du drapeau.</summary>
    public const double AimReach = 260.0;

    /// <summary>Un golfeur attend tant que le groupe devant n'est pas au moins à cette distance (mètres) au-delà du point visé.</summary>
    public const double AimClearance = 30.0;

    private readonly DeterministicRng _rng;
    private readonly IShotResolver _resolver;
    private readonly List<Group> _groups = [];
    private readonly List<Golfer> _roamers = [];
    private readonly List<Point2> _waypoints = [];
    private readonly List<Point2> _beautySources = [];
    private int _nextGolferId;
    private int _nextGroupId;
    private Point2?[] _exitPoints = [];
    private long _exitPointVersion = -1;

    public World(ulong seed, TerrainGrid? terrain = null, IShotResolver? resolver = null, bool league = false, EconomyParams? economy = null, ProgressionParams? progression = null, bool placeClubhouse = false, ArrivalParams? arrivals = null, MoodParams? mood = null, MembershipParams? membership = null, bool emptyClub = false)
    {
        Seed = seed;
        Arrivals = arrivals ?? ArrivalParams.Default;
        Moods = mood ?? MoodParams.Default;
        Memberships = membership ?? MembershipParams.Default;
        Reputation = Arrivals.ReputationStart;
        _arrivalRng = new DeterministicRng(seed ^ ArrivalSeedMask);
        Terrain = terrain;
        Economy = economy ?? EconomyParams.Facile;
        Progression = progression ?? ProgressionParams.Default;
        Ledger = new Ledger(Economy.StartingMoney);
        if (terrain is not null)
        {
            _holesOpen = [.. terrain.Course.Holes.Select(_ => !terrain.Blank)];
        }

        if (terrain is { Blank: true })
        {
            ArgumentOutOfRangeException.ThrowIfEqual(league, true, nameof(league));
        }
        _rng = new DeterministicRng(seed);
        _resolver = resolver ?? (terrain is null ? new StubShotResolver() : new PhysicalShotResolver());
        Flow = terrain is null ? null : new FlowFieldCache(terrain);
        if (league && terrain is not null)
        {
            League = new LeagueSeason(seed, emptyClub ? LeagueSeason.EmptyClub(seed) : LeagueSeason.GenerateMembers(seed));
            Pro = new ProSkills(Progression);
            _proMemberId = League.Members.First(m => m.IsPro).Id;
            AdoptClub();
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
                physical.BeautySources = _beautySources;
                physical.MapCapacity = MapCapacityFor(terrain);
            }

            if (placeClubhouse)
            {
                PlaceClubhouseNearTee();
            }
        }
        else
        {
            Trees = new TreeStand(1.0, 1.0);
        }
    }

    /// <summary>Ligue du club : nulle sauf si le monde est créé avec <c>league: true</c> et un terrain (le monde de C01 à C05 n'en a pas).</summary>
    public LeagueSeason? League { get; }

    /// <summary>Sources de beauté posées dans le monde (objets de décor, aucun pour l'instant), lues par l'artiste (<see cref="BeautyMap"/>).</summary>
    public IReadOnlyList<Point2> BeautySources => _beautySources;

    /// <summary>Pose une source de beauté (objet de décor) au point <paramref name="at"/>.</summary>
    public void AddBeautySource(Point2 at) => _beautySources.Add(at);

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
        Mix(ref hash, (ulong)(r.ScratchByHole?.Count ?? -1));
        foreach (var s in r.ScratchByHole ?? [])
        {
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(s));
        }
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
        foreach (ref readonly var t in Trees.AsSpan())
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
        CloseMonth(CurrentMonth);
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

    /// <summary>Point où les golfeurs sortent du parcours : celui du dernier trou.</summary>
    public Point2 ExitPoint => ExitPointOf(RequireTerrain().Course.Count);

    /// <summary>Point où les golfeurs sortent du green du trou <paramref name="hole"/> (près du green, hors green et hors bunker).</summary>
    public Point2 ExitPointOf(int hole)
    {
        var terrain = RequireTerrain();
        if (_exitPoints.Length != terrain.Course.Count || _exitPointVersion != terrain.SurfaceVersion)
        {
            _exitPoints = new Point2?[terrain.Course.Count];
            _exitPointVersion = terrain.SurfaceVersion;
        }

        return _exitPoints[hole - 1] ??= ComputeExitPoint(terrain, terrain.Course[hole]);
    }

    /// <summary>Ajoute une paire de golfeurs à la file du départ du trou 1. Elle part quand la précédente a quitté la zone de départ et que ses balles sont loin (<see cref="DepartureGap"/>) ou sur le green.</summary>
    public Group SpawnGroup() => HoleOpen ? AddGroup(2, null, -1) : throw new InvalidOperationException("Le trou est fermé : il manque encore quelque chose (HoleCheck).");

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
            var golfer = new Golfer(_nextGolferId++, p, GolferState.Arriving) { Ball = terrain.Tee, Card = new ScoreCard([.. terrain.Course.Holes.Select(h => h.Par)]) };
            if (members is not null)
            {
                golfer.MemberId = members[i].Id;
                golfer.Skill = members[i].Skill;
                golfer.Profile = members[i].Profile;
            }
            else
            {
                golfer.Profile = GolferProfile.Pick(_rng);
            }

            golfers.Add(golfer);
        }

        foreach (var g in golfers)
        {
            g.InLeague = leagueRound >= 0;
        }

        var group = new Group(_nextGroupId++, golfers) { LeagueRound = leagueRound };
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

    /// <summary>Une paire de ligue a fini : la carte de chaque joueur (balle ramassée = 12 coups, trou sauté = 0) revient à la ligue.</summary>
    private void ReturnLeagueCards(Group group)
    {
        var cards = group.Golfers.Select(g => (g.MemberId, g.Card!)).ToArray();
        if (_ratingStale)
        {
            _heldCards.Add((group.LeagueRound, cards, Tick)); // la note d'un autre nombre de trous ne servirait pas : on attend la bonne
        }
        else
        {
            League!.GroupFinished(group.LeagueRound, cards, CourseRating, Tick);
        }
    }

    /// <summary>
    /// Inscrit sur la carte de chaque golfeur du groupe le trou qui vient de finir (balle ramassée = 12 coups) ; chacun paie son green fee, selon son humeur
    /// à la fin du trou. Un golfeur qui a abandonné avant ne paie plus (C14).
    /// </summary>
    private void RecordHole(Group group)
    {
        AfterHole(group);
        foreach (var g in group.Active)
        {
            Ledger.Post(LedgerCategory.GreenFees, GreenFee(g));
            if (g.PickedUp)
            {
                g.Card!.PickUp(group.Hole - 1);
            }
            else
            {
                g.Card!.Record(group.Hole - 1, g.Strokes);
            }
        }
    }

    /// <summary>Premier trou ouvert après celui du groupe, jusqu'au dernier de sa carte (les trous fermés sont sautés), 0 s'il n'y en a plus.</summary>
    private int NextOpenHole(Group group)
    {
        var last = Math.Min(_holesOpen.Length, group.Golfers[0].Card!.HoleCount); // sa carte fixe son dernier trou
        for (var n = group.Hole + 1; n <= last; n++)
        {
            if (_holesOpen[n - 1])
            {
                return n;
            }
        }

        return 0;
    }

    /// <summary>Le groupe passe au trou <paramref name="next"/> : ses golfeurs marchent vers ce départ, coups remis à zéro.</summary>
    private static void MoveToHole(Group group, CourseHole next)
    {
        group.Hole = next.Number;
        foreach (var g in group.Active)
        {
            g.Strokes = 0;
            g.WaitTicks = 0;
            g.Holed = false;
            g.PickedUp = false;
            g.Ball = next.Tee;
            g.State = GolferState.ToNextTee;
        }
    }

    public void Step()
    {
        // Le tirage garde l'état du RNG lié au temps : la somme de contrôle prouve le déterminisme même à vide.
        _rng.NextUInt64();
        ApplyPendingEdits();
        ApplyTreeCommands();
        ApplyHoleCommands();
        ApplyTurfCommands();
        ApplyProShots();
        ApplyJoins();
        while (_ratingCommands.Count > 0)
        {
            ApplyRating(_ratingCommands.Dequeue());
        }

        if (Terrain is not null)
        {
            if (_resolver is PhysicalShotResolver physical)
            {
                physical.Wind = Wind;
            }

            Flow!.BeginTick();
            StepArrivals();
            for (var i = 0; i < _groups.Count; i++)
            {
                UpdateGroup(Terrain, i);
            }

            if (_groupLeft)
            {
                _groups.RemoveAll(g => g.Left);
                _groupLeft = false;
            }

            AdvancePlans(Terrain);

            for (var i = 0; i < _roamers.Count; i++)
            {
                UpdateRoamer(Terrain, _roamers[i]);
            }
        }

        var month = CurrentMonth;
        var day = DayIndex;
        Tick++;
        if (DayIndex != day)
        {
            EndOfDay(day);
            Terrain?.Turf.NextDay(Season, WeatherOn(day).Sky >= Sky.Rain, DayIndex);
            if (Terrain is not null && PinPicker.Pick(Terrain, Seed, DayIndex) is { } pin)
            {
                Terrain.Course[1].MovePin(pin);
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

        if (CurrentMonth != month)
        {
            CloseMonth(month);
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
            var pin = Terrain is { } terrain ? terrain.Course[group.Hole].Hole : default;
            foreach (var g in group.Golfers)
            {
                items.Add(new GolferView(g.Id, group.Id, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g), g.PickedUp, pin));
            }
        }

        foreach (var g in _roamers)
        {
            items.Add(new GolferView(g.Id, -1, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g), g.PickedUp));
        }

        return snap;
    }

    /// <summary>Points de trajectoire lus ce tick : la cadence de lecture du vol d'un golfeur IA est <see cref="ArrivalParams.AiFlightSpeedFactor"/>, la trajectoire et le point de chute ne changent pas. Fonction du tick seul, donc déterministe.</summary>
    private int FlightStep(Golfer g)
    {
        var f = IsManualPro(g) ? 1.0 : Arrivals.AiFlightSpeedFactor;
        return (int)(Math.Floor(f * (Tick + 1)) - Math.Floor(f * Tick));
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

        if (Flow is { Pending.Count: > 0 })
        {
            Mix(ref hash, (ulong)Flow.Pending.Count);
            foreach (var key in Flow.Pending)
            {
                Mix(ref hash, (ulong)key);
            }
        }

        MixClubhouse(ref hash);
        MixArrivals(ref hash);
        MixMood(ref hash);
        MixMembership(ref hash);
        MixPendingEdits(ref hash);
        MixStroke(ref hash);
        MixTreeCommands(ref hash);
        MixHoleCommands(ref hash);
        MixHeldCards(ref hash);
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

        Mix(ref hash, (ulong)_beautySources.Count);
        foreach (var b in _beautySources)
        {
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(b.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(b.Z));
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
            if (group.Hole > 1 || !group.Released)
            {
                // Un trou seul, groupe parti : rien à ajouter, la somme reste celle d'avant le parcours.
                Mix(ref hash, ((ulong)group.Hole << 1) | (group.Released ? 1UL : 0UL));
            }

            if (group.FromArrivals)
            {
                Mix(ref hash, (ulong)group.ArrivedTick);
            }

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

        if (g.VisitorId >= 0)
        {
            Mix(ref hash, (ulong)g.VisitorId);
        }

        if (g.VisitorId >= 0)
        {
            Mix(ref hash, (ulong)(uint)(g.Mood - MoodParams.Min) | (g.Quit ? 1UL << 8 : 0UL) | ((ulong)(uint)g.WaitTicks << 16));
            foreach (var o in g.Occurrences)
            {
                Mix(ref hash, (ulong)o);
            }

            if (g.LikedHoles != 0 || g.LikedThisHole)
            {
                Mix(ref hash, (ulong)(uint)g.LikedHoles | (g.LikedThisHole ? 1UL << 32 : 0UL));
            }
        }

        Mix(ref hash, (ulong)g.Profile);
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

        g.Plan?.MixInto(ref hash);

        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.Z));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.Z));
        if (g.Card is { HoleCount: > 1 } card)
        {
            for (var h = 0; h < card.HoleCount; h++)
            {
                Mix(ref hash, (ulong)card.Strokes(h));
            }
        }
    }

    internal static void Mix(ref ulong hash, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= 0x100000001B3UL;
        }
    }

    private static Point2 ComputeExitPoint(TerrainGrid terrain, CourseHole hole)
    {
        var want = new Point2(hole.Green.X, hole.Green.Z + 20.0);
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

    /// <summary>
    /// Cartes de jeu à garder : un drapeau et un centre de green par trou (le prudent), plus quatre pour les drapeaux déplacés d'un jour à l'autre, dans la limite d'environ 32 Mo ; avec moins, chaque coup
    /// d'un autre trou refait une carte de plusieurs millisecondes.
    /// </summary>
    internal static int MapCapacityFor(TerrainGrid terrain) =>
        Math.Clamp(32_000_000 / (terrain.CellsX * terrain.CellsZ * 8), 4, (2 * terrain.Course.Count) + 4);

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
        if (Flow!.TryGet(target) is not { } field)
        {
            return;
        }

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
        if (!group.Released && !TryRelease(terrain, index))
        {
            return;
        }

        if (group.Leaving || (group.FromArrivals && group.Finished))
        {
            UpdateLeaving(terrain, group);
            return;
        }

        var hole = terrain.Course[group.Hole];
        var holedOut = false;
        foreach (var g in group.Golfers)
        {
            switch (g.State)
            {
                case GolferState.Arriving:
                case GolferState.WalkingToBall:
                case GolferState.ToNextTee:
                    if (Walk(terrain, g, g.Ball))
                    {
                        g.State = GolferState.WaitingTurn;
                    }

                    break;
                case GolferState.Exiting:
                    if (Walk(terrain, g, ExitPointOf(group.Hole)))
                    {
                        g.State = GolferState.Done;
                        holedOut = group.Active.All(o => o.State == GolferState.Done);
                    }

                    break;
                case GolferState.Quitting:
                    if (Walk(terrain, g, Clubhouse?.DoorPosition(terrain.CellSize) ?? ExitPoint))
                    {
                        g.State = GolferState.Done;
                    }

                    break;
                case GolferState.Swinging:
                case GolferState.Putting:
                    // Le geste dure SwingTicksCount ticks jusqu'à l'impact ; le coup ne part pas avant que sa planification soit finie.
                    if (--g.SwingTicks <= 0 && g.Plan is not { IsDone: false })
                    {
                        Hit(terrain, group, g, hole.Hole);
                    }

                    break;
                case GolferState.BallInFlight:
                    g.FlightIndex += FlightStep(g);
                    if (g.FlightIndex >= g.FlightPath!.Count - 1)
                    {
                        Land(group, g);
                    }

                    break;
                default:
                    break;
            }
        }

        if (holedOut)
        {
            // Tout le groupe a fini le trou : il marche vers le prochain trou ouvert, ou la partie est finie.
            RecordHole(group);
            var next = NextOpenHole(group);
            if (next > 0)
            {
                ResolveQuits(group); // il reste des trous : celui qui est sous zéro rentre au pavillon, les autres continuent
                if (group.Active.Any())
                {
                    BeforeMove(group, terrain.Course[next]);
                    MoveToHole(group, terrain.Course[next]);
                }
            }
            else if (group.LeagueRound >= 0)
            {
                ReturnLeagueCards(group);
            }
            else if (group.FromArrivals)
            {
                foreach (var g in group.Active)
                {
                    g.State = GolferState.Leaving;
                }
            }

            return;
        }

        // Un coup à la fois dans le groupe ; le plus loin du trou joue.
        Golfer? shooter = null;
        var farthest = -1.0;
        foreach (var g in group.Golfers)
        {
            if (g.State is GolferState.Swinging or GolferState.Putting or GolferState.BallInFlight or GolferState.AwaitingShot)
            {
                return;
            }

            if (g.Holed || g.PickedUp || g.Quit || g.State == GolferState.Exiting || g.State == GolferState.Done)
            {
                continue;
            }

            var dx = hole.Hole.X - g.Ball.X;
            var dz = hole.Hole.Z - g.Ball.Z;
            var d2 = (dx * dx) + (dz * dz);
            if (d2 > farthest)
            {
                farthest = d2;
                shooter = g;
            }
        }

        if (shooter is not null && shooter.State == GolferState.WaitingTurn && MustWait(index, hole, shooter))
        {
            Waiting(group, shooter);
        }
        else if (shooter is not null && shooter.State == GolferState.WaitingTurn)
        {
            group.Started = true;
            shooter.State = terrain.SurfaceAt(shooter.Ball.X, shooter.Ball.Z) == Surface.Green ? GolferState.Putting : GolferState.Swinging;
            shooter.SwingTicks = SwingTicksCount;
            shooter.Plan = shooter.State == GolferState.Swinging && !IsManualPro(shooter) && _resolver is PhysicalShotResolver ? new PlanJob() : null;
            if (IsManualPro(shooter) && !Gimme(terrain, hole.Hole, shooter))
            {
                shooter.State = GolferState.AwaitingShot;
            }
        }
    }

    /// <summary>
    /// File de départ : le groupe part quand son heure de ligue est venue, que le trou 1 est ouvert et que le groupe précédent est parti,
    /// a quitté la zone de départ et a ses balles à plus de <see cref="DepartureGap"/> du départ (ou sur le green).
    /// </summary>
    private bool TryRelease(TerrainGrid terrain, int index)
    {
        var group = _groups[index];
        if ((group.LeagueRound >= 0 && Tick < group.ReleaseTick) || !HoleOpen || (group.FromArrivals && !CanStartArrival(terrain)) || (index > 0 && !TeeCleared(terrain, _groups[index - 1])))
        {
            return false;
        }

        group.Released = true;
        AfterRelease(group);
        return true;
    }

    private static bool TeeCleared(TerrainGrid terrain, Group ahead)
    {
        if (!ahead.Released)
        {
            return false;
        }

        if (ahead.Hole > 1)
        {
            return true;
        }

        var tee = terrain.Course[1].Tee;
        foreach (var g in ahead.Golfers)
        {
            if (g.State is GolferState.Exiting or GolferState.Done or GolferState.Leaving or GolferState.Quitting)
            {
                continue; // il a fini le trou : sa balle (ramassée, elle peut traîner près du départ) ne bloque plus le départ
            }

            if (Dist2(g.Position, tee) <= TeeZoneRadius * TeeZoneRadius
                || (Dist2(g.Ball, tee) <= DepartureGap * DepartureGap && terrain.SurfaceAt(g.Ball.X, g.Ball.Z) != Surface.Green))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Avant de jouer, le golfeur attend le groupe devant : tant qu'un golfeur d'un groupe d'indice plus petit, sur le même trou, n'est pas
    /// au moins <see cref="AimClearance"/> au-delà du point visé (le drapeau, ou le point à <see cref="AimReach"/> sur sa ligne), il est plus
    /// près du point visé que cela ou derrière lui. Un green à portée est donc libre quand le groupe devant a fini le trou. L'ordre des groupes
    /// ne change jamais (un groupe ne dépasse pas celui qui le précède), donc deux groupes ne s'attendent jamais l'un l'autre.
    /// </summary>
    private bool MustWait(int index, CourseHole hole, Golfer shooter)
    {
        var group = _groups[index];
        var dx = hole.Hole.X - shooter.Ball.X;
        var dz = hole.Hole.Z - shooter.Ball.Z;
        var aimDistance = Math.Max(Math.Sqrt((dx * dx) + (dz * dz)) - AimReach, 0.0);
        var limit = aimDistance - AimClearance;
        for (var i = 0; i < index; i++)
        {
            var ahead = _groups[i];
            if (!ahead.Released || ahead.Hole != group.Hole)
            {
                continue;
            }

            foreach (var o in ahead.Golfers)
            {
                // Un golfeur parti n'est plus sur le parcours (le dernier trou le laisse au point de sortie).
                if (o.State is not (GolferState.Done or GolferState.Leaving or GolferState.Quitting) && Math.Sqrt(Dist2(o.Position, hole.Hole)) >= limit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double Dist2(Point2 a, Point2 b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));

    private bool Walk(TerrainGrid terrain, Golfer g, Point2 target)
    {
        if (Flow!.TryGet(target) is not { } field)
        {
            return false; // son champ de flux attend d'être refait après une retouche : il reste sur place ce tick-là
        }

        var pos = g.Position;
        var (cx, cz) = (terrain.CellXOf(pos.X), terrain.CellZOf(pos.Z));
        var result = FlowWalker.Advance(terrain, field, ref pos, target, TickSeconds, IsManualPro(g) ? 1.0 : Arrivals.AiWalkSpeedFactor);
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

    /// <summary>
    /// Fait avancer les planifications de coups en cours, golfeur après golfeur dans l'ordre des groupes, avec un budget de
    /// <see cref="PlanBudgetPerTick"/> pas de simulation par tick pour tout le monde : un drive planifié d'un seul tenant coûte des dizaines de
    /// millisecondes. Le budget se compte en pas de simulation, jamais en temps : le résultat ne dépend que de l'état. Une évaluation en cours
    /// finit toujours, le budget peut donc être dépassé d'un vol.
    /// </summary>
    private void AdvancePlans(TerrainGrid terrain)
    {
        if (_resolver is not PhysicalShotResolver physical)
        {
            return;
        }

        var budget = PlanBudgetPerTick;
        foreach (var group in _groups)
        {
            foreach (var g in group.Golfers)
            {
                if (budget <= 0)
                {
                    return;
                }

                if (g.Plan is { IsDone: false } job && g.State == GolferState.Swinging)
                {
                    var request = new ShotRequest(g.Ball, terrain.Course[group.Hole].Hole, g.Strokes, g.Skill, g.Profile);
                    budget = physical.Advance(job, request, terrain, budget, _rng).Left;
                }
            }
        }
    }

    private void Hit(TerrainGrid terrain, Group group, Golfer g, Point2 pin)
    {
        BeforeHit(group, g);
        if (terrain.SurfaceAt(g.Ball.X, g.Ball.Z) is Surface.Fairway or Surface.Tee)
        {
            terrain.Turf.AddWear(terrain.CellXOf(g.Ball.X), terrain.CellZOf(g.Ball.Z), TurfGrid.DivotWear);
        }

        var request = new ShotRequest(g.Ball, pin, g.Strokes, g.Skill, g.Profile);
        var result = g.PlannedShot is { } shot ? ExecuteProShot(terrain, g, shot, pin)
            : g.Plan is { Result: { } plan } && _resolver is PhysicalShotResolver physical ? physical.ExecutePlan(request, plan, terrain, _rng)
            : _resolver.Resolve(request, terrain, _rng);
        g.Plan = null;
        g.Pending = result;
        g.FlightPath = result.Trajectory;
        g.FlightIndex = 0;
        if (result.Trajectory is null || result.Trajectory.Count <= 1)
        {
            Land(group, g);
            return;
        }

        // La balle part : les coups et pénalités ne sont comptés qu'à son arrêt.
        g.State = GolferState.BallInFlight;
    }

    private void Land(Group group, Golfer g)
    {
        var result = g.Pending;
        var from = g.Ball;
        var putt = Terrain is { } played0 && played0.SurfaceAt(from.X, from.Z) == Surface.Green;
        if (g.PlannedShot is { } shot && Terrain is { } played)
        {
            Pro!.Record(played, g.Ball, shot, AimedResult(played, g, shot), result, g.InLeague, PlayYear, Math.Sqrt((Wind.X * Wind.X) + (Wind.Z * Wind.Z)), PinOf(played, g));
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
        if (Terrain is not null)
        {
            AfterShot(group, g, from, putt, result);
        }

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
