Mis à jour : 2026-10-30 (compte de G. St-Arnaud, session de bureau Windows)

## État
Vague 1 finie (C00 à C14, C21, C25). Vague 2 : C15 (pavillon et arrivée des golfeurs) livré, #138 à #142, audits sans bloquant. Sauvegarde en v13. Plus d'heures dans le jeu : barre du jour, soleil sans nuit, arrivées toute la journée ; IA marche ×3 et vol ×1,5 ; « Le neuf » absorbe environ 3 à 5 départs par jour (`groupsPerDay` 4).

## À signaler à Guillaume (C15)
- Choix du groupe de la file pour le pro reporté à C16 (décision 20) : le clic sélectionne et la caméra suit, mais `Main.ChosenGroupId` n'est lu par rien ; le message « choisi dans la file » promet plus que ce qui est fait.
- À regarder en jeu : la façade du pavillon est-elle du côté de la porte (le .glb est supposé faire face en +Z) ; le soleil, peu lisible dans la capture (pluie).
- Le chemin de sortie longe le départ du trou 9 (visuel).
- Job Captures porté à 540 s (environ 6 min en CI) ; le banc « vent » a échoué une fois (ratio 1,078 contre 1,05), passé au relancement.

## En cours
- **Petit correctif de test** (réserve de l'audit de #135) : `ProSeasonSim` appelle `ProQuality.Awards(...)` sans le trou ; passer `hole.Hole` puis régénérer le tableau de qualité de la spec C25. [agent]
- **C23 lot 2** : attend les animations de Guillaume (marche, putt, attente, réactions). [guillaume]
- À vérifier par Guillaume : FPS avec `Mesurer FPS.bat` (C13 critère 13, C23 critère 8, C21 critère 2) ; drapeau sans scintillement ; pente de la rive (`shore_depth`, `blur_meters`) ; liste d'autorisations de `.claude/settings.json` proposée dans #133 (facultatif). [guillaume]

## Reporté
Choix du groupe de la file par le pro, migration des 40 membres de la ligue vers le vivier, vivier qui grossit avec les visiteurs acceptés (C16) ; test CI du critère 4 en résolveur simplifié, sans le régime saturé du physique ; chargement v12 testé seulement sur un monde neuf ; journal des commandes et rejeu ; sauvegardes nommées ; migration de format à la première version publique ; tampons des champs de flux avant les grands parcours ; une journée de ligue qui ne se clôt jamais gèle la ligue (à surveiller en C22).

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même (`gh pr merge N --merge --match-head-commit <sha>`). On ne supprime pas la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Guillaume veut être interrogé avant chaque spec : rondes de questions numérotées avec recommandation, maquette quand c'est visuel, spec validée par lui (« go ») avant les lots.
- Orchestration : la session principale délègue chaque chantier ou audit à `codeur` ou `auditeur` en arrière-plan, `isolation: worktree` ; un correctif se renvoie au même agent par `SendMessage`. Un codeur lance banc et CI au premier plan (en tâche de fond, le CPU est étranglé).
- Environnement local Windows (`C:\dev\golferoo`) : .NET 10 et `gh`, pas de Godot (la CI fait foi). Astuces Windows dans `CLAUDE.md`.
- Assets 3D : partir du catalogue `catalogue-assets/` ; filières dans `docs/assets-3d.md`.

## Décisions récentes
- C15 (Guillaume) : pavillon placé automatiquement et déplaçable contre un coût ; file de 8, refus qui baisse la réputation ; vivier persistant ; pas d'effet de saison ni de prix ; plafond 2 groupes par trou ; plus de journée réelle (décisions 14 à 19 de la spec).
- C25 : 17 habiletés, niveaux 0 à 50, `distanceFloor` 75 %.

## Prochaine étape
Cadrer C16 (humeur, pensées et adhésion) avec Guillaume, une ronde de questions à la fois ; y inclure le choix du groupe de la file et la migration des membres de la ligue vers le vivier. Rien n'est lancé avant son « go ».
