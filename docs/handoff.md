Mis à jour : 2026-10-27

## État
C00 à C08 livrés. C09 (golfeurs stratèges) en cours : lot 1 fusionné (PR #74), lot 2 en PR #75, lots 3 et 4 à faire. Reprise après un changement de compte Claude : tout le contexte est dans le dépôt, rien dans l'ancien projet.

## En cours
- C09, `specs/C09-golfeurs-strateges.md` (statut validé, choix de Guillaume dans la spec) [agent]
  - Lot 2 : PR #75 (`claude/project-thread-idoo4e`), carte de jeu par drapeau (`Pathing/PlayMap.cs`). Audit sain, 303 tests verts ; reste à confirmer la CI sur le dernier commit puis fusionner.
  - Lots 3 (six profils : prudent, long frappeur, draw, fade, artiste, fonceur) et 4 (rendu, fiche du membre, capture `c09-profils`) : à faire, après la #75.
- PR #73 « Arbres et golfeurs deux fois plus grands » (`claude/project-thread-wyqjkw`, commit 80da40f), conflits résolus mais en pause : le test de sortie de forêt de C09 y échoue (22 retapes sur 100 à la lisière ouest, un sapin de 33 m collé à la balle). À remettre à jour sur `main` après la #75, corriger ce test, puis fusionner. Ensuite Guillaume relance `Mesurer FPS.bat` : 1 250 arbres doublés, 204 FPS mesurés avant ce changement contre 333 auparavant [agent, puis guillaume].
- C07 (tutoriel) et correctifs de pousse et d'outils (PR #72) : livrés. Guillaume juge encore les captures `tutorial-*` ; contours en pointillé à épaissir s'ils sont trop discrets [guillaume].
- C08 : Guillaume juge les captures `pro-aim` et `pro-sheet`.

## Reporté (à reprendre plus tard)
Dispersion des coups, lissage des bordures de fairway, green et bunker ; accueil des membres par le pro avec l'humeur ; eau en escalier et scintillement du drapeau (`docs/prototype-c01-rapport.md`) ; tampons des champs de flux à réutiliser avant les grands parcours ; le banc du changement de jour frôle sa limite de 5 ms en CI.

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même (`merge_pull_request`, méthode merge, `expectedHeadSha`). On ne supprime pas la branche. Exception : une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Assets 3D : tout ce que Guillaume génère avec d'autres IA est recensé dans `catalogue-assets/` (catalogue_complet.html et recensement-catalogue.md, dossier partagé du projet, copie sur son PC). Partir de ce catalogue avant de demander ou d'importer un asset ; filières dans `docs/assets-3d.md`.
- Agents du dépôt (codeur, auditeur, relecteur) non chargés en session cloud de projet : déléguer à un agent général qui lit `.claude/agents/<rôle>.md`.

## Environnement cloud
`apt-get install dotnet-sdk-10.0` marche (dot.net refusé par le proxy). Godot ne se télécharge pas : la CI fait foi. `pip install bpy==5.2.3` marche. Artefacts CI non téléchargeables : lire les journaux. Mesure de référence (RTX 2080, 1080p) : 632 FPS prototype, 322 FPS à 5 000 arbres.

## Prochaine étape
Fusionner la PR #75 si la CI est verte, puis remettre la #73 à jour sur `main`, puis le lot 3 de C09.
