Mis à jour : 2026-10-24

## État
Spec C01 (prototype d'un trou) validée et fusionnée (PR #5). Lot 1 en PR : solution `Golferoo.sln` (Golferoo.Sim, tests xUnit, banc, projet Godot `game/`), CI GitHub Actions (format, tests, banc, import et fumée Godot 4.7.2 headless).

## En cours
- C01, lot 1 : branche `claude/project-thread-we8mvk`. Fait : CI verte, risque `net10.0` levé, critère 3 prouvé (commit 769af8e : les deux jobs rouges, puis retiré), commandes confirmées dans `CLAUDE.md`. En attente de fusion par Guillaume.

## Décisions récentes
- Godot.NET.Sdk 4.7.2, jeu et simulation en `net10.0` (confirmé par la fumée en CI).
- RNG maison (xoshiro256**) pour un déterminisme indépendant de `System.Random`.
- Pinceau en jeu, assets par code, arbre par TRELLIS sur le PC de Guillaume au lot 7 (Meshy Pro en repli), FPS sur son PC en 1080p.

## Prochaine étape
Guillaume fusionne la PR du lot 1, puis lot 2 (terrain sim : grille, `HeightField`, surfaces, trou JSON), contrôle complet.

## Environnement cloud
Pas de .NET préinstallé : `apt-get install dotnet-sdk-10.0` marche (dépôt Ubuntu) ; dot.net/builds.dotnet est refusé par le proxy. Godot ne se télécharge pas (github releases en 403) : la CI fait foi pour Godot.
