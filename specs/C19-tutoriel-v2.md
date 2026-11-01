# Tutoriel v2 et conseiller
Statut : cadré (en attente du « go » de Guillaume)
Créé : 2026-11-01

## But
Le tutoriel de C07 s'arrête au premier trou valide, avant les golfeurs, l'argent et les réactions (manque M8 de `docs/plan-alpha.md`). C19 le prolonge jusqu'à la boucle complète (ouvrir, accueillir, encaisser, lire une pensée, équiper), puis installe un **conseiller** : pendant la première année, le pro commente l'état du club en bulles courtes.

## Périmètre et hors périmètre
Dans le périmètre : étapes de la suite du tutoriel ; activation du club (pavillon, argent, arrivées) dans le monde de la leçon ; bon de pose offert par le tutoriel ; conseils du pro (4 déclencheurs) ; option « Conseils du pro » ; sauvegarde v21.

Hors périmètre :
- nouveaux bâtiments, nouvelles pensées ou nouveaux seuils d'économie ;
- conseils au-delà de la première année ou en dehors des 4 du plan (bâtiment jamais posé, membre perdu : plus tard, en données) ;
- vérifier que le joueur a *corrigé* le trou désigné par une pensée (le pro le suggère seulement) ;
- tutoriel dans la Nouvelle partie de C18 (la leçon garde sa terre vierge `blank-coude`).

## Choix tranchés par Guillaume (2026-11-01)
1. Suite jouée dans le **même monde** : au trou valide, le club est activé (pavillon, argent, arrivées) dans ce monde.
2. « Lire une pensée » : **une pensée lue suffit** (la correction du trou est conseillée, non contrôlée).
3. Snack-bar : le tutoriel offre un **bon de pose** (mécanisme de C18) après le premier green fee.
4. Conseils : **les 4 du plan, une fois chacun**, première année.

## Conception
- **Garde `Blank` → drapeau de leçon** : pavillon, arrivées et bâtiments sont aujourd'hui refusés si `Terrain.Blank` (`World.Arrivals`, `World.Buildings`, `World.Clubhouse`). Le terrain de la leçon reste `Blank` (revalidation du trou à chaque édition) ; un nouvel état `World.ClubActive` (dans `GameState`) autorise ces trois systèmes. Il passe à vrai au moment où `HoleOpen` devient vrai pour la première fois dans la leçon : la sim pose alors le pavillon (même emplacement que `placeClubhouse`) et charge l'économie Facile. Hors leçon, `ClubActive` est vrai d'office.
- **Étapes de suite** (`TutorialStep`, déduites de l'état comme v1, sans état propre sauf les drapeaux ci-dessous) : `OpenHole` (le trou valide s'ouvre ; texte d'accueil du club) → `WatchPair` (première paire au départ) → `FirstGreenFee` (premier encaissement au grand livre) → `ReadThought` (le joueur ouvre la fiche ou l'info-bulle d'un golfeur qui porte une pensée ; le pro explique ce qu'on corrige) → `PlaceSnackBar` (bon de pose, voir ci-dessous) → `Done`. Chaque étape a son texte français dans `UiNames`/données et ses identifiants dans `UiIds` (C13).
- **Bon de pose** : à `FirstGreenFee`, `GameState` reçoit un bon de snack-bar. Le seuil de déblocage (3 trous) ne doit pas bloquer un bon du tutoriel : **à vérifier au lot 1** que `CheckBuilding` laisse poser avec un bon quand le genre est verrouillé ; sinon le bon lève le seuil pour ce seul genre (règle écrite dans le test, pas dans les données).
- **Pensée lue** : un drapeau `ThoughtRead` posé par une commande de la sim quand la fiche/l'info-bulle est ouverte sur un golfeur dont la pensée n'est pas vide (commande rejouable, dans `Checksum` et `Save`).
- **Conseiller** (`data/pro/advice.json`, `AdviceEvaluator` appelé au changement de jour, comme `ObjectiveEvaluator`) : 4 conseils, chacun avec condition, texte du pro et seuil en données :
  - `firstWalkout` : premier abandon d'un golfeur ;
  - `lowCash` : solde sous un seuil (même idée que la « trésorerie basse » de la pause automatique) ;
  - `unplayedHole` : un trou ouvert depuis N jours sans aucun passage ;
  - `wornTurf` : part de cellules usées de fairway/green au-dessus d'un seuil.
  Fenêtre : première année civile de la partie, un conseil une seule fois (`GameState` : conseils déjà donnés, bitset). Sortie : `AdviceGiven` journalisé, affiché en bulle du pro (réutilise `Birdie.Post(Birdie.Pro, …)` et la pause automatique de C18 si elle est active). Option « Conseils du pro » (oui par défaut) dans `options.cfg`, sans effet sur la sim : seul l'affichage est coupé, l'évaluateur tourne toujours (déterminisme).
