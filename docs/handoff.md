Mis à jour : 2026-10-31 (reprise sur le compte de Guillaume ; correctifs #150 livrés)

## État
Vague 2 : C15 (pavillon et arrivées, #138 à #143) et C16 (humeur, pensées et adhésion, #144 à #147) livrés. La boucle est fermée : les golfeurs arrivent, réagissent, abandonnent ou deviennent membres, et la ligue recrute parmi eux. Sauvegarde en v16. C'est le moment de la première vraie séance de jeu de Guillaume (`docs/plan-alpha.md`).

## À signaler à Guillaume (C16)
- Calage de l'humeur : départ 7, baisse de 1 tous les 5 trous, contrariété mineure comptée à partir de sa 6e répétition, coup décevant −1 (−2 dans l'original), vue panoramique 0,2, beau trou 0,5. Mesuré sur 30 jours sans ligue : 1,9 à 2,4 % d'abandons (graines 1 à 3). La beauté pèse donc peu : à revoir en jeu.
- Les jours de ligue engorgent le parcours, avec 7 à 9 % d'abandons sur 45 à 60 jours : effet gardé (décision 9). Première raison d'abandon : l'eau.
- Adhésion (« Le neuf », graine 1) : premier membre au jour 6, premier Silver au jour 14, ligue ouverte vers le jour 15. Le jeu crée toujours le club pré-rempli, et le club vide n'existe que dans la sim (C22 ajoutera le menu de nouvelle partie).
- À relire : les 16 pensées de `data/golfers/mood.json` et les libellés du rapport, en dur dans `UiNames.Reactions`.
- C15, à voir en jeu : façade du pavillon du côté de la porte, soleil, chemin de sortie qui longe le départ du trou 9.

## En cours
- Livrés en #150 : `ProSeasonSim` passe le trou à `Awards` (tableau C25 régénéré) ; garde-fou ligue/parcours (un groupe de ligue attend un membre ou le pro qui joue devant lui, et un membre en ligue ne revient pas en visiteur).
- **C23 lot 2** : attend les animations de Guillaume. [guillaume]
- À vérifier par Guillaume : FPS avec `Mesurer FPS.bat` (C13, C21, C23), drapeau sans scintillement, pente de la rive. [guillaume]

## Reporté
Fumée : info-bulle masquée pendant `ProAim`/`PlaceTool` non exercée, jauge de fiche assertée seulement si l'humeur change ; `Reactions.Spots` (256 dernières) non remis à zéro au mois ; `ArrivalParams` et historique des pensées de la fiche non sauvegardés ; critère 4 de C15 tenu en CI en résolveur simplifié ; journal des commandes et rejeu ; sauvegardes nommées ; tampons des champs de flux ; ligue qui ne se clôt jamais (C22).

## Règles de projet
- Fusion : une PR prête (4 jobs de CI verts sur le dernier commit, audit prévu par la spec sans bloquant, aucun commentaire de revue) est fusionnée par la session elle-même (`gh pr merge N --merge --match-head-commit <sha>`), sans supprimer la branche. Une PR qui touche `CLAUDE.md`, `.claude/` ou `docs/workflow.md` attend Guillaume ; `ci.yml` n'en fait pas partie.
- Guillaume est interrogé avant chaque spec : rondes de questions numérotées avec recommandation, maquette si c'est visuel, spec validée par son « go » avant les lots.
- Orchestration : la session principale délègue chaque lot à `codeur` et chaque audit à `auditeur`, en arrière-plan avec `isolation: worktree`. Un correctif est renvoyé au même agent (`SendMessage`). Banc, CI et calages se lancent au premier plan, une commande par appel.
- Calage : borner aussi ce qui est perdu (par exemple les positifs écrêtés), pas seulement le taux visé, et toujours sur 3 graines en physique. Chaque montée de version de sauvegarde archive une vraie sauvegarde de `main` comme fixture (`tests/Golferoo.Sim.Tests/Fixtures/`).
- Environnement Windows (`C:\dev\golferoo`) : .NET 10 et `gh`, pas de Godot (la CI fait foi). Assets : `catalogue-assets/`, `docs/assets-3d.md`.

## Décisions récentes
- C16 (Guillaume) : moins de 5 % d'abandons hors ligue, adhésion à 2/4/8/16 points, ligue ouverte dès 8 avec le pro, bulles à l'événement, le pro se joint au groupe cliqué, pas d'effet direct de la météo, contagion à l'abandon, aucun plafond de membres.
- C15 (Guillaume) : plus de journée réelle (barre du jour, soleil sans nuit), file de 8, plafond de 2 groupes par trou.

## Prochaine étape
EN ATTENTE de Guillaume : première séance de jeu sur « Le neuf » (export Windows de la CI, artefact `golferoo-windows`) et ses retours sur l'humeur et les pensées. Ensuite, cadrer C17 (bâtiments et aménagements) avec lui.
