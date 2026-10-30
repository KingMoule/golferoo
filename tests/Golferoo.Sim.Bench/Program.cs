using System.Diagnostics;
using System.Text.Json;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Tutorial;
using Golferoo.Sim.Weather;

// Banc de mesure de la simulation, à lancer en Release :
//   dotnet run -c Release --project tests/Golferoo.Sim.Bench [-- --out bench.json]
// Mesure aux facteurs de subdivision 2, 3 et 4 les seuils du critère 5 de la spec C01.
// Code de sortie non nul si un seuil échoue.

var outPath = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault();
var holePath = FindHole();

const double tickMeanLimitMs = 2.0;
const double tickP99LimitMs = 5.0;
const double holeFieldLimitMs = 5.0;
const double bigFieldLimitMs = 20.0;
const double retouchLimitMs = 16.0;
const double treeFlightRatioLimit = 1.2;
const double treeGrowLimitMs = 5.0;
const double windFlightRatioLimit = 1.05;
const double windPlanRatioLimit = 1.2;
const double dayChangeLimitMs = 3.5;
const double leagueDayLimitMs = 5.0;
const double saveLimitMs = 50.0;
const double loadLimitMs = 1000.0;
const double saveFileLimitBytes = 2.0 * 1024 * 1024;
const double puddleFloodLimitMs = 20.0;
const double shotPreviewLimitMs = 2.0;
const double tutorialLimitMs = 1.0;
const double planTreesRatioLimit = 2.5;
const double playMapDoglegLimitMs = 5.0;
const double playMap512LimitMs = 150.0;
const double planProfileRatioLimit = 2.5;
const double fullCourseTickMeanLimitMs = 2.0;
const double fullCourseTickP99LimitMs = 5.0;
const double fullCourseRetouchLimitMs = 16.0;

// --arrivals-only : seulement le parcours chargé avec arrivées continues (C15 lot 2).
if (args.Contains("--arrivals-only"))
{
    var a = MeasureArrivals(Path.Combine(Path.GetDirectoryName(holePath)!, "..", "..", "tests", "Golferoo.Sim.Bench", "Courses", "dix-huit.json"));
    Console.WriteLine($"{a.Arrived} groupes en file, {a.TurnedAway} refusés, {a.Left} sortis ; au plus {a.MostOnCourse} groupes sur le parcours (plafond {a.GroupCeiling}), file jusqu'à {a.LongestQueue} ; tick moyen {a.TickMean:F3} ms, p99 {a.TickP99:F3} ms, pire {a.TickMax:F2} ms");
    return a.MostOnCourse <= a.GroupCeiling && a.TickMean < fullCourseTickMeanLimitMs && a.TickP99 < fullCourseTickP99LimitMs ? 0 : 1;
}

// --full-only : seulement le parcours chargé (C12 lot 4), pour itérer vite.
if (args.Contains("--full-only"))
{
    var only = MeasureFullCourse(Path.Combine(Path.GetDirectoryName(holePath)!, "..", "..", "tests", "Golferoo.Sim.Bench", "Courses", "dix-huit.json"));
    var onlyPassed = only.Passed(fullCourseTickMeanLimitMs, fullCourseTickP99LimitMs, fullCourseRetouchLimitMs, saveLimitMs, loadLimitMs, saveFileLimitBytes);
    Console.WriteLine($"tick moyen {only.TickMean:F3} ms, p99 {only.TickP99:F3} ms, pire {only.TickMax:F2} ms ; retouche médiane {only.Retouch.Median:F2} ms, pire {only.Retouch.Max:F2} ms ({only.CachedFields} champs) ; sauvegarde {only.SaveMs:F1} ms, chargement {only.LoadMs:F1} ms, {only.SaveBytes / 1024.0:F0} Ko ({only.Swinging} gestes en cours) ; {only.Trees} arbres ; mise en place {only.WarmupTicks} ticks ; ligue {only.LeagueDays:F2} jours, {only.LeagueGroups} groupes");
    WriteSummary(only, onlyPassed);
    if (outPath is not null)
    {
        File.WriteAllText(outPath, JsonSerializer.Serialize(new { fullCourse = FullJson(only, onlyPassed) }, new JsonSerializerOptions { WriteIndented = true }));
    }

    return onlyPassed ? 0 : 1;
}

// Carte plate de 512 x 512 cellules, la même à tous les facteurs.
var big = new TerrainGrid(new HeightField(512, 512, 2.0), "plat", new Point2(2, 2), new Point2(1000, 1000), new Point2(1000, 1000));
for (var z = 0; z < 512; z++)
{
    for (var x = 0; x < 512; x++)
    {
        big.PaintCell(x, z, Surface.Fairway);
    }
}

// Tour à blanc : le JIT à paliers optimise le code avant la première mesure,
// sinon le premier facteur mesuré paie l'échauffement.
{
    var warmup = new World(seed: 1, HoleLoader.Load(holePath, 2));
    warmup.SetLoadMode(200);
    var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
    while (Stopwatch.GetTimestamp() < until)
    {
        warmup.Step();
    }

    // Même chose pour le champ 512 x 512 : sa boucle tourne longtemps par appel, et sans ce tour le facteur 2,
    // mesuré en premier, tournait encore en partie sur du code non optimisé (25,8 ms de médiane en CI contre 9,4 ms
    // au facteur 4, sur la même carte). La pause laisse le JIT finir la compilation optimisée en arrière-plan.
    for (var i = 0; i < 60; i++)
    {
        FlowField.Build(big, 256, 256);
    }

    Thread.Sleep(500);
}