- **Sauvegarde v21** : `ClubActive`, `ThoughtRead`, bon du tutoriel (déjà dans les bons), bitset des conseils entrent dans `Checksum` et `Save`, avec un cas `SaveTests` qui échoue s'ils sont oubliés ; une v20 se relit (`OldestReadable` reste à 11) avec `ClubActive` vrai hors leçon, faux dans une leçon non terminée ; fixture v20 de `main` archivée. La leçon, aujourd'hui non sauvegardable, le devient **une fois le club activé** (`CanSave`).

## Mesure du lot 1 : le bon et le seuil de déblocage
Mesuré sur le code de `main` (test `Voucher_LiftsTheUnlockThreshold_ForItsKindOnly` retiré de sa règle) : `CheckBuilding` testait `IsUnlocked` (nombre de trous ouverts seulement) avant tout le reste et ne regardait pas les bons : avec un bon de snack-bar en main et 1 trou ouvert, il rendait `Locked`, la pose était refusée. Règle retenue, écrite dans `World.IsUnlocked` et dans le test, pas dans les données : **un bon de pose d'un genre lève le seuil de ce seul genre** (vaut aussi pour les bons des objectifs de C18). « Se rembourse » du critère 4 est lu comme « se consomme, et le bâtiment n'est pas facturé » (le défrichage l'est toujours).

## Critères d'acceptation
1. `ClubActive` : faux sur la terre vierge tant que le trou est fermé (aucune arrivée, aucun pavillon) ; vrai dès la première ouverture, qui pose le pavillon une fois ; défaire le green après coup ne le retire pas (test sur le vrai `World`).
2. Chaque étape de suite se déclenche par son évènement précis (test par étape, sur la vraie date de `Calendar` et le vrai `Ledger`), dans l'ordre, sans état propre hors drapeaux sauvegardés.
3. `ThoughtRead` ne se pose que sur un golfeur à pensée non vide ; une commande sur un golfeur sans pensée est sans effet.
4. Le bon du tutoriel permet de poser un snack-bar avec 1 trou ouvert, une seule fois, et se rembourse (test ; mesure du comportement actuel de `CheckBuilding` consignée en tête du lot 1).
5. Chaque conseil se déclenche par sa condition, une seule fois, seulement la première année ; l'option coupe l'affichage sans changer le `Checksum` (tests).
6. Sauvegarde v21 : cas `SaveTests` par nouvel état ; une v20 se relit ; écrire puis relire une leçon en cours donne le même `Checksum`.
7. Déterminisme : même graine et mêmes commandes, même `Checksum` après 10 jours de leçon.
8. Banc P1 : le tick tient les seuils de C12 (moyenne sous 2 ms, 99e centile sous 5 ms).
9. Fumée Godot (`SMOKE OK`) : leçon complète de la terre vierge au snack-bar posé, avec une paire qui joue ; captures `c19-*` (club ouvert, première paire, pensée, bon de pose, conseil) dans le job « Captures » (scène rapide).
10. Tous les textes sont en français, dans les données ou `UiNames`.

## Lots
1. **Suite du tutoriel** : `ClubActive`, étapes `OpenHole` à `PlaceSnackBar`, bon de pose, `ThoughtRead`, panneau et identifiants, sauvegarde v21 (partie). Critères 1 à 4, 6 (partie), 7, 9 (partie). Contrôle complet.
2. **Conseils du pro** : `advice.json`, `AdviceEvaluator`, bulles, option, critères 5, 6 (reste), 8, 9 (reste). Contrôle léger.

## Règle de livraison (briefs du codeur)
Chaque brief de lot reprend la règle de la PR #161 : **avant de dire « terminé », lancer la suite complète (`dotnet test Golferoo.sln`) et la fumée Godot (`SMOKE OK`) et montrer leur sortie**. Un test ciblé ne suffit pas.

## Niveau de contrôle
complet (lot 1), léger (lot 2).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
