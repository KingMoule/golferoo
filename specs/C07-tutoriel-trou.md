# Tutoriel : construire son premier trou
Statut : validé
Créé : 2026-10-26

## But
La décision D7 de `docs/boucles-v1.md` (Guillaume, 2026-10-24) : pas de scénarios pour l'instant, seulement un **tutoriel très simple** de la construction d'un trou. Votre pro sert de guide, comme le conseiller de l'original (D7, Q5).

Aujourd'hui le jeu s'ouvre sur un trou déjà construit (le dogleg « Le coude » de C08). Rien n'apprend au joueur les touches du pinceau ni de l'outil arbres, et la sim ne sait pas dire si un trou est jouable.

Ce qui prouve la réussite : un joueur qui n'a jamais vu le jeu dégage la place, peint et pose un trou que la sim juge valide, sans autre aide que le tutoriel ; la CI rejoue le tutoriel de bout en bout, par commandes, dans les tests et dans la fumée Godot.

## Périmètre et hors périmètre
**Dans le périmètre**
- Une **terre vierge** : le terrain du coude, vidé de son départ, de son fairway, de son green et de ses bunkers, avec quelques arbres plantés là où devrait passer le fairway.
- Les étapes : enlever des arbres, planter une pousse, peindre le départ, le fairway et le green, poser le départ et le trou. Le tutoriel est terminé dès que la sim juge le trou valide (Guillaume).
- Une validité du trou, lisible par la sim, et deux commandes : poser le départ, poser le trou. Tant que le trou de la terre vierge n'est pas valide, aucun golfeur n'y vient.
- Les étapes sont déduites de l'état du monde (pas des clics), avec la bulle du pro, les contours fantômes, indicatifs, et la mise en évidence de la touche ou du panneau à utiliser.
- Un lancement proposé, jamais imposé : on peut explorer le jeu seul.

**Hors périmètre, et où ça ira**
- Regarder la première paire jouer, inauguration par votre pro : le tutoriel s'arrête au trou valide ; le joueur lance ses golfeurs (G) comme d'habitude.
- Scénarios, objectifs à étoiles, éditeur de scénarios (fiche D7) : plus tard, sur le même moteur d'étapes.
- Sauvegarde (F4) : le tutoriel se refait depuis le début ; il ne se sauvegarde pas.
- Fondation d'UI (P3 : thème, dock, composants) : non requise. Les éléments mis en évidence reçoivent un **identifiant stable** (le nom de leur nœud Godot), que P3 reprendra.
- Plusieurs trous, argent, coût des travaux : le moteur reste à un trou, sans économie.
- Voix enregistrée, portrait ou animation du pro : une bulle de texte signée par le pro.
- Aucun asset nouveau : contours, anneaux et bulle sont procéduraux, les arbres sont ceux de C02 ; le catalogue de Guillaume n'a rien qui serve ici.

## Conception
**Terre vierge** (`data/holes/`)
- `blank-coude.json` : copie du coude où `T`, `F`, `G`, `B` deviennent `R` ; relief, eau et bois identiques ; une poignée d'arbres isolés (de l'ordre de 6 à 10) sur la ligne du fairway, dont un remarquable à garder ou non. Le départ et le trou du fichier ne servent que de positions provisoires.
- Les contours fantômes sont ceux du coude, lus dans `dogleg-par4.json` : on reconstruit le vrai trou, ou on s'en écarte librement (contours indicatifs, Guillaume).

**Sim** (`src/Golferoo.Sim/`)
- `Terrain/HoleCheck.cs` : fonction pure qui rend la liste des manques (départ trop petit, green trop petit, trou hors du green, départ hors du départ peint, pas de chemin praticable, fairway trop court, arbre sur la ligne de jeu…), avec les seuils au lot 1 (lot 1 : 40 m² de départ, 500 m² de fairway, 300 m² de green, 50 m entre départ et trou). Le par se déduit de la distance (`TerrainGrid.DeducedPar`).
- Commandes rejouables : `World.PlaceTee(point)` (sur une cellule `Tee`), `World.PlaceHole(point)` (sur une cellule `Green` ; réutilise `MovePin` de C05) ; elles rendent faux, sans rien mettre en file, hors de leur surface. Les arbres passent par les commandes de C02 (`RazeTree`, `RazeArea`, `PlantTree`).
- `World.HoleOpen` : vrai par défaut, si bien que tous les mondes de C01 à C08 gardent leur somme de contrôle au bit près ; faux sur la terre vierge, où il passe à vrai dès que `HoleCheck` ne rend plus de manque (vérifié au changement de version du terrain, pas à chaque tick). Fermé, `SpawnGroup` lève une exception (le rendu teste `HoleOpen` d'abord) ; la terre vierge se crée sans ligue (le constructeur refuse `league: true`).
- `Tutorial/TutorialSteps.cs` : la liste ordonnée des étapes, chacune une condition sur le monde et un identifiant de texte. L'étape courante est la première non remplie : défaire un green ramène à l'étape du green. Aucun état propre, sauf le nombre d'arbres plantés depuis le début (pour l'étape de la pousse).

