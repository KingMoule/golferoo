# Génération de la propriété
Statut : lots 1 à 3 livrés (#175, #176, #177) ; lot 4 à venir
Créé : 2026-11-02 · révisé le même jour après ses réponses

## But
Une nouvelle partie démarre aujourd'hui sur un terrain plat, vide et identique à chaque graine (`BlankProperty`, provisoire depuis C18). C24 (manque M13 de `docs/plan-alpha.md`, décision 3) le remplace par une **propriété vierge entièrement aléatoire**, tirée de la graine : relief doux, étangs et ruisseau, bois d'âges variés, le pavillon au milieu. Aucun trou n'est tracé : le joueur construit tout. La simulation vit dans `Golferoo.Sim` (sans Godot) ; le rendu lit déjà un `TerrainGrid` quelconque.

## Choix tranchés par Guillaume (2026-11-02)
1. **Propriété vierge, entièrement aléatoire, pavillon au centre.** Pas de premier trou tracé.
2. **Tutoriel désactivé pour l'alpha, sans être effacé** : leçon de C07 et suite de C19 restent dans le code et les données, derrière un drapeau (`Main.TutorialEnabled`, faux par défaut) ; ni la proposition au menu ni la leçon ne se lancent, mais `-- --tutorial` et les tests continuent de les atteindre. Les conseils du pro de C19 restent actifs. Son avenir se décidera plus tard.
3. **Arbres** : les trois arbres matures de `mature_species_8000` (chêne pédonculé 7 500 triangles, bouleau verruqueux 7 860, épicéa commun 7 998 ; textures d'atlas, un seul maillage chacun) deviennent les modèles principaux des essences existantes (Oak, Birch, Fir). Voir lot 4 et l'écart de budget plus bas.
4. Le paysage (≈ 25 % de bois, un à deux étangs et un ruisseau) suit la recommandation du cadrage, à confirmer en jouant.

## Périmètre et hors périmètre
Dans le périmètre : générateur `PropertyGenerator.Create(seed)` et ses bornes en données ; pavillon au centre ; boisement ; arbres remarquables ; désactivation du tutoriel ; aperçu de la graine ; intégration des trois arbres matures ; captures `c24-*`.

Hors périmètre : autres thèmes que Parkland ; tailles de propriété variables ; premier trou, bunkers et chemins générés ; régénération en cours de partie ; arbres matures nus ou d'automne (le pack n'en fournit pas : les saisons de C04 gardent les modèles actuels, voir lot 4). « Le neuf » reste le parcours de démonstration, inchangé.

## Taille et limites de la propriété
Ce que « vierge, aléatoire, pavillon au centre » impose :
- **Taille fixe de 1 200 × 800 m** (300 × 200 cellules de 4 m, 96 ha), la même que « Le neuf » et que `BlankProperty`. Un 9 trous occupe 40 à 55 ha avec ses marges ; il reste donc la moitié de la propriété pour l'eau, les bois et de futurs trous. Tout est déjà calé sur cette grille (champs de flux, horizon, caméra, bancs), et la faire varier d'une graine à l'autre n'apporte rien à l'alpha : seul le contenu varie.
- **Pavillon au centre (600 ; 400)** : les golfeurs partent et reviennent de là, donc les marches restent courtes. Les trous peuvent partir dans toutes les directions, diagonales comprises (Guillaume) ; le générateur garantit des couloirs en plus d'une direction (voir limites).
- **Limites en données** (`game/data/property/parkland.json`, mêmes règles que les autres fichiers : clé inconnue ou champ manquant refusent le fichier), valeurs de départ à caler par balayage :

| Paramètre | Borne |
|---|---|
| Relief (amplitude, 3 octaves) | 4 à 10 m ; pente moyenne ≤ 6 % ; 1 à 2 collines, 1 creux |
| Clairière du pavillon | rayon 30 m, plate (`ClubhouseMaxRelief`), sans eau ni flaque permanente |
| Zone dégagée autour du pavillon | rayon 150 m, au plus 10 % d'arbres (le joueur y construit ses premiers trous) |
| Étangs / ruisseau | 1 à 2 / 1 ; à plus de 40 m du pavillon ; au moins 2 gués de ruisseau dans la zone jouable |
| Boisement | 20 à 30 % de la surface hors eau et hors zone dégagée (Guillaume : 35 % faisait trop) |
| Arbres à la génération | 3 500 au plus (≈ 2 400 attendus à 25 %) ; le banc P1 garde son plafond de 5 000 |
| Arbres remarquables | 10 à 20, très vieux, hors des massifs |
| Surface constructible (sans eau, pente ≤ 8 %, arbres compris) | au moins 55 ha, d'un seul tenant autour du pavillon |
| Couloirs de 400 m | au moins 3, partant à moins de 200 m du pavillon, sans eau, sous 8 % de pente, boisement compris (déboisable) ; au moins 2 directions distantes de 60° |

Un tirage qui rate une garantie est retenté avec le sel suivant (8 essais au plus, déterministe) ; si les 8 échouent, le test d'acceptation échoue : on corrige les bornes, pas le test.
- **Coût du défrichage** : un boisement de 25 % sur 96 ha, ≈ 2 400 arbres à 100 à l'hectare, se rase à `TreeRaze` pièce (×multiplicateur pour un remarquable). À vérifier au lot 2 : l'argent de départ de « Facile » doit permettre de dégager un premier 3 trous ; sinon la zone dégagée s'élargit (réglage en données).

## Conception
- **Fichier** : `src/Golferoo.Sim/Terrain/PropertyGenerator.cs` ; `BlankProperty` est supprimé au lot 3, ses tests deviennent ceux du générateur.
- **Aléa** : `DeterministicRng` dérivé de la graine par un sel propre au terrain ; la météo, les arrivées et la ligue continuent de lire la graine telle quelle. Même graine, même carte, au bit près.
- **Relief** : bruit de valeur sur les coins de `HeightField`, clairière aplanie après coup.
- **Eau** : les étangs occupent les points bas (cellules `Surface.Water`, bords en pente douce) ; le ruisseau suit la plus forte descente entre deux points bas, sur 2 à 3 cellules.
- **Pavillon** : posé au centre par le chemin existant (`PlaceClubhouseNearTee` devient `PlaceClubhouse` avec un point, le centre de la clairière, quand le terrain n'a pas de départ).
- **Boisement** : cellules `Surface.Trees` par bruit de seuil, puis `WoodlandGenerator.Fill` (âges et essences variés), plus les arbres remarquables.
- **Sauvegarde** : terrain et arbres sont déjà sauvegardés en entier ; **aucun nouvel état de sim**, donc ni `Checksum` ni version de sauvegarde. Un test d'aller-retour sur une propriété générée le prouve.
- **Tutoriel** : un drapeau dans `Main`, le reste intact ; une fumée vérifie qu'une nouvelle partie n'ouvre ni offre ni leçon.
- **Aperçu** (lot 3) : miniature vue du dessus de la graine dans l'écran de nouvelle partie (hauteur, eau, bois, pavillon), mise à jour quand la graine change ; bouton « Autre graine ».
- **Arbres matures** (lot 4) : voir ci-dessous.

## Les arbres matures : un écart de budget à trancher
Les trois modèles font environ **10 fois les modèles actuels** (≈ 800 triangles) : 7 500 à 8 000 triangles, un GLB de 3,6 Mo avec 3 textures embarquées (écorce, normales, atlas de feuillage découpé par masque alpha), feuillage été seulement. Le jeu les charge aujourd'hui comme des maillages sans texture, en `MultiMesh` par essence et par stade. À 2 400 arbres, 8 000 triangles chacun feraient 19 millions de triangles : intenable, et la fumée plafonne les arbres à 800.
Proposition (lot 4, après la génération) :
- le chargeur lit les matériaux et textures de ces GLB (matériaux `Foliage*` recolorables pour les saisons, comme le dit le README du pack) ;
- **niveau de détail**, deux voies essayées dans l'ordre : (a) le LOD automatique de Godot à l'import (le même modèle, simplifié par la distance, sans second fichier) ; (b) si (a) abîme le feuillage (les cartes de feuilles se simplifient mal) ou ne s'applique pas aux `MultiMesh`, le modèle mature à moins de ~80 m et le modèle de 800 triangles au-delà ; un billboard lointain reste possible ;
- stade adulte seulement : jeunes plants, souches et troncs gardent leurs modèles ; les saisons d'automne, d'hiver et nues gardent les modèles actuels de chaque essence (le pack n'en fournit pas) : changement de modèle visible à l'automne, à juger en jeu ; demander les versions saisonnières aux autres IA si Guillaume les veut toute l'année après avoir jugé le look ;
- budget et FPS mesurés avec `Mesurer FPS.bat` ; si le coût est trop haut, le lot 4 se repousse sans bloquer C24 (lots 1 à 3 autonomes).
Le dépôt gagne ≈ 11 Mo de GLB : accepté sauf avis contraire. Les scripts `tools/blender/*_mature_atlas_8000_lowpoly.py` pèsent 2,6 à 2,9 Mo chacun (atlas en base64) : à ne pas versionner tels quels ; on garde les GLB, `LICENSES.md` ou `docs/assets-3d/catalogue.md` notent leur provenance.

## Critères d'acceptation
1. Même graine, même terrain, mêmes arbres (somme de contrôle identique) ; deux graines différentes donnent des terrains différents (test).
2. Sur **100 graines** : toutes les bornes du tableau tenues (surface constructible, couloirs de 400 m, zone dégagée, eau à 40 m du pavillon, boisement 20 à 30 %, ≤ 3 500 arbres) ; pavillon posé au centre et son champ de flux atteint la porte (tests).
3. Au moins un étang et un ruisseau par graine ; arbres de plusieurs âges et de trois essences (test).
4. Génération sous 1 s par graine en Release (banc).
5. Une partie neuve sur chaque graine tient 30 jours avec ses arrivées sans exception ; le `Checksum` se recharge à l'identique (test).
6. Nouvelle partie : pas d'offre de tutoriel ni de leçon ; `-- --tutorial` fonctionne encore (fumée).
7. Le menu titre montre l'aperçu de la graine et « Autre graine » ; la fumée lance une partie neuve générée (`SMOKE OK`) ; captures `c24-*` sur trois graines.
8. Lot 4 : les trois arbres matures s'affichent près de la caméra avec leurs textures ; arbres en dessous de la limite de budget fixée au banc ; FPS mesuré et reporté ; captures `c24-arbres-*`.
9. `dotnet test Golferoo.sln` verte, sommes épinglées de « Le neuf » inchangées ; `BlankProperty` supprimé sans test orphelin ; textes en français.

## Lots
1. **Relief, eau, clairière et pavillon central** (sim) : `PropertyGenerator` sans arbres, bornes en données, tests sur 100 graines (critères 1, 2 hors boisement, 3 hors arbres, 4). Contrôle **complet**.
2. **Boisement et arbres remarquables** : massifs, âges, zone dégagée, couloirs de 400 m, reprise de graine, budget d'arbres, coût du défrichage vs argent de départ (critères 2 complet, 3, 5). Contrôle léger.
3. **Branchement, tutoriel et aperçu** : `Main` appelle le générateur, `BlankProperty` supprimé, tutoriel derrière le drapeau, aperçu dans `TitleMenu`, fumée et captures `c24-*` (critères 6, 7, 9). Contrôle léger.
4. **Arbres matures** : import des trois GLB texturés, LOD mature/800, banc et FPS (critère 8). Contrôle léger. Peut passer après la séance de jeu de Guillaume.

## Règle de livraison (briefs du codeur)
Avant de dire « terminé », lancer la suite complète (`dotnet test Golferoo.sln`) et, pour les lots 3 et 4, la fumée Godot (`SMOKE OK`), et montrer leur sortie. Le calage des bornes se fait par balayage de paramètres sur les 100 graines, pas par essais successifs.

## Niveau de contrôle
complet (lot 1), léger (lots 2 à 4).

## Écarts du lot 3
- Le terrain généré reste `Blank` (« terre vierge », trou 1 fermé tant que `HoleCheck` y voit un manque), mais le club est actif dès le départ : `World` reçoit `plantedTrees` (les arbres générés, remarquables compris, sans replantation des cellules `Trees`), ce qui active le club, autorise la ligue et pose le pavillon au centre (`PlaceClubhouseNear`). Sans trou ouvert, personne n'arrive ; la note du parcours ne se calcule qu'à l'ouverture du premier trou (`Main`).
- `TerrainGrid.IsLesson` (terre vierge portant les arbres du passage) distingue la leçon du tutoriel de la propriété générée là où `Blank` ne suffisait plus : pose du club au chargement (donc `Checksum` identique après rechargement), snack-bar offert, panneau du tutoriel. Aucun état nouveau, aucune version de sauvegarde.
- `OfferTutorial` est remplacé par `Main.TutorialEnabled` (faux par défaut).
- L'aperçu génère la propriété hors du fil principal et la remet à `Main` (`Main.NewGameProperty`) au lieu de la régénérer ; une graine sans propriété valide en tire une autre avec un message.

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
