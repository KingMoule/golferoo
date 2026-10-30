# Pavillon et arrivée des golfeurs
Statut : validé (go de Guillaume le 2026-10-30)
Créé : 2026-10-30

## But
Le parcours vit seul. Les golfeurs arrivent au pavillon selon l'attrait du club. Ils attendent dans une file, partent dès que le premier départ est libre et ressortent par le pavillon. La touche G ne sert plus qu'en mode débogage. C'est le manque M3 de `docs/plan-alpha.md`, et le premier maillon du chemin critique (C15 → C16 → C17).

## Périmètre et hors périmètre
Dans le périmètre :
- le pavillon comme objet de la sim : emprise, porte, position ;
- le déplacement du pavillon ;
- les arrivées, la file d'attente, les départs et les sorties ;
- un vivier persistant de visiteurs ;
- une réputation simple du club ;
- le banc P1 avec arrivées continues ;
- le rendu du pavillon et de la file.

Hors périmètre :
- l'humeur, les pensées et l'adhésion (C16) ;
- les autres bâtiments et leur outil de pose générique (C17) ;
- l'effet du prix du green fee sur les arrivées (C18) ;
- la génération de la propriété (C24 : en attendant, le pavillon se place sur la propriété actuelle) ;
- les animations d'attente (C23 lot 2).

## Conception
- **Pavillon** (`Buildings/Clubhouse.cs`, sim pure) : emprise rectangulaire en cellules, porte (cellule et orientation), position.
  - C'est le premier bâtiment, sur un modèle générique `Building` que C17 étendra.
  - Il entre dans `World.Checksum` et `World.Save` (v11 → v12), avec un cas `SaveTests`.
  - À la nouvelle partie, il est placé automatiquement sur un terrain plat et libre, au plus près du départ du trou 1.
  - Le joueur peut le déplacer : coût en données dans `data/economy/`, écriture au grand livre.
  - L'emprise bloque le passage et les arbres. Le champ de flux vers la porte est mis en cache, et seule l'invalidation par champ touché s'applique (règle de C12 lot 4).
- **Attrait et arrivées** (`Golfers/Arrivals.cs`) : chaque heure de jeu, on tire un nombre de groupes qui arrivent.
  - Le taux suit un profil horaire : les arrivées vont de 6 h à 18 h, avec une pointe le matin. Aucun départ après 16 h, pour finir avant la nuit.
  - Le taux est multiplié par l'attrait, qui combine :
    - le nombre de trous ouverts : aucun trou ouvert, aucune arrivée ;
    - la réputation ;
    - la météo du jour.
  - La saison n'a pas d'effet en elle-même : seule la météo du jour compte (décision de Guillaume).
  - La note du parcours est neutre dans l'attrait jusqu'à C16.
  - Un plafond limite les groupes de visiteurs sur le parcours (`maxGroupsPerHole`, 2 par trou) : au-delà, le premier de la file attend. Il tient le banc P1 sous les 36 groupes mesurés à C12.
  - Les tirages passent par le RNG du monde (déterministe).
  - Tous les paramètres sont dans `data/golfers/arrivals.json`.
- **Groupes** : de 1 à 4 golfeurs, surtout des paires et des quatuors. La distribution est en données.
- **File** : 8 groupes au plus, devant la porte. Un groupe qui arrive quand la file est pleine repart aussitôt (évènement `GroupTurnedAway`) et compte comme une mauvaise sortie dans la réputation. Le premier groupe de la file part dès que le premier départ est libre (règles de départ de C12).
- **Sortie** : après le dernier trou, le groupe marche jusqu'à la porte, puis disparaît. Le green fee reste payé comme en C14.
- **Vivier** (`Golfers/VisitorPool.cs`) : des visiteurs nommés et persistants, chacun avec un profil (niveau, hasard propre), une date de dernière visite et une envie de revenir.
  - Cette envie monte avec une bonne sortie et baisse avec un refus à la file. En C15, une sortie est « bonne » si la partie est finie et « mauvaise » si le groupe a été refusé ; C16 la remplacera par l'humeur.
  - Un groupe se compose d'abord de visiteurs du vivier qui ont envie de revenir, puis de nouveaux visiteurs, qui entrent dans le vivier.
  - Le vivier entre dans `Checksum` et `Save`.
  - Les 40 membres de la ligue restent tels quels : leur migration vers le vivier se fait en C16.
- **Réputation** : moyenne glissante des sorties (bonne = 1, mauvaise = 0), dans `Checksum` et `Save`.
- **Jeu** :
  - le modèle `game/assets/buildings/clubhouse_pavilion_v2.glb` est posé à la position de la sim ;
  - la file est visible : les golfeurs attendent devant la porte ;
  - la commande « déplacer le pavillon » est dans le dock (C13) ;
  - un clic sur un groupe de la file le choisit pour le pro (C06) ;
  - la touche G est réservée au mode débogage.

