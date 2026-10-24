Mis à jour : 2026-10-24

## État
Lots 1 à 3 de C01 fusionnés (PR #6, #7, #8) : solution et CI, terrain sim, champs de flux, golfeurs par paires, mode charge, banc (tous seuils passés en CI).

## En cours
- C01, lot 4 (coup) : branche `claude/project-thread-we8mvk`, PR ouverte. `Shots/` : `Club`, `BallSimulator` (pas 1/200 s), `PhysicalShotResolver` (planification sans RNG, exécution avec dispersion, putt). Balle en vol suivie au tick (`GolferState.BallInFlight`, `GolferView.Ball3D`). Audit complet : sain. Moyenne 3,84 coups sur le par 4 (profil « pro », à recalibrer si on veut des golfeurs moyens) ; planification d'un drive environ 15 ms, non mesurée quand plusieurs groupes frappent au même tick.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, arbre par TRELLIS sur le PC de Guillaume au lot 7 (Meshy Pro en repli), FPS sur son PC en 1080p.

## Prochaine étape
Guillaume fusionne la PR du lot 1, puis lot 2 (terrain sim : grille, `HeightField`, surfaces, trou JSON), contrôle complet.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot.
