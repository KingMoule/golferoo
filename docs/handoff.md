Mis à jour : 2026-10-28

## État
C00 à C12 livrés. C12 (parcours de plusieurs trous, P1) : spec #98, lots #100, #101, #102, #104, #103. Aucune PR ouverte.

## En cours
- C12 livré : `Course` de N trous (`src/Golferoo.Sim/Course/`, `data/courses/`), « Le neuf » (9 trous, par 36) par défaut, Page suivante/précédente pour aller de trou en trou, outil Nouveau trou (V). P1 tenue en CI (18 trous, 36 groupes : tick 1,09 ms, p99 2,05 ms) grâce à la planification étalée et aux champs de flux refaits par lots. Sauvegarde v4 (anciennes refusées). À juger : captures `c12-*`, `Mesurer FPS.bat` sur « Le neuf » [guillaume]. Constats ouverts dans la spec : journée de ligue de 13,4 jours sur 18 trous (journées espacées de 10), handicap faussé par un trou fermé.
- C11 livré : `World.Save`/`World.Load` (`src/Golferoo.Sim/Save/`, `World.Save.cs`), tout l'état entre deux ticks, format binaire `GOLF` v1 compressé (somme de contrôle et empreinte du corps dans l'en-tête, lecture plafonnée à 64 Mo). Jeu : F5 sauve (`user://saves/rapide.golf`), F8 charge le plus récent qui se lit (rapide ou `auto.golf`), autosave à chaque jour de jeu, `-- --load <fichier>`, pas de sauvegarde pendant le tutoriel ni en mesure FPS. Banc : dogleg + ligue + 5 000 arbres, écriture 3,7 ms, chargement 3,4 ms, 61 Ko. À essayer en jeu (F5, quitter, relancer, F8) et captures `c11-avant`/`c11-apres` à juger [guillaume].
- C10 livré : mesure de Guillaume (RTX 2080, 1080p) iso 680,2 FPS, rasante 369,8 ; lot 4 sauté. Captures `c10-libre-*` et `c10-horizon` à juger [guillaume].
- C09 livré. Mineurs de l'audit à reprendre si Guillaume les remarque en jeu : fonceur moins pénalisé que le neutre (9 contre 12) ; dispersion de l'artiste en baisse de 6 % seulement ; légende de `c09-profils` ; bannière du champion sur la fiche ; erreurs de jugement des golfeurs (à reparler). Images dans le dossier partagé `c09/`.
- PR #73 (arbres et golfeurs ×2) fusionnée : Guillaume relance `Mesurer FPS.bat` (204 FPS mesurés avant contre 333) [guillaume].
- C07 et C08 : Guillaume juge les captures `tutorial-*`, `pro-aim` et `pro-sheet` [guillaume].

## Reporté (à reprendre plus tard)
Journal des commandes et rejeu (moitié de F4) avec les scénarios ; sauvegardes nommées avec la fondation d'UI (F7) ; migration de format à la première version publique. Dispersion des coups, lissage des bordures de fairway, green et bunker ; accueil des membres par le pro avec l'humeur ; eau en escalier et scintillement du drapeau ; tampons des champs de flux avant les grands parcours ; le banc du changement de jour frôle sa limite de 5 ms en CI.

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même (`merge_pull_request`, méthode merge, `expectedHeadSha`). On ne supprime pas la branche. Exception : une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Guillaume délègue les choix de cadrage aux recommandations de la session (2026-10-27) : décider, noter dans la spec, ne le solliciter que pour ce qu'il est seul à pouvoir faire.
- Assets 3D : partir du catalogue `catalogue-assets/` (catalogue_complet.html et recensement-catalogue.md, dossier partagé du projet) ; filières dans `docs/assets-3d.md`.
- Agents du dépôt non chargés en session cloud de projet : déléguer à un agent général qui lit `.claude/agents/<rôle>.md`. Deux codeurs en parallèle : un worktree chacun.

## Décisions récentes
- Sauvegarde (session, par délégation) : binaire plutôt que JSON (exactitude des doubles, taille) ; à n'importe quel tick ; F5/F8. Tout nouvel état de la sim entre dans `Checksum` et dans `Save`, avec un cas `SaveTests` qui échoue si on l'oublie ; à monter dans `CLAUDE.md` si Guillaume l'accepte.

## Environnement cloud
`apt-get update` puis `apt-get install dotnet-sdk-10.0` ; `dotnet test Golferoo.sln` dure 8 à 10 min. Godot ne se télécharge pas : la CI fait foi. `gh` en REST seulement (GraphQL bloqué : `gh pr create` échoue, outil MCP GitHub à la place). Journaux CI trop gros : lignes de preuve en fin de job et dans le résumé (`$GITHUB_STEP_SUMMARY`). Mesure de référence (RTX 2080, 1080p) : 632 FPS prototype, 322 FPS à 5 000 arbres.

## Prochaine étape
C12 livré. **Pas de C13** : Guillaume veut une série de planification, la phase de prototypage touchant à sa fin (2026-10-28).
