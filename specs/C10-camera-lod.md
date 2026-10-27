# Caméra à modes, caméra libre et niveaux de détail
Statut : proposé
Créé : 2026-10-27

## But
Dernière étape de l'« Ordre proposé » de `docs/boucles-v1.md` : caméra à modes et niveaux de détail (F6), puis caméra libre en perspective (A9), avec les raccourcis de caméra de la piste A2. Choisi par Guillaume le 2026-10-27 comme chantier suivant C09.

Aujourd'hui `game/src/Camera/IsoCamera.cs` est une caméra orthographique seule, qui lit ses touches en dur (`Key.W`, `Key.Z`, flèches). Le terrain est déjà rendu en blocs de 32 × 32 cellules (`TerrainRenderer.ChunkCells`) et les arbres ont déjà un `MultiMesh` par bloc (C02) ; aucun niveau de détail ni `visibility_range` n'existe.

Ce qui prouve la réussite : le joueur passe de la vue iso à une vue libre jusqu'au ras du sol et revient, sans voir le bord du monde ; la vue rasante est mesurée sur la machine de Guillaume et tient le seuil fixé plus bas.

## Périmètre et hors périmètre
**Dans le périmètre**
- Un `CameraRig` à modes (iso, libre) qui remplace `IsoCamera`, avec ses actions dans l'`InputMap`.
- Caméra libre : déplacement, orbite, inclinaison, garde au-dessus du relief, transitions lissées. L'iso reste la vue par défaut (ADR 0001, Q1 de la fiche caméra).
- Raccourcis A2 : revenir au départ du trou ; suivre un golfeur.
- Une scène « vue rasante » au banc de FPS, avec ombres en cascades.
- Niveaux de détail du terrain et des arbres, seulement si la mesure les demande.
- Un horizon autour de la carte.

**Hors périmètre, et où ça ira**
- Mode photo, album et caméra de télévision : reportés (décision D12 du 2026-10-24) ; le `CameraRig` les recevra comme modes.
- Visite à pied à 1,70 m : plus tard (Q2 de la fiche caméra).
- Info-bulle et sélection d'un golfeur à la souris : chantier à part, avec l'humeur.
- Réglages pour le Steam Deck : à part, une fois la mesure sur PC faite.
- Thèmes autres que le terrain actuel.

## Conception
**Lot 1 : caméra à modes** (`game/src/Camera/`, `project.godot`)
- `IsoCamera` devient `CameraRig`, avec le seul mode iso, mêmes réglages (inclinaison 35,264°, zoom 6 à 400, rotation par quarts). Les accesseurs utilisés ailleurs (`View`, `Target`, `YawDegrees`, `Size`, `SetBounds`) gardent leur forme.
- Actions `camera_*` dans l'`InputMap` à la place des `Key.*` codés en dur.
- Raccourcis : revenir au départ du trou (touche Début) ; suivre un golfeur, au choix de Guillaume (question 3).

**Lot 2 : caméra libre** (`game/src/Camera/`)
- Mode libre en perspective : déplacement, orbite au clic du milieu, inclinaison jusqu'au ras du sol, zoom en distance.
- Garde : la caméra reste au moins à 1,5 m au-dessus du relief (`TerrainGrid.Sample`) et dans les limites de la carte.
- Bascule iso et libre par une touche, transition lissée autour du même point visé.
- Captures `c10-libre-*` (`tools/Captures.cs`) : trois vues, haute, mi-hauteur et rasante, sur le dogleg.

**Lot 3 : mesure du pire cas** (`game/src/Tools/FpsBench.cs`, `game/src/Main.cs`)
- Ombres en cascades de la lumière `Sun` (4 tranches, environ 400 m) en mode libre ; ombres actuelles en iso.
- Mode `-- --fps-bench-low` : vue rasante qui longe le parcours chargé (5 000 arbres, 200 golfeurs), 5 s de chauffe puis 30 s de mesure ; `Mesurer FPS.bat` lance les deux mesures.
- Guillaume mesure sur sa RTX 2080 en 1080p. Si le seuil (question 2) tient, le lot 4 ne se fait pas.

**Lot 4 : niveaux de détail, conditionnel** (`game/src/Rendering/TerrainRenderer.cs`, `TreeLayer.cs`)
- Terrain : une seconde résolution par bloc (une cellule sur deux), choisie par Godot selon la taille à l'écran.
- Arbres : `visibility_range` sur les `MultiMeshInstance3D` par bloc ; un arbre simplifié au loin seulement si la mesure le demande encore.
- Rien ne change dans la sim.

**Lot 5 : horizon** (nouvelle couche dans `game/src/Rendering/`)
- Anneau de relief simplifié autour de la carte, qui prolonge les bords du terrain, et collines en fond (question 4).

**Lots (4 ou 5 PR)** : 1, puis 2, puis 3 ; 4 et 5 en parallèle après 3 (fichiers disjoints).

## Choix à trancher par Guillaume
1. **Prochain chantier.** Tranché le 2026-10-27 : la caméra libre et les niveaux de détail.
2. **Seuil de FPS en vue rasante** au-delà duquel on saute le lot 4. **Recommandation : 144 FPS** sur la RTX 2080 en 1080p (322 FPS aujourd'hui en iso à 5 000 arbres).
3. **Suivre un golfeur au lot 1.** (a) Oui, par une touche qui passe d'un golfeur sur le parcours au suivant, sans clic. (b) Plus tard, avec la sélection à la souris et l'info-bulle. **Recommandation : (a)** : utile tout de suite pour regarder jouer un profil de C09, et la sélection à la souris s'y branchera.
4. **Horizon.** (a) Anneau de relief et collines simples, procéduraux. (b) Décor plus riche avec des assets du catalogue de Guillaume. **Recommandation : (a)** ; (b) quand viendront les autres thèmes.

## Critères d'acceptation
1. Lot 1 : les captures iso existantes sont inchangées ; aucun `Key.` ne reste dans `game/src/Camera/` (`grep`) ; la fumée Godot écrit `SMOKE OK`.
2. Lot 1 : le raccourci « départ » centre la caméra sur le départ du trou ; le suivi garde le golfeur suivi au centre de l'écran (fumée ou capture).
3. Lot 2 : en mode libre, à toute position atteignable, la caméra reste à 1,5 m au moins au-dessus du relief et dans les limites de la carte (test de la fonction de garde, sans Godot si possible).
4. Lot 2 : les captures `c10-libre-*` montrent la vue haute, mi-hauteur et rasante ; la bascule iso, libre, iso revient au même point visé.
5. Lot 3 : `-- --fps-bench-low` écrit son résultat dans le journal ; Guillaume a mesuré sur sa machine et le chiffre est noté dans `docs/handoff.md`.
6. Lot 4 (s'il a lieu) : la vue rasante passe le seuil sur la machine de Guillaume ; la vue iso ne perd pas plus de 5 % de FPS.
7. Lot 5 : aucune capture `c10-libre-*` ne montre le bord du monde ni le fond vide.
8. Tous lots : `dotnet test Golferoo.sln`, `dotnet format Golferoo.sln --verify-no-changes` et les quatre jobs de CI verts.

## Niveau de contrôle
léger (rendu et caméra seulement, aucune règle de la sim touchée).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
