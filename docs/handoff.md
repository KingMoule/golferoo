Mis à jour : 2026-10-27

## État
C00 à C09 livrés. C09 (golfeurs stratèges) : quatre lots (#74, #75, #79, #80), réglage final du draw et du fade (#83). Reprise après un changement de compte Claude : tout le contexte est dans le dépôt, rien dans l'ancien projet.

## En cours
- C10, `specs/C10-camera-lod.md` : lot 1 fusionné (#84) : `Cameras/CameraRig.cs` (mode iso seul), actions `camera_*` dans `project.godot`, Début centre sur le départ, Tab suit le golfeur suivant (lu dans `_Input`, avant l'UI). Vue iso inchangée : seuls les réglages ont été vérifiés, pas les pixels (journaux de captures non lisibles depuis le conteneur).
- C10 lot 2 fusionné (#86) : mode `Free` en perspective (touche I, V étant prise par PlaceTool), garde `CameraRig.KeepAbove` (1,5 m au-dessus du relief, dans la carte), captures `c10-libre-haute`, `-mi`, `-rasante` à juger par Guillaume dans le journal du job « Captures » [guillaume].
- C09 livré. Mineurs relevés par l'audit, à reprendre si Guillaume les remarque en jeu : fonceur avec moins de pénalités que le neutre (9 contre 12) ; dispersion de l'artiste en baisse de 6 % seulement au test ; légende de `c09-profils` sur le tee ; bannière du champion sur la fiche ; erreurs de jugement des golfeurs (hors périmètre, à reparler). Images dans le dossier partagé `c09/`.
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
`apt-get update` puis `apt-get install dotnet-sdk-10.0` marche ; `dotnet test Golferoo.sln` dure environ 13 min (dot.net refusé par le proxy). Godot ne se télécharge pas : la CI fait foi. `pip install bpy==5.2.3` marche. Artefacts CI non téléchargeables : lire les journaux. Mesure de référence (RTX 2080, 1080p) : 632 FPS prototype, 322 FPS à 5 000 arbres.

## Prochaine étape
C10 lot 3 : ombres en cascades et banc « vue rasante » (`-- --fps-bench-low`), puis mesure de Guillaume sur sa RTX 2080 (seuil 144 FPS).
