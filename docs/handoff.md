Mis à jour : 2026-10-24

## État
Lots 1 à 6 de C01 fusionnés (PR #6 à #11) : CI, terrain sim, golfeurs, physique du coup, rendu Godot (blocs, fondu flouté sur 5 m, caméra iso, captures en CI dont les vignettes `THUMB` sont dans le journal du job « Captures »), pinceau (B, F1 à F9, [ ], pas encore essayé à la souris).

## En cours
- C01, lot 7 (assets), PR #12 : `game/src/Rendering/ProceduralMeshes.cs` (golfeur, balle, drapeau, 4 variantes d'arbre de repli ; budgets 300/80/60/400 triangles, lignes `ASSET nom ms tri` vérifiées par la fumée). Arbre du jeu : `game/assets/trees/tree.glb`, produit par `python -I tools/blender/tree_lowpoly.py` (`pip install bpy==5.2.3`, Python 3.13, graine 7, 340 triangles, 1 s, sortie identique d'un lancement à l'autre). `TreeLayer` exige un seul MeshInstance3D et le recale (pied au sol, centré, 4 m) ; la fumée vérifie qu'il est chargé.
- Pour le lot 8 : le JSON du trou est cherché hors de `res://`, à embarquer à l'export ; l'eau reste en blocs de cellules ; le liseré du drapeau peut scintiller de près.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, FPS sur le PC de Guillaume en 1080p.
- 2026-10-24 : arbre par script Blender (`bpy`) dans le cloud, au lieu de TRELLIS (sa carte, une RTX 2080 de 8 Go, est trop juste). Filière comparée dans `docs/assets-3d.md` (PR #13).

## Prochaine étape
Guillaume fusionne la PR #12, puis lot 8 : mesures, captures dans `docs/prototype-c01/`, FPS sur son PC, retouche par agent chronométrée, rapport, facteur choisi.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot. `pip install bpy==5.2.3` marche.
