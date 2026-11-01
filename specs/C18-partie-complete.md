# Une partie du début à la fin
Statut : validé (go de Guillaume le 2026-11-01)
Créé : 2026-11-01

## But
Le jeu démarre aujourd'hui sur un trou chargé d'office : pas de menu, pas de partie à commencer, pas de fin. C18 (manque M7 de `docs/plan-alpha.md`) donne une **carrière en difficulté Facile** : menu titre, nouvelle partie, sauvegardes à emplacements, objectifs, faillite, bilan de fin d'année, options.

## Périmètre et hors périmètre
Dans le périmètre : menu titre ; nouvelle partie ; jeu de paramètres de difficulté en données (seul « Facile » proposé) ; menu pause (Échap) ; trois emplacements + autosave avec date et vignette ; objectifs du club et leurs récompenses ; faillite ; bilan annuel ; options (résolution, plein écran, échelle d'interface, pause automatique).

Hors périmètre :
- la propriété générée et l'aperçu de graine (C24, qui remplacera le terrain provisoire) ;
- les niveaux Normal et Difficile (attendent l'économie réglée, C22) ;
- le volume et les bus audio (C20 ajoutera son curseur aux options) ;
- le tutoriel v2 (C19) ; les sauvegardes nommées et le journal de commandes (déjà reportés).

## Conception
- **Scènes** : `game/src/Ui/TitleMenu.cs` (menu titre, charger) devient la scène de lancement ; `Main` ne charge plus un trou d'office mais reçoit une demande (nouvelle partie, sauvegarde à charger, « Le neuf » démo). Les options `--course` et le tutoriel restent inchangés (ils contournent le menu).
- **Nouvelle partie** (choix 1, terrain plat provisoire) : `Terrain/BlankProperty.cs` produit, d'une graine entière, une carte plate d'environ 1 200 × 800 m en rough, sans arbre ni eau, avec le pavillon posé près d'un bord ; la graine est affichée et sauvée, mais n'influe que sur la tonalité du sol. C24 remplace ce générateur sans toucher au menu. « Le neuf » reste au menu comme « Parcours de démonstration » (argent normal de Facile, sans objectifs).
- **Difficulté** : `data/economy/facile.json` reste le fichier de Facile ; `data/difficulty/facile.json` ajoute ce qui manque : exigence des golfeurs et vitesse de baisse de l'humeur (multiplicateurs, 1,0 = actuel), `objectiveRewards`. L'argent de départ, les coûts et le seuil de faillite restent dans l'économie. Un fichier par niveau, un seul chargé ; tous obligatoires, un oubli refuse le fichier (même règle que C14).
- **Sauvegardes** (choix 2) : `SaveSlots` passe de deux fichiers à `slot1..3.golf` + `auto.golf` + `rapide.golf`. Chaque emplacement a un `.jpg` voisin (vignette prise par `ThumbnailRenderer` à l'écriture, 320×180) et une ligne de métadonnées (nom du club, date de jeu du `Calendar`, argent, trous ouverts) lue sans désérialiser le monde. Écran « Charger » et « Sauvegarder » partagés entre menu titre et pause.
- **État de partie** (`GameState`, dans `World`) : objectifs accomplis (bitset), drapeau `Bankrupt`, drapeau `ContinuedAfterBankruptcy`, mois consécutifs sous le seuil, solde au 1er janvier, bilans clos. Tout entre dans `Checksum` et `Save` (v19 → v20), avec un cas `SaveTests` qui échoue si un champ manque et une fixture de `main` archivée.
- **Objectifs** (choix 4, en plus des seuils de C17) : `data/objectives/objectives.json`, chaque objectif = condition évaluée au changement de jour, récompense en argent et/ou un bâtiment offert (bon de pose, remboursé par la pose). Liste d'alpha, seuils en données : ouvrir 3, 6 puis 9 trous ; 10 membres ; un membre Gold ; finir l'année en positif ; amener son pro à une division supérieure. `ObjectiveEvaluator` lève `ObjectiveCompleted`, journalisé en français. Panneau « Objectifs » sous l'écusson (décidé à C13).
- **Faillite** (choix 3) : au `LedgerMonth` clos, si le solde est sous −`bankruptcyThreshold`, le compteur monte, sinon retombe à zéro ; à `bankruptcyMonths` (6) la sim lève `Bankrupt`. `Main` met en pause et ouvre l'écran de fin (jours, golfeurs accueillis, argent, objectifs) avec trois sorties : charger, menu titre, **« Continuer quand même »** (pose `ContinuedAfterBankruptcy`, plus aucun écran de fin, une ligne de journal rappelle l'état). **Mesure faite au lot 2** (test `Bankruptcy_IsReachable_WithTheCurrentOverdraftLimit_BecauseUpkeepAndInterestIgnoreIt`) : `overdraftLimit` ne borne que les achats (`TrySpend`) ; l'entretien mensuel et les intérêts passent par `Post`, sans plancher. « Le neuf » sans bâtiment ni arrivée (9 trous, 9 900 $ d'entretien par mois, aucune cotisation) : solde de −28 752 $ au mois 18, −92 035 $ au mois 24, **−102 954 $ au mois 25** (premier mois sous −100 000 $), puis `Bankrupt` au mois 30 (six clôtures de suite) ; au mois 60, −562 406 $. Avec les arrivées (graines 1 à 3, « Le neuf » vide de bâtiments, 12 mois), le club gagne de 4 000 à 27 000 $ par mois (solde de 336 000 à 375 000 $ au mois 12) : la faillite n'arrive donc qu'à un club que plus personne ne visite (aucun golfeur, entretien qui continue). La faillite est atteignable avec le découvert actuel, aucun chiffre n'a bougé. 
- **Bilan annuel** : à la clôture de décembre, un `YearReport` (revenus, dépenses par catégorie, résultat, golfeurs, membres, humeur moyenne, objectifs accomplis cette année) est conservé et affiché dans un panneau, avec « Année positive » qui coche l'objectif correspondant.
- **Options** (choix : toutes celles du plan sauf le volume) : `user://options.cfg` ; résolution et plein écran appliqués à `DisplayServer`, échelle d'interface branchée sur le facteur de C13 (panneaux repliés à 150 % déjà couverts), **pause automatique** sur messages importants (liste fermée en données : abandon, faillite, objectif accompli, trésorerie basse). Aucune option n'entre dans la sim.
- **Menu pause** : Échap ouvre Reprendre, Sauvegarder, Charger, Options, Menu titre, Quitter ; il remplace l'usage actuel d'Échap (`Main`, `case Key.Escape`).

## Choix tranchés par Guillaume (2026-11-01)
1. Nouvelle partie : **terrain plat provisoire** (contre la recommandation : « Le neuf », graine plus tard) ; C24 remplacera le générateur.
2. Trois emplacements + autosave, date et vignette.
3. Faillite : écran de fin avec « continuer quand même ».
4. Objectifs en plus des seuils de trous de C17.

## Critères d'acceptation
1. Sauvegarde : `GameState` entre dans `Checksum` et `Save` (v20), un cas `SaveTests` échoue s'il est oublié ; une v19 se relit encore (`OldestReadable` reste à 11, comme pour les montées précédentes) avec un état de partie vierge ; une fixture v19 de `main` est archivée (`Fixtures/v19-partie.sav`).
2. Terrain : `BlankProperty` produit la même carte pour la même graine, accessible depuis le pavillon, sur au moins 100 graines (test).
3. Difficulté : un fichier de difficulté incomplet ou négatif est refusé (test) ; Facile est chargé par une nouvelle partie.
4. Objectifs : chaque objectif se déclenche par un évènement précis sur la vraie date de `Calendar` (test par objectif), une seule fois, et sa récompense apparaît au grand livre ou comme bon de pose.
5. Faillite : trésorerie sous le seuil six clôtures de mois de suite lève `Bankrupt` ; un mois remonté au-dessus remet le compteur à zéro ; « continuer quand même » ne relève plus l'écran (tests sur le vrai `Ledger`). Mesure préalable consignée dans la spec : le solde peut-il atteindre −seuil avec le découvert actuel ?
6. Bilan : le bilan de décembre égale la somme des mois clos de l'année ; « Année positive » ne se coche que si le résultat est > 0.
7. Emplacements : écrire puis relire un emplacement donne le même `Checksum` ; les métadonnées se lisent sans charger le monde ; l'autosave n'écrase jamais un emplacement.
8. Déterminisme : même graine et mêmes commandes, même `Checksum` après 10 jours.
9. Banc P1 : le tick tient les seuils de C12 (moyenne sous 2 ms, 99e centile sous 5 ms), la dernière mesure de référence étant celle de #155 (pire tick 2,3 ms).
10. Fumée Godot (`SMOKE OK`) : menu titre → nouvelle partie → Échap → Sauvegarder → Menu titre → Charger → l'état est identique ; captures `c18-*` (menu, charger, pause, objectifs, fin, bilan, options) dans le job « Captures ».
11. Tous les textes sont en français, dans les données ou `UiNames`.

## Lots
1. **Menus et nouvelle partie** : menu titre, `BlankProperty`, difficulté en données, emplacements avec vignettes, menu pause, écrans Charger et Sauvegarder (`game/src/Ui`, `SaveSlots`). Critères 1 (v20 vide), 2, 3, 7, 10 (partie).
2. **Objectifs, faillite, bilan** (sim) : `GameState`, `ObjectiveEvaluator`, `Bankrupt`, `YearReport`, panneaux et écran de fin. Critères 1, 4, 5, 6, 8, 9, 10 (partie). Contrôle complet.
3. **Options et pause automatique** : `options.cfg`, écran Options, pause sur messages. Critère 10 (partie).

## Niveau de contrôle
complet (lot 2), léger (lots 1 et 3).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
