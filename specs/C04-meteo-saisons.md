# Météo et saisons
Statut : livré (2026-10-25)
Créé : 2026-10-25

## But
La fiche D1 de `docs/boucles-v1.md`, ramenée à ce que le jeu peut porter aujourd'hui.
1. **Les saisons se voient** : les feuillus prennent leurs couleurs d'automne et perdent une partie de leurs feuilles (modèles `oak_winter_light_NN` et `birch_winter_light_NN` des PR #30 et #32), le gazon change un peu de teinte, les arbres et le drapeau bougent avec le vent.
2. **Une météo par jour** : ciel, pluie et vent, tirés de la graine et du numéro du jour, avec une prévision sur trois jours.
3. **Le vent pousse la balle**, et les golfeurs en tiennent compte en visant.
4. **La pluie a des effets, dont des bons** : sol mouillé, greens qui retiennent la balle (M2), jeunes arbres qui poussent plus vite après une pluie de printemps (M9), et parfois un arbre abattu par la tempête (M7, décision Q5).
5. **Le relief compte** : après une forte pluie, l'eau s'accumule dans les creux (flaques, D1 Q2). Un green dans une cuvette devient une erreur visible.
6. **Le printemps fleurit** : des touffes de fleurs dans le rough.

Ce qui prouve la réussite : les captures montrent le même trou aux quatre saisons et sous la pluie ; un drive par vent de travers dévie, et le golfeur compense ; un approche s'arrête plus vite sur un green mouillé ; une tempête abat quelques arbres, pas une forêt ; une cuvette garde une flaque le lendemain d'un orage ; le rough fleurit au printemps.

## Périmètre et hors périmètre
**Dans le périmètre**
- Rendu des saisons : modèle de feuillu selon la saison, teinte du gazon, balancement au vent.
- `Weather/` dans la sim : un climat (tempéré, celui du terrain actuel), la météo du jour, la prévision.
- Vent dans `BallSimulator` (traînée relative à l'air) ; le planificateur simule avec ce vent.
- Humidité du sol (facteur de plus dans `SoilGrid`), chutes d'arbres par tempête, pousse après la pluie.
- Rendu de la météo : ciel couvert, pluie en particules, sol plus sombre quand il est mouillé, drapeau orienté par le vent ; HUD avec le temps du jour et la prévision.
- Flaques : cuvettes du relief, remplies selon l'humidité ; la balle s'en dégage sans pénalité.
- Fleurs de printemps dans le rough (rendu seul).
- Debug : F9 impose la météo suivante du cycle (commande déterministe), pour les essais et les captures.

**Hors périmètre, et où ça ira**
- Drains à poser (D1 Q2 : « plus tard »).
- Affluence et humeur selon la météo, M3 (purs et durs), M4 (club-house), M5 (pensées, faune) : il n'y a encore ni arrivées automatiques, ni humeur, ni bâtiments. Chaque chantier qui les crée lira la météo du jour, qui sera déjà là.
- M1 (arrosage gratuit) et la pousse du gazon après la pluie : C05 (entretien du gazon).
- M6 et M8 (vent et prestige, tempête légendaire) : avec les tournois et l'évaluation.
- Vente du bois de M7 : il n'y a pas encore d'argent. Le tronc tombé à garder ou à dégager existe déjà (C02).
- Vent dans la ligne de visée de votre pro selon le prestige (D1 Q4) : avec le swing contrôlable (B17). La règle est notée ici pour ce moment : vent inclus en partie amicale, ligue et tournoi local ; absent en tournoi prestigieux.
- Erreur de lecture du vent selon la compétence Imagination : les golfeurs n'ont pas encore de compétences. En attendant, ils lisent le vent exactement.
- Climats par thème (Links, Desert, Tropical) : un seul thème existe. `Climate` est une table de constantes ; un thème en ajoutera une autre.
- Rafales, brume du matin, canicule, coup de froid, reflets mouillés (spéculaire).

## Conception
**Saisons à l'écran** (`game/src/Rendering/TreeLayer.cs`, `game/shaders/`)
- `TreeLayer` charge deux jeux de plus de modèles adultes de feuillus et choisit le jeu selon la saison : modèle d'été au printemps et en été, `*_winter_light_NN` (roux, doré) en automne, `*_winter_NN` (brun terne) en hiver, avec repli sur `winter_light` tant que ces derniers manquent (décision 2). Les MultiMesh sont refaits une fois par changement de saison, comme après une croissance. Sapins inchangés.
- Gazon : un `uniform vec3 season_tint` dans `terrain.gdshader`, multiplié aux couleurs d'herbe (fairway, rough, green, départ ; pas le sable ni l'eau). Vert vif au printemps, référence en été, un peu jaune en automne, terne en hiver.
- Balancement : décalage des sommets dans `tree.gdshader` proportionnel à la hauteur du sommet et à un `global uniform` de vent (direction et force) ; même chose pour la toile du drapeau, qui s'oriente aussi dans le sens du vent.

