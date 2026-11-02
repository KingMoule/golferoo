# Génération de la propriété
Statut : cadré (en attente du « go » de Guillaume)
Créé : 2026-11-02

## But
Une nouvelle partie démarre aujourd'hui sur un terrain plat, vide et identique à chaque graine (`BlankProperty`, provisoire depuis C18). C24 (manque M13 de `docs/plan-alpha.md`, décision 3) le remplace par une **propriété générée à partir de la graine** : relief doux, eau, boisements d'âges variés, une clairière pour le pavillon et un premier trou tracé. Tout vit dans la sim (`Golferoo.Sim`, sans Godot) ; le rendu lit déjà un `TerrainGrid` quelconque.

## Périmètre et hors périmètre
Dans le périmètre : générateur déterministe `PropertyGenerator.Create(seed)` ; relief, eau, clairière, premier trou ; boisement et arbres remarquables ; aperçu de la graine dans l'écran de nouvelle partie ; captures `c24-*`.

Hors périmètre : autres thèmes que Parkland ; propriété plus grande que 1 200 × 800 m ; bunkers générés ; chemins générés au-delà de la sortie du pavillon ; choix de taille ou de difficulté du terrain ; régénération en cours de partie. « Le neuf » reste le parcours de démonstration, inchangé.

## Choix tranchés par Guillaume
À confirmer par ses cartes du 2026-11-02 (les recommandations sont reprises ici en attendant) :
1. **Départ** : le pavillon est posé dans une clairière près d'un bord et **un premier trou de 180 m est tracé** (départ, fairway, green) sur un couloir plat voisin, comme aujourd'hui : le tutoriel (C07, C19) marche tel quel.
2. **Paysage** : environ **35 % de boisement** (massifs aux bords et dans les creux, centre plus dégagé), **un à deux étangs et un ruisseau**.

## Conception
- **Fichier** : `src/Golferoo.Sim/Terrain/PropertyGenerator.cs`. Même grille que `BlankProperty` (300 × 200 cellules de 4 m). `BlankProperty` est supprimé quand le lot 3 branche `Main` sur le générateur ; ses tests deviennent ceux du générateur.
- **Aléa** : `DeterministicRng` dérivé de la graine par un sel propre au terrain, sans toucher au flux de la météo, des arrivées ou de la ligue (qui continuent de lire la graine telle quelle). Même graine, même carte, au bit près.
- **Relief** : bruit de valeur à 3 octaves sur les coins de `HeightField`, amplitude de l'ordre de 6 m, une colline douce et un creux ; pente moyenne bornée. Les zones de jeu (clairière, couloir du premier trou) sont aplanies après coup.
- **Eau** : les étangs occupent les points bas du relief (cellules `Surface.Water`, bords en pente douce) ; le ruisseau suit la ligne de plus forte descente entre deux points bas, sur 2 à 3 cellules de large, avec au moins deux gués dans la zone jouable. Aucun plan d'eau à moins de 40 m du pavillon ni du premier trou.
- **Clairière et pavillon** : un disque d'environ 30 m de rayon, au relief plat (`ClubhouseMaxRelief`), à moins de 120 m d'un bord, hors de toute flaque permanente (`Puddles`) ; le pavillon y est posé par le même chemin que `ClubhouseOnNewGame` aujourd'hui. Le générateur renvoie la clairière avec le terrain.
- **Premier trou** : couloir de 180 m près du pavillon, choisi parmi les candidats par score (pente la plus faible, éloignement de l'eau) ; peint en Tee, Fairway, Green comme `BlankProperty.Create` et validé par `HoleCheck`.
- **Boisement** (lot 2) : cellules `Surface.Trees` peintes par un bruit de seuil (≈ 35 % de la surface hors clairière, couloir et eau), puis `WoodlandGenerator.Fill` avec le RNG du terrain ; âges variés par le générateur existant ; 10 à 20 **arbres remarquables** (très vieux, hors massifs) près du centre. Garde : **au moins 3 couloirs de 400 m** sans eau, sans arbre et sous une pente bornée (valeurs à caler), sinon la graine est retentée avec le sel suivant (8 essais au plus, déterministe).
- **Budget** : au plus 5 000 arbres à la génération ; le banc P1 de la sim garde son plafond.
- **Sauvegarde** : le terrain et les arbres sont déjà sauvegardés en entier ; **aucun nouvel état de sim**, donc ni `Checksum` ni version de sauvegarde à toucher. Un test d'aller-retour sur une propriété générée le prouve.
- **Aperçu** (lot 3) : l'écran de nouvelle partie (`TitleMenu`) affiche une miniature vue du dessus de la graine saisie (hauteur, eau et bois), mise à jour quand la graine change ; un bouton « Autre graine » tire au hasard.

## Critères d'acceptation
1. Même graine, même terrain, mêmes arbres (somme de contrôle identique) ; deux graines différentes donnent des terrains différents (test).
2. Sur **100 graines** : pavillon posable dans la clairière ; chemin du pavillon au premier départ possible (champ de flux atteint la porte) ; premier trou valide selon `HoleCheck` ; au moins 3 couloirs de 400 m ; aucune flaque permanente à moins de 40 m du pavillon (tests).
3. Boisement entre 30 et 40 % hors eau ; au plus 5 000 arbres ; arbres de plusieurs âges et trois essences (test).
4. Au moins un étang et un ruisseau sur chaque graine ; aucun à moins de 40 m du pavillon ni du premier trou (test).
5. Génération sous 1 s par graine en Release (banc).
6. Une partie neuve sur chaque graine tient 30 jours de jeu avec les arrivées sans exception ; le `Checksum` se recharge à l'identique (test).
7. Le menu titre montre l'aperçu de la graine et « Autre graine » ; la fumée Godot (`SMOKE OK`) lance une partie neuve générée ; captures `c24-*` sur trois graines.
8. `dotnet test Golferoo.sln` verte : les sommes épinglées de « Le neuf » inchangées ; `BlankProperty` supprimé sans test orphelin.
9. Tous les textes en français.

## Lots
1. **Relief, eau, clairière et premier trou** (sim) : `PropertyGenerator` sans arbres, tests sur 100 graines (critères 1, 2 sans les couloirs, 4, 5). Contrôle **complet**.
2. **Boisement et arbres remarquables** : massifs, âges, couloirs de 400 m, reprise de graine, budget d'arbres, banc (critères 2 complet, 3, 6). Contrôle léger.
3. **Branchement et aperçu** : `Main` appelle le générateur, `BlankProperty` supprimé, aperçu dans `TitleMenu`, fumée et captures `c24-*` sur trois graines (critères 7, 8). Contrôle léger.

## Règle de livraison (briefs du codeur)
Avant de dire « terminé », lancer la suite complète (`dotnet test Golferoo.sln`) et, pour le lot 3, la fumée Godot (`SMOKE OK`), et montrer leur sortie. Le calage des seuils (pente, bruit, nombre de couloirs) se fait par balayage de paramètres sur les 100 graines, pas par essais successifs.

## Niveau de contrôle
complet (lot 1), léger (lots 2 et 3).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