**Étapes** (textes définitifs au lot 2 ; la sim ne connaît que les étapes 2 à 8, l'accueil du pro est du rendu)
1. Bienvenue : le pro se présente ; la caméra montre la terre vierge.
2. Enlever les arbres du passage : touche T, clic sur un arbre puis R (ou X pour la zone) ; remplie quand les arbres de la ligne du fairway sont rasés.
3. Planter une pousse : P plante un plant au curseur ; remplie au premier plant.
4. Peindre le départ : touche B, F1 (Tee), contour fantôme du départ.
5. Peindre le fairway : F3, contour du fairway.
6. Peindre le green : F2, contour du green.
7. Poser le départ puis le trou : nouvel outil de pose (touche V), clic sur le départ puis sur le green.
8. Trou valide : le pro félicite et donne le par ; le tutoriel est terminé, le joueur garde la main. Tant qu'il manque quelque chose, le pro dit quoi (liste de `HoleCheck`).

**Rendu** (`game/src/Tutorial/`)
- `TutorialPanel` : la bulle du pro en haut à gauche, l'étape (« Étape 3 sur 6 »), « Passer le tutoriel ».
- `GhostOutline` : le contour de la surface attendue, au sol, en pointillé ; un anneau autour des arbres à enlever.
- Mise en évidence : les actions du tutoriel sont au clavier ou à la souris dans le monde, pas dans des panneaux : la touche à presser est écrite en gras dans la bulle, les arbres à raser sont cerclés de rouge, et les boutons de la bulle (`TutorialYes`, `TutorialLater`, `TutorialStart`, `TutorialSkip`, `TutorialClose`) ont des noms de nœud stables pour P3. Le cadre clignotant autour d'un panneau viendra avec P3.
- Lancement : au démarrage du jeu (sur le coude), la bulle du pro propose « Construire ton premier trou ? Oui / Plus tard » ; « Plus tard » la ferme et le joueur explore seul. L'option `-- --tutorial` et un `Tutoriel.bat` dans l'export ouvrent directement la terre vierge.

**Lots (2 PR)**
1. **Sim** : terre vierge, `HoleCheck`, `PlaceTee`, `PlaceHole`, `HoleOpen`, `TutorialSteps` ; tests et un test de bout en bout qui joue le tutoriel par commandes. (complet : nouvel état de partie)
2. **Rendu** : outil de pose, bulle du pro, contours fantômes, mises en évidence, proposition au lancement, `Tutoriel.bat` ; la fumée Godot rejoue le tutoriel ; captures ; clôture. (léger)

## Décisions de Guillaume (2026-10-26)
1. **Terrain** : le coude vidé (sans départ, fairway, bunkers ni green), avec quelques arbres là où devrait passer le fairway, pour apprendre à enlever un arbre ; on apprend aussi à planter une pousse.
2. **Contours** : indicatifs.
3. **Lancement** : proposé, avec un bouton « Plus tard » ; personne n'est obligé de le faire.
4. **Fin** : dès que le jeu vérifie que le trou est valide, le tutoriel est terminé.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent : la terre vierge se charge, avec ses arbres sur la ligne du fairway, trou fermé ; fermé, aucune paire ne part (ni `SpawnGroup` ni ligue) ; `PlaceTee` hors d'un départ et `PlaceHole` hors du green sont refusées ; `HoleCheck` rend chaque manque attendu, et rien pour le coude et le prototype ; le trou s'ouvre de lui-même dès qu'il est valide ; l'étape courante suit l'état (défaire un green ramène à l'étape du green) ; un test joue tout le tutoriel par commandes (raser, planter, peindre, poser) jusqu'au trou valide, deux fois, avec la même somme de contrôle.
2. Les sommes de contrôle et trajectoires des tests existants de C01 à C08 sont inchangées.
3. Banc : `HoleCheck` et le calcul de l'étape courante restent sous 1 ms ; le tick à 200 golfeurs reste sous 2 ms.
4. Fumée Godot : elle lance `--tutorial`, rejoue les étapes par commandes et écrit `SMOKE OK`. Captures de la CI (lignes `THUMB`) : la terre vierge avec les arbres à enlever, l'étape du green avec son contour, la proposition au lancement, le trou valide. Guillaume juge, et fait le tutoriel avec `Tutoriel.bat`.
5. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet pour le lot 1 (nouvel état de partie et commandes) ; léger pour le lot 2 (rendu et textes)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
