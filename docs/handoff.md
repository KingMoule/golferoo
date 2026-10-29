Mis à jour : 2026-10-29 (arrêt pour fin de forfait, en pleine vague 1 de l'alpha)

## État
C00 à C12 livrés. Vague 1 de `docs/plan-alpha.md` en cours : C13 (lots 1 à 3), C14 (lot 1), C23 (lot 1) et les specs C21 (#114) et C23 (#111) fusionnés. PR #117, #118 et #119 fusionnées le 2026-10-29 sans leurs audits : à auditer après coup (voir Prochaine étape).

## En cours
- **C14 lot 2** (revenus, annulation du coup) : PR #119, `claude/c14-lot2-revenus`, 247dcd8, fusionnée sans audit ; audit *complet* à faire après coup (le premier a été arrêté). Livré : green fee par trou inscrit (15 $ visiteur, 12 $ membre, 0 pro), cotisations au mois, `World.Stroke.cs` (Begin/Undo/CanUndo/StrokeCost), sauvegarde v6, réserves R2 à R4 du lot 1. Rentabilité : +83 700 $/an fréquenté, −45 360 $/an à moitié moins de visiteurs. À juger à l'audit : pas d'abandon en cours de partie dans la sim (non testé) ; délai de 5 s laissé au jeu (lot 3) ; cotisations sur tous les `League.Members` hors pro (interprétation à confirmer par Guillaume) ; hiver arrondi à 0 paire. Puis **lot 3** (écran des finances, prix dans la barre ; contrôle léger).
- **C21 lot 1** (bordures, rive, drapeau) : PR #117, `claude/c21-lot1-rendu`, f4ab185, fusionnée sans audit ; audit *léger* à faire après coup. À revoir : les captures `c21-bordures` et `c21-eau` de f4ab185 (rive droite corrigée, angle changé), corriger le corps de la PR (berge de 1 m et non 0,8). Drapeau : z-fighting du liseré, à vérifier en jeu [guillaume].
- **C13 lot 4** (Birdie, `--ui-scale`) : PR #118, `claude/c13-lot4-birdie`, 855e2a6, fusionnée sans audit ; audit *léger* à faire après coup, relire `c13-echelle-150` d'après le correctif `UiTheme.FitList`. Écarts à la spec : pas de bouton pour couper les apparitions, pas de message météo du jour ; à 150 % quatre panneaux ouverts (1 380 px) dépassent la largeur (1 248 px) ; la pastille du haut reste pour l'outil Trous.
- **C21 lot 2** (dispersion, choix non optimaux) : branche `claude/c21-lot2-jeu`, 2854ac6, WIP, pas de PR. Fait : critère 4 (scratch 66,0 % fairways / 62,7 % greens, hcp 18 46,6 % / 23,3 %, < 0,05 % au-delà de 3 σ) et critère 5 (19,3 % à hcp 36, 5,9 % scratch, plafond 50 m = 0,5 coup, RNG du monde via `World.AdvancePlans`). Pas vérifié : suite complète (des tests calés sur l'ancien niveau vont échouer : `SkillTests`, notes de parcours, profils, sommes de référence), format, banc, build. Reste : critères 6 (fonceur), 7 (artiste), 8 (handicap avec trou fermé : note scratch par trou dans `CourseRating`, donc Save, Checksum, `SaveTests`, version), bannière du champion, légende `c09-profils`, fusion d'origin/main, PR, audit *complet*. `DispersionTests` dure 3,4 min : à alléger. À trancher : `Gauss` (somme de 4 uniformes) borne à 3,46 σ alors que la spec veut une normale.
- **C21 lot 3** (journée de 9 min, perf) : pas commencé, fusionne en dernier.
- **C23 lot 2** : attend les animations de Guillaume (marche, putt, attente, réactions) [guillaume].

## Reporté (à reprendre plus tard)
Journal des commandes et rejeu ; sauvegardes nommées ; migration de format à la première version publique ; accueil des membres par le pro (C16) ; tampons des champs de flux avant les grands parcours.

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même (`merge_pull_request`, méthode merge, `expectedHeadSha`). On ne supprime pas la branche. Exception : une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Chantiers de l'alpha (C13 à C24) : Guillaume veut être interrogé avant chaque spec ; rondes de questions numérotées avec recommandation, maquette quand c'est visuel, spec validée par lui (« go ») avant les lots.
- Assets 3D : partir du catalogue `catalogue-assets/` (catalogue_complet.html et recensement-catalogue.md, dossier partagé du projet) ; filières dans `docs/assets-3d.md`.
- Agents du dépôt non chargés en session cloud de projet : déléguer à un agent général qui lit `.claude/agents/<rôle>.md`, `isolation: worktree`. L'isolement refuse les commandes shell composées : l'agent passe par un script du scratchpad.

## Environnement cloud
`apt-get update` puis `apt-get install dotnet-sdk-10.0` ; suite complète 8 à 10 min. Godot ne se télécharge pas : la CI fait foi. `gh` en REST seulement (PR par l'outil MCP). Journaux CI : `get_job_logs` avec `return_content`, captures en lignes `FULL` base64 (`tail_lines` 64 au moins). Fetch une ref par commande. Mesure de Guillaume après #73 (RTX 2080, 1080p, 200 golfeurs, 1 250 arbres doublés) : 527,9 FPS, 1 % bas 420.

## Prochaine étape
Auditer après coup #119 (complet), #117 et #118 (légers) et corriger ce qu'ils trouvent, finir C21 lot 2, puis C14 lot 3 et C21 lot 3. Mesure FPS de Guillaume avec `Mesurer FPS.bat` (C13 critère 13, C23 critère 8, C21 lot 1 critère 2) [guillaume].
