# Bâtiments et aménagements
Statut : cadré (en attente du « go » de Guillaume)
Créé : 2026-10-31

## But
Le joueur dépense son argent pour équiper le club. Trois bâtiments (snack-bar, pro shop, hangar d'entretien) et des bancs se posent à la souris, ont un coût, et les golfeurs les visitent, y paient et en repartent de meilleure humeur. C'est le manque M6 de `docs/plan-alpha.md`, et le dernier chantier de contenu avant le tutoriel v2 (C19).

## Périmètre et hors périmètre
Dans le périmètre :
- le modèle générique `Building` étendu à trois genres, plus un aménagement (le banc) ;
- l'outil de pose (emprise, quart de tour, coût, vente) ;
- le déblocage par trous ouverts ;
- les visites à pied, leur revenu et leur bonus d'humeur ;
- l'effet du hangar sur l'entretien et le départ des tondeuses ;
- le rendu et le dock.

Hors périmètre (décidé le 2026-10-31) :
- garage à voiturettes, abri de practice, maisons et terrains à bâtir (modèles au dépôt, aucune mécanique) ;
- amélioration d'un niveau du pavillon (aucun effet crédible avant C18) ;
- poubelles ;
- la balle qui touche un bâtiment ;
- faim, soif, fatigue (hors alpha) ;
- les objectifs du club comme source de déblocage (C18).

## Conception
- **Genres** (`Buildings/Building.cs`) : `BuildingKind` gagne `SnackBar`, `ProShop`, `MaintenanceShed` et `Bench`, à la suite de `Clubhouse` (index de sauvegarde stables). Emprise, rotation d'un quart de tour (la porte suit), tailles en données (`data/buildings/buildings.json`, modèles `snack_bar_01`, `pro_shop_01`, `maintenance_shed_01`, `bench_01` au dépôt).
- **Plusieurs exemplaires permis** (choix 2) : aucune limite par genre. Un même golfeur ne visite qu'**un** bâtiment de chaque genre par partie.
- **Déblocage** (choix 3) : par nombre de trous ouverts, seuils en données (hypothèse : snack-bar 3, pro shop 4, hangar 6, banc 1). Un genre verrouillé est grisé dans le dock, avec son seuil. C18 pourra ajouter les objectifs.
- **Pose** (`World.PlaceBuilding`, `World.SellBuilding`, commandes de la sim, déterministes) :
  - refus sur les surfaces de jeu (départ, fairway, green, bunker, eau) et sur toute emprise existante ;
  - sur le rough et sous les arbres, le défrichage est payé et les arbres de l'emprise sont coupés aux coûts de C14 (`treeClearPerM2`, `treeClearFallen`) ;
  - le bâtiment bloque le passage et les arbres, et invalide les champs de flux limités aux blocs touchés (règle de C12 lot 4) ;
  - **vente à moitié prix** (choix 10), défrichage non remboursé ;
  - coûts en données (`data/economy/facile.json`), écriture au grand livre dans une catégorie « Bâtiments » ; le refus par solde insuffisant suit le découvert de C14.
- **Visites à pied** (choix 4, `Golfers/Visits.cs`) :
  - le **pro shop** est proposé au golfeur avant son premier départ, le **snack-bar** au virage (après le trou du milieu du parcours, ou le dernier trou pour un parcours court) ;
  - un tirage selon le profil (en données) décide s'il visite, puis le golfeur marche jusqu'à la porte par le champ de flux, paie, repart par le même chemin et reprend sa partie ;
  - le bâtiment visité est le plus proche dont la porte est à portée (hypothèse : 8 cellules du point de décision pour le snack-bar, 5 pour le pro shop, en données) ; aucun bâtiment à portée, aucune visite ;
  - un détour qui rend la partie trop lente est refusé : le jeu lent reste une contrariété de C16 (pas de visite si le groupe a déjà été signalé lent).
- **Effet d'une visite** (choix 5) : +1 d'humeur (événement `ServiceVisited`, lève `MoodChanged`, pensée en français en données) et un revenu par visite, hypothèse snack-bar 6 $, pro shop 10 $ sur un green fee de 15 $, **calé par balayage de paramètres** (critère 6). Le pro shop donne en plus un **bonus de précision** au golfeur pour la fin de la partie (valeur en données, bornée).
- **Hangar** (choix 6) : l'entretien mensuel baisse de 15 % dès qu'il y a au moins un hangar (l'effet **ne se cumule pas** : hypothèse, en données). Les tondeuses de C05 (`MowerLayer`) partent du hangar le plus proche le matin et y rentrent ; sans hangar, elles partent du pavillon comme aujourd'hui.
- **Banc** (choix 8) : un aménagement d'une cellule, sans porte ni visite. Il ajoute un petit bonus à la beauté (`BeautyMap`) des cellules proches (valeur et rayon en données). Remboursé à moitié prix comme le reste.
- **État** : la liste des bâtiments posés (genre, emprise, côté) entre dans `World.Checksum` et `World.Save` (v16 → v17), avec un cas `SaveTests` qui échoue si elle est oubliée. Une sauvegarde v16 se charge sans bâtiment autre que le pavillon. Une vraie sauvegarde de `main` est archivée comme fixture.
- **Jeu** :
  - l'outil de pose dans le dock de C13 (bouton « bâtiments », fantôme sous la souris, coût affiché, Tab ou molette pour tourner, clic droit pour vendre) ;
  - rendu par les modèles du dépôt, posés à la position de la sim ;
  - messages au journal : bâtiment posé, vendu, verrou levé.

## Choix tranchés par Guillaume (2026-10-31)
1. Trois bâtiments dans l'alpha : snack-bar, pro shop, hangar d'entretien.
2. **Plusieurs exemplaires permis** de chaque bâtiment (contre la recommandation d'un seul).
3. Déblocage par trous ouverts, seuils en données.
4. Visite par un vrai détour à pied.
5. +1 d'humeur et un petit revenu par visite ; bonus de précision pour le pro shop.
6. Hangar : tondeuses et entretien mensuel −15 %.
7. Amélioration du pavillon reportée.
8. Bancs seulement (pas de poubelles).
9. Pose hors surfaces de jeu, défrichage payé ; la balle contre un bâtiment n'est pas traitée.
10. Vente à moitié prix.

Hypothèses de l'orchestrateur, réglables en données : seuils de déblocage, prix des bâtiments, portées, revenus par visite, bonus de précision, effet non cumulatif du hangar, rayon du banc. Le choix 2 impose de les caler pour que **plusieurs snack-bars** ne rapportent pas plus que leur prix à eux seuls (critère 6).

## Calage du lot 2
Mesuré sur « Le neuf » (9 trous, cellules de 4 m), résolveur physique, 30 jours, sans ligue, graines 1, 2 et 3 (`BuildingCalibrationTests.Sweep_ThirtyDays_ThreeSeeds`, lancé avec `GOLFEROO_LONG=1` en `-c Release`, 21 parties, environ 25 minutes). Les visites ne dépendent ni des prix ni des revenus : on a balayé ce qui change le comportement (portées, probabilités), puis fixé prix et revenus sur les visites mesurées. L'année vaut 120 jours (12 mois de 10 jours) : revenu annuel = 4 × celui des 30 jours, sans compter la montée en puissance du club (estimation prudente).

**Les portées d'hypothèse (8 cellules le snack-bar, 5 le pro shop) ne tiennent pas sur cette carte** : le rough le plus proche de la sortie du trou 5 est à 17 cellules du drapeau, et la porte du pro shop la plus proche du pavillon à 9 cellules de sa porte (le pavillon est entouré de fairway et de chemin, où l'on ne pose pas). Avec 12 et 8, personne ne visite. Retenu : **24 cellules pour le snack-bar, 12 pour le pro shop** (la porte la plus proche posable est à 17 et 9 cellules). Un joueur qui pose plus loin n'a aucune visite : c'est la règle, pas un défaut.

| Parties (graines 1, 2, 3) | Golfeurs | Abandons | Humeur de sortie | Visites snack-bar | Visites 2e snack-bar | Visites pro shop |
|---|---|---|---|---|---|---|
| Sans bâtiment | 286, 295, 267 | 7, 6, 5 (2,4 / 2,0 / 1,9 %) | 6,76 · 7,10 · 5,11 | 0 | 0 | 0 |
| Probabilités 0,5 et 0,3 | 287, 299, 273 | 8, 8, 2 (2,8 / 2,7 / 0,7 %) | 7,01 · 7,51 · 6,30 | 114, 134, 103 | 0 | 76, 85, 79 |
| Probabilités 0,8 et 0,6 | 288, 296, 267 | 6, 8, 8 (2,1 / 2,7 / 3,0 %) | 7,36 · 7,67 · 6,54 | 185, 198, 179 | 0 | 164, 179, 133 |
| 2 snack-bars, 0,8 et 0,6 | 288, 299, 267 | 3, 5, 8 (1,0 / 1,7 / 3,0 %) | 7,36 · 7,69 · 6,54 | 192, 196, 179 (+0 au 2e) | 0 | 173, 182, 133 |
| **Données finales** (profils) | 288, 296, 269 | 3, 9, 3 (1,0 / 3,0 / 1,1 %) | 7,41 · 7,59 · 6,48 | 169, 190, 176 | 0 | 188, 189, 146 |
| Données finales, 2 snack-bars | 288, 296, 271 | 5, 9, 4 (1,7 / 3,0 / 1,5 %) | 7,27 · 7,59 · 6,87 | 171, 190, 197 (+0 au 2e) | 0 | 178, 189, 151 |
| Portées 12 et 8 | 282, 295, 267 | 8, 8, 6 (2,8 / 2,7 / 2,2 %) | 6,59 · 7,06 · 5,12 | 0 | 0 | 0 |

Lecture :
- **Un deuxième snack-bar voisin ne rapporte rien** : chacun ne visite qu'un snack-bar par partie et va au plus proche, donc le premier garde toutes les visites (le 2e en a 0 sur 6 parties). Le total des visites au snack-bar reste celui d'un seul (169, 190, 176 contre 171, 190, 197 ; l'écart de la graine 3 vient de parties qui divergent, pas du 2e snack-bar). Le revenu ne double donc pas, et le 2e snack-bar ne rembourse jamais son prix : c'est ce que le choix 2 exigeait.
- **Abandons hors ligue sous 5 %** sur toutes les parties : au plus 3,0 % (sans bâtiment : 1,9 à 2,4 %). Les visites ne ralentissent pas assez le jeu pour fâcher, et l'humeur de sortie monte (+0,5 à +1,4).
- **Prix et revenus** (revenu annuel = 4 × visites des 30 jours, graine la plus faible comprise) :

| Bâtiment | Prix | Revenu | Revenu annuel (graines 1, 2, 3) | Rapport au prix, pire graine |
|---|---|---|---|---|
| Snack-bar | 12 000 $ (hypothèse : 20 000 $) | 22 $ par visite (hypothèse : 6 $) | 14 872 · 16 720 · 15 488 $ | 124 % |
| Pro shop | 20 000 $ (hypothèse : 30 000 $) | 40 $ par visite (hypothèse : 10 $) | 30 080 · 30 240 · 23 360 $ | 117 % |
| Hangar | 15 000 $ (inchangé) | −15 % d'entretien : 9 trous × 1 100 $ × 15 % × 12 mois | 17 820 $ | 119 % |

  Les revenus d'hypothèse (6 $ et 10 $) ne pouvaient pas rembourser des bâtiments de 20 000 et 30 000 $ : il y a environ 290 golfeurs en 30 jours, donc au plus 1 160 par an et une visite par genre et par golfeur.
- Bonus du pro shop : dispersion × 0,97 pour le reste de la partie (une seule visite par partie, donc borné). Hangar : −15 %, non cumulatif. Banc : +0,1 de beauté, rayon 6 m.
- Banc P1 (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`, « Arrivées continues », 18 trous, 5 000 arbres, attrait au maximum, un snack-bar, un pro shop, un hangar et un banc posés, 11 visites pendant la mesure) : tick moyen 1,06 ms (limite 2), p99 2,02 ms (limite 5).

## Critères d'acceptation
1. Sauvegarde : la liste des bâtiments entre dans `Checksum` et `Save` (v17), un cas `SaveTests` échoue si elle est oubliée, une sauvegarde v16 se charge.
2. Pose : un bâtiment se pose sur du rough, pas sur un fairway, un green, un départ, un bunker, de l'eau ni sur une autre emprise ; les arbres sous l'emprise sont coupés et facturés ; un genre verrouillé est refusé ; un solde insuffisant suit les règles du découvert. Chaque cas par un test qui vérifie la commande refusée ou acceptée, pas un compteur.
3. Vente : rembourse la moitié du prix, libère l'emprise et rend le passage (champ de flux recalculé sur les blocs touchés seulement).
4. Visite : sur « Le neuf » avec un snack-bar et un pro shop posés, des golfeurs marchent jusqu'à la porte, paient et lèvent `ServiceVisited` ; un golfeur ne visite qu'un bâtiment de chaque genre par partie ; sans bâtiment à portée, aucune visite. Test d'événement, pas de compteur.
5. Effets : une visite fait +1 d'humeur ; le pro shop applique son bonus de précision ; le hangar baisse l'entretien de 15 %, et deux hangars ne baissent pas de 30 %.
6. Calage (3 graines, résolveur physique, 30 jours sur « Le neuf ») : un snack-bar, un pro shop et un hangar finissent par rapporter plus que leur prix sur une année de jeu, et un deuxième snack-bar voisin ne double pas le revenu du premier. Les abandons restent sous 5 % hors ligue (critère de C16).
7. Déterminisme : même graine et mêmes commandes de pose, même `Checksum` après 10 jours.
8. Banc P1 : le tick tient les seuils de C12 (moyenne sous 2 ms, 99e centile sous 5 ms) avec bâtiments posés et visites actives.
9. Fumée Godot : chaque bouton du dock pose son bâtiment sans touche ; le fantôme montre un refus sur un fairway ; une vente rembourse. Captures `c17-pose` (fantôme valide et refusé) et `c17-visite` (golfeur à la porte du snack-bar) en CI.
10. Les textes (pensées, messages, noms des bâtiments) sont en français, dans les données.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Genres de bâtiment, pose, vente, déblocage, défrichage, grand livre, invalidation des champs de flux, sauvegarde v17 ; critères 1, 2, 3 et 7 | complet |
| 2 | Visites, revenus, humeur, bonus du pro shop, effet du hangar, banc et beauté, calage ; critères 4, 5, 6 et 8 | complet |
| 3 | Outil de pose dans le dock, fantôme, rendu, tondeuses depuis le hangar, messages, captures ; critères 9 et 10 | léger |

## Niveau de contrôle
Complet (lots 1 et 2), léger (lot 3).

## Clôture
- [x] Critères vérifiés (lots 1 et 2 : audits complets ; lot 3 : audit léger, #154), avec deux écarts acceptés :
  - critère 9 : `c17-visite` montre un golfeur à la porte d'un pro shop posé près du départ du trou 1. À la livraison du lot 3, la sortie du trou 5 de « Le neuf » était enfermée par les bois et la visite au snack-bar s'y faisait par téléportation. #155 a ouvert les passages, la capture du snack-bar peut revenir.
  - critère 10 : les noms, descriptions et raisons de refus des bâtiments sont en français mais dans `UiNames` et `BuildingTool`, comme les saisons et le ciel ; seuls les nombres sont dans `data/buildings/buildings.json`.
- [x] Calage du lot 2 : les visites au snack-bar comptées ici passaient en partie par la téléportation de l'enclos. Depuis #155, `BuildingCalibrationTests` en mode court reste vert ; le mode long n'a pas été relancé.
- [x] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
