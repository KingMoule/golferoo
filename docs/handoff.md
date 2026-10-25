Mis à jour : 2026-10-25

## État
C01 (prototype d'un trou) : lots 1 à 7 fusionnés (PR #6 à #12), lot 8 en PR #14. Bilan dans `docs/prototype-c01-rapport.md` : simulation C# pure, déterministe, très sous ses budgets ; rendu Godot 4.7 .NET à 632 FPS (RTX 2080, 1080p, 200 golfeurs) ; **facteur 2 retenu** (cellules de 4 m, `Main.DefaultFactor`).

## Outils en place
- CI : simulation (format, tests, banc JSON), fumée Godot, captures en rendu logiciel (lignes `THUMB`/`FULL` en JPEG base64 dans le journal, facteurs 3 et 4 en plus pour comparaison), export Windows (artefact `golferoo-windows` : `Lancer.bat`, `Mesurer FPS.bat` → `fps-result.txt`).
- Le JSON du trou est copié dans `game/data/` à la compilation (ignoré par git) et lu en `res://`.
- Arbre affiché : `game/assets/trees/tree.glb`, produit par `python -I tools/blender/tree_lowpoly.py` (`pip install bpy==5.2.3`). Golfeur, balle, drapeau et arbre de repli : `game/src/Rendering/ProceduralMeshes.cs`.
- Banc : le champ 512² est jugé sur la médiane, après un tour à blanc qui laisse le JIT optimiser (sinon le facteur 2, mesuré en premier, échouait au hasard).

## Défauts connus (suites proposées dans le rapport)
- L'eau garde un contour en escalier de cellules ; le liseré du drapeau peut scintiller de près.
- Chaque champ de flux alloue ~2 Mo (tas des gros objets) : réutiliser les tampons avant les grands parcours. Planifier un drive coûte ~15 ms, non mesuré sous charge.
- Le pinceau n'a été essayé à la souris que par Guillaume.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0`. RNG maison (xoshiro256**).
- 2026-10-24 : arbre par script Blender (`bpy`) au lieu de TRELLIS (RTX 2080 de 8 Go trop juste) ; filières dans `docs/assets-3d.md`.
- 2026-10-24 : facteur 2 (Guillaume, sur le rapport C01).

## Prochaine étape
C03 livré le 2026-10-25 (PR #27, #31, #33, `specs/C03-calendrier-sol.md`) : calendrier déduit du tick (`src/Golferoo.Sim/Time/Calendar.cs` : journée de 6 min en x1, mois de 10 jours, 12 mois, départ le 1er avril), saison calculée (plus de setter), arbres +4 mois par mois de jeu, hiver doux sans neige, soleil qui tourne (`game/src/SunPath.cs`), F10 = mois suivant ; sol par cellule selon la saison (`TerrainGrid.Soil`, été = référence au bit près, un seul monde par terrain). Suite : C04 (météo ; feuillus d'hiver de la PR #30 pour le rendu des saisons).
C02 livré le 2026-10-25 (PR #19 à #26, `specs/C02-arbres-echelle.md`) : arbres objets de la sim (`src/Golferoo.Sim/Trees/`), chêne, bouleau et sapin à l'échelle réelle (pack à 800 triangles depuis la PR #29, 302 FPS avec 5 000 arbres), balle contre tronc et couronne selon la saison, croissance (F10), outil arbres (T : raser, planter, faire tomber, dégager). Mesure FPS (Guillaume) : 453,5 FPS avec 5 000 arbres et 200 golfeurs, 1080p, RTX 2080 (`Mesurer FPS.bat` remplit la carte à 5 000 arbres). Suites : `World.Season` hors somme de contrôle (C03), chutes par la météo (C04), le planificateur ignore les arbres.
R03 tranché par Guillaume le 2026-10-24 (`docs/boucles-v1.md`, section « Décisions ») : première version avec météo et saisons, entretien du gazon, ligues, arbres vivants et un tutoriel simple ; ni scénarios, ni mode photo (caméra prête), ni nuit. Côtés positifs de la pluie M1 à M9 tous gardés. Chantiers C03 à C07 à cadrer, C02 élargi aux arbres vivants. Ordre : P1, C02, C03, C04, C05 ; en parallèle C06 et P3 puis C07.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot. `pip install bpy==5.2.3` marche. Artefacts CI non téléchargeables depuis le conteneur : passer par les journaux.
