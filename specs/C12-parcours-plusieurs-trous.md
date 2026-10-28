# Un parcours de plusieurs trous, et la preuve du parcours chargé (A7, P1)
Statut : validé
Créé : 2026-10-28

## But
La feuille de route de la première version (`docs/boucles-v1.md`, « Ordre proposé ») est livrée avec C10 et C11. Mais le monde n'a toujours qu'un seul trou : `TerrainGrid` porte un départ (`Tee`), un trou (`Hole`) et un par, et un groupe qui finit le trou sort par `ExitPoint`. Le cœur du jeu (construire des trous, regarder des golfeurs jouer un parcours) et tout ce qui reste à venir (bâtiments, économie, humeur, départs multiples B1, tournois avec cut B18) supposent un parcours.

C'est aussi la preuve P1 de `docs/faisabilite-features.md`, jamais faite et notée comme le plus gros risque technique (R1) : plusieurs groupes qui *jouent* en même temps sur 9 à 18 trous, avec la planification des coups (environ 15 ms par drive) et les champs de flux vers chaque balle.

Ce qui prouve la réussite : dans le jeu, des paires partent les unes après les autres, jouent les 9 trous dans l'ordre, s'attendent sans se frapper dessus et rendent une carte de 9 trous ; au banc, 18 trous et 36 groupes tiennent les seuils de tick actuels (moyenne sous 2 ms, 99e centile sous 5 ms).

## Périmètre et hors périmètre
**Dans le périmètre**
- Un parcours de N trous (1 à 18) dans la sim : départ, green, drapeau et par par trou, ordre de jeu, vérification par trou (`HoleCheck`).
- Un fichier de parcours assemblé à partir des trous existants, posés sur une grande carte.
- Le jeu d'une partie complète : enchaîner les trous, marcher du green au départ suivant, attendre le groupe devant, carte de N trous ; départs espacés depuis le premier trou.
- La ligue, le pro, la note du parcours et la sauvegarde sur N trous.
- Le parcours « Le neuf » (9 trous, par 36) chargé par défaut ; drapeaux, repères de départ et numéros pour chaque trou ; aller de trou en trou à la caméra.
- Le banc du parcours chargé (P1) sur 18 trous, et les corrections qu'il demande.
- Ajouter et retirer un trou en jeu.

**Hors périmètre, et où ça ira**
- Pavillon, bâtiments, économie, arrivée des golfeurs par la réputation, humeur : chantiers suivants, qui s'appuient sur celui-ci.
- Plusieurs départs par trou (B1) et changement quotidien du drapeau sur un parcours : chantier B1.
- Renuméroter les trous, parcours de 27 ou 36 trous (A7) : avec la fondation d'UI (F7).
- Génération procédurale d'une propriété : chantier à part.
- Parties de moins de N trous (9 trous sur un 18) : plus tard, avec l'arrivée des golfeurs.
- Migration des sauvegardes de C11 : aucune version publique, le format passe en v2 sans lecteur v1 (choix 4).

