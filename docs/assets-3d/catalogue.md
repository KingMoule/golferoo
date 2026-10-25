# Catalogue des modèles 3D

Mis à jour : 2026-10-25. 69 modèles générés par une autre IA à partir de `docs/guide-ia-assets-3d.md`, livrés par Guillaume en deux lots (66 objets, puis trois club-houses v2). Chaque modèle a son script `tools/blender/<nom>_lowpoly.py` ; relancé sans argument depuis la racine du dépôt (`python -I tools/blender/<nom>_lowpoly.py`), il réécrit son `.glb` à l'identique.

**Placés dans le jeu : les arbres** (C02), pack à 800 triangles depuis le 2026-10-25 ; `trees/tree.glb` reste le modèle de repli. Le mobilier, l'entretien, les véhicules et les bâtiments attendent les chantiers qui les placeront.

Planches de comparaison (cadrage individuel, pas d'échelle commune) : [arbres](catalogue/01_trees.jpg), [décor](catalogue/02_decor.jpg), [mobilier](catalogue/03_furniture.jpg), [entretien](catalogue/04_maintenance.jpg), [voiturette et bâtiments](catalogue/05_later.jpg), [club-houses v2](catalogue/06_clubhouses_v2.jpg).

Vérifié le 2026-10-25 dans le conteneur cloud (`bpy` 5.2.3) : les 69 scripts tournent, sortent des `.glb` identiques à ceux livrés et d'un lancement à l'autre ; chaque `.glb` a un seul maillage, aucune texture ni animation, le pied à 0, au plus 4 matériaux.

| Famille (`game/assets/`) | Budget | Modèles (triangles ; largeur × profondeur × hauteur en m) |
|---|---|---|
| `trees` | 800 | chêne `oak_01` à `03` (776 à 792 ; 8,5 à 14 × 9,8 à 13 × 14 à 18) ; sapin `conifer_01` à `03` (776 à 796 ; 4,9 à 7,4 × 4,5 à 8,1 × 16 à 19,6) ; bouleau `birch_01` à `03` (744 à 780 ; 8,5 à 9,9 × 8,8 à 9,1 × 15 à 17) ; jeunes plants `oak_sapling`, `conifer_sapling`, `birch_sapling` `_01` à `_03` (218 à 756 ; 1,2 à 3,1 m de haut) ; tronc couché `fallen_log_01` à `03` (456 à 548 ; 6,3 à 7,9 m de long) ; souche `stump_01` à `03` (536 à 600 ; 0,8 à 2,2 m de haut) |
| `bushes` | 150 | `bush_01` à `03` (120 ; 1,9 à 2,7 m de large, 1 à 1,6 m de haut) |
| `rocks` | 120 | `rock_01` à `03` (40 à 80 ; 2,2 à 2,4 m) |
| `furniture` | 300 | marqueur de départ `tee_marker_01` à `03` (24 à 108) ; banc `bench_01` à `03` (80 à 204 ; 1,76 m) ; poubelle `bin_01` à `03` (56 à 228) ; lave-balles `ball_washer_01` à `03` (100 à 168 ; 1,14 m) ; drapeau `flag_01` à `03` (58 ; mât 2,23 m) |
| `maintenance` | 800 | tondeuse de green `green_mower_01` à `03` (228 à 568 ; deux piétonnes, un triplex) ; de fairway `fairway_mower_01` à `03` (568 à 740) ; de rough `rough_mower_01` à `03` (424 à 484) |
| `vehicles` | 800 | voiturette `golf_cart_01` à `03` (420 à 468 ; 1,2 × 2,3 × 1,8) |
| `buildings` | 1 500 | hangar d'entretien `maintenance_shed_01` à `03` (212 à 356 ; 7 à 10 × 6 à 9 × 4,5) ; snack-bar `snack_bar_01` à `03` (152 à 224 ; 5 à 6 × 7 × 3 à 4) ; club-house `clubhouse_01` à `03` (200 à 224 ; 11 à 15 × 11,5 × 4 à 5,4) ; club-house v2 `clubhouse_lodge_v2`, `clubhouse_pavilion_v2`, `clubhouse_modern_v2` (1 350 à 1 408 ; 20 à 23 × 14 à 18 × 5,5 à 8) |

Matériaux des arbres : le feuillage est dans `FoliageDark` et `FoliageLight`, le bois dans `Bark`, `BarkWhite`, `BarkDark` ou `CutWood`. Les saisons ne recolorent que les deux matériaux de feuillage.

Remarques pour l'intégration :
- Les couronnes de chêne sont ovales (12 à 14 m sur 8 m) : à tourner au hasard par instance.
- Les troncs couchés font 5 à 6 m, bien moins que les 15 à 17 m des arbres matures : bon comme décor, à rallonger pour la chute d'un arbre de C02.
- Les club-houses v1 (200 triangles, 11 à 15 m) sont de simples pavillons ; les v2 sont plus détaillés et plus grands.
