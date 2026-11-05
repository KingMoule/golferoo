Mis à jour : 2026-11-05 (C30 : suites du code review)

## État
Vague 3 livrée (C18, C19, C20, C24, C26, C27, C28) ; C29 cadré, lots à faire. Code review complet à trois relecteurs le 2026-11-05, ses recommandations appliquées en C30. Sauvegarde en v24, lecture dès la v23 seulement.

## À signaler à Guillaume
- C30 : les sommes de contrôle ont toutes changé (elles mêlent tout l'état) ; les sauvegardes v22 et plus vieilles ne se chargent plus. Peindre sous un arbre et le bulldozer coûtent maintenant l'abattage. L, F9 et F10 demandent le HUD de débogage (F12). Reportés : découpage de `World` (M9) et file de commandes unique (M8).
- C24 : propriété vierge générée de la graine (1 200 × 800 m, pavillon au centre, 1 à 2 étangs, un ruisseau, bois ≈ 25 % soit ~1 000 arbres et 10 à 20 remarquables), aperçu et « Autre graine » dans Nouvelle partie, tutoriel désactivé par `Main.TutorialEnabled` (faux, code gardé). Le trou 1 reste fermé : tu traces tout, personne n'arrive avant un trou ouvert. À juger en jouant : relief très doux (pente 1 à 3 %), peu d'arbres (44 à l'hectare, beaucoup de sapins), captures `c24-*` non regardées. Écarts dans la spec (`IsLesson`, `plantedTrees`).
- C20 : son minimal livré en 2 lots. Volumes (général, musique, effets, ambiance) dans les options. Sons synthétisés (clics, caisse, refus, objectif, conseil, abandon) + sons d'`opensgolf-free` (GPL-2.0-or-later, exception de `CLAUDE.md` pour l'usage personnel) + ta boucle d'oiseaux (extrait de 40 s). Réserves : pas de pluie ni de vent seul (silence en hiver et sous la pluie) ; club des frappes déduit de la distance au drapeau ; eau, arbre et bunker pour les visiteurs seulement ; à écouter et à régler en jeu ; origine de la boucle oiseaux-vent à confirmer. Banc `puddleFlood` : faux rouge à chaque run de #166, #169, #170 (33 à 35 ms pour 20), repassé au vert à la relance : à traiter en C22.
- C19 : le tutoriel continue après le premier trou dans le même monde (club activé à l'ouverture, paire, premier green fee, fiche d'un golfeur ouverte, bon de pose du snack-bar), puis 4 conseils du pro la première année (abandon, trésorerie sous 25 000 $, trou 3 jours sans partie, gazon usé à 15 %), option « Conseils du pro ». Sauvegarde en v22. Un bon de pose lève désormais le seuil de déblocage de son genre (y compris les bons d'objectifs de C18). Réserves connues : pavillon non posé si le terrain est encombré, leçon bloquée si un green fee vaut 0, premier abandon testé en posant le compteur. Seuils et textes des conseils à relire en jeu.
- Flaques (« casual water », C04 lot 4) retirées le 2026-11-02, décision de Guillaume : code, rendu, tests et banc `puddleFlood` supprimés ; la balle reste où elle s'arrête. Humidité du sol, pluie et gazon inchangés (PR `claude/retirer-flaques`).
- C18 : la faillite n'arrive qu'à un club sans visiteurs (sous −100 000 $ au mois 25, `Bankrupt` au mois 30 ; avec des visiteurs le solde reste vers +340 000 $ au mois 12). Revente d'un bâtiment offert par un bon : +23 500 $ au total, toléré. `golferDemand` branché sur l'humeur requise d'une sortie parfaite : à confirmer. « Trésorerie basse » de la pause automatique = achat refusé. Vignettes des emplacements non vérifiées en headless.
- C17 : la capture `c17-visite` montre le pro shop (le snack-bar était dans l'enclos) ; les noms des bâtiments sont dans `UiNames`, pas dans les données. Écarts acceptés, notés dans la spec.
- Marche IA gardée à ×3,0 (décision de Guillaume) : un groupe seul marche 36 à 43 % de sa partie, borne du test 33 à 55 %. Le garde-fou « au moins un abandon en 8 jours » est retiré : 0 abandon sur 8 jours, 2 à 4 % sur 30 jours.
- À relire en jeu : textes des bâtiments, pensées de `mood.json`, façade du pavillon, chemin de sortie le long du départ 9.

## En cours
- **C24 lot 4** : arbres matures de 8 000 triangles (`mature_species_8000`) et LOD, après que Guillaume a jugé la propriété en jeu. [guillaume]
- **C23 lot 2** : attend les animations de Guillaume. [guillaume]
- À vérifier par Guillaume : FPS avec `Mesurer FPS.bat`, drapeau sans scintillement, pente de la rive. [guillaume]

## Reporté
- C17 : capture du snack-bar à remettre (la poche est ouverte) ; calage long des bâtiments à relancer depuis #155 ; réserves de l'audit du lot 2 (golfeurs en visite pas exclus de `MustWait` et `TeeCleared`, `VisitDoor` hors somme).
- Champs de flux : `ReachesRing` pourrait garder un champ dont un pas diagonal longe le coin d'une emprise ajoutée (hypothèse d'audit, non testée).
- Fumée : info-bulle masquée pendant `ProAim`/`PlaceTool` non exercée ; `Reactions.Spots` non remis à zéro au mois ; `ArrivalParams` et historique des pensées non sauvegardés ; journal des commandes et rejeu ; sauvegardes nommées.

## Règles de projet
- Fusion : une PR prête (4 jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue) est fusionnée par la session elle-même (`merge_pull_request`, méthode merge, `expectedHeadSha`), sans supprimer la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume.
- Guillaume est interrogé avant chaque spec (questions en cartes, recommandation), spec validée par son « go » avant les lots.
- Orchestration : lots délégués à un codeur, audits à un auditeur, en arrière-plan ; un correctif est renvoyé au même agent.
- Calage : 3 graines en physique, borner aussi ce qui est perdu. Chaque montée de version de sauvegarde archive une fixture de `main`.
- Captures : la suite prend environ 600 s sur 720 en CI ; une nouvelle capture qui attend la sim doit choisir une scène rapide.

## Prochaine étape
Lots de C29 (retours de la séance 2), puis C22 (préparation de l'alpha).
