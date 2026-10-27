Mis à jour : 2026-10-27

## État
C00 à C10 livrés. C09 (golfeurs stratèges) : quatre lots (#74, #75, #79, #80), réglage final du draw et du fade (#83). Reprise après un changement de compte Claude : tout le contexte est dans le dépôt, rien dans l'ancien projet.

## En cours
- C10 livré (#82, #84, #86, #88, #90) : `Cameras/CameraRig.cs` (iso par défaut, libre en perspective par la touche I ; Début = départ, Tab = suivre le golfeur suivant), garde `KeepAbove` (1,5 m au-dessus du relief), banc `-- --fps-bench-low` (ombres PSSM en libre seulement : l'iso n'a aucune ombre), horizon procédural `HorizonLayer` (reconstruit si le relief du bord change). Mesure de Guillaume (RTX 2080, 1080p) : iso 680,2 FPS (1 % bas 500,8), rasante 369,8 (1 % bas 183,4) ; lot 4 (niveaux de détail) sauté. Captures `c10-libre-*` et `c10-horizon` à juger dans le journal du job « Captures » [guillaume].
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
Choisir le chantier suivant avec Guillaume (candidat noté au cadrage de C10 : sauvegarde de partie, F4).