**Météo** (`src/Golferoo.Sim/Weather/`, sans Godot)
- `Climate` : record de constantes par saison (probabilité de pluie, de forte pluie, vent moyen et écart). `Climate.Calm` (jamais de pluie, vent nul) sert aux tests qui veulent l'ancien comportement.
- `WeatherDay` : `Sky` (soleil, nuages, pluie, forte pluie), direction du vent (radians) et force de base (m/s). Pendant la journée, la force suit `base × (0,7 + 0,6 × DayFraction)` : le vent se lève l'après-midi.
- Fonction pure `WeatherDay For(int day)` : le hasard vient d'un flux dérivé de la graine et du numéro du jour (D1, « Coût en code »). Pour que les jours se suivent sans état, l'humidité de l'air et la direction du vent sont tirées tous les trois jours et interpolées entre ces points : la pluie vient par épisodes, sans chaîne de Markov à stocker.
- Prévision : les trois jours suivants, avec une chance sur cinq par jour qu'elle se trompe d'un cran (flux séparé). Rien n'est stocké en plus du tick et de la commande F9.

**Vent sur la balle** (`Shots/BallSimulator.cs`)
- `Run` reçoit le vent (un `Vec3` horizontal) ; en vol, la traînée devient `−k·|v − w|·(v − w)`. Une soustraction de plus par pas, déterministe. Au roulement, pas de vent.
- `PhysicalShotResolver` passe le vent du moment à ses simulations : le golfeur compense sans code de plus. Le coût de planification par vent fort est mesuré (critère 4).

**Pluie et sol** (`Terrain/SoilGrid.cs`, `Trees/`)
- Humidité du sol, sans état : 1 s'il pleut aujourd'hui, 0,5 s'il a fortement plu hier, 0 sinon. Facteur de `SoilGrid` = saison × humidité (roulement freiné d'environ 30 %, rebond plus mou à humidité 1). Recalcul au changement de jour seulement.
- Chutes : au changement de jour, si la journée est en forte pluie ou si le vent de base dépasse 14 m/s (50 km/h), chaque arbre adulte a une faible chance de tomber (de l'ordre d'un pour 2 000), dans le sens du vent à ±30°. On réutilise la chute de C02 (`TreeFelled`, tronc puis souche), gratuite pour le joueur.
- M9 : au changement de mois, un mois de printemps avec au moins trois jours de pluie donne un mois de croissance de plus aux jeunes arbres.

**Flaques** (`Terrain/Puddles.cs`)
- Profondeur de cuvette par cellule : remplissage depuis les bords (« priority flood ») sur le relief, une seule fois par retouche de relief (estimé à quelques millisecondes pour 100 000 cellules).
- Une cellule est en flaque si sa profondeur de cuvette dépasse un seuil qui dépend de l'humidité du sol : 0,10 m à humidité 1, 0,30 m à 0,5 (seuls les creux profonds tiennent jusqu'au lendemain), aucune à 0. Recalcul au changement de jour, comme le sol. Pas sur l'eau ni le sable.
- Balle : une balle qui s'arrête dans une flaque (« casual water ») est replacée sans pénalité sur la cellule sèche la plus proche, pas plus près du trou. Le coût de marche ne change pas (règle de performance).
- Rendu : un maillage plat sur les cellules en flaque, avec le shader d'eau existant, refait au changement de jour.

