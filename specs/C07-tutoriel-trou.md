# Tutoriel : construire et ouvrir son premier trou
Statut : proposé
Créé : 2026-10-26

## But
La décision D7 de `docs/boucles-v1.md` (Guillaume, 2026-10-24) : pas de scénarios pour l'instant, seulement un **tutoriel très simple** de la boucle de construction et d'ouverture d'un trou. Le joueur peint un départ, un fairway et un green, pose le trou, l'ouvre, puis regarde la première paire le jouer. Votre pro sert de guide, comme le conseiller de l'original (D7, Q5).

Aujourd'hui le jeu s'ouvre sur un trou déjà construit (le dogleg « Le coude » de C08). Rien n'apprend au joueur les touches du pinceau, et la sim ne connaît ni trou fermé, ni départ ou drapeau que l'on pose.

Ce qui prouve la réussite : un joueur qui n'a jamais vu le jeu construit un trou jouable et voit une paire le finir sans autre aide que le tutoriel ; la CI le rejoue de bout en bout, par commandes, dans les tests et dans la fumée Godot.

## Périmètre et hors périmètre
**Dans le périmètre**
- Une **terre vierge** : le terrain du coude (même relief, même eau, mêmes bois) dont le départ, le fairway, le green et les bunkers sont redevenus du rough.
- Trois commandes de la sim : poser le départ, poser le trou, ouvrir le trou. Un trou fermé ne reçoit aucun golfeur.
- Une validité du trou, lisible par la sim : assez de départ, de fairway et de green, trou posé sur le green, chemin praticable du départ au green.
- Les étapes du tutoriel, déduites de l'état du monde (pas des clics), avec la bulle du pro, les contours fantômes à peindre et la mise en évidence de la touche ou du panneau à utiliser.
- Un lancement proposé, jamais imposé.

**Hors périmètre, et où ça ira**
- Scénarios, objectifs à étoiles, éditeur de scénarios (fiche D7) : plus tard, sur le même moteur d'étapes.
- Sauvegarde (F4) : le tutoriel se refait depuis le début ; il ne se sauvegarde pas.
- Fondation d'UI (P3 : thème, dock, composants) : non requise. Les éléments mis en évidence reçoivent un **identifiant stable** (le nom de leur nœud Godot), que P3 reprendra.
- Plusieurs trous, argent, coût des travaux : le moteur reste à un trou, sans économie.
- Voix enregistrée, portrait ou animation du pro : une bulle de texte signée par le pro.
- Aucun asset nouveau : contours, anneaux et bulle sont procéduraux ; le catalogue de Guillaume n'a rien qui serve ici.

## Conception
**Terre vierge** (`data/holes/`)
- `blank-coude.json` : copie du coude où `T`, `F`, `G`, `B` deviennent `R` ; relief, eau, bois et arbres isolés identiques. Le départ et le trou du fichier ne servent que de positions provisoires (le trou est fermé).
- Les contours fantômes sont ceux du coude, lus dans `dogleg-par4.json` : on reconstruit le vrai trou, ou on s'en écarte librement.

