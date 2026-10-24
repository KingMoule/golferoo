Mis à jour : 2026-10-24

## État
Lots 1 à 4 de C01 fusionnés (PR #6 à #9) : CI, terrain sim, champs de flux et golfeurs, physique du coup (moyenne 3,84 coups, profil « pro »).

## En cours
- C01, lot 5 (rendu Godot) : PR #10. Terrain par blocs de 32 × 32, shader de fondu (poids par cellule floutés sur 2,5 m, bruit, maximum), eau, caméra iso, interpolation, FPS, captures en CI sous xvfb + lavapipe (Forward+). Les vignettes des captures sont dans le journal du job « Captures » (lignes `THUMB`, JPEG base64) : l'artefact n'est pas téléchargeable depuis le conteneur cloud.
- Pour le lot 6 : `TerrainRenderer.RebuildChunks(rect de cellules modifiées)` reconstruit avec une marge de 2 cellules ; il reconstruit aussi tous les arbres à chaque appel (à restreindre pour le pinceau). Pour le lot 8 : le JSON du trou est cherché hors de `res://`, à embarquer à l'export.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, arbre par TRELLIS sur le PC de Guillaume au lot 7 (Meshy Pro en repli), FPS sur son PC en 1080p.

## Prochaine étape
Guillaume fusionne la PR du lot 5, puis lot 6 (pinceau de surface et de relief), contrôle léger.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot.