## Conception
**Le modèle** (`src/Golferoo.Sim/Course/`, nouveau)
- `CourseHole` : numéro, départ, centre du green, drapeau, par. `Course` : liste ordonnée de `CourseHole`, lecture et écriture (`Write`/`Read`), entre dans `Checksum`.
- `TerrainGrid` garde le relief et les surfaces ; ses `Tee`, `Green`, `Hole`, `Par`, `MoveTee` et `MovePin` passent au trou concerné du `Course`. `PlaceTee`/`PlaceHole` prennent un numéro de trou.
- `HoleCheck` vérifie un trou ; `World` garde un état ouvert par trou. Un groupe ne part que si le trou 1 est ouvert, et saute un trou fermé (la carte le note non joué, sans pénalité).
- Le point de sortie devient celui du dernier trou ; entre deux trous, le groupe marche vers le départ suivant (champ de flux mis en cache, comme aujourd'hui).

**Le fichier de parcours** (`data/courses/`, nouveau format, `CourseLoader`)
- Taille de la carte en tuiles, surface de fond (rough), puis une liste de trous : fichier de trou existant (`data/holes/*.json`), décalage en tuiles, rotation d'un quart de tour, numéro. Le chargeur pose chaque trou sur la carte avec ses surfaces, son relief et ses arbres isolés. Deux trous qui se chevauchent sur autre chose que du rough ou des bois : `HoleDataException`.
- Un trou seul (`data/holes/*.json`) se charge comme un parcours d'un trou : le tutoriel, `FpsBench` et les tests existants ne changent pas.

**Le jeu d'une partie** (`World.cs`, `Golfers/`)
- `Group` porte le trou en cours ; `Golfer` garde sa carte de N trous. Après le dernier putt du groupe : `Exiting` vers le départ suivant (nouvel état `ToNextTee`, ajouté en fin d'enum pour ne pas changer la somme des valeurs existantes), puis `Arriving` sur ce départ.
- **Départs** : un groupe en attente part quand le groupe précédent a quitté la zone de départ du trou 1 et que ses deux balles sont à plus de 150 m du départ (ou sur le green). `SpawnGroup` met en file au lieu de poser au départ.
- **Attendre le groupe devant** : avant de jouer, un golfeur attend si un golfeur d'un autre groupe sur le même trou est à moins de 30 m du point visé, ou si le green visé est occupé. Règle testée par un test sans contact (aucune balle ne s'arrête à moins de 10 m d'un golfeur d'un autre groupe sur 500 parties).
- Ligue, pro, entraînement et `CourseRater` jouent les N trous ; `CourseRating.Holes` vaut N.

**Rendu et caméra** (`game/src/`)
- Drapeau, repère de départ et numéro (`Label3D`) pour chaque trou ; `ActorLayer` et `ProAim` lisent le trou du groupe suivi.
- Actions `cam_next_hole` et `cam_prev_hole` (Page suivante, Page précédente) dans l'`InputMap` : la caméra va au départ du trou suivant ou précédent ; `Début` va au trou 1.
- Petit cartouche « Trou n, par p » quand un groupe est suivi.

**Construire un trou en jeu** (`game/src/Tools/PlaceTool.cs`, `World.Hole.cs`)
- « Nouveau trou » : poser un départ sur une cellule de départ, puis un drapeau sur un green ; le trou prend le numéro suivant, avec un par calculé d'après la longueur (seuils de l'original recalés : moins de 230 m par 3, moins de 430 m par 4, sinon par 5). « Retirer le dernier trou ». Commandes en file, comme les autres retouches.

