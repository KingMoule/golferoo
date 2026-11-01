Mis à jour : 2026-11-01 (C17 livré, passages de « Le neuf » #155)

## État
Vague 2 : C15, C16 et C17 (bâtiments : snack-bar, pro shop, hangar, bancs, #149 à #154) livrés. #155 a ouvert des passages dans les bois entre les trous de « Le neuf » : avant, les golfeurs sortaient des enclos des trous 4 à 8 par téléportation (130 en 8 jours, 0 depuis). Les champs de flux des marcheurs sont bornés à leur trajet (pire tick du banc 2,3 ms au lieu de 5,4). Sauvegarde en v19.

## À signaler à Guillaume
- C17 : la capture `c17-visite` montre le pro shop (le snack-bar était dans l'enclos) ; les noms des bâtiments sont dans `UiNames`, pas dans les données. Écarts acceptés, notés dans la spec.
- Marche IA gardée à ×3,0 (décision de Guillaume) : un groupe seul marche 36 à 43 % de sa partie, borne du test 33 à 55 %. Le garde-fou « au moins un abandon en 8 jours » est retiré : 0 abandon sur 8 jours, 2 à 4 % sur 30 jours.
- À relire en jeu : textes des bâtiments, pensées de `mood.json`, façade du pavillon, chemin de sortie le long du départ 9.

## En cours
- **C23 lot 2** : attend les animations de Guillaume. [guillaume]
- À vérifier par Guillaume : FPS avec `Mesurer FPS.bat`, drapeau sans scintillement, pente de la rive. [guillaume]

## Reporté
- C17 : capture du snack-bar à remettre (la poche est ouverte) ; calage long des bâtiments à relancer depuis #155 ; réserves de l'audit du lot 2 (golfeurs en visite pas exclus de `MustWait` et `TeeCleared`, `VisitDoor` hors somme).
- Champs de flux : `ReachesRing` pourrait garder un champ dont un pas diagonal longe le coin d'une emprise ajoutée (hypothèse d'audit, non testée).
- Fumée : info-bulle masquée pendant `ProAim`/`PlaceTool` non exercée ; `Reactions.Spots` non remis à zéro au mois ; `ArrivalParams` et historique des pensées non sauvegardés ; journal des commandes et rejeu ; sauvegardes nommées ; ligue qui ne se clôt jamais (C22).

## Règles de projet
- Fusion : une PR prête (4 jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue) est fusionnée par la session elle-même (`merge_pull_request`, méthode merge, `expectedHeadSha`), sans supprimer la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Guillaume est interrogé avant chaque spec (questions en cartes, recommandation), spec validée par son « go » avant les lots.
- Orchestration : lots délégués à un codeur, audits à un auditeur, en arrière-plan ; un correctif est renvoyé au même agent.
- Calage : 3 graines en physique, borner aussi ce qui est perdu. Chaque montée de version de sauvegarde archive une fixture de `main`.
- Captures : la suite prend environ 600 s sur 720 en CI ; une nouvelle capture qui attend la sim doit choisir une scène rapide.

## Prochaine étape
EN ATTENTE de Guillaume : séance de jeu sur « Le neuf » avec les bâtiments (artefact `golferoo-windows`), puis cadrage de C18 (une partie du début à la fin).
