Mis à jour : 2026-10-24

## État
Lots 1 à 5 de C01 fusionnés (PR #6 à #10) : CI, terrain sim, golfeurs, physique du coup, rendu Godot (blocs, fondu flouté sur 5 m, caméra iso, captures en CI dont les vignettes `THUMB` sont dans le journal du job « Captures »).

## En cours
- C01, lot 6 (pinceau) : PR ouverte. `World.PaintSurface` / `RaiseTerrain` en file, appliquées au tick suivant ; versions surface et hauteur séparées (un relief ne reconstruit pas les champs de flux) ; repose sans pénalité des golfeurs et balles dans une cellule devenue infranchissable ; peinture qui ne laisserait aucune cellule franchissable ignorée. Jeu : B, F1 à F9, [ ]. Le pinceau n'a pas été essayé à la souris (pas de Godot dans le conteneur).
- Pour le lot 8 : le JSON du trou est cherché hors de `res://`, à embarquer à l'export ; l'eau reste en blocs de cellules.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, arbre par TRELLIS sur le PC de Guillaume au lot 7 (Meshy Pro en repli), FPS sur son PC en 1080p.

## Prochaine étape
Guillaume fusionne la PR du lot 6, puis lot 7 (assets : golfeur, drapeau, balle par code ; arbre par TRELLIS sur son PC en Remote Control), contrôle léger.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot.
