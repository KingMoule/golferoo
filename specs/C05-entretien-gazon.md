# Entretien du gazon
Statut : proposé
Créé : 2026-10-25

## But
La fiche D2 de `docs/boucles-v1.md` (sans mauvaises herbes ni maladies), ramenée à ce que le jeu peut porter aujourd'hui : il n'y a encore ni argent, ni personnel, ni bâtiments, ni tournois.
1. **L'herbe pousse, on la tond.** Chaque cellule de gazon a une hauteur. Elle pousse selon la saison et la pluie (M1, M9), et la tonte la ramène chaque nuit à la hauteur visée.
2. **La hauteur règle la balle.** Un green tondu ras roule vite, un rough épais retient la balle. La vitesse des greens s'affiche, comme le « stimp » des vrais parcours.
3. **Des consignes plutôt que de la microgestion** (D2, idée 7 ; Q1 et Q4) : greens rapides, normaux ou lents ; fairways fermes, normaux ou tendres ; rough court, moyen ou épais ; pour tout le parcours.
4. **L'usure se voit.** Là où les golfeurs marchent hors des chemins, un sentier brun apparaît (Q2). Les coups arrachent des mottes sur les fairways et les départs, les balles marquent les greens. L'herbe se répare chaque nuit, plus vite après la pluie (M1).
5. **Le gazon se lit à l'écran** : herbe rase plus claire, bandes de tonte sur les fairways, damier sur les greens, sentiers et mottes, et une surcouche « état du gazon ».

Ce qui prouve la réussite : un même putt roule plus loin sur un green « rapide » que sur un green « lent » ; une semaine de jeu dessine un sentier entre un green et le départ suivant ; les captures montrent les bandes, le damier et les sentiers.

