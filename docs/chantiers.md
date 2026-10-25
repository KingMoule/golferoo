# Chantiers

| Id | Titre | Statut | Contrôle | PR | Note |
|---|---|---|---|---|---|
| C00 | Choisir le moteur du jeu | livré | aucun | #4 | docs/adr/0001-moteur.md : Godot .NET + simulation C# pure, 3D low-poly, code fermé, PC/Mac/Linux |
| C01 | Prototype d'un trou en 3D low-poly | livré | complet (sim), léger (rendu) | #5 à #14 | specs/C01-prototype-trou.md ; bilan `docs/prototype-c01-rapport.md` : facteur 2, 632 FPS sur RTX 2080, terrain jugé « pas en cases » |
| C02 | Arbres et échelle du monde | lot 1 en PR | léger (lot 2 complet) | #19, #20 | `specs/C02-arbres-echelle.md` : arbres objets de la sim à leur vraie taille (feuillu 12 à 20 m, environ 100 à l'hectare), boisement généré, balle contre tronc et couronne selon la saison, croissance, garder, raser, planter, chute ; 4 lots |
| C03 | Calendrier, saisons et sol par cellule (F1, F2) | à cadrer | complet | | `docs/boucles-v1.md` : jours, mois, saisons, moment de la journée sans nuit ; propriétés du sol précalculées par cellule |
| C04 | Météo et saisons (D1) | à cadrer | complet | | `docs/boucles-v1.md` : vent sur la balle et dans la visée selon le prestige, pluie et ses côtés positifs (M1 à M9, tous gardés), chute d'arbres, saisons sur la végétation |
| C05 | Entretien du gazon (D2) et shader de terrain v2 (F3) | à cadrer | complet (sim), léger (rendu) | | `docs/boucles-v1.md` : usure, tonte, vitesse des greens, consignes au personnel, sans mauvaises herbes ni maladies ; shader au-delà de 8 surfaces |
| C06 | Handicap, moteur de score et ligues (B10, F5, D5) | à cadrer | complet | | `docs/boucles-v1.md` : divisions avec montées et descentes, votre pro part de la plus basse, seuls ses scores de ligue comptent pour son handicap |
| C07 | Tutoriel de construction d'un trou | à cadrer | léger | | `docs/boucles-v1.md` : peindre, poser le trou, l'ouvrir, regarder la première paire ; après la preuve P3 (UI) |
| R03 | Pistes de features et de boucles de jeu | livré | aucun | #16 | `docs/pistes-features.md` (tri de Guillaume : A, C et 4 pistes B) `docs/boucles-de-jeu.md` (analyse des 13 boucles D) et `docs/boucles-v1.md` (les 5 boucles recommandées en détail, ≈ 27 lots dont 10 de fondations) |
| R02 | Revue de faisabilité des features sous Godot | livré | aucun | #15 | `docs/faisabilite-features.md` : rien de bloqué par Godot ; risques : parcours plein (R1), personnages animés (R2), volume d'UI et de contenu ; preuves P1 à P3 proposées |
| R01 | Recenser les features du SimGolf original | livré | aucun | #3 | docs/recensement-sim-golf.md |

## Archive
