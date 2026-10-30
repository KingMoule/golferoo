Mis à jour : 2026-10-30 (compte de G. St-Arnaud, session de bureau Windows)

## État
Vague 1 finie : C00 à C14, C21 et C25 livrés. C25 (progression du pro par l'expérience) : lots 1 à 4 fusionnés (#127, #128, #132, #134, #135), audits complets sans bloquant. Sauvegarde en v11.

## À signaler à Guillaume (C25)
- Lot 4 : `distanceFloor` de Distance à 75 % au lieu de 85 %, confirmé par Guillaume le 2026-10-30.
- Lot 3 : le vent réel est retiré de la note de visée (l'aperçu faussé suffit).
- Recalage : niveau 24 environ après la saison 1, 47–48 en saison 10, 50 en saison 12–13 (`costScale` 0,085).
- Putt : le pro simulé vise 0,5 m après le trou ; sur pente lue, Putter reste vers 0,47 de qualité au niveau 50 (putts ratés à plus de 1,6 m), c'est le jeu et non la notation.

## En cours
- **Petit correctif de test** (réserve de l'audit de #135) : `ProSeasonSim` appelle `ProQuality.Awards(...)` sans le trou ; passer `hole.Hole` puis régénérer le tableau de qualité de la spec C25. Ne change ni le jeu ni les saisons. [agent]
- **C23 lot 2** : attend les animations de Guillaume (marche, putt, attente, réactions). [guillaume]
- À vérifier par Guillaume : mesure FPS avec `Mesurer FPS.bat` (C13 critère 13, C23 critère 8, C21 critère 2) ; drapeau sans scintillement en jeu ; pente de la rive (`shore_depth`, `blur_meters`) ; liste d'autorisations de `.claude/settings.json` proposée dans #133 (facultatif). [guillaume]

## Reporté
Journal des commandes et rejeu ; sauvegardes nommées ; migration de format à la première version publique ; accueil des membres par le pro (C16) ; abandon en cours de partie (la règle « un abandon ne paie pas » de C14 est sans objet tant qu'il n'existe pas) ; tampons des champs de flux avant les grands parcours ; une journée de ligue qui ne se clôt jamais gèle la ligue (garde-fou de C21 lot 3, à surveiller en C22).

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même, méthode merge, épinglée au commit de tête (`gh pr merge N --merge --match-head-commit <sha>`, autorisé par `.claude/settings.local.json`, fichier local). On ne supprime pas la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Chantiers de l'alpha et nouveaux chantiers : Guillaume veut être interrogé avant chaque spec ; rondes de questions numérotées avec recommandation, maquette quand c'est visuel, spec validée par lui (« go ») avant les lots.
- Orchestration sans Threads : la session principale orchestre et délègue chaque chantier ou audit à un agent du dépôt (`codeur`, `auditeur`) en arrière-plan, `isolation: worktree`, plusieurs en parallèle sur des zones disjointes ; un correctif se renvoie au même agent par `SendMessage`.
- Environnement local Windows (`C:\dev\golferoo`) : .NET 10 et `gh` installés, pas de Godot (la CI fait foi). Les astuces Windows (chemins courts, Release) sont dans `CLAUDE.md` ; `.gitattributes` force LF sur les `.cs` (plus d'ENDOFLINE après un nouveau checkout).
- Assets 3D : partir du catalogue `catalogue-assets/` ; filières dans `docs/assets-3d.md`.

## Décisions récentes
- C25 (Guillaume) : 17 habiletés, XP = 10 × (1 − écart/seuil)², plancher de 5 % au club, niveaux 0 à 50 à gain fixe du 36 au +4, courbe d'XP façon OSRS (le coût double tous les 7 niveaux), pénalité de lie et backspin selon le bâton pour tous, aperçus faussés pour le vent et le green ; Chip = contrôle du roulement et dispersion.
- C14 (Guillaume) : cotisations pour tous sauf le pro ; un golfeur qui ramasse paie le trou ; une clôture de mois fige l'annulation ; Annuler et Ctrl+Z.
- C13 (Guillaume) : à 150 %, le panneau le plus ancien se replie ; bouton pour couper Birdie ; messages de saison et de météo.

## Prochaine étape
Cadrer la vague 2 de `docs/plan-alpha.md` avec Guillaume (C15 pavillon et arrivée, C16 humeur et adhésion, C17 bâtiments), une ronde de questions par chantier. Rien n'est lancé avant ses réponses.