**Sim** (`src/Golferoo.Sim/`)
- `World.HoleOpen` : vrai par défaut, si bien que tous les mondes de C01 à C08 gardent leur somme de contrôle au bit près ; faux pour la terre vierge. Fermé, `SpawnGroup` ne fait rien et la ligue ne lance pas ses journées.
- Commandes rejouables, dans la somme de contrôle seulement quand elles servent : `World.PlaceTee(point)` (sur une cellule `Tee`), `World.PlaceHole(point)` (sur une cellule `Green` ; réutilise `MovePin` de C05), `World.OpenHole()` (refusée tant que le trou n'est pas valide ; à l'ouverture, une première paire part du départ).
- `Terrain/HoleCheck.cs` : fonction pure qui rend la liste des manques (départ trop petit, green trop petit, trou hors du green, pas de chemin, fairway trop court…), avec les seuils au lot 1 (de l'ordre de 40 m² de départ, 300 m² de green, 60 m de fairway). Le par se déduit de la distance (`TerrainGrid.DeducedPar`).
- `Tutorial/TutorialSteps.cs` : la liste ordonnée des étapes, chacune une condition sur le monde (`Func<World, bool>`) et un identifiant de texte. L'étape courante est la première non remplie : défaire un green ramène à l'étape du green. Aucun état propre, donc rien à sauver.

**Étapes** (textes définitifs au lot 2)
1. Bienvenue : le pro se présente ; la caméra montre la terre vierge.
2. Peindre le départ : touche B, F1 (Tee), contour fantôme du départ.
3. Peindre le fairway : F3, contour du fairway.
4. Peindre le green : F2, contour du green.
5. Poser le départ puis le trou : nouvel outil de pose (touche libre fixée au lot 2, P étant prise par l'outil arbres), clic sur le départ puis sur le green.
6. Ouvrir le trou : bouton « Ouvrir le trou », mis en évidence ; s'il manque quelque chose, le pro dit quoi (liste de `HoleCheck`).
7. Regarder la première paire : la caméra la suit ; l'étape se remplit quand la paire sort du green.
8. Fin : le pro félicite, montre le par et les scores, et rend la main (bac à sable sur le trou construit).

**Rendu** (`game/src/Tutorial/`)
- `TutorialPanel` : la bulle du pro en haut à gauche, l'étape (« 3 sur 8 »), « Passer le tutoriel ».
- `GhostOutline` : le contour de la surface attendue, au sol, en pointillé ; il disparaît quand la zone est assez peinte.
- Mise en évidence : un cadre clignotant autour d'un nœud nommé (`TutorialAnchor`), ou la touche à presser écrite en gras dans la bulle quand l'action est au clavier.
- Lancement : au démarrage du jeu, la bulle du pro propose « Construire ton premier trou ? Oui / Plus tard » ; l'option `-- --tutorial` et un `Tutoriel.bat` dans l'export ouvrent directement la terre vierge.

**Lots (2 PR)**
1. **Sim** : terre vierge, `HoleOpen`, `PlaceTee`, `PlaceHole`, `OpenHole`, `HoleCheck`, `TutorialSteps` ; tests et un test de bout en bout qui joue le tutoriel par commandes. (complet : nouvel état de partie)
2. **Rendu** : outil de pose, bulle du pro, contours fantômes, mises en évidence, proposition au lancement, `Tutoriel.bat` ; la fumée Godot rejoue le tutoriel ; captures ; clôture. (léger)

## Choix à trancher par Guillaume
1. **Sur quel terrain ?** (a) la terre vierge du coude, avec ses contours fantômes : on reconstruit le trou que le jeu montre ensuite, relief et eau déjà là ; (b) une prairie plate, sans contour : plus libre, moins guidée, un fichier de plus à dessiner ; (c) le coude à moitié construit (seul le green manque) : très court, mais on n'apprend pas à peindre un départ ni un fairway. **Recommandé : (a)**, la reconstruction donne un but visible et ne demande aucun nouveau terrain.
2. **Les contours sont-ils obligatoires ?** (a) indicatifs : l'étape se remplit à la surface peinte, où qu'elle soit ; (b) obligatoires : il faut peindre dans le contour. **Recommandé : (a)**, le tutoriel apprend les outils et non un dessin imposé ; `HoleCheck` garantit seulement que le trou se joue.
3. **Comment le lance-t-on ?** (a) proposé par le pro au démarrage, avec « Plus tard », plus `Tutoriel.bat` ; (b) seulement par `Tutoriel.bat` et une touche ; (c) imposé au premier lancement. **Recommandé : (a)**, la proposition de D7 (Q3 : proposé, avec un rappel discret).
4. **Une dernière étape avec votre pro ?** (a) non, le tutoriel s'arrête après la première paire, comme D7 ; (b) oui, le pro inaugure le trou et le joueur joue son premier coup en mode visée (C08). **Recommandé : (a)**, deux lots comme prévu ; le mode visée se découvre déjà avec `--pro`, et l'inauguration pourra s'ajouter en une étape plus tard.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent : la terre vierge se charge, trou fermé ; fermé, aucune paire ne part (ni `SpawnGroup` ni ligue) ; `PlaceTee` hors d'un départ et `PlaceHole` hors du green sont refusées ; `HoleCheck` rend chaque manque attendu ; `OpenHole` est refusée tant qu'il manque quelque chose, puis lance une paire ; l'étape courante suit l'état (défaire un green ramène à l'étape du green) ; un test joue tout le tutoriel par commandes jusqu'à la sortie de la première paire, deux fois, avec la même somme de contrôle.
2. Les sommes de contrôle et trajectoires des tests existants de C01 à C08 sont inchangées.
3. Banc : `HoleCheck` et le calcul de l'étape courante restent sous 1 ms ; le tick à 200 golfeurs reste sous 2 ms.
4. Fumée Godot : elle lance `--tutorial`, rejoue les étapes par commandes et écrit `SMOKE OK`. Captures de la CI (lignes `THUMB`) : la terre vierge avec le contour du départ, l'étape du green, la bulle « Ouvrir le trou » avec le bouton mis en évidence, la première paire sur le trou construit. Guillaume juge, et fait le tutoriel avec `Tutoriel.bat`.
5. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet pour le lot 1 (nouvel état de partie et commandes) ; léger pour le lot 2 (rendu et textes)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
