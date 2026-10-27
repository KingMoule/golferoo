Mis à jour : 2026-10-27

## État
C00 à C08 livrés. C09 (golfeurs stratèges) : les quatre lots sont fusionnés (#74, #75, #79, #80) ; reste une décision de Guillaume sur le bonus du draw et du fade. Reprise après un changement de compte Claude : tout le contexte est dans le dépôt, rien dans l'ancien projet.

## En cours
- C09, `specs/C09-golfeurs-strateges.md` [guillaume, puis agent]
  - Lots 3 (profils, `Golfers/GolferProfile.cs`, `Terrain/BeautyMap.cs`) et 4 (style sur la fiche du membre, captures `c09-profils`, `c09-approche`, `c09-sortie`, `league-card-member`) fusionnés. Images dans le dossier partagé `c09/`.
  - Ouvert : joueurs de draw et de fade à +3 % et −8 % de dispersion avec leur effet, soit environ 0,3 coup de mieux par trou que le neutre sur le dogleg (0,17 sans aucun bonus, par le seul choix entre deux coups). Guillaume choisit : garder, supprimer le bonus (reco) ou supprimer et +10 % de dispersion en coup droit. Deux constantes : `GolferProfile.ShapedDistance`, `ShapedDispersion`.
  - Mineurs relevés par l'audit, non traités : fonceur avec moins de pénalités que le neutre (9 contre 12) ; dispersion de l'artiste en baisse de 6 % seulement au test ; légende de `c09-profils` sur le tee ; bannière du champion sur la fiche.
- PR #73 (arbres et golfeurs ×2) fusionnée : Guillaume relance `Mesurer FPS.bat` (204 FPS mesurés avant contre 333) [guillaume].
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
Appliquer la décision de Guillaume sur le draw et le fade, puis passer C09 à « livré ».
