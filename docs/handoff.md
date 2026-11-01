Mis à jour : 2026-11-01 (C18 livré, #157 à #160)

## État
Vague 3 : C18 (une partie du début à la fin) livré en 3 lots : menu titre, nouvelle partie sur un terrain plat provisoire (C24 le remplacera), 3 emplacements + auto avec vignette, pause (Échap), objectifs (7, bons de pose), faillite avec « continuer quand même », bilan annuel, options et pause automatique. Sauvegarde en v20.

## À signaler à Guillaume
- C18 : la faillite n'arrive qu'à un club sans visiteurs (sous −100 000 $ au mois 25, `Bankrupt` au mois 30 ; avec des visiteurs le solde reste vers +340 000 $ au mois 12). Revente d'un bâtiment offert par un bon : +23 500 $ au total, toléré. `golferDemand` branché sur l'humeur requise d'une sortie parfaite : à confirmer. « Trésorerie basse » de la pause automatique = achat refusé. Vignettes des emplacements non vérifiées en headless.
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
EN ATTENTE de Guillaume : séance de jeu (menu, nouvelle partie, objectifs, options ; artefact `golferoo-windows`), puis cadrage de C19, C20, C22 ou C24 (génération de la propriété, qui remplace le terrain plat).
