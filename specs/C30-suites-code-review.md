# Suites du code review
Statut : livré
Créé : 2026-11-05

## But
Appliquer les recommandations du rapport final du code review à trois relecteurs (2026-11-05) : corriger les défauts trouvés, fermer les trous de la somme de contrôle et de la sauvegarde, et ajouter les tests qui les auraient attrapés.

## Choix tranchés par Guillaume (2026-11-05, « j'y vais avec tes recommandations »)
1. Peindre sur un arbre qui ne tient plus paie son abattage ; l'annulation rend l'arbre et l'argent.
2. L'avance rapide reste : elle devient une commande en file, sauvée et dans la somme.
3. La sauvegarde ne relit que la version courante et la précédente (v24 et v23).
4. Le drapeau du jour change de place sur tous les trous.
Reportés : découpage de `World` (M9), file de commandes unique (M8 ; seul le correctif « toute commande de terrain ferme le coup de pinceau » est fait).

## Ce qui change
- C1 : un bâtiment (pavillon compris) ne chevauche plus un autre ni sa porte, à la pose, au déplacement du pavillon et au chargement ; le chemin vers chaque porte est vérifié depuis le départ.
- H1 : les groupes finis (ligue, pro) quittent la liste ; `CanStartProRound` grise le bouton d'entraînement tant que le pro joue.
- H2 : peinture et bulldozer (`Bulldoze`, une seule commande) paient les arbres qu'ils retirent au tarif du rasage ; devis `QuotePaint`, `QuoteBulldoze`, `QuoteRaise`, `QuoteRazeArea` dans la sim (le jeu ne recalcule plus les prix) ; l'annulation rend les arbres et leur prix. `CanStand`/`CanPlantOn` : une seule règle pour planter et garder un arbre.
- M1 : `PinPicker.Pick` par trou, graine mêlée du numéro de trou.
- M2 : `SetFastForward` en file ; `FastForward` sauvé et mêlé.
- M3 : `Checksum` mêle tout l'état sans condition (fin des « une somme d'avant reste valable ») : compteurs d'identifiants, points de passage, champs de flux en cache, cartes complètes, groupes, golfeurs, par du trou 1.
- M4 : déplacer un départ ou un drapeau invalide la note ; sur une terre du joueur, le par suit la longueur. Le jeu relance la note quand la sim la retire.
- M5 : une retouche qui ne change aucun coût de marche garde les champs de flux.
- M6 : L, F9, F10 derrière le HUD de débogage (F12), comme G.
- M7 : une paire de ligue partie depuis plus de 3 jours finit d'office (trous restants ramassés) ; des cartes gardées 3 jours reviennent sans note.
- M10 : sauvegarde v24, lecture dès la v23 ; branches et fixtures d'avant retirées, fixture `v23-partie` produite par `main` (1ff1cdb).
- Sauvegarde des emplacements : relue avant d'être écrite (refus si elle ne se relit pas, l'ancienne est gardée), écriture vidée sur disque avant le renommage. Remplace la rotation des sauvegardes automatiques proposée au rapport.

## Critères d'acceptation
1. `CodeReviewTests` : pavillon refusé sur un snack-bar ; eau peinte sur un arbre puis annulée ; bulldozer au prix du devis et annulable ; drapeau qui bouge sur chaque trou ; par et note après un déplacement ; journée de ligue close sans note ; 60 jours de ligue avec groupes bornés et pro disponible ; 300 commandes au hasard, sauvegarde relue toutes les 10 (même somme, mêmes octets) ; fil d'alarme sur les champs de `World`.
2. `Load_RealV23Save_KeepsTheGame` ; les versions 2 à 22 sont refusées comme anciennes.
3. Suite complète, format, build du jeu et fumée Godot verts en CI.