## Choix tranchés par Guillaume (2026-10-30)
1. Pavillon placé automatiquement, que le joueur peut déplacer contre un coût.
2. Un 9 trous de note moyenne, en difficulté facile, attire **15 à 25 groupes par jour** (remplacé par la décision 9).
3. Groupes de 1 à 4 golfeurs.
4. Un groupe qui trouve la file pleine repart mécontent, ce qui fait baisser la réputation.
5. Arrivées de 6 h à 18 h.
6. Pas d'effet de l'hiver en soi : seule la météo du jour compte.
7. Vivier persistant de visiteurs qui reviennent.
8. Le prix du green fee n'a pas d'effet sur les arrivées dans C15.
9. On garde l'échelle de temps : les arrivées se calent sur ce que le parcours absorbe, environ 5 à 8 départs par jour sur « Le neuf », avec peu de refus (remplace la décision 2).
10. La note du parcours est neutre dans l'attrait jusqu'à C16.
11. Le plafond `maxGroupsPerHole` = 2 est validé.
12. On accélère les golfeurs (marche, préparation, attentes, pas le vol de balle) pour qu'un 9 trous tienne en 2 à 3 h de jeu, dans un lot à part (2b).
13. Le lot 2 est fusionné tel quel.

Hypothèses de l'orchestrateur, réglables en données :
- file de 8 groupes ;
- dernier départ à 16 h ;
- le taux de base cale le critère 4 ;
- le pavillon fait environ 12 × 8 cellules.

## Critères d'acceptation
1. Le pavillon (position, emprise, porte), le vivier, la file et la réputation entrent dans `Checksum` et `Save` (v12). Un cas `SaveTests` échoue si l'un d'eux est oublié. Une sauvegarde v11 se charge avec un pavillon placé automatiquement.
2. Nouvelle partie sur « Le neuf » : le pavillon est sur des cellules libres et plates, et il existe un chemin de la porte au départ du trou 1. Le déplacer débite son coût au grand livre ; un emplacement invalide est refusé sans débit. Un test vérifie que seuls les champs de flux touchés par le déplacement sont invalidés.
3. Avec la vraie date de `Calendar`, aucun groupe n'arrive avant 6 h ni après 18 h, et aucun ne part après 16 h. Sans trou ouvert, aucun groupe n'arrive.
4. « Le neuf » ouvert, graine de référence, 30 jours de printemps et d'été à météo normale (`-c Release`) : entre 15 et 25 arrivées par jour en moyenne. Cette partie est tenue au lot 2. Le critère final (5 à 8 départs de visiteurs par jour, au plus 10 % de refus, réputation finale au-dessus de 0,8) est celui du lot 2b.
5. La taille des groupes reste entre 1 et 4, et la distribution observée sur 1 000 groupes suit celle des données à ±5 points.
6. La file ne dépasse jamais 8 groupes. Un groupe qui arrive file pleine lève `GroupTurnedAway`, ne paie rien et fait baisser la réputation. Le test vérifie l'évènement, pas un compteur.
7. Un groupe qui finit le dernier trou marche jusqu'à la porte puis quitte le monde. Ses visiteurs retournent au vivier avec la date de leur visite. Sur 60 jours, au moins un visiteur revient.
8. Déterminisme : même graine, même suite de commandes, donc même `Checksum` après 10 jours de jeu avec arrivées.
9. Banc P1 avec arrivées continues (18 trous, attrait forcé au maximum, 5 000 arbres) : le nombre de groupes posés reste sous le plafond mesuré à C12 (36). Le tick tient les seuils de C12 : moyenne sous 2 ms, 99e centile sous 5 ms.
10. La touche G ne fait rien hors du mode débogage (fumée Godot).
11. Fumée Godot : le pavillon est rendu à la position de la sim, et une file de golfeurs est visible devant la porte. Capture `c15-pavillon` en CI.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Pavillon dans la sim (`Building`, emprise, porte, placement automatique, déplacement, coût), champ de flux vers la porte, sauvegarde v12 ; critères 1 et 2 | complet |
| 2 | Arrivées, file, départs, sortie, vivier, réputation, banc P1 avec arrivées ; critères 3 à 9 | complet |
| 2b | Rythme de jeu : un 9 trous en 2 à 3 h de jeu sur un parcours vide, le parcours chargé reste fluide, puis le critère 4 final (5 à 8 départs de visiteurs par jour, au plus 10 % de refus, réputation finale au-dessus de 0,8) | complet |
| 3 | Rendu du pavillon et de la file, déplacement au dock, choix du groupe pour le pro, touche G en débogage, capture ; critères 10 et 11 | léger |

## Niveau de contrôle
Complet (lots 1 et 2), léger (lot 3).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