var factors = new List<object>();
var allPassed = true;
foreach (var factor in new[] { 2, 3, 4 })
{
    var terrain = HoleLoader.Load(holePath, factor);

    // Tick à 200 golfeurs en mode charge.
    var world = new World(seed: 1, terrain);
    world.SetLoadMode(200);
    world.Step(1_000);
    const int measuredTicks = 5_000;
    var ticks = new double[measuredTicks];
    for (var i = 0; i < measuredTicks; i++)
    {
        var start = Stopwatch.GetTimestamp();
        world.Step();
        ticks[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(ticks);
    var tickMean = ticks.Average();
    var tickP99 = ticks[(int)(measuredTicks * 0.99)];

    // Un champ de flux sur la carte du trou.
    var hx = terrain.CellXOf(terrain.Hole.X);
    var hz = terrain.CellZOf(terrain.Hole.Z);
    var holeField = Measure(200, 20, () => FlowField.Build(terrain, hx, hz));

    // Un champ de flux sur la carte plate de 512 x 512 cellules, jugé sur la médiane : sur un runner partagé,
    // la moyenne est tirée par les pauses du ramasse-miettes (chaque champ alloue environ 2 Mo).
    var bigField = Measure(50, 30, () => FlowField.Build(big, 256, 256));

    // Retouche de 5 x 5 cellules, champs de flux compris (cache : trou, tee, sortie et dix autres destinations).
    var retouchWorld = new World(seed: 2, terrain);
    var cache = retouchWorld.Flow!;
    cache.Get(terrain.Hole);
    cache.Get(terrain.Tee);
    cache.Get(retouchWorld.ExitPoint);
    for (var i = 0; i < 10; i++)
    {
        cache.Get((terrain.CellsX * (i + 1) / 12), terrain.CellsZ / 2);
    }

    var flip = false;
    var retouch = Measure(300, 20, () =>
    {
        flip = !flip;
        for (var dz = 0; dz < 5; dz++)
        {
            for (var dx = 0; dx < 5; dx++)
            {
                terrain.PaintCell((terrain.CellsX / 3) + dx, (terrain.CellsZ / 3) + dz, flip ? Surface.Bunker : Surface.Fairway);
            }
        }

        cache.Refresh();
    });

    var passed = tickMean < tickMeanLimitMs && tickP99 < tickP99LimitMs
        && holeField.Mean < holeFieldLimitMs && bigField.Median < bigFieldLimitMs && retouch.P99 < retouchLimitMs;
    allPassed &= passed;
    factors.Add(new
    {
        factor,
        cells = new { x = terrain.CellsX, z = terrain.CellsZ },
        passed,
        tick200 = new
        {
            golfers = 200,
            ticks = measuredTicks,
            meanMs = tickMean,
            p99Ms = tickP99,
            maxMs = ticks[^1],
            limitMeanMs = tickMeanLimitMs,
            limitP99Ms = tickP99LimitMs,
            passed = tickMean < tickMeanLimitMs && tickP99 < tickP99LimitMs,
        },
        flowFieldHole = new { meanMs = holeField.Mean, maxMs = holeField.Max, limitMs = holeFieldLimitMs, passed = holeField.Mean < holeFieldLimitMs },
        flowField512 = new { meanMs = bigField.Mean, medianMs = bigField.Median, maxMs = bigField.Max, limitMs = bigFieldLimitMs, passed = bigField.Median < bigFieldLimitMs },
        retouch5x5 = new
        {
            cachedFields = cache.Count,
            meanMs = retouch.Mean,
            p99Ms = retouch.P99,
            maxMs = retouch.Max,
            limitMs = retouchLimitMs,
            passed = retouch.P99 < retouchLimitMs,
        },
        checksum = world.Checksum().ToString("x16"),
    });
}

// Vol d'une balle sur la carte plate de 512 x 512 avec 20 000 arbres (C02 lot 2), comparé au même vol sans arbres.
// Les mêmes 400 coups de driver (départs et directions tirés d'un RNG fixe) sont joués des deux côtés ; avec les arbres,
// la balle est arrêtée bien plus tôt (forêt dense partout) : le critère porte sur le coût d'un coup, et le coût par seconde
// simulée est donné pour information (un pas dans la canopée coûte plus qu'un pas de roulement sur le sol).
var treeFlight = MeasureTreeFlight(big);
allPassed &= treeFlight.Ratio <= treeFlightRatioLimit;

// Vieillir 20 000 arbres d'un mois (C02 lot 3) : âge, index par case et version. Médiane de 200 passes.
var grow = MeasureTreeGrowth(big);
allPassed &= grow.Median < treeGrowLimitMs;

// Vent (C04 lot 2) : les mêmes 400 drives sur la carte plate avec et sans vent de 14 m/s, coût par seconde simulée
// (le vent change la durée du vol) ; et planifier un drive sur le trou avec et sans ce vent.
var wind = MeasureWind(big, HoleLoader.Load(holePath, 2));
allPassed &= wind.FlightRatio <= windFlightRatioLimit && wind.PlanRatio <= windPlanRatioLimit;

// Changement de jour (C04 lot 3) sur une carte de 512 x 512 avec 20 000 arbres adultes, en tempête : météo, sol mouillé
// recalculé (humidité qui change chaque jour) et chutes. Médiane de 30 changements de jour.
var dayChange = MeasureDayChange();
allPassed &= dayChange.Median < dayChangeLimitMs;

// Changement de jour avec une journée de ligue (C06 lot 3) : tirage des paires et création des groupes (15 inscrits), sur le trou du prototype.
var leagueDay = MeasureLeagueDay(holePath);
allPassed &= leagueDay.Median < leagueDayLimitMs;

// Sauvegarde (C11 lot 2) du cas réel du jeu : dogleg, ligue en cours de journée, 5 000 arbres. Écriture en mémoire, relecture et taille du fichier.
var save = MeasureSave(holePath);
allPassed &= save.Save.Median < saveLimitMs && save.Load.Median < loadLimitMs && save.Bytes < saveFileLimitBytes;
Console.WriteLine($"Sauvegarde ({save.Trees} arbres, ligue) : écriture {save.Save.Median:F1} ms (limite {saveLimitMs}), chargement {save.Load.Median:F1} ms (limite {loadLimitMs}), fichier {save.Bytes / 1024.0:F0} Ko (limite {saveFileLimitBytes / 1024:F0})");

// Cuvettes des flaques (C04 lot 4) : remplissage depuis les bords sur 320 x 320 cellules (102 400) après une retouche du relief,
// médiane de 11 passes. Critère sur un relief de collines et de creux comme en peint le pinceau ; le bruit pur à chaque coin
// (pire cas, des creux partout) est donné pour information. Au sec, rien n'est calculé.
var flood = MeasurePuddleFlood(noise: false);
var floodNoise = MeasurePuddleFlood(noise: true);
allPassed &= flood.Median < puddleFloodLimitMs;

// Note du parcours (C06 lot 2) : CourseRater.Rate, 50 parties par niveau, médiane de 5 appels. Pour information : le jeu le lance
// sur un fil à part, jamais sur celui de la sim.
var rateMs = new double[5];
var rateTerrain = HoleLoader.Load(holePath, 2);
for (var i = 0; i < rateMs.Length; i++)
{
    var start = Stopwatch.GetTimestamp();
    CourseRater.Rate(rateTerrain, 1);
    rateMs[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
}

Array.Sort(rateMs);

// Trajectoire prévue d'un coup du pro (C08 lot 2) : un drive en draw sans hasard sur le trou du prototype, médiane de 200 appels.
var preview = Measure(200, 20, () => new PhysicalShotResolver().ExecuteShaped(rateTerrain.Tee, Clubs.Driver, 1, 0, 1.0, ShotShape.Draw, rateTerrain, null));
allPassed &= preview.Median < shotPreviewLimitMs;

// Tutoriel (C07 lot 1) : validité du trou et étape courante sur la terre vierge, médiane de 200 appels.
var blankWorld = new World(1, HoleLoader.Load(Path.Combine(Path.GetDirectoryName(holePath)!, "blank-coude.json"), 2));
var holeCheck = Measure(200, 20, () => HoleCheck.Check(blankWorld.Terrain!));
var tutorialStep = Measure(200, 20, () => TutorialSteps.Current(blankWorld));
allPassed &= holeCheck.Median < tutorialLimitMs && tutorialStep.Median < tutorialLimitMs;

// Planification avec arbres (C09 lot 1) : un drive planifié sur le dogleg avec ses arbres contre le même planificateur qui les ignore
// (le temps d'avant le lot), médianes de passes alternées. Limite : 2,5 fois. Depuis la lisière de la forêt (ligne du trou bouchée, directions
// élargies), la mesure est donnée pour information.
var planTrees = MeasurePlanWithTrees(Path.Combine(Path.GetDirectoryName(holePath)!, "dogleg-par4.json"));
allPassed &= planTrees.TeeMs / planTrees.TeeBlindMs <= planTreesRatioLimit;
Console.WriteLine($"Planification dogleg : tee {planTrees.TeeMs:F2} ms avec arbres, {planTrees.TeeBlindMs:F2} ms sans (rapport {planTrees.TeeMs / planTrees.TeeBlindMs:F2}, limite {planTreesRatioLimit}) ; lisière {planTrees.EdgeMs:F2} ms");

// Carte de jeu d'un drapeau (C09 lot 2) : construction sur le dogleg avec ses arbres, et sur la carte plate de 512 x 512 (sans arbre),
// médianes. Une carte se refait quand le drapeau change de cellule, que le terrain est repeint ou que les arbres bougent : jamais dans un tick de jeu.
var doglegTerrain = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(holePath)!, "dogleg-par4.json"), 2);
var doglegWorld = new World(1, doglegTerrain);
var mapDogleg = Measure(100, 20, () => PlayMap.Build(doglegTerrain, doglegWorld.Trees, doglegTerrain.Hole));
var map512 = Measure(20, 5, () => PlayMap.Build(big, null, new Point2(1000, 1000)));
allPassed &= mapDogleg.Median < playMapDoglegLimitMs && map512.Median < playMap512LimitMs;
Console.WriteLine($"Carte de jeu : dogleg {mapDogleg.Median:F2} ms (limite {playMapDoglegLimitMs}), 512x512 {map512.Median:F2} ms (limite {playMap512LimitMs})");

// Profils de golfeurs (C09 lot 3) : un drive planifié sur le dogleg par chaque archétype contre le golfeur neutre, médianes de passes
// alternées. Le joueur de draw ou de fade essaie chaque coup plein droit et avec son effet (deux fois plus de simulations). Limite : 2,5 fois.
var planProfiles = MeasurePlanProfiles(Path.Combine(Path.GetDirectoryName(holePath)!, "dogleg-par4.json"));
var planProfileWorst = planProfiles.Max(p => p.Ms) / planProfiles.First(p => p.Profile == Archetype.Neutral).Ms;
allPassed &= planProfileWorst <= planProfileRatioLimit;
Console.WriteLine($"Planification par profil (dogleg, tee) : {string.Join(", ", planProfiles.Select(p => $"{p.Profile} {p.Ms:F2} ms"))} ; pire rapport {planProfileWorst:F2} (limite {planProfileRatioLimit})");

// Parcours chargé (C12 lot 4, P1) : 18 trous, 36 groupes en jeu, 5 000 arbres, résolveur physique. Les groupes partent en file jusqu'à
// ce que les 18 trous soient occupés, puis 20 000 ticks sont mesurés (un groupe qui finit est remplacé par un nouveau au départ).
// Retouche de surface en pleine charge : pire tick de la peinture et des 40 ticks qui suivent (champs de flux refaits quelques-uns par tick).
var full = MeasureFullCourse(Path.Combine(Path.GetDirectoryName(holePath)!, "..", "..", "tests", "Golferoo.Sim.Bench", "Courses", "dix-huit.json"));
var fullPassed = full.Passed(fullCourseTickMeanLimitMs, fullCourseTickP99LimitMs, fullCourseRetouchLimitMs, saveLimitMs, loadLimitMs, saveFileLimitBytes);
allPassed &= fullPassed;
Console.WriteLine($"Parcours chargé (18 trous, {full.Groups} groupes, {full.Trees} arbres, sauvegarde {full.SaveMs:F1} ms / chargement {full.LoadMs:F1} ms / {full.SaveBytes / 1024.0:F0} Ko) : tick moyen {full.TickMean:F3} ms (limite {fullCourseTickMeanLimitMs}), p99 {full.TickP99:F3} ms (limite {fullCourseTickP99LimitMs}), pire {full.TickMax:F2} ms ; retouche {full.Retouch.Median:F2} ms médiane, {full.Retouch.Max:F2} ms pire ({full.CachedFields} champs en cache, limite {fullCourseRetouchLimitMs}) ; mise en place {full.WarmupTicks} ticks ; journée de ligue : {full.LeagueDays:F2} jour(s) de jeu pour {full.LeagueGroups} groupes");
WriteSummary(full, fullPassed);

// Arrivées continues (C15 lot 2, critère 9) : même parcours chargé, mais les groupes arrivent seuls, attrait forcé au maximum.
var arrivals = MeasureArrivals(Path.Combine(Path.GetDirectoryName(holePath)!, "..", "..", "tests", "Golferoo.Sim.Bench", "Courses", "dix-huit.json"));
var arrivalsPassed = arrivals.MostOnCourse <= arrivals.GroupCeiling && arrivals.TickMean < fullCourseTickMeanLimitMs && arrivals.TickP99 < fullCourseTickP99LimitMs;
allPassed &= arrivalsPassed;
Console.WriteLine($"Arrivées continues ({arrivals.Holes} trous, {arrivals.Trees} arbres, attrait au maximum, {arrivals.Ticks} ticks) : {arrivals.Arrived} groupes en file, {arrivals.TurnedAway} refusés, {arrivals.Left} sortis ; au plus {arrivals.MostOnCourse} groupes sur le parcours (plafond {arrivals.GroupCeiling}), file jusqu'à {arrivals.LongestQueue} ; tick moyen {arrivals.TickMean:F3} ms (limite {fullCourseTickMeanLimitMs}), p99 {arrivals.TickP99:F3} ms (limite {fullCourseTickP99LimitMs}), pire {arrivals.TickMax:F2} ms");

var report = new
{
    fullCourse = FullJson(full, fullPassed),
    arrivals = new { arrivals.Holes, arrivals.Trees, arrivals.Ticks, arrivals.Arrived, arrivals.TurnedAway, arrivals.Left, arrivals.MostOnCourse, ceiling = arrivals.GroupCeiling, arrivals.LongestQueue, tickMeanMs = arrivals.TickMean, tickP99Ms = arrivals.TickP99, tickMaxMs = arrivals.TickMax, passed = arrivalsPassed },
    planProfiles = new { teeMedianMs = planProfiles.ToDictionary(p => p.Profile.ToString(), p => p.Ms), worstRatio = planProfileWorst, limit = planProfileRatioLimit, passed = planProfileWorst <= planProfileRatioLimit },
    playMap = new { doglegMedianMs = mapDogleg.Median, doglegLimitMs = playMapDoglegLimitMs, map512MedianMs = map512.Median, map512LimitMs = playMap512LimitMs, passed = mapDogleg.Median < playMapDoglegLimitMs && map512.Median < playMap512LimitMs },
    tutorial = new { holeCheckMedianMs = holeCheck.Median, stepMedianMs = tutorialStep.Median, limitMs = tutorialLimitMs, passed = holeCheck.Median < tutorialLimitMs && tutorialStep.Median < tutorialLimitMs },
    shotPreview = new { medianMs = preview.Median, maxMs = preview.Max, limitMs = shotPreviewLimitMs, passed = preview.Median < shotPreviewLimitMs },
    courseRating = new { games = CourseRater.DefaultGames, medianMs = rateMs[rateMs.Length / 2], maxMs = rateMs[^1] },
    benchmark = "c04-lot4",
    puddleFlood = new { cells = 320 * 320, medianMs = flood.Median, maxMs = flood.Max, noiseMedianMs = floodNoise.Median, limitMs = puddleFloodLimitMs, passed = flood.Median < puddleFloodLimitMs },
    save = new { trees = save.Trees, saveMedianMs = save.Save.Median, loadMedianMs = save.Load.Median, fileBytes = save.Bytes, saveLimitMs, loadLimitMs, fileLimitBytes = saveFileLimitBytes, passed = save.Save.Median < saveLimitMs && save.Load.Median < loadLimitMs && save.Bytes < saveFileLimitBytes },
    leagueDayChange = new { groups = leagueDay.Groups, medianMs = leagueDay.Median, maxMs = leagueDay.Max, limitMs = leagueDayLimitMs, passed = leagueDay.Median < leagueDayLimitMs },
    dayChange = new { trees = dayChange.Trees, medianMs = dayChange.Median, maxMs = dayChange.Max, fallen = dayChange.Fallen, limitMs = dayChangeLimitMs, passed = dayChange.Median < dayChangeLimitMs },
    wind = new
    {
        speed = 14.0,
        flightRatioPerSimulatedSecond = wind.FlightRatio,
        flightLimit = windFlightRatioLimit,
        planCalmMs = wind.PlanCalmMs,
        planWindyMs = wind.PlanWindyMs,
        planRatio = wind.PlanRatio,
        planLimit = windPlanRatioLimit,
        passed = wind.FlightRatio <= windFlightRatioLimit && wind.PlanRatio <= windPlanRatioLimit,
    },
    passed = allPassed,
    treeGrowth = new { trees = grow.Trees, monthMeanMs = grow.Mean, monthMedianMs = grow.Median, monthMaxMs = grow.Max, limitMs = treeGrowLimitMs, passed = grow.Median < treeGrowLimitMs },
    treeFlight = new
    {
        trees = treeFlight.Trees,
        flights = treeFlight.Flights,
        noTreesMsPerFlight = treeFlight.NoTreesMs,
        withTreesMsPerFlight = treeFlight.WithTreesMs,
        ratio = treeFlight.Ratio,
        simulatedSecondsPerFlightNoTrees = treeFlight.NoTreesSeconds,
        simulatedSecondsPerFlightWithTrees = treeFlight.WithTreesSeconds,
        ratioPerSimulatedSecond = treeFlight.RatioPerSimulatedSecond,
        limit = treeFlightRatioLimit,
        passed = treeFlight.Ratio <= treeFlightRatioLimit,
    },
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    factors,
};

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (outPath is not null)
{
    File.WriteAllText(outPath, json);
}

return allPassed ? 0 : 1;

static object FullJson(FullCourseResult r, bool passed) => new
{
    holes = 18,
    groups = r.Groups,
    trees = r.Trees,
    ticks = r.Ticks,
    warmupTicks = r.WarmupTicks,
    tickMeanMs = r.TickMean,
    tickP99Ms = r.TickP99,
    tickMaxMs = r.TickMax,
    retouchMedianMs = r.Retouch.Median,
    retouchMaxMs = r.Retouch.Max,
    cachedFields = r.CachedFields,
    leagueDays = r.LeagueDays,
    leagueGroups = r.LeagueGroups,
    saveMedianMs = r.SaveMs,
    loadMedianMs = r.LoadMs,
    saveFileBytes = r.SaveBytes,
    swingingAtSave = r.Swinging,
    limitTickMeanMs = 2.0,
    limitTickP99Ms = 5.0,
    limitRetouchMedianMs = 16.0,
    limitSaveMs = 50.0,
    limitLoadMs = 1000.0,
    limitSaveFileBytes = 2 * 1024 * 1024,
    passed,
    checksum = r.Checksum,
};

static void WriteSummary(FullCourseResult r, bool passed)
{
    var path = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
    if (string.IsNullOrEmpty(path))
    {
        return;
    }

    var mark = passed ? "OK" : "ECHEC";
    File.AppendAllText(path, $"""

        ### Parcours chargé (C12 lot 4, P1) : {mark}

        18 trous, {r.Groups} groupes en jeu, {r.Trees} arbres, résolveur physique ; {r.Ticks} ticks mesurés.

        | Mesure | Valeur | Limite |
        |---|---|---|
        | Tick moyen | {r.TickMean:F3} ms | 2 ms |
        | Tick 99e centile | {r.TickP99:F3} ms | 5 ms |
        | Pire tick | {r.TickMax:F2} ms | |
        | Retouche de surface, pire tick des 41 suivants (médiane / pire des 30) | {r.Retouch.Median:F2} / {r.Retouch.Max:F2} ms, {r.CachedFields} champs en cache | 16 ms |
        | Sauvegarde ({r.Swinging} gestes en cours) : écriture / chargement | {r.SaveMs:F1} / {r.LoadMs:F1} ms | 50 / 1000 ms |
        | Fichier de sauvegarde | {r.SaveBytes / 1024.0:F0} Ko | 2048 Ko |
        | Journée de ligue ({r.LeagueGroups} groupes) | {r.LeagueDays:F2} jour(s) de jeu | |

        """);
}

// Boisement complété à treeCount arbres en grille sur les cellules de rough et de sous-bois (jamais sur un fairway ou un green, ni près du pavillon).
static void FillTrees(World world, TerrainGrid terrain, int treeCount)
{
    var missing = treeCount - world.Trees.Count;
    if (missing <= 0)
    {
        return;
    }

    var width = terrain.CellsX * terrain.CellSize;
    var depth = terrain.CellsZ * terrain.CellSize;
    var step = Math.Sqrt(width * depth / missing) * 0.8;
    var zone = world.Clubhouse?.Footprint.Expand(2, terrain.CellsX, terrain.CellsZ);
    var n = 0;
    for (var z = step / 2; z < depth && n < missing; z += step)
    {
        for (var x = step / 2; x < width && n < missing; x += step)
        {
            if (terrain.SurfaceAt(x, z) is Surface.Rough or Surface.Trees && !(zone?.Contains(terrain.CellXOf(x), terrain.CellZOf(z)) ?? false))
            {
                world.Trees.Add(x, z, (TreeSpeciesId)(n % 3), 40 * 12);
                n++;
            }
        }
    }
}

// Parcours chargé avec arrivées continues (C15 lot 2, critère 9) : 18 trous, pavillon, attrait forcé au maximum, 5 000 arbres, résolveur physique.
// Les groupes arrivent seuls, font la file, partent et sortent ; après une mise en place de trois jours, 20 000 ticks sont mesurés
// et on relève le plus grand nombre de groupes en même temps sur le parcours (plafond mesuré à C12 : 36).
static ArrivalsResult MeasureArrivals(string coursePath)
{
    const int treeCount = 5_000;
    const int measuredTicks = 20_000;
    const int groupCeiling = 36;
    var terrain = CourseLoader.Load(coursePath, 2);
    var world = new World(seed: 1, terrain, placeClubhouse: true, arrivals: ArrivalParams.Default.AtMaxAttraction()) { Climate = Climate.Calm };
    FillTrees(world, terrain, treeCount);
    var arrived = 0;
    var turnedAway = 0;
    var left = 0;
    world.GroupArrived += _ => arrived++;
    world.GroupTurnedAway += _ => turnedAway++;
    world.GroupLeft += _ => left++;
    world.Step(3 * Calendar.TicksPerDay);

    var ticks = new double[measuredTicks];
    var mostOnCourse = 0;
    var longestQueue = 0;
    for (var i = 0; i < measuredTicks; i++)
    {
        var start = Stopwatch.GetTimestamp();
        world.Step();
        ticks[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        mostOnCourse = Math.Max(mostOnCourse, world.Groups.Count(g => g.Released && !g.Finished && !g.Leaving));
        longestQueue = Math.Max(longestQueue, world.QueueLength);
    }

    Array.Sort(ticks);
    return new ArrivalsResult(terrain.Course.Count, treeCount, measuredTicks, arrived, turnedAway, left, mostOnCourse, groupCeiling, longestQueue, ticks.Average(), ticks[(int)(measuredTicks * 0.99)], ticks[^1]);
}

static FullCourseResult MeasureFullCourse(string coursePath)
{
    const int groups = 36;
    const int treeCount = 5_000;
    const int measuredTicks = 20_000;
    var terrain = CourseLoader.Load(coursePath, 2);
    var world = new World(seed: 1, terrain) { Climate = Climate.Calm };

    FillTrees(world, terrain, treeCount);

    bool AllHolesBusy()
    {
        var busy = new HashSet<int>();
        foreach (var g in world.Groups)
        {
            if (g.Released && !g.Finished)
            {
                busy.Add(g.Hole);
            }
        }

        return busy.Count == terrain.Course.Count;
    }

    // Les 36 groupes restent en jeu : un groupe fini est remplacé (hors de la mesure).
    void TopUp()
    {
        for (var a = world.Groups.Count(g => !g.Finished); a < groups; a++)
        {
            world.SpawnGroup();
        }
    }

    for (var i = 0; i < groups; i++)
    {
        world.SpawnGroup();
    }

    var warmup = 0;
    while (!AllHolesBusy() && warmup < 400_000)
    {
        world.Step();
        warmup++;
    }

    var ticks = new double[measuredTicks];
    for (var i = 0; i < measuredTicks; i++)
    {
        var start = Stopwatch.GetTimestamp();
        world.Step();
        ticks[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (i % 100 == 0)
        {
            TopUp();
        }
    }

    // Sauvegarde en pleine charge (C12 lot 4) : 36 groupes, des coups en cours de planification ; écriture en mémoire, relecture, taille. Mêmes seuils que C11.
    var swinging = world.Groups.SelectMany(g => g.Golfers).Count(g => g.State == GolferState.Swinging);
    var saved = Array.Empty<byte>();
    var save = Measure(11, 3, () =>
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        saved = stream.ToArray();
    });
    var load = Measure(5, 1, () => World.Load(new MemoryStream(saved)));

    // Retouche de surface en pleine charge, sur un bout de fairway du trou 1 : le tick qui applique la peinture et les 40 suivants, pendant lesquels
    // les champs de flux en cache se refont quelques-uns par tick. Un échantillon = le pire de ces 41 ticks ; 30 retouches, une sur deux en bunker ; le critère porte sur la médiane (un pire tick isolé peut être la première carte de jeu d'un drapeau, sans rapport avec la retouche).
    // Les retouches se mesurent en milieu de journée : le changement de jour (nouveau drapeau, donc une carte de jeu à refaire) est un autre coût, une fois par jour.
    for (var k = Calendar.TicksPerDay - (int)(world.Tick % Calendar.TicksPerDay) + 1_000; k > 0; k--)
    {
        world.Step();
        if (k % 100 == 0)
        {
            TopUp();
        }
    }

    var first = terrain.Course[1];
    var spot = new Point2((first.Tee.X * 0.6) + (first.Hole.X * 0.4), (first.Tee.Z * 0.6) + (first.Hole.Z * 0.4)); // sur le fairway, loin du green et du départ
    var flip = false;
    var worst = new double[30];
    for (var r = 0; r < worst.Length; r++)
    {
        flip = !flip;
        TopUp();
        world.PaintSurface(spot, 10, flip ? Surface.Bunker : Surface.Fairway);
        for (var t = 0; t < 41; t++)
        {
            var start = Stopwatch.GetTimestamp();
            world.Step();
            worst[r] = Math.Max(worst[r], Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    Array.Sort(worst);
    var retouch = (Mean: worst.Average(), P99: worst[^1], Max: worst[^1], Median: worst[worst.Length / 2]);
    var cached = world.Flow!.Count;
    Array.Sort(ticks);
    var mean = ticks.Average();
    var p99 = ticks[(int)(measuredTicks * 0.99)];
    var checksum = world.Checksum().ToString("x16");

    // Journée de ligue sur ce parcours : durée de la première journée, en jours de jeu.
    var seed = Enumerable.Range(1, 500).First(x => new LeagueSeason((ulong)x, LeagueSeason.GenerateMembers((ulong)x)).Divisions.Sum(d => d.Count) >= 23);
    var league = new World((ulong)seed, CourseLoader.Load(coursePath, 2), league: true) { Climate = Climate.Calm };
    while (!league.League!.Rounds[0].Closed && league.DayIndex < 30)
    {
        league.Step(Calendar.TicksPerDay / 4);
    }

    var round = league.League.Rounds[0];
    var days = (round.EndTick - round.StartTick) / (double)Calendar.TicksPerDay;
    return new FullCourseResult(groups, world.Trees.Trees.Count, measuredTicks, warmup, mean, p99, ticks[^1], retouch, cached, checksum, days, round.Pairs.Count, save.Median, load.Median, saved.Length, swinging);
}

static (int Trees, double Mean, double Median, double Max) MeasureTreeGrowth(TerrainGrid map)
{
    const int treeCount = 20_000;
    var size = map.CellsX * map.CellSize;
    var stand = new TreeStand(size, size, map);
    var placer = new DeterministicRng(13);
    for (var i = 0; i < treeCount; i++)
    {
        stand.Add(2 + (placer.NextDouble() * (size - 4)), 2 + (placer.NextDouble() * (size - 4)), (TreeSpeciesId)(i % TreeSpeciesTable.Count), (int)(placer.NextDouble() * 30 * 12));
    }

    // Un mois de jeu : les arbres vieillissent et le sol est recalculé (saison changée à chaque passe, lecture qui force le recalcul).
    var summer = true;
    var m = Measure(200, 20, () =>
    {
        stand.Grow(Golferoo.Sim.Time.Calendar.TreeMonthsPerMonth);
        summer = !summer;
        map.Soil.Season = summer ? Season.Summer : Season.Autumn;
        map.Soil.RollResistanceAt(0, 0);
    });
    return (treeCount, m.Mean, m.Median, m.Max);
}

static (int Trees, int Flights, double NoTreesMs, double WithTreesMs, double Ratio, double RatioPerSimulatedSecond, double NoTreesSeconds, double WithTreesSeconds) MeasureTreeFlight(TerrainGrid map)
{
    // 20 000 arbres à l'échelle réelle ; agrandis, leurs couronnes couvrent autant de terrain avec SizeScale² fois moins d'arbres.
    var treeCount = (int)(20_000 / (TreeSpeciesTable.SizeScale * TreeSpeciesTable.SizeScale));
    const int flights = 400;
    var size = map.CellsX * map.CellSize;
    var stand = new TreeStand(size, size, map);
    var placer = new DeterministicRng(11);
    for (var i = 0; i < treeCount; i++)
    {
        var species = (TreeSpeciesId)(i % TreeSpeciesTable.Count);
        stand.Add(2 + (placer.NextDouble() * (size - 4)), 2 + (placer.NextDouble() * (size - 4)), species, (int)((20 + (placer.NextDouble() * 40)) * 12));
    }

    var aim = new DeterministicRng(12);
    var starts = new (Point2 Start, Vec3 Velocity)[flights];
    for (var i = 0; i < flights; i++)
    {
        var angle = aim.NextDouble() * 2 * Math.PI;
        var club = Clubs.Driver;
        starts[i] = (
            new Point2(150 + (aim.NextDouble() * (size - 300)), 150 + (aim.NextDouble() * (size - 300))),
            new Vec3(Math.Cos(angle) * club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, Math.Sin(angle) * club.LaunchCos * club.Speed));
    }

    double Round(TreeStand? trees, out double simulatedSeconds)
    {
        var rng = new DeterministicRng(13);
        var seconds = 0.0;
        var t0 = Stopwatch.GetTimestamp();
        foreach (var (start, velocity) in starts)
        {
            seconds += BallSimulator.Run(map, start, velocity, rolling: false, BallSimulator.DefaultDrag, recordPath: false, trees: trees, rng: rng).Duration;
        }

        simulatedSeconds = seconds;
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    for (var i = 0; i < 5; i++)
    {
        Round(null, out _);
        Round(stand, out _);
    }

    // Médiane de 15 tours de chaque côté, en alternance.
    var none = new double[15];
    var with = new double[15];
    double secNone = 0, secWith = 0;
    for (var i = 0; i < 15; i++)
    {
        none[i] = Round(null, out secNone);
        with[i] = Round(stand, out secWith);
    }

    Array.Sort(none);
    Array.Sort(with);
    var noTrees = none[7] / flights;
    var withTrees = with[7] / flights;
    return (treeCount, flights, noTrees, withTrees, withTrees / noTrees, (with[7] / secWith) / (none[7] / secNone), secNone / flights, secWith / flights);
}

static (double FlightRatio, double PlanCalmMs, double PlanWindyMs, double PlanRatio) MeasureWind(TerrainGrid map, TerrainGrid hole)
{
    const int flights = 400;
    var windy = new Vec3(14.0 * 0.6, 0, 14.0 * 0.8);
    var aim = new DeterministicRng(21);
    var size = map.CellsX * map.CellSize;
    var starts = new (Point2 Start, Vec3 Velocity)[flights];
    for (var i = 0; i < flights; i++)
    {
        var angle = aim.NextDouble() * 2 * Math.PI;
        var club = Clubs.Driver;
        starts[i] = (
            new Point2(300 + (aim.NextDouble() * (size - 600)), 300 + (aim.NextDouble() * (size - 600))),
            new Vec3(Math.Cos(angle) * club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, Math.Sin(angle) * club.LaunchCos * club.Speed));
    }

    double Round(Vec3 w)
    {
        var seconds = 0.0;
        var t0 = Stopwatch.GetTimestamp();
        foreach (var (start, velocity) in starts)
        {
            seconds += BallSimulator.Run(map, start, velocity, rolling: false, BallSimulator.DefaultDrag, recordPath: false, wind: w).Duration;
        }

        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds / seconds;
    }

    var request = new ShotRequest(hole.Tee, hole.Hole, 0);
    double Plan(Vec3 w)
    {
        var resolver = new PhysicalShotResolver { Wind = w };
        var t0 = Stopwatch.GetTimestamp();
        resolver.Resolve(request, hole, new DeterministicRng(3));
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    for (var i = 0; i < 5; i++)
    {
        Round(default);
        Round(windy);
        Plan(default);
        Plan(windy);
    }

    // Médianes de passes alternées.
    var calm = new double[15];
    var blow = new double[15];
    var planCalm = new double[31];
    var planWindy = new double[31];
    for (var i = 0; i < 15; i++)
    {
        calm[i] = Round(default);
        blow[i] = Round(windy);
    }

    for (var i = 0; i < 31; i++)
    {
        planCalm[i] = Plan(default);
        planWindy[i] = Plan(windy);
    }

    Array.Sort(calm);
    Array.Sort(blow);
    Array.Sort(planCalm);
    Array.Sort(planWindy);
    return (blow[7] / calm[7], planCalm[15], planWindy[15], planWindy[15] / planCalm[15]);
}

static (double Median, double Max) MeasurePuddleFlood(bool noise)
{
    var map = new TerrainGrid(new HeightField(320, 320, 2.0), "relief", new Point2(2, 2), new Point2(600, 600), new Point2(600, 600));
    var rng = new DeterministicRng(41);
    for (var j = 0; j <= 320; j++)
    {
        for (var i = 0; i <= 320; i++)
        {
            // Collines de 60 à 100 m et creux de 1 à 2 m, ou bruit pur.
            map.SetCornerHeight(i, j, noise ? rng.NextDouble() * 2.0 : (3.0 * Math.Sin(i * 0.1) * Math.Cos(j * 0.07)) + (1.5 * Math.Sin((i + j) * 0.23)));
        }
    }

    map.Soil.Wetness = 1.0;
    var samples = new double[11];
    for (var k = -2; k < samples.Length; k++)
    {
        map.SetCornerHeight(160, 160, rng.NextDouble() * 2.0);
        var t0 = Stopwatch.GetTimestamp();
        map.Puddles.DepthAt(0, 0);
        if (k >= 0)
        {
            samples[k] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }
    }

    Array.Sort(samples);
    return (samples[samples.Length / 2], samples[^1]);
}

static (int Groups, double Median, double Max) MeasureLeagueDay(string holePath)
{
    var world = new World(seed: 5, HoleLoader.Load(holePath, 2), league: true) { Climate = Climate.Calm };
    var samples = new List<double>();
    foreach (var round in world.League!.Rounds.Append(world.League.Final))
    {
        world.Step((round.NominalDay * Calendar.TicksPerDay) - 1 - (int)world.Tick);
        var t0 = Stopwatch.GetTimestamp();
        world.Step();
        samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
    }

    samples.Sort();
    return (world.League.Rounds[0].Pairs.Count, samples[samples.Count / 2], samples[^1]);
}

static (int Trees, (double Mean, double P99, double Max, double Median) Save, (double Mean, double P99, double Max, double Median) Load, int Bytes) MeasureSave(string holePath)
{
    // Le cas réel du jeu : le dogleg (facteur 2), la ligue, et le boisement complété à 5 000 arbres en grille comme FpsBench.FillTrees.
    const int treeCount = 5_000;
    var dogleg = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(holePath)!, "dogleg-par4.json"), 2);
    var world = new World(seed: 41, dogleg, league: true) { Climate = Climate.Calm };
    var width = dogleg.CellsX * dogleg.CellSize;
    var depth = dogleg.CellsZ * dogleg.CellSize;
    var missing = treeCount - world.Trees.Count;
    var step = Math.Sqrt(width * depth / missing);
    var cols = Math.Max(1, (int)(width / step));
    for (var i = 0; i < missing; i++)
    {
        world.Trees.Add(((i % cols) + 0.5) * width / cols, Math.Min(((i / cols) + 0.5) * step, depth - 0.5), (TreeSpeciesId)(i % 3), 40 * 12);
    }

    world.Step((world.League!.Rounds[0].NominalDay * Calendar.TicksPerDay) + 3_000);
    var bytes = Array.Empty<byte>();
    var save = Measure(11, 3, () =>
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        bytes = stream.ToArray();
    });
    var load = Measure(5, 1, () => World.Load(new MemoryStream(bytes)));
    return (world.Trees.Trees.Count, save, load, bytes.Length);
}

static (int Trees, double Median, double Max, int Fallen) MeasureDayChange()
{
    const int treeCount = 20_000;
    var map = new TerrainGrid(new HeightField(512, 512, 2.0), "plat", new Point2(2, 2), new Point2(1000, 1000), new Point2(1000, 1000));
    for (var z = 0; z < 512; z++)
    {
        for (var x = 0; x < 512; x++)
        {
            map.PaintCell(x, z, Surface.Rough);
        }
    }

    var world = new World(seed: 31, map) { Climate = Climate.Calm };
    var placer = new DeterministicRng(32);
    for (var i = 0; i < treeCount; i++)
    {
        world.Trees.Add(2 + (placer.NextDouble() * 1020), 2 + (placer.NextDouble() * 1020), (TreeSpeciesId)(i % TreeSpeciesTable.Count), 40 * 12);
    }

    var samples = new double[30];
    for (var i = -5; i < samples.Length; i++)
    {
        // Pluie un jour sur deux, forte : l'humidité du sol change à chaque changement de jour, et il y a des chutes.
        while (world.Weather.Sky != (i % 2 == 0 ? Sky.HeavyRain : Sky.Sun))
        {
            world.CycleSky();
        }

        world.Step(Calendar.TicksPerDay - 1 - (int)(world.Tick % Calendar.TicksPerDay));
        map.Soil.RollResistanceAt(0, 0);
        var t0 = Stopwatch.GetTimestamp();
        world.Step();
        map.Soil.RollResistanceAt(0, 0); // le sol se recalcule à la première lecture : on la compte
        if (i >= 0)
        {
            samples[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }
    }

    var fallen = world.Trees.Trees.Count(t => t.State == TreeState.Fallen);
    Array.Sort(samples);
    return (treeCount, samples[samples.Length / 2], samples[^1], fallen);
}

static (double TeeMs, double TeeBlindMs, double EdgeMs) MeasurePlanWithTrees(string path)
{
    var hole = HoleLoader.Load(path, 2);
    var world = new World(1, hole);
    var seeing = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
    var blind = new PhysicalShotResolver { Season = world.Season };
    var edge = new Point2(18.5 * 8, 26.5 * 8);
    double Plan(PhysicalShotResolver resolver, Point2 ball)
    {
        var t0 = Stopwatch.GetTimestamp();
        resolver.Resolve(new ShotRequest(ball, hole.Hole, 0, SkillLevels.Bogey), hole, new DeterministicRng(3));
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    for (var i = 0; i < 10; i++)
    {
        Plan(seeing, hole.Tee);
        Plan(blind, hole.Tee);
        Plan(seeing, edge);
    }

    var tee = new double[31];
    var teeBlind = new double[31];
    var edgeMs = new double[31];
    for (var i = 0; i < tee.Length; i++)
    {
        tee[i] = Plan(seeing, hole.Tee);
        teeBlind[i] = Plan(blind, hole.Tee);
        edgeMs[i] = Plan(seeing, edge);
    }

    Array.Sort(tee);
    Array.Sort(teeBlind);
    Array.Sort(edgeMs);
    return (tee[15], teeBlind[15], edgeMs[15]);
}

static List<(Archetype Profile, double Ms)> MeasurePlanProfiles(string path)
{
    var hole = HoleLoader.Load(path, 2);
    var world = new World(1, hole);
    var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
    var profiles = Enum.GetValues<Archetype>();
    double Plan(Archetype profile)
    {
        var t0 = Stopwatch.GetTimestamp();
        resolver.Resolve(new ShotRequest(hole.Tee, hole.Hole, 0, SkillLevels.Bogey, profile), hole, new DeterministicRng(3));
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    var samples = profiles.ToDictionary(p => p, _ => new double[31]);
    for (var i = -10; i < 31; i++)
    {
        foreach (var p in profiles)
        {
            var ms = Plan(p);
            if (i >= 0)
            {
                samples[p][i] = ms;
            }
        }
    }

    return [.. profiles.Select(p => (p, samples[p].Order().ElementAt(15)))];
}

static (double Mean, double P99, double Max, double Median) Measure(int runs, int warmup, Action action)
{
    for (var i = 0; i < warmup; i++)
    {
        action();
    }

    // Ramasse-miettes avant la mesure : une collecte héritée des étapes précédentes ne doit pas tomber dans l'échantillon.
    GC.Collect();
    GC.WaitForPendingFinalizers();

    var samples = new double[runs];
    for (var i = 0; i < runs; i++)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(samples);
    return (samples.Average(), samples[(int)(runs * 0.99)], samples[^1], samples[runs / 2]);
}

static string FindHole()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Golferoo.sln")))
    {
        dir = dir.Parent;
    }

    return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Golferoo.sln introuvable."), "data", "holes", "proto-par4.json");
}

internal readonly record struct FullCourseResult(
    int Groups, int Trees, int Ticks, int WarmupTicks, double TickMean, double TickP99, double TickMax,
    (double Mean, double P99, double Max, double Median) Retouch, int CachedFields, string Checksum, double LeagueDays, int LeagueGroups,
    double SaveMs, double LoadMs, int SaveBytes, int Swinging)
{
    internal bool Passed(double meanLimit, double p99Limit, double retouchLimit, double saveLimit, double loadLimit, double bytesLimit) =>
        TickMean < meanLimit && TickP99 < p99Limit && Retouch.Median < retouchLimit && SaveMs < saveLimit && LoadMs < loadLimit && SaveBytes < bytesLimit;
}

internal readonly record struct ArrivalsResult(int Holes, int Trees, int Ticks, int Arrived, int TurnedAway, int Left, int MostOnCourse, int GroupCeiling, int LongestQueue, double TickMean, double TickP99, double TickMax);
