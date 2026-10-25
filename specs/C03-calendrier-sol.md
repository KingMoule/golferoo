# Calendrier, saisons et sol par cellule
Statut : validé
Créé : 2026-10-25

## But
Les fondations F1 et F2 de `docs/boucles-v1.md` (section « Fondations communes »).
1. **Calendrier (F1)** : la sim ne connaît aujourd'hui que des ticks. Elle compte désormais des jours, des mois, des années et des saisons, et un moment de la journée de l'ouverture à la fermeture, sans horloge à l'écran et sans nuit (décision de Guillaume du 2026-10-24). La saison pilote la densité des couronnes (déjà prête depuis C02, fixée à l'été) et les arbres poussent au fil des mois au lieu de la commande de debug F10.
2. **Sol par cellule (F2)** : un tableau précalculé par cellule (roulement, rebond, frottement) remplace les appels à `SurfaceTable.Get` dans `BallSimulator` et `PhysicalShotResolver`. C04 (humidité) et C05 (état du gazon) y brancheront leurs facteurs sans retoucher la balle.

Ce qui prouve la réussite : le HUD affiche la date et la saison ; le soleil traverse le ciel pendant la journée et les ombres tournent (captures) ; un bouleau planté grandit sans commande de debug ; une balle traverse plus souvent un feuillu en avril qu'en juillet ; en été, les trajectoires restent identiques au bit près à celles d'avant F2.

## Périmètre et hors périmètre
**Dans le périmètre**
- `Calendar` dans la sim, déduit du tick : jour, mois, année, saison, moment de la journée.
- Saison et croissance des arbres branchées sur le calendrier ; `World.Season` n'est plus réglable de l'extérieur, ce qui règle aussi son absence de la somme de contrôle (suite notée dans `docs/handoff.md`).
- Rendu : date et saison dans le HUD, soleil (`Sun` de `game/Main.tscn`) qui tourne d'est en ouest de l'ouverture à la fermeture, plus bas en hiver qu'en été.
- Debug : F10 saute au mois suivant (au lieu de « +1 an »), de façon déterministe.
- `SoilGrid` (F2) et l'effet de la saison sur le sol.

**Hors périmètre** : météo, vent, pluie et sol mouillé (C04) ; couleurs de saison des arbres et du gazon, fleurs (C04) ; état du gazon (C05) ; arrivées et départs des golfeurs selon l'heure (il n'y a pas encore d'arrivées automatiques) ; système général d'événements datés (les ligues de C06 liront le calendrier directement) ; sauvegarde (F4).

## Conception
**Calendrier** (`src/Golferoo.Sim/Time/Calendar.cs`, sans Godot)
- `Calendar` : record immuable de constantes (ticks par jour, jours par mois, mois par an, mois de départ) et fonction pure `Date At(long tick, int dayOffset)`. `Date` : jour, mois, année, `Season`, `DayFraction` (0 à l'ouverture, 1 à la fermeture). Rien n'est stocké en plus du tick et d'un décalage de jours : pas d'état à recalculer.
- Valeurs proposées (constantes réglables après essai) : une journée de 6 minutes réelles en vitesse x1 (7 200 ticks, 22 s en x16), des mois de 10 jours, 12 mois par an, départ le 1er avril de l'an 1. Une année dure ainsi 12 h en x1, 45 min en x16 ; une saison, 11 min en x16.
- `Season` reste dans `Trees/` (pas de déplacement pour rien) ; mars à mai printemps, et ainsi de suite.
- `World` : `Date` se lit depuis le tick ; au changement de mois, `Trees.Grow` (coût mesuré par C02 : moins de 5 ms pour 20 000 arbres, une fois par mois de jeu). `Season` devient une propriété calculée, transmise au résolveur. `SkipToNextMonth()` (debug) avance le décalage de jours et fait pousser les arbres ; le décalage entre dans la somme de contrôle.
- `AdvanceTrees` et `AdvanceYear` disparaissent, sauf s'ils servent aux tests.

**Sol** (`src/Golferoo.Sim/Terrain/SoilGrid.cs`)
- Trois tableaux `float` par cellule (résistance au roulement, restitution, frottement), environ 1,2 Mo pour 100 000 cellules. Valeur = `SurfaceTable` × facteur de saison. Recalcul complet au changement de saison, partiel sur les cellules repeintes (compteur `SurfaceVersion` existant).
- `BallSimulator` et `PhysicalShotResolver` lisent `SoilGrid` ; `SurfaceTable` garde ce qui ne varie pas (marche, obstacle, pénalité). Le coût de marche n'en dépend jamais (règle de performance de `docs/boucles-v1.md`).
- L'été est la référence : facteurs à 1, donc trajectoires inchangées par rapport à C02.

**Rendu** (`game/src/Main.cs`)
- HUD : « 12 avril, an 1, printemps » à la place de l'âge moyen des arbres.
- Soleil : angle de la `DirectionalLight3D` = fonction de `DayFraction` (lever à l'est, coucher à l'ouest, jamais sous l'horizon) et de la saison (hauteur à midi). Coût nul : Godot recalcule déjà les ombres à chaque image.

**Lots proposés (2 PR)**
1. **Calendrier** : `Calendar`, saison et croissance branchées, `SkipToNextMonth`, HUD, soleil, captures du matin, de midi et du soir.
2. **Sol par cellule** : `SoilGrid`, branchement dans la balle et le planificateur, effet de saison, tests, banc.

## Décisions de Guillaume (2026-10-25)
1. **Journée de 6 min en x1** : une partie de 18 trous s'étale sur plusieurs jours, comme dans l'original.
2. **Arbres quatre fois plus rapides que le calendrier** : un an de jeu vaut quatre ans pour un arbre (une constante).
3. **12 mois, hiver doux et sans neige** : un hybride entre l'automne canadien et l'hiver du sud des États-Unis. Le golf continue. Les feuillus perdent une partie de leurs feuilles, sans être nus : leur densité de couronne en hiver monte à environ la moitié de l'été (chêne 0,15 → 0,45, bouleau 0,10 → 0,40 dans `TreeSpeciesTable`), au lot 1. Le gazon change un peu de couleur en hiver : ce rendu va avec les couleurs de saison de C04. Le soleil est plus bas en hiver.
4. **La saison change le sol** : souple au printemps (la balle roule environ 15 % moins), ferme en été (référence), un peu plus lent en automne et en hiver.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent le calendrier (tick vers date, changement de mois, d'année et de saison, `DayFraction` de 0 à 1), la croissance (un changement de mois fait pousser chaque arbre une seule fois ; `SkipToNextMonth` aussi), la saison du résolveur (un feuillu laisse passer plus de balles en avril qu'en juillet sur 10 000 tirages, sans régler la saison à la main) et le déterminisme (même graine et mêmes commandes, dont `SkipToNextMonth`, même somme de contrôle).
2. En été, les trajectoires et les sommes de contrôle des tests existants de C01 et C02 sont inchangées après F2.
3. Sur un fairway plat, un même drive roule au printemps entre 10 et 20 % de moins qu'en été (test).
4. Banc (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`) : le vol d'une balle avec `SoilGrid` coûte au plus 1,05 fois le vol d'avant ; le tick à 200 golfeurs reste sous 2 ms ; un changement de mois avec 20 000 arbres coûte moins de 5 ms, recalcul du sol compris.
5. Captures de la CI (lignes `THUMB`) : le trou le matin, à midi et le soir ; les ombres changent de direction. Guillaume juge.
6. Fumée Godot (`SMOKE OK`) ; le HUD affiche la date et la saison.
7. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (le lot 2 touche la physique de la balle)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
