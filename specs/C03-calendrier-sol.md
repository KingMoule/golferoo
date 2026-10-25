# Calendrier, saisons et sol par cellule
Statut : proposé
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
- `SoilGrid` (F2) et, selon le choix 4, l'effet de la saison sur le sol.

**Hors périmètre** : météo, vent, pluie et sol mouillé (C04) ; couleurs de saison des arbres et du gazon, fleurs (C04) ; état du gazon (C05) ; arrivées et départs des golfeurs selon l'heure (il n'y a pas encore d'arrivées automatiques) ; système général d'événements datés (les ligues de C06 liront le calendrier directement) ; sauvegarde (F4).

## Conception
**Calendrier** (`src/Golferoo.Sim/Time/Calendar.cs`, sans Godot)
- `Calendar` : record immuable de constantes (ticks par jour, jours par mois, mois par an, mois de départ) et fonction pure `Date At(long tick, int dayOffset)`. `Date` : jour, mois, année, `Season`, `DayFraction` (0 à l'ouverture, 1 à la fermeture). Rien n'est stocké en plus du tick et d'un décalage de jours : pas d'état à recalculer.
- Valeurs proposées (constantes réglables après essai, voir le choix 1) : une journée de 6 minutes réelles en vitesse x1 (7 200 ticks, 22 s en x16), des mois de 10 jours, 12 mois par an, départ le 1er avril de l'an 1. Une année dure ainsi 12 h en x1, 45 min en x16 ; une saison, 11 min en x16.
- `Season` reste dans `Trees/` (pas de déplacement pour rien) ; mars à mai printemps, et ainsi de suite.
- `World` : `Date` se lit depuis le tick ; au changement de mois, `Trees.Grow` (coût mesuré par C02 : moins de 5 ms pour 20 000 arbres, une fois par mois de jeu). `Season` devient une propriété calculée, transmise au résolveur. `SkipToNextMonth()` (debug) avance le décalage de jours et fait pousser les arbres ; le décalage entre dans la somme de contrôle.
- `AdvanceTrees` et `AdvanceYear` disparaissent, sauf s'ils servent aux tests.

**Sol** (`src/Golferoo.Sim/Terrain/SoilGrid.cs`)
- Trois tableaux `float` par cellule (résistance au roulement, restitution, frottement), environ 1,2 Mo pour 100 000 cellules. Valeur = `SurfaceTable` × facteur de saison (choix 4). Recalcul complet au changement de saison, partiel sur les cellules repeintes (compteur `SurfaceVersion` existant).
- `BallSimulator` et `PhysicalShotResolver` lisent `SoilGrid` ; `SurfaceTable` garde ce qui ne varie pas (marche, obstacle, pénalité). Le coût de marche n'en dépend jamais (règle de performance de `docs/boucles-v1.md`).
- L'été est la référence : facteurs à 1, donc trajectoires inchangées par rapport à C02.

**Rendu** (`game/src/Main.cs`)
- HUD : « 12 avril, an 1, printemps » à la place de l'âge moyen des arbres.
- Soleil : angle de la `DirectionalLight3D` = fonction de `DayFraction` (lever à l'est, coucher à l'ouest, jamais sous l'horizon) et de la saison (hauteur à midi). Coût nul : Godot recalcule déjà les ombres à chaque image.

**Lots proposés (2 PR)**
1. **Calendrier** : `Calendar`, saison et croissance branchées, `SkipToNextMonth`, HUD, soleil, captures du matin, de midi et du soir.
2. **Sol par cellule** : `SoilGrid`, branchement dans la balle et le planificateur, effet de saison, tests, banc.

## Choix à trancher par Guillaume
1. **Une partie peut-elle durer plusieurs jours ?** Les golfeurs marchent à vitesse réelle : un par 4 prend environ 5 minutes en x1.
   - A. **Journée courte, 6 min en x1** (recommandé) : on voit passer les saisons en jouant (11 min par saison en x16). Une paire joue environ un trou par jour et un 18 trous s'étale sur plusieurs jours, comme dans l'original, où un mois durait 89 s (`docs/recensement-sim-golf.md`, § 3.4). Il n'y a pas de nuit, donc ça ne se voit pas, sauf le soleil qui revient à l'est.
   - B. **Journée longue, 90 min en x1** : un 18 trous tient dans la journée, mais une saison dure près de 3 h en x16. Il faudrait des mois très courts (3 jours) pour revoir les saisons.
2. **À quelle vitesse poussent les arbres ?** Leur courbe de C02 est réaliste (bouleau adulte à 22 ans, chêne à 50).
   - A. **Au rythme du calendrier** : un bouleau planté mesure environ 8 m après 5 ans de jeu (près de 4 h en x16) ; un chêne n'est adulte qu'après 37 h en x16.
   - B. **Quatre fois plus vite** (recommandé) : un an de jeu vaut quatre ans pour un arbre. Le bouleau est adulte en 5 ans et demi de jeu, le chêne en 12 ans et demi. La difficulté d'un trou change de façon visible sur une longue partie, ce que vous demandiez pour les arbres vivants. Un seul nombre à régler.
3. **Hiver** (question mise de côté le 2026-10-24, c'est ici qu'elle se pose) :
   - A. **Année continue, l'hiver se joue** (recommandé pour l'instant) : le plus simple ; les feuillus nus laissent passer la balle. Fermer l'hiver plus tard revient à sauter trois mois, sans rien défaire.
   - B. **Hiver fermé** : décembre à février passent d'un coup, on construit sans golfeurs, comme les 8 mois de l'original.
4. **La saison change-t-elle le sol ?** F2 n'a de sens que si au moins un facteur varie, et l'humidité n'arrive qu'en C04.
   - A. **Oui, légèrement** (recommandé) : sol souple au printemps (la balle roule environ 15 % moins), ferme en été (référence), un peu plus lent en automne, ferme et vif en hiver. Les saisons changent le jeu, pas seulement les couleurs, comme vous l'aviez demandé pour la végétation.
   - B. **Non, F2 attend C04** : C03 se limite au calendrier, et le tableau du sol naît avec la pluie.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent le calendrier (tick vers date, changement de mois, d'année et de saison, `DayFraction` de 0 à 1), la croissance (un changement de mois fait pousser chaque arbre une seule fois ; `SkipToNextMonth` aussi), la saison du résolveur (un feuillu laisse passer plus de balles en avril qu'en juillet sur 10 000 tirages, sans régler la saison à la main) et le déterminisme (même graine et mêmes commandes, dont `SkipToNextMonth`, même somme de contrôle).
2. En été, les trajectoires et les sommes de contrôle des tests existants de C01 et C02 sont inchangées après F2.
3. Si le choix 4 est A : sur un fairway plat, un même drive roule au printemps entre 10 et 20 % de moins qu'en été (test).
4. Banc (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`) : le vol d'une balle avec `SoilGrid` coûte au plus 1,05 fois le vol d'avant ; le tick à 200 golfeurs reste sous 2 ms ; un changement de mois avec 20 000 arbres coûte moins de 5 ms, recalcul du sol compris.
5. Captures de la CI (lignes `THUMB`) : le trou le matin, à midi et le soir ; les ombres changent de direction. Guillaume juge.
6. Fumée Godot (`SMOKE OK`) ; le HUD affiche la date et la saison.
7. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (le lot 2 touche la physique de la balle)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