## Périmètre et hors périmètre
**Dans le périmètre**
- `Turf/` dans la sim : état par cellule (hauteur, usure), consignes, croissance, tonte et réparation au changement de jour.
- Hauteur et fermeté dans `SoilGrid` (un facteur de plus, comme la saison et l'humidité en C03 et C04).
- Usure : un appel dans la marche (`World.Walk`) et un au départ ou à l'arrivée d'un coup.
- Rendu : texture d'état du gazon (la partie « état » de F3), bandes de tonte, damier, sentiers, mottes, surcouche (touche G), HUD (vitesse des greens), panneau des consignes.
- Les commandes de consigne sont des commandes de la sim (rejouables, dans la somme de contrôle).

**Hors périmètre, et où ça ira**
- Mauvaises herbes et maladies : retirées (décision du 2026-10-24).
- Personnel visible, tondeuses et hangar d'entretien (idées 8 et 11, Q3) : selon le choix 1 ; le hangar attend les bâtiments.
- Arroseurs à poser, coût de l'eau, restrictions par canicule (idée 9, Q5) : l'arrosage est automatique et gratuit tant qu'il n'y a pas d'argent ; la canicule n'existe pas encore dans `Climate`.
- Préparation des tournois (idée 10) et usure accélérée les jours de ligue : avec les tournois et C06.
- Humeur (« gazon abîmé −2 », golfeur en colère qui arrache une motte), landmarks « gazon soigné », difficulté qui accélère l'usure : avec l'humeur et les landmarks.
- Bunker à ratisser et mauvais lie : il n'y a pas encore de lie.
- Consignes par trou (Q4) ou réglages fins par zone (question mise de côté) : le format des consignes le permettra.
- Texture d'indices au-delà de 8 surfaces (F3, partie 1) : selon le choix 2.

## Conception
**État du gazon** (`src/Golferoo.Sim/Turf/`, sans Godot)
- `TurfGrid` : deux tableaux d'octets par cellule, hauteur (en dixièmes de mm pour les greens et départs, en mm au-delà ; un pas par surface) et usure (0 à 255). Pas de champ humidité : elle reste globale (`SoilGrid.Wetness`), l'arrosage automatique la rend inutile par cellule. Environ 0,2 Mo pour 100 000 cellules.
- `TurfSettings` : trois consignes (`GreenSpeed`, `FairwayFirmness`, `RoughLength`) à trois crans. Le cran « normal » donne les hauteurs de référence : 4 mm (green), 10 mm (départ), 15 mm (fairway), 50 mm (rough). Rapides et lents : 3 et 5 mm ; rough court et épais : 30 et 80 mm.
- Changement de jour (déjà le point d'entrée de la météo et des flaques) : pousse selon la saison (forte au printemps, nulle en hiver), ×1,5 s'il a plu la veille (M1, M9) ; tonte vers la hauteur visée (greens chaque jour, départs et fairways un jour sur trois, rough une fois par semaine) ; réparation de l'usure d'un cran fixe, doublé après la pluie. Tout se fait d'un coup au changement de jour : pas de mise à jour par tranches, puisque rien d'autre ne change la hauteur.
- Au jour 0, tout est à la hauteur de référence et sans usure.

**Le gazon et la balle** (`Terrain/SoilGrid.cs`)
- Facteur hauteur sur la résistance au roulement : `(h / h_ref)` à la puissance 0,5 (un green à 3 mm roule environ 15 % plus loin qu'à 4 mm, un green à 5 mm environ 10 % moins loin), à calibrer au lot 1. Fermeté des fairways : fermes = restitution ×1,15 et roulement ×0,85, tendres l'inverse.
- À la hauteur de référence et en consigne normale, le facteur vaut 1 : les tableaux restent identiques au bit près à ceux de C04.
- Recalcul au changement de jour et au changement de consigne seulement. L'usure ne touche ni la balle ni le coût de marche (règle de performance de D2 : les champs de flux en cache restent valides).
- Vitesse des greens : `Turf.Stimp()` calcule la distance d'une balle lâchée à 1,83 m/s (la vitesse du stimpmètre) sur la résistance moyenne des greens, en mètres et en pieds.

**Usure** (`World.cs`, `Turf/`)
- Marche : la cellule sous chaque golfeur reçoit un cran d'usure à chaque changement de cellule (pas sur les chemins, l'eau, le sable). Un tableau d'octets incrémenté, saturé à 255 : quelques nanosecondes par pas.
- Mottes : un coup joué depuis un fairway ou un départ ajoute de l'usure à sa cellule ; une balle qui tombe sur un green marque sa cellule d'arrivée.
- Le drapeau change de place chaque jour (choix 3) : parmi les cellules de green à au moins 3 m du bord, une position tirée du flux de la graine et du jour. Le champ de flux du green est refait une fois par jour.

**Rendu** (`game/shaders/terrain.gdshader`, `game/src/Rendering/TerrainRenderer.cs`)
- Une texture RG8 d'un texel par cellule (hauteur normalisée, usure), envoyée au GPU au changement de jour et au plus une fois par seconde pour l'usure. Une lecture par pixel.
- Herbe plus claire si elle est rase, plus sombre si elle est haute ; brun vers la terre selon l'usure ; mottes et marques dessinées par un bruit piloté par l'usure (pas de décalques).
- Bandes de tonte : celles du fairway existent (`stripe_width`), leur contraste suit la fraîcheur de la tonte ; damier sur les greens.
- Surcouche G : rouge (usé) à vert (sain), bleu pour l'herbe haute.
- HUD : « greens 3,4 m (11 pieds) » à côté de la météo. Panneau des consignes : trois listes déroulantes, chacune émet une commande.

**Lots (3 PR, 4 si le choix 2 le demande)**
1. **Hauteur, consignes et tonte** : `Turf/`, facteur hauteur et fermeté dans `SoilGrid`, croissance, tonte, stimp ; commandes de consigne ; tests et banc. (complet)
2. **Usure et drapeau mobile** : usure par la marche, mottes, marques, réparation, position du drapeau par jour ; tests et banc. (complet)
3. **Le gazon à l'écran** : texture d'état, couleurs, bandes et damier, sentiers et mottes, surcouche G, HUD, panneau des consignes ; captures. (léger)

## Choix à trancher par Guillaume
1. **Qui entretient le parcours ?**
   - A. **Un personnel invisible qui travaille la nuit** : la tonte et la réparation se font au changement de jour, selon les consignes. Gain : 3 lots, aucun nouvel acteur à faire marcher. Perte : on ne voit pas de tondeuse.
   - B. **Des jardiniers visibles** : quelques tondeuses (modèles `bpy`) qui suivent une file de tâches par zone. Gain : le parcours vit. Perte : 2 lots de plus (acteurs, tâches, modèles), et il faudra les payer et les embaucher quand l'économie arrivera, ce qui les rouvrira.
   - Recommandation : **A**, et des jardiniers visibles avec l'économie (embauche, salaires, hangar). Les consignes resteront les mêmes, seul l'exécutant changera.
2. **La texture d'indices (au-delà de 8 surfaces) entre-t-elle dans C05 ?**
   - A. **Plus tard**, dans le chantier qui ajoute la 9e surface (contenu coupé ou un thème). Gain : rien à refaire maintenant, aucun risque de retour à l'aspect « en cases ». Perte : la fondation reste à faire.
   - B. **Maintenant**, en lot 4. Gain : fondation prête. Perte : une réécriture moyenne de `terrain.gdshader` sans rien de nouveau à l'écran, à valider sur les captures.
   - Recommandation : **A**. Les consignes remplacent Firm Fairway et Deep Rough (Q1) : C05 n'ajoute aucune surface, les 8 actuelles suffisent.
3. **Le drapeau change-t-il de place chaque jour ?**
   - A. **Oui, dans C05** : l'usure du green se répartit, et le planificateur vise déjà un trou qui peut bouger (un champ de flux par jour).
   - B. **Plus tard**, avec les départs multiples (B1).
   - Recommandation : **A**. C'est la réponse de l'idée 6 à l'usure des greens, pour un coût d'environ une demi-journée de lot.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent la tonte (même graine et mêmes commandes, mêmes hauteurs ; sans tonte l'herbe dépasse la hauteur visée en une semaine de printemps, avec tonte elle y revient ; en hiver elle ne pousse pas), la balle (un même putt roule au moins 10 % plus loin sur un green rapide que sur un green normal, au moins 10 % moins loin sur un lent ; un rough épais arrête une balle plus tôt qu'un rough court ; un fairway ferme fait rouler un drive plus loin qu'un tendre), le stimp (green normal entre 10 et 12 pieds, soit la résistance actuelle de 0,05 : 3,4 m), l'usure (2 000 passages de golfeurs sur une ligne d'herbe y laissent une usure supérieure à la moitié du maximum ; une cellule de chemin n'en prend aucune ; une semaine sans passage la répare ; la pluie répare plus vite ; le coût de marche ne change pas), le drapeau (même graine et même jour, même position ; toujours sur le green à 3 m du bord au moins) et le déterminisme (mêmes commandes, dont les consignes, même somme de contrôle).
2. À la hauteur de référence et en consignes normales (jour 0), les trajectoires et sommes de contrôle des tests existants de C01 à C04 sont inchangées.
3. Banc : le tick à 200 golfeurs reste sous 2 ms avec l'usure ; un changement de jour (météo, sol, flaques, chutes et gazon) coûte moins de 5 ms ; le vol et le roulement d'une balle coûtent au plus 1,05 fois ceux de C04.
4. Captures de la CI (lignes `THUMB`) : le trou en consignes normales, puis greens rapides et rough épais, puis après une semaine de jeu (sentier, mottes, marques), puis la surcouche G. Guillaume juge.
5. Mesure FPS sur la RTX 2080 (`Mesurer FPS.bat`, 5 000 arbres, 200 golfeurs) : au moins 90 % de la référence de C04 (320,9 FPS sous forte pluie).
6. Fumée Godot (`SMOKE OK`) ; le HUD affiche la vitesse des greens ; changer une consigne dans le panneau change le stimp affiché le lendemain.
7. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (lots 1 et 2 : physique de la balle et état de partie) ; léger pour le lot 3 (rendu)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