**Fleurs de printemps** (`game/src/Rendering/`, rendu seul)
- Un MultiMesh de touffes (`game/assets/bushes/flowers_spring_NN.glb`, PR #38 ; sans les modèles, pas de fleurs) sur une cellule de rough sur trois, choisies par un hachage de la cellule (stable d'une partie à l'autre), pétales teintés par instance (jaune, blanc, rose, violet), visible au printemps seulement. Rien dans la sim.

**Rendu de la météo** (`game/src/Main.cs`, `Main.tscn`)
- Ciel (`ProceduralSkyMaterial`) et énergie du soleil selon `Sky` ; pluie : `GPUParticles3D` attachées à la caméra ; sol mouillé : `uniform float wetness` qui assombrit le terrain.
- HUD : « 12 avril, an 1, printemps · pluie · vent 22 km/h NO » et une ligne de prévision « demain nuages, puis soleil, soleil ».

**Lots (5 PR)**
1. **Saisons visibles** (rendu seul, sans toucher la sim) : modèles par saison, teinte du gazon, balancement par un vent fixe en attendant le lot 2 ; captures des quatre saisons (F10).
2. **Météo et vent** : `Weather/`, vent dans la balle et le planificateur, HUD et prévision, drapeau et balancement branchés sur le vrai vent, F9 ; tests et banc.
3. **Pluie** : humidité dans `SoilGrid`, chutes, M9, ciel, particules, sol mouillé ; tests et captures sous la pluie.
4. **Flaques** : cuvettes, flaques selon l'humidité, dégagement de la balle, rendu ; tests, banc, captures.
5. **Fleurs de printemps** : touffes dans le rough au printemps, dès que l'asset est là ; captures.

## Décisions de Guillaume (2026-10-25)
1. **Tout dans C04** : flaques et fleurs de printemps comprises (lots 4 et 5).
2. **Hiver plus terne** : en hiver, les feuillus gardent la moitié de leurs feuilles, recolorées en brun terne (`oak_winter_NN`, `birch_winter_NN`, même maillage que `winter_light`, demandés au fil des assets) ; l'automne garde le roux et le doré.
3. **Vent renforcé** (lot 2) : le modèle n'a ni portance ni effet, et sa traînée est basse ; le vent compte ×3 dans l'axe du coup (face, dos) et ×6 de travers, décomposé selon la direction du coup au départ (`BallSimulator.WindGainAlong`, `WindGainAcross`). Un drive de 220 m par 10 m/s : −29 m de face, +21 m de dos, 17 m de dérive.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent la météo (même graine et même jour, même temps ; retoucher le terrain ne change pas la météo ; sur 12 000 jours, la fréquence de pluie de chaque saison est à ±5 points de `Climate` ; deux jours qui se suivent se ressemblent plus que deux jours au hasard), le vent (un drive par vent de travers de 10 m/s dévie d'une distance attendue ; par vent arrière il porte plus loin ; le planificateur compense et atterrit dans sa tolérance habituelle), l'humidité (sur un green, une même approche roule au moins 30 % de moins mouillé que sec), les chutes (sur 5 000 arbres, un jour de tempête en abat entre 1 et 10 ; un jour calme, aucun) les flaques (une cuvette du relief se remplit après une forte pluie, une pente jamais ; une cuvette profonde tient encore le lendemain, une peu profonde non ; une balle arrêtée dans une flaque est replacée au sec sans coup de pénalité, pas plus près du trou) et le déterminisme (même graine et mêmes commandes, dont F9, même somme de contrôle).
2. Avec `Climate.Calm`, les trajectoires et sommes de contrôle des tests existants de C01 à C03 sont inchangées.
3. Banc : le vol d'une balle avec vent coûte au plus 1,05 fois le vol sans vent ; le tick à 200 golfeurs reste sous 2 ms ; un changement de jour (météo, sol, flaques, chutes sur 20 000 arbres) coûte moins de 5 ms ; le calcul des cuvettes après une retouche du relief coûte moins de 20 ms pour 100 000 cellules.
4. Banc : planifier un drive par vent de 14 m/s coûte au plus 1,2 fois le même drive sans vent.
5. Captures de la CI (lignes `THUMB`) : le trou aux quatre saisons (feuillus roux et dorés en automne, bruns et ternes en hiver, teinte du gazon), puis sous la pluie, une flaque dans un creux le lendemain d'un orage, et le rough fleuri au printemps. Guillaume juge.
6. Mesure FPS sur la RTX 2080 (`Mesurer FPS.bat`, 5 000 arbres, 200 golfeurs, sous la pluie) : au moins 90 % du résultat de C02 (453,5 FPS).
7. Fumée Godot (`SMOKE OK`) ; le HUD affiche le temps et la prévision.
8. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (le lot 2 touche la physique de la balle et le planificateur ; le lot 1 peut être léger)

## Clôture
- [x] Critères vérifiés (PR #36, #37, #39, #40 et lot 5 ; 172 tests, banc : vol avec vent ≈ 1,0 fois, plan 1,07 fois, changement de jour 0,95 ms, cuvettes 16,3 ms pour 102 400 cellules ; critère 5 à juger par Guillaume sur les captures, critère 6 mesuré par Guillaume le 2026-10-25 : 320,9 FPS de moyenne, 270 au 1 % bas, sous forte pluie (jour 0 de la graine 1), 5 000 arbres et 200 golfeurs en 1080p sur RTX 2080, version 68f573b ; la référence juste est le pack à 800 triangles de la PR #29, 302 FPS, pas les 453,5 FPS de C02 mesurés avec les anciens arbres)
- [x] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
