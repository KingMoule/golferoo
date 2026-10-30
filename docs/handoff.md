Mis à jour : 2026-10-29 (reprise sur le compte de G. St-Arnaud, session de bureau Windows)

## État
C00 à C14 et C21 livrés ; les audits après coup de #117, #118 et #119 sont faits et leurs constats corrigés (#122, #123, #124). C25 (progression du pro par l'expérience, nouveau chantier cadré aujourd'hui) : lot 1 livré (#127), lot 2 en cours.

## En cours
- **C25 lot 2** (pénalité de lie et backspin selon le club et le lie, pour tous) : branche `claude/c25-lot2-lie`, codeur lancé, pas de PR encore. Audit *complet* à faire. Brief : décisions 5 et 9, critères 5 (absolu : le pro joue comme un +4 au niveau 50 et comme un 36 au niveau 0 ; le lot 1 mesurait +1,4 et 41) et 6, critère 4 de C21 à garder, correction du latéral du putt qui dépend du Dosage (réserve du contre-audit de #127). Sauvegarde en v9 : un nouvel état la fait passer en v10. [agent]
- **C25 lot 3** (jeu : aperçus faussés pour le vent et le green, fiche du pro, avis Birdie) : après le lot 2, car les deux touchent `Pro/`. **Lot 4** (calibrage) en dernier ; l'effet de Chip est encore nul, à définir au lot 4. [agent]
- **C23 lot 2** : attend les animations de Guillaume (marche, putt, attente, réactions). [guillaume]
- À vérifier par Guillaume : mesure FPS avec `Mesurer FPS.bat` (C13 critère 13, C23 critère 8, C21 critère 2) ; drapeau sans scintillement en jeu ; pente de la rive (`shore_depth`, `blur_meters`, si elle reste trop raide). [guillaume]

## Reporté
Journal des commandes et rejeu ; sauvegardes nommées ; migration de format à la première version publique ; accueil des membres par le pro (C16) ; abandon en cours de partie (la règle « un abandon ne paie pas » de C14 est sans objet tant qu'il n'existe pas) ; tampons des champs de flux avant les grands parcours ; une journée de ligue qui ne se clôt jamais gèle la ligue (garde-fou de C21 lot 3, à surveiller en C22).

## Règles de projet
- Fusion : une PR prête (quatre jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert) est fusionnée par la session elle-même, méthode merge, épinglée au commit de tête (`gh pr merge N --merge --match-head-commit <sha>`, autorisé par `.claude/settings.local.json`, fichier local). On ne supprime pas la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Chantiers de l'alpha et nouveaux chantiers : Guillaume veut être interrogé avant chaque spec ; rondes de questions numérotées avec recommandation, maquette quand c'est visuel, spec validée par lui (« go ») avant les lots.
- Orchestration sans Threads : la session principale orchestre et délègue chaque chantier ou audit à un agent du dépôt (`codeur`, `auditeur`) en arrière-plan, `isolation: worktree`, plusieurs en parallèle sur des zones disjointes ; un correctif se renvoie au même agent par `SendMessage`.
- Environnement local Windows (`C:\dev\golferoo`) : .NET 10 et `gh` installés, pas de Godot (la CI fait foi). Les astuces Windows (chemins courts, Release) sont dans `CLAUDE.md` ; `.gitattributes` force LF sur les `.cs` (plus d'ENDOFLINE après un nouveau checkout).
- Assets 3D : partir du catalogue `catalogue-assets/` ; filières dans `docs/assets-3d.md`.

## Décisions récentes
- C14 (Guillaume) : cotisations pour tous les membres sauf le pro ; un golfeur qui ramasse sa balle paie le trou ; une clôture de mois fige l'annulation ; finances en deux barres par mois et ligne du solde, détail mois courant et précédent ; Annuler et Ctrl+Z ; un seul message météo dans Birdie, sans non-lu.
- C13 (Guillaume) : à 150 %, le panneau le plus ancien se replie ; bouton pour couper Birdie ; messages de saison et de météo ; la pastille de l'outil Trous reste.
- C25 (Guillaume) : 17 habiletés, XP = 10 × (1 − écart/seuil)², plancher de 5 % au club, niveaux 0 à 50 à gain fixe du 36 au +4, courbe d'XP façon OSRS (le coût double tous les 7 niveaux), pénalité de lie et backspin selon le bâton pour tous, aperçus faussés pour le vent et le green.
- C21 : bannière du champion retirée (Guillaume) ; `Gauss` remplacé par Box-Muller (orchestrateur).

## Prochaine étape
Auditer C25 lot 2 dès sa PR et le fusionner, puis lancer C25 lot 3 puis le lot 4. Ensuite, `/ameliorer` sur `docs/lecons.md` (fin de vague 1), puis cadrer la vague 2 de `docs/plan-alpha.md` avec Guillaume (C15, C16, C17).