**Lots (5 PR)**
1. **Le modèle** (complet) : `Course`, `CourseHole`, `CourseLoader`, `TerrainGrid` sans repères, `HoleCheck` par trou, sauvegarde v2. Comportement inchangé sur un trou. Tests : critères 1 à 3.
2. **Jouer un parcours** (complet) : enchaînement des trous, départs espacés, attente du groupe devant, carte de N trous, ligue, pro et note sur N trous. Tests : critères 4 à 6.
3. **« Le neuf » dans le jeu** (léger) : trois nouveaux trous (par 3 court, par 3 au-dessus de l'eau, par 5), fichier `data/courses/le-neuf.json` (9 trous, par 36, environ 1 200 × 800 m), chargé par défaut ; rendu de chaque trou, caméra de trou en trou, cartouche. Captures `c12-*`.
4. **Le parcours chargé, P1** (complet) : banc sur 18 trous (« Le neuf » posé deux fois), 36 groupes et 5 000 arbres ; si un seuil casse, la correction se fait dans ce lot (planification étalée sur plusieurs ticks avec un budget par tick, invalidation des champs de flux limitée aux blocs touchés). `FpsBench` sur « Le neuf » chargé, mesuré par Guillaume.
5. **Nouveau trou en jeu** (léger) : outils « Nouveau trou » et « Retirer le dernier trou », par calculé, fumée.

Ordre : 1 → 2, puis 3 et 4 en parallèle (fichiers disjoints : `game/` et `data/courses/` contre `tests/Golferoo.Sim.Bench` et le cœur du tick), puis 5.

## Choix tranchés
Guillaume a délégué le choix du chantier et le cadrage le 2026-10-27 et le 2026-10-28 (« prochain chantier ? », carte blanche) : les choix ci-dessous sont pris par la session, avec leur raison.

1. **Le chantier : un parcours de plusieurs trous**, plutôt que l'info-bulle des golfeurs et l'humeur, ou les finitions reportées (dispersion, bordures). Tout ce qui reste à construire suppose un parcours, et P1 est le seul risque qui pourrait remettre en cause une décision d'architecture (les champs de flux, `docs/faisabilite-features.md`). Le faire tard coûterait plus cher.
2. **Parcours assemblé à partir de trous posés sur une carte**, plutôt qu'un seul grand fichier dessiné en caractères (150 × 100 tuiles, illisible à retoucher) ou une génération procédurale (un chantier à elle seule, au rendu à juger). Les trois trous existants servent tels quels, et chaque nouveau trou reste un petit fichier relu en une page.
3. **9 trous dans le jeu, 18 au banc.** Un neuf trous se joue en une séance de test et montre l'enchaînement ; le banc pose « Le neuf » deux fois pour mesurer le cas d'un 18 trous plein.
4. **Sauvegarde v2 sans lecteur v1.** Aucune version publique n'existe ; les sauvegardes d'essai de C11 deviennent illisibles, avec un message clair (« sauvegarde d'une ancienne version »). Écrire une migration pour des fichiers de test serait du code sans usager.
5. **Règles de départ et d'attente simples et chiffrées** (150 m, 30 m, green occupé), plutôt qu'un modèle de rythme de jeu. Elles suffisent à éviter les coups dans le groupe devant ; le rythme de jeu et le temps de partie viendront avec l'évaluation.
6. **Seuils du banc inchangés** (tick moyen 2 ms, 99e centile 5 ms, retouche 16 ms). Ce sont ceux qui tiennent le jeu fluide à ×16 ; les assouplir reviendrait à cacher le risque que P1 doit mesurer.
7. **Pas de renumérotation** : ajouter et retirer le dernier trou suffisent tant qu'il n'y a pas d'écran de parcours (F7).

## Critères d'acceptation
1. Lot 1 : chaque trou existant (`proto-par4`, `dogleg-par4`, `blank-coude`), chargé comme parcours d'un trou, donne la même `Checksum` après 2 000 ticks qu'avant le chantier, pour trois graines (`dotnet test --filter CourseTests`).
2. Lot 1 : un parcours de trois trous se charge, chaque trou a ses repères et son par ; un fichier où deux trous se chevauchent, ou où un trou sort de la carte, lève `HoleDataException` avec le numéro du trou.
3. Lot 1 : `SaveTests` passe sur un parcours de trois trous ; un fichier v1 lève `InvalidDataException` au message « ancienne version ».
4. Lot 2 : sur un parcours de trois trous, quatre groupes lancés d'affilée jouent les trois trous dans l'ordre et rendent chacun des cartes de trois trous complètes ; même graine, même somme de contrôle.
5. Lot 2 : sur 500 parties de deux groupes rapprochés, aucune balle ne s'arrête à moins de 10 m d'un golfeur d'un autre groupe, et aucun groupe n'attend plus de 2 minutes de jeu sur un parcours libre devant lui.
6. Lot 2 : une journée de ligue et une partie d'entraînement du pro se jouent sur tous les trous ; `CourseRating.Holes` vaut le nombre de trous.
7. Lot 3 : `godot --headless` charge « Le neuf » ; la fumée fait jouer une paire sur les 9 trous à ×16 et écrit `COURSE OK 9` puis `SMOKE OK`. Captures `c12-parcours` (vue d'ensemble), `c12-trou-3` et `c12-groupes` (plusieurs groupes en jeu) à juger par Guillaume.
8. Lot 4 : le banc sur 18 trous, 36 groupes et 5 000 arbres tient tick moyen < 2 ms, 99e centile < 5 ms, retouche de surface < 16 ms ; les chiffres sont écrits dans le résumé de la CI. `FpsBench` sur « Le neuf » chargé ; Guillaume mesure sur sa RTX 2080 en 1080p.
9. Lot 5 : la fumée crée un quatrième trou sur un parcours de trois, une paire le joue, puis il est retiré ; le par calculé suit les seuils.
10. Tous lots : `dotnet test Golferoo.sln`, `dotnet format Golferoo.sln --verify-no-changes` et les quatre jobs de CI verts ; tout nouvel état de la sim entre dans `Checksum` et dans `Save`, avec un cas dans `SaveTests`.

## Niveau de contrôle
complet (lots 1, 2 et 4), léger (lots 3 et 5).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
