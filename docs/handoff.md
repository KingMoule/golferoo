Mis à jour : 2026-10-24

## État
Lots 1 et 2 de C01 fusionnés (PR #6, #7) : solution et CI ; terrain sim (HeightField Catmull-Rom, surfaces, trou JSON de 381 yd).

## En cours
- C01, lot 3 : branche `claude/project-thread-we8mvk`, PR ouverte. Champs de flux (Dial exact, cache 64, reconstruit au changement de `TerrainGrid.Version`), golfeurs par paires (machine à états, `IShotResolver` + `StubShotResolver`), mode charge, `World.Snapshot`, banc aux facteurs 2/3/4 (tous seuils passés ; marge la plus faible : champ 512² vers 10 à 15 ms pour 20). Audit complet : sain ; corrigés : échauffement du banc, ramassage distinct du trou (`PickedUp`).
- Pour le lot 4 : `ShotResult` sans pénalité ni drop, coup instantané (pas d'état balle en vol) : `World.Hit` devra s'élargir.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, arbre par TRELLIS sur le PC de Guillaume au lot 7 (Meshy Pro en repli), FPS sur son PC en 1080p.

## Prochaine étape
Guillaume fusionne la PR du lot 1, puis lot 2 (terrain sim : grille, `HeightField`, surfaces, trou JSON), contrôle complet.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot.
