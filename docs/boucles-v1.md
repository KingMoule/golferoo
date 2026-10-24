# Les cinq boucles de la première version, en détail

Mis à jour : 2026-10-24. Chantier R03. Suite de `docs/boucles-de-jeu.md`. Guillaume a demandé (2026-10-24) d'approfondir les cinq boucles recommandées pour une première version : idées concrètes, questions concrètes, effets sur les autres boucles et features, et coût en code et en rendu.

Les coûts sont donnés en **lots** (un lot ≈ une PR d'agent, comme les 8 lots de C01). Les chiffres de performance viennent de `docs/prototype-c01-rapport.md`, ou sont des estimations, marquées comme telles. Les noms de fichiers renvoient au code actuel.

## En bref
- **Guillaume a tranché le 2026-10-24** (section suivante) : ses réponses aux questions des fiches sont « oui à tout », sauf ce qu'il a commenté. Pour la première version, on garde la météo et les saisons, l'entretien du gazon et les ligues. Les scénarios cèdent la place à un **tutoriel simple** de construction et d'ouverture d'un trou. Le **mode photo est reporté**, avec une caméra prête à le recevoir. Il n'y a **ni nuit ni golf de nuit**.
- Une nouvelle boucle naît de ses réponses : les **arbres vivants**. Les arbres déjà présents sur le terrain sont gardés ou rasés, ceux qu'on plante sont jeunes et grandissent, et une tempête peut en abattre un. Le parcours évolue ainsi dans le temps. Elle rejoint C02 (échelle des arbres), qui devient « Arbres et échelle du monde ».
- La pluie et le vent ne doivent pas être qu'une punition tirée au hasard : neuf côtés positifs sont proposés plus bas, à trier.
- Le total passe d'environ 27 lots à **environ 25**, dont 8 de fondations. Les scénarios et la photo sortent, les arbres vivants entrent. La sauvegarde (F4) reste nécessaire pour le jeu, mais elle n'est plus portée par une boucle de cette liste.
- Les deux règles de performance tiennent : l'usure et la croissance ne changent jamais le coût de marche, et les propriétés du sol sont précalculées.
- **Le shader de terrain ne peut afficher que 8 surfaces**, et elles sont toutes prises. C'est la fondation F3, nécessaire à l'entretien et au contenu coupé.

## Décisions de Guillaume (2026-10-24)
Toutes les propositions des fiches sont acceptées, sauf ce qui suit.

**Calendrier (F1).** Pas d'heure affichée et pas de nuit : le golf se joue de jour. La lumière du jour reste, avec des ombres qui tournent au fil de la journée, si le rendu le permet. *Coût vérifié : Godot recalcule déjà les ombres de la lumière directionnelle à chaque image ; faire tourner le soleil ne coûte rien de plus.* La sim garde donc un « moment de la journée » de l'ouverture à la fermeture, sans horloge à l'écran. Le golf de nuit (D13) est abandonné.

**Saisons et végétation.** Les saisons changent la végétation et le jeu, pas seulement les couleurs :
- au printemps, les feuillus ont moins de feuilles : la balle a plus de chances de traverser un arbre ; il y a plus de fleurs ;
- en automne, les feuillus prennent leurs couleurs ;
- les conifères et les palmiers changent peu.

**Météo, point 5.** Une pluie tirée au hasard qui ne fait que baisser l'affluence n'est pas intéressante. Il faut des côtés positifs, différents ou inhabituels. Voir « Côtés positifs de la pluie et du vent ».

**Arbres, et lien avec le passage du temps.** Plus d'arbres matures qui apparaissent d'un coup. Le terrain de départ porte des arbres déjà là, matures ou plus jeunes, et le joueur décide ce qu'il garde et ce qu'il rase pour ses premiers trous. Un arbre ajouté commence jeune et pousse lentement : la difficulté d'un trou change avec le temps, et le terrain évolue. Voir « Arbres vivants ».

**Vent dans la ligne de visée (D1 Q4).** Ça dépend du prestige de l'épreuve : le vent est inclus en partie amicale, en ligue et en tournoi local ; il ne l'est pas dans les tournois prestigieux, où le joueur doit lui-même anticiper le vent.

**Fermeture par l'orage (D1 Q5).** Pas d'orage qui ferme le parcours. À la place, par grand vent ou forte pluie, un arbre a un faible pourcentage de chances de tomber : le parcours évolue dans le temps.

**Gazon (D2).** Ni mauvaises herbes ni maladies : c'est de la gestion « plate ». Les mauvaises herbes de l'original disparaissent aussi (voir la fiche D2 pour ce qu'il faut remplacer).

**Ligue (D5).** Votre pro joue la ligue. Il commence dans la division la plus basse, parce que ses statistiques de départ sont faibles, et il cherche à monter. Seuls ses scores de ligue comptent pour son handicap.

**Scénarios (D7).** Pas de scénarios pour l'instant. Un tutoriel très simple pour la boucle de construction et d'ouverture d'un trou.

**Mode photo (D12).** Pas pour l'instant. On le laisse prêt, sans l'activer, si ça ne coûte pas trop de code.

## Côtés positifs de la pluie et du vent (propositions à trier)
Le but : que le mauvais temps change la journée plutôt que de seulement la gâcher. Chaque proposition s'appuie sur un système déjà prévu, pour rester peu coûteuse.

| # | Idée | Ce qui se passe | S'appuie sur | Coût |
|---|---|---|---|---|
| M1 | **Arrosage gratuit** | la pluie remplace l'arrosage (économie) et répare l'usure plus vite | D2 | nul |
| M2 | **Greens réceptifs** | après la pluie, la balle s'arrête sur le green : les scores baissent, journée à birdies, humeur en hausse et records possibles | F2 (sol mouillé) | nul |
| M3 | **Les purs et durs** | un trait de golfeur « aime le mauvais temps », surtout sur les links : ces golfeurs viennent par la pluie, gagnent plus vite des points d'adhésion et profitent d'un parcours sans attente | profils (9.2), adhésion | S |
| M4 | **Le club-house se remplit** | pendant une averse, les golfeurs s'abritent au snack-bar ou au pub : les ventes montent ; une scène d'histoire « à l'abri » peut se déclencher | bâtiments, histoires | S |
| M5 | **Après la pluie** | arc-en-ciel et lumière rasante donnent une pensée « vue panoramique » plus forte ; les animaux sortent (canards, grenouilles) | pensées, faune | S, et le rendu de l'arc-en-ciel |
| M6 | **Le vent fait le prestige** | un trou qui reste jouable par grand vent fait monter la note de compétence et l'évaluation SGA ; les tournois prestigieux préfèrent les parcours venteux et paient mieux | évaluation, tournois | S |
| M7 | **L'arbre tombé, une occasion** | il ouvre une nouvelle ligne de jeu à garder, ou une place à replanter ; le bois se vend (petit revenu) ; la souche peut rester comme décor | arbres vivants | S |
| M8 | **La tempête légendaire** | un tournoi joué dans la tempête entre au journal du club, donne un accomplissement et fait parler du club | tournois, accomplissements | S |
| M9 | **La pluie de printemps** | les jeunes arbres et l'herbe poussent plus vite après la pluie | arbres vivants, D2 | nul |

## Arbres vivants (nouvelle boucle, liée à C02)

### La boucle
Le terrain d'une propriété arrive boisé : des arbres matures, d'autres plus jeunes, de plusieurs essences selon le thème. Pour tracer ses premiers trous, le joueur choisit ce qu'il garde et ce qu'il rase. Raser coûte, et un vieil arbre ne se remplace pas vite. Un arbre planté est un jeune plant qui grandit sur plusieurs saisons. Le trou qu'on a dessiné change donc avec les années : un couloir se resserre, un dogleg se ferme, une ligne de jeu disparaît. Une tempête peut abattre un arbre et en rouvrir une autre.

### Idées concrètes
1. **Chaque arbre est un objet de la simulation**, avec son essence, son âge, sa hauteur et le rayon de sa couronne, et non plus une cellule « arbres ». C'est aussi ce que C02 demande déjà : découpler les arbres de la grille.
2. **Le sous-bois reste une surface.** La surface « Trees » actuelle devient le sous-bois infranchissable des massifs. Les arbres isolés, eux, peuvent pousser dans le rough ou au bord du fairway, où l'on marche.
3. **Essences par thème** : feuillus et conifères en Parkland, palmiers en Tropical, arbres de Josué et cactus en Desert, peu d'arbres et des ajoncs en Links. Chaque essence a sa vitesse de croissance, sa taille adulte et sa réaction aux saisons.
4. **La balle et l'arbre.** La couronne a une densité qui dépend de l'essence et de la saison : au printemps, un feuillu laisse plus souvent passer la balle. Le tronc arrête la balle. Un jeune arbre gêne peu.
5. **Garder ou raser.** Outil de sélection : un arbre, une zone, ou « tout sauf les arbres remarquables ». Les vieux arbres remarquables plaisent aux golfeurs (pensée positive, comme les arbres décoratifs de l'original) et valorisent les lots (immobilier).
6. **Planter.** Un jeune plant coûte peu. Un arbre plus grand, planté en motte, coûte cher mais pousse moins longtemps : c'est le compromis entre l'argent et le temps.
7. **La difficulté suit la croissance.** La classe du trou et sa difficulté (note du parcours, D5) sont recalculées à chaque saison. Le joueur reçoit un avis quand un trou change de classe (« le 7 est devenu un trou de précision »).
8. **Chute d'un arbre** par grand vent ou forte pluie, avec un faible pourcentage. L'arbre tombé est à dégager, ou à garder un temps comme obstacle ; voir M7.
9. **Élagage**, plus tard : relever la couronne d'un arbre pour rouvrir une ligne sans l'abattre.

### Effets sur les autres boucles et features
- **C02, échelle du monde** : la taille adulte, l'espacement et la densité des massifs deviennent des paramètres par essence. C02 devient « Arbres et échelle du monde » et porte cette boucle.
- **Météo et saisons (D1)** : chute par tempête, densité des feuilles au printemps, couleurs en automne, pousse après la pluie (M9).
- **Ligues (D5)** : la note du parcours change avec les saisons. Un handicap mesuré sur un parcours qui évolue reste juste, puisqu'on le rapporte à la note du jour de la partie.
- **Immobilier (section 7)** : la valeur des lots dépend déjà des arbres proches ; les vieux arbres comptent plus.
- **Génération du terrain (3.2)** : elle doit produire des boisements crédibles, avec des âges variés.
- **Construction** : le coût de défrichage de l'original (section 5.1) s'applique aux arbres rasés.
- **Plusieurs départs (B1)** : quand un arbre ferme une ligne, on recule ou on décale le départ.

### Coût en code (≈ 4 lots)
- **Arbres de la simulation (≈ 2 lots).** Une liste d'arbres et un index spatial (les arbres rangés par cellule). Pendant le vol, la balle ne teste les arbres que si elle est plus basse que l'arbre le plus haut de la cellule : un test de plus, rare. La densité de la couronne donne une probabilité de traverser, tirée du hasard déterministe. Les tests remplacent ceux de `BallSimulator` sur la surface « Trees » et sa hauteur fixe de 8 m.
- **Croissance et saisons (≈ 1 lot).** La croissance est calculée une fois par mois de jeu, pas à chaque tick : quelques milliers d'arbres, coût négligeable. **Règle de performance** : un arbre isolé ne rend pas sa cellule infranchissable et sa croissance ne change jamais le coût de marche. Seuls le sous-bois, une plantation ou une chute (événements rares) touchent les champs de flux.
- **Outils (≈ 1 lot).** Garder, raser, planter, avec leur coût.
- **Recalcul de la difficulté des trous** à chaque saison, en arrière-plan, avec le même calcul que la note du parcours (D5).
- Sauvegarde : quelques octets par arbre.

### Coût en rendu
- Les arbres restent en `MultiMesh`, coupés en un `MultiMesh` par bloc de terrain (fondation F6). La croissance met à jour l'échelle des instances une fois par mois, bloc par bloc : coût négligeable.
- **Jeunes plants** : un modèle à part par essence (script `bpy`), parce qu'un arbre adulte réduit ne ressemble pas à un jeune arbre.
- **Saisons** : couleur par instance (`use_colors` ou données personnalisées du `MultiMesh`). Pour le feuillage plus clairsemé du printemps, les sommets du feuillage sont marqués dans le modèle et le shader les resserre ou les écarte. Il n'y a pas de modèle en plus par saison.
- **Chute** : une courte animation de l'arbre qui bascule (`Tween`), puis un modèle de tronc couché.
- Le nombre total d'arbres, fixé par C02, décide du besoin de niveaux de détail (F6).

## Questions mises de côté pour plus tard
À la demande de Guillaume (2026-10-24), ces trois questions de `docs/boucles-de-jeu.md` attendent :
1. La réputation (D3) remplace-t-elle les invitations de l'original, ou s'y ajoute-t-elle ?
2. Un hiver fermé où l'on construit sans golfeurs, ou une année de jeu continue ?
3. Entretien : seulement des consignes au personnel, ou aussi des réglages fins par zone ?

Les fiches ci-dessous sont conçues pour que ces choix restent ouverts : l'hiver est un paramètre du calendrier (F1) et les consignes d'entretien peuvent recevoir plus tard un mode fin.

## Fondations communes

| # | Fondation | Sert à | Contenu | Lots |
|---|---|---|---|---|
| F1 | Calendrier et moment de la journée | D1, D2, D5, arbres | jours, mois, saisons ; moment de la journée pour le soleil, sans horloge ni nuit (Guillaume) ; événements datés ; C01 ne connaît que des ticks | 1 |
| F2 | Propriétés du sol par cellule | D1, D2, balle, putt | un tableau précalculé (roulement, rebond, frottement) = table de base × état du gazon × humidité ; remplace les appels à `SurfaceTable.Get` dans `BallSimulator` et `PhysicalShotResolver` | 1 |
| F3 | Shader de terrain v2 | D2, contenu coupé (C), thèmes | texture d'indices de surface (au-delà de 8 surfaces) et texture d'état du gazon | 2 |
| F4 | Sauvegarde et journal des commandes | le jeu (R7), D11 et des scénarios plus tard, tests | format versionné ; rechargement à somme de contrôle égale ; chaque action du joueur enregistrée comme commande rejouable. Hors de cette liste depuis le report des scénarios, mais nécessaire au jeu | 2 |
| F5 | Moteur de score | D5, tournois (B18), formats (B20), défis | carte de score, brut, net, stableford, classement | 1 |
| F6 | Caméra et niveaux de détail | A9, swing (B17), C02, arbres vivants ; D12 plus tard | caméra à modes, dont un mode photo prêt mais désactivé ; un `MultiMesh` d'arbres par bloc ; LOD du terrain et des arbres ; horizon | 3 |
| F7 | Fondation d'UI | toutes | la preuve P3 de R02 : thème, dock, composants (liste, tableau, fiche, graphique) | (P3) |

---

## D1. Météo et saisons

> **Décidé (2026-10-24)** : propositions acceptées, sauf : pas d'orage qui ferme le parcours, mais une chute d'arbre possible par grand vent ou forte pluie (Q5) ; le vent est dans la ligne de visée selon le prestige de l'épreuve (Q4) ; pas de nuit ; la pluie doit avoir des côtés positifs (voir M1 à M9) ; les saisons agissent sur la végétation (voir « Arbres vivants »).

### Idées concrètes
1. **Un climat par thème, une météo par jour.** Une table de climat par thème (Links venteux et pluvieux, Desert sec et chaud, Tropical avec des averses l'après-midi et des orages, Parkland tempéré), corrigée par la propriété (côtier = plus de vent). Le temps du jour suit une chaîne simple : un jour de pluie a plus de chances d'être suivi d'un autre. Dans la journée, le vent se lève l'après-midi et la brume se dissipe le matin.
2. **Prévisions sur trois jours, pas toujours justes.** Le joueur prépare la semaine : déplacer les départs (B1), avancer un tour de tournoi. Une prévision qui se trompe de temps en temps crée des surprises sans paraître injuste.
3. **Le vent sur la balle.** Direction, force et rafales. Dans la physique, la traînée devient relative à l'air : `a = −k·|v − w|·(v − w)` au lieu de `−k·|v|·v` (`BallSimulator.Run`). C'est une soustraction de plus par pas, sans fonction coûteuse : le déterminisme est conservé.
4. **Les golfeurs lisent le vent selon leur imagination.** Le planificateur simule avec le vent qu'*estime* le golfeur : le vent réel plus une erreur qui diminue avec la compétence Imagination. Un links venteux devient un trou d'imagination, ce qui relie la météo aux classes de trous de l'original (Creative, Heroic, Strategic, Classic).
5. **La pluie.** Moins d'arrivées, humeur en baisse, sol plus lent (roulement plus freiné, rebond plus mou, via F2). Sous l'orage, une sirène renvoie tout le monde au pavillon (le champ de flux vers le pavillon existe déjà) et le parcours est suspendu.
6. **Les flaques, et le relief qui compte.** Après une forte pluie, l'eau s'accumule dans les creux du relief. Une flaque est un « casual water » : la balle s'en dégage sans pénalité, mais elle déplaît. Elle se dessine avec le shader d'eau existant. Construire un green dans une cuvette devient une erreur visible, ce qui donne du sens au relief.
7. **Les saisons.** Le printemps est vert vif et pousse vite (lien avec D2), l'été jaunit sans arrosage, l'automne colore les feuilles qui tombent, et l'hiver reste un paramètre (question mise de côté).
8. **Événements météo.** Canicule (restrictions d'eau), tempête qui abat un arbre (un tronc à dégager, lien avec les arbres de C02), coup de froid.
9. **Le cycle du jour.** Le soleil tourne, les ombres s'allongent, le matin est brumeux. C'est la base du golf de nuit (D13) et des photos (D12).

### Questions concrètes
| # | Question | Ma proposition |
|---|---|---|
| Q1 | Un vent réaliste (jusqu'à 50 km/h sur un links) ou adouci ? | réaliste ; les golfeurs s'y adaptent seuls, et l'humeur ne baisse qu'au-delà d'un seuil |
| Q2 | Les flaques et le drainage dès la première version ? | oui, flaques visibles et effet sur la balle ; les drains à poser viendront plus tard |
| Q3 | Climats stylisés par thème, ou climats réels par région ? | stylisés par thème, corrigés par la situation (côtier, île, intérieur) |
| Q4 | Le vent dans la ligne de visée de votre pro (B17) ? | oui, la trajectoire prévue l'inclut, puisqu'elle vient du même simulateur |
| Q5 | Le joueur peut-il fermer le parcours par mauvais temps, ou seulement le jeu ? | les deux : fermeture automatique sous l'orage, fermeture manuelle par grand vent ou par pluie |

### Effets sur les autres boucles et features
- **Swing contrôlable (B17)** et **analyse de coup (B4)** : le vent apparaît sur la trajectoire prévue, sans code en plus.
- **Tournois avec cut (B18)** : la météo de chaque tour change la ligne du cut ; un orage reporte un tour.
- **Plusieurs départs (B1)** : ils deviennent la réponse au vent du jour.
- **Handicap et mémoire (B10)** : le golfeur se souvient d'un trou « par vent de face ».
- **Entretien (D2)** : la pluie arrose, la canicule assèche. C'est le couplage le plus fort.
- **Ligues (D5)** : une soirée de ligue peut être reportée.
- **Scénarios (D7)** : « année de sécheresse », « links dans la tempête ».
- **Évaluation SGA** : le temps de jeu cible doit être corrigé de la météo pour ne pas punir un parcours venteux.
- **Vitesse du temps (A1)** : l'avance rapide s'arrête avant un orage annoncé.

### Coût en code
- **Simulation (≈ 3 lots).**
  - `Weather/` : `ClimateTable`, `WeatherDay`, générateur quotidien. La météo tire son hasard d'un flux à part, dérivé de la graine et du numéro du jour : retoucher le terrain ne change pas la météo de demain, ce qui garde les parties et les tests comparables.
  - `BallSimulator` : la signature de `Run` prend un environnement (vent, humidité) au lieu du seul `drag`. Tests : un drive par vent de travers de 10 m/s dévie d'une distance attendue ; le planificateur compense et atterrit dans une tolérance ; même graine, même somme de contrôle.
  - Planificateur (`PhysicalShotResolver`) : aucun coût par candidat en plus, mais il faudra peut-être plus de directions à essayer par vent fort. **À mesurer dans P1**, puisque la planification coûte déjà ~15 ms par drive.
  - Flaques : calcul des cuvettes du relief par remplissage depuis les bords (« priority flood »), une seule fois par retouche de relief. Pour ~100 000 cellules, de l'ordre de quelques millisecondes (estimation).
- **Dépend de** F1 (calendrier) et F2 (propriétés du sol).

### Coût en rendu
- Ciel et soleil : `ProceduralSkyMaterial` et une `DirectionalLight3D` qui tourne avec l'heure. Coût nul.
- Pluie et neige : `GPUParticles3D` attachées à la caméra, environ 5 000 à 10 000 gouttes. Le coût est fixe, quelle que soit la taille de la carte (moins de 0,5 ms sur une RTX 2080, estimation).
- Sol mouillé : le shader de terrain est en `specular_disabled`. Pour un reflet mouillé, il faut réactiver la spéculaire, ce qui est un petit coût par pixel.
- Saisons : couleurs en paramètres (uniforms) dans le shader de terrain et dans celui des arbres. Pour les arbres, il faut une couleur par instance dans le `MultiMesh` (`use_colors`) et un matériau qui remplace celui de `tree.glb`.
- Vent visible : balancement des arbres et du drapeau par décalage des sommets dans leur shader. Coût négligeable.
- Flaques : réutilisent le shader d'eau existant.

### Lots proposés (≈ 5, dont 2 de fondations)
1. F1 calendrier, climats et météo quotidienne, avec tests.
2. F2 propriétés du sol par cellule, et le vent dans la balle et le planificateur, avec tests.
3. Rendu : ciel, soleil, pluie, vent visible.
4. Saisons, sol mouillé, flaques.
5. Prévisions à l'écran, effets sur l'humeur et l'affluence, fermeture du parcours.

---

## D2. Entretien du gazon

> **Décidé (2026-10-24)** : propositions acceptées, **sans mauvaises herbes ni maladies**. Ce que l'original attachait aux mauvaises herbes est à remplacer :
> - l'effet « No Dandelions » de 4 landmarks devient « gazon soigné » (usure réduite autour du landmark) ;
> - un golfeur mécontent ne fait plus naître de mauvaise herbe ; à la place, un golfeur en colère arrache une motte (usure, D2) ;
> - la difficulté accélère l'usure au lieu de multiplier les mauvaises herbes ;
> - le parterre de fleurs « envahi » devient un parterre fané faute d'arrosage ;
> - l'événement d'humeur « mauvaises herbes (−2) » devient « gazon abîmé (−2) ».
> Les idées 6 (maladies, Q6) et 12 (mauvaises herbes) ci-dessous sont retirées. Effort : environ 5 lots au lieu de 6.

### Idées concrètes
1. **Un état par cellule** : santé, hauteur de l'herbe, humidité et tassement, soit 4 octets. Pour ~100 000 cellules, 0,4 Mo.
2. **L'herbe pousse, on la tond.** Elle pousse selon la saison et l'humidité ; la tonte la ramène à la hauteur visée. **La hauteur règle le roulement** : un green tondu à 3 mm roule vite, à 5 mm il roule lent. C'est la vitesse des greens des vrais parcours (le « stimp »), affichable.
3. **Le fairway ferme se mérite.** Un fairway sec et tassé rebondit et roule plus. Le « Firm Fairway » peint de l'original devient un état obtenu par l'entretien.
4. **Les sentiers d'usure.** Là où les golfeurs marchent sans chemin, l'herbe s'use et un sentier brun apparaît. Ça montre au joueur où poser ses chemins : c'est un retour visuel direct, que l'original n'avait pas.
5. **Mottes et marques de balle.** Les coups arrachent des mottes sur les fairways et les départs, les balles marquent les greens. Le personnel répare. Un départ usé se soigne en déplaçant les marques, ce qui donne une utilité de plus aux départs multiples (B1).
6. **Le drapeau change de place.** On le déplace chaque jour pour répartir l'usure du green. Une position difficile met plus à l'épreuve l'imagination des golfeurs.
7. **Des consignes plutôt que de la microgestion.** Par type de zone, trois réglages : greens rapides, normaux ou lents ; fairways fermes, normaux ou tendres ; rough court, moyen ou épais. Le « Deep Rough » de l'original devient une hauteur de rough. Le personnel applique les consignes seul.
8. **Le jardinier reçoit de vraies tâches** : tondre (les greens chaque jour, les fairways trois fois par semaine), ratisser les bunkers (un bunker non ratissé donne un mauvais lie), réparer les mottes, arroser. Ses compteurs de l'original deviennent des tâches.
9. **L'arrosage.** On choisit un réglage d'arrosage automatique, ou l'on pose des arroseurs comme aménagement. L'eau coûte de l'argent, et la canicule (D1) impose des restrictions.
10. **La préparation d'un tournoi** devient réelle : on monte la vitesse des greens et la hauteur du rough pour la semaine. Le parcours est plus dur et s'use plus vite.
11. **Un hangar d'entretien**, nouveau bâtiment : il abrite les tondeuses, et ses niveaux donnent de meilleures machines (plus rapides, qui tassent moins).
12. **Les mauvaises herbes de l'original s'y rangent** : elles poussent sur une herbe malade.

### Questions concrètes
| # | Question | Ma proposition |
|---|---|---|
| Q1 | Firm Fairway et Deep Rough restent-ils des surfaces à peindre, ou deviennent-ils des réglages d'entretien ? | réglages : le pinceau dessine la forme, l'entretien règle la hauteur. Ça libère aussi deux places dans le shader |
| Q2 | Sentiers d'usure visibles ? | oui, c'est le meilleur retour visuel de la boucle |
| Q3 | Un hangar d'entretien comme nouveau bâtiment ? | oui, débloqué tôt (trou 2 ou 3) |
| Q4 | Consignes pour tout le parcours, ou par trou ? | pour tout le parcours en première version ; par trou plus tard (lié à la question mise de côté) |
| Q5 | Arroseurs à poser, ou arrosage automatique seulement ? | automatique en première version, arroseurs plus tard |
| Q6 | Maladies du gazon ? | plus tard |

### Effets sur les autres boucles et features
- **Météo (D1)** : humidité, sécheresse, croissance selon la saison.
- **Tournois avec cut (B18)** : préparation et usure accélérée.
- **Plusieurs départs (B1)** et positions de drapeau : outils contre l'usure.
- **Personnel** : le jardinier de l'original est refondu ; le technicien qualifié travaille plus vite et abîme moins.
- **Économie** : l'eau, les machines et le personnel deviennent des postes de dépense. C'est le « budget d'entretien » de la piste B8, encore à trier.
- **Humeur et évaluation** : « le fairway est en mauvais état » rejoint la soixantaine d'événements de l'original.
- **Scénarios (D7)** : « remettre en état un club négligé ».
- **Écologie (D8), plus tard** : l'eau et les produits deviennent son levier.
- **Champs de flux, règle importante** : l'usure ne change jamais le coût de marche. Sinon chaque pas de golfeur invaliderait les champs de flux en cache, et R1 exploserait.

### Coût en code
- **Simulation (≈ 4 lots).**
  - `Turf/` : `TurfGrid` (tableaux par champ), `TurfUpdater` qui traite une tranche de la carte à chaque tick. Avec 1/200 de la carte par tick, ça fait ~500 cellules par tick, quelques microsecondes (estimation). La carte entière est revue toutes les 10 s de jeu.
  - **Propriétés du sol précalculées (F2)** : un tableau par cellule (roulement, rebond, frottement), mis à jour par la même tranche. La balle lit une case de tableau, comme aujourd'hui. Mémoire : ~1,2 Mo pour ~100 000 cellules.
  - Enregistrement de l'usure : un appel dans la marche (`World.Walk`) et un au départ d'un coup.
  - Tâches du personnel : une file de tâches par zone. Le personnel marche par champ de flux vers sa zone, puis se déplace localement à l'intérieur, pour ne pas créer une destination par tâche (R1).
  - Sauvegarde (F4) : l'état du gazon est le plus gros bloc, ~0,4 Mo brut, bien compressible.

### Coût en rendu
- **Shader v2 (F3)** : une troisième texture d'une texel par cellule (santé, hauteur, humidité, usure). Elle est remise à jour une fois par seconde au plus, soit ~0,4 Mo envoyés au GPU, ce qui est négligeable. Elle est lue une fois par pixel, contre 34 lectures de texture aujourd'hui pour le flou des surfaces : coût marginal.
- Couleurs : brunissement selon la santé, teinte plus claire pour l'herbe rase, sentiers d'usure. Les mottes et les marques sont dessinées par un bruit piloté par l'usure, sans géométrie ni décalques (les nœuds `Decal` seraient trop nombreux).
- **Bandes de tonte** : le shader raye déjà le fairway le long de l'axe X (`stripe_width`). On stocke un sens de tonte par zone et l'on ajoute le damier des greens. Coût nul.
- Tondeuses : moins de 10 véhicules rigides, sans squelette, faits en `bpy`. Coût nul.
- Arroseurs : particules actives seulement quand elles sont à l'écran.
- **Limite des 8 surfaces** : le passage à une texture d'indices empêche le filtrage bilinéaire direct des surfaces. Le flou se fait alors en lisant les voisins à la main dans le shader. C'est une réécriture moyenne de `terrain.gdshader`, à valider sur les captures de la CI (« pas en cases »).

### Lots proposés (≈ 6, dont 2 de fondation F3)
1. `TurfGrid`, mise à jour par tranches, propriétés précalculées, avec tests (même graine, même somme ; un green tondu court roule plus loin).
2. Usure, mottes et marques ; croissance et tonte.
3. F3, partie 1 : texture d'indices, au-delà de 8 surfaces, captures comparées.
4. F3, partie 2 : texture d'état du gazon, bandes de tonte, sentiers.
5. Tâches et consignes du personnel, hangar d'entretien.
6. Surcouche « état du gazon » à l'écran et préparation des tournois.

---

## D5. Ligues de membres

> **Décidé (2026-10-24)** : propositions acceptées. Votre pro joue la ligue **en classement** (Q5 modifiée). Il commence dans la division la plus basse, parce que ses statistiques de départ sont faibles, et cherche à monter. **Seuls ses scores de ligue comptent pour son handicap.** Les catégories par handicap (idée 4) deviennent donc des **divisions avec montées et descentes** en fin de saison. Proposition par défaut : classement en brut dans chaque division, pour qu'une montée récompense un vrai progrès ; le handicap reste affiché.

### Idées concrètes
1. **Le handicap (B10) sert de base.** On le calcule comme dans la réalité, en simplifié : moyenne des meilleurs écarts sur les dernières parties, rapportés à la difficulté du parcours.
2. **La difficulté du parcours est mesurée par la simulation.** Un golfeur fictif « scratch » joue chaque trou à son ouverture, comme le faisait déjà l'original pour calculer le par et le dogleg. On en tire une note de difficulté (« course rating » et « slope »), affichable comme sur une vraie carte de score.
3. **Une ligue par semaine** (« la ligue du mardi »), ouverte aux membres. Formats : stroke play en net, stableford ; puis match play à élimination et scramble par équipes (B20).
4. **Des catégories par handicap** (A, B, C), pour que chacun ait une chance.
5. **Une saison et une finale** : le classement court d'avril à septembre, la finale tombe le dernier mois, et le trophée entre dans la vitrine du club.
6. **Des rivalités.** Deux golfeurs souvent proches au classement deviennent rivaux : leur humeur monte quand ils se battent et baisse quand l'un domine. C'est la piste B12, encore à trier, et un déclencheur d'histoires.
7. **La fidélité.** Un membre inscrit à une ligue démissionne moins vite.
8. **Le jour de ligue réserve les premiers départs du matin.** Les autres golfeurs attendent un peu plus : c'est la tension de la boucle.
9. **Votre pro peut s'inscrire** hors classement, pour le plaisir et pour progresser.

### Questions concrètes
| # | Question | Ma proposition |
|---|---|---|
| Q1 | Handicap réaliste et visible (valeur de 0 à 54, note du parcours) ou plus abstrait ? | réaliste : c'est un vrai marqueur de jeu de golf moderne, et la sim le calcule seule |
| Q2 | Formats en première version ? | stroke play net et stableford ; match play et scramble ensuite |
| Q3 | Le jour de ligue bloque-t-il des départs (une vraie feuille de départs), ou passe-t-il simplement en priorité dans la file du pavillon ? | priorité dans la file en première version ; une feuille de départs complète est une boucle à part |
| Q4 | Une ligue par club ou plusieurs ? | une ligue avec catégories par handicap, puis une ligue junior avec l'école de golf (D6) |
| Q5 | Votre pro peut-il jouer la ligue ? | oui, hors classement |

### Effets sur les autres boucles et features
- **Handicap et mémoire (B10)** : la ligue rend le handicap visible et utile.
- **Tournois avec cut (B18)** et **formats (B20)** : même moteur de score (F5) et même composant de classement.
- **Adhésion** : un nouvel argument de fidélité, qui rejoint les niveaux Member à Platinum.
- **Histoires et relations (B12)** : les rivalités les nourrissent.
- **Météo (D1)** : une soirée reportée. **Entretien (D2)** : plus d'usure le jour de ligue.
- **Scénarios (D7)** : « fonder une ligue de 40 membres ».
- **Évaluation SGA** : la note de difficulté du parcours peut s'y ajouter comme indicateur.

### Coût en code
- **Simulation (≈ 3 lots).**
  - F5 moteur de score : carte de score, brut, net, stableford, classement. Partagé avec les tournois.
  - Handicap et note du parcours. **Point de coût** : jouer le golfeur fictif 50 fois par trou à ~4 coups de ~15 ms donnerait ~3 s par trou (estimation). Trois parades : faire le calcul sur un fil en arrière-plan (la sim est pure, on lui passe une copie du terrain), réduire l'échantillonnage du planificateur pour ce calcul, ou le recalculer seulement à l'ouverture d'un trou et après une retouche, jamais en continu.
  - Calendrier de ligue sur F1, inscriptions, fidélité, rivalités.
- **Aucun golfeur en plus** : un jour de ligue amène les membres habituels, R1 ne bouge pas.

### Coût en rendu
- Écran de classement et carte de score (F7) : un tableau réutilisé par les tournois.
- Un tableau d'affichage près du pavillon et une vitrine des trophées (objets `bpy`). Coût nul.

### Lots proposés (≈ 4, dont 1 de fondation F5)
1. F5 moteur de score, avec tests.
2. Handicap des golfeurs et note du parcours en arrière-plan.
3. Ligue : calendrier, formats, catégories, finale, fidélité.
4. Écrans : classement, carte de score, annonce de la finale.

---

## D7. Scénarios et tutoriel

> **Décidé (2026-10-24)** : **pas de scénarios pour l'instant.** On garde seulement un **tutoriel très simple** de la boucle de construction et d'ouverture d'un trou : peindre un départ, un fairway et un green, poser le trou, l'ouvrir, regarder la première paire le jouer. C'est environ 2 lots : des étapes déclenchées par les actions du joueur, et une mise en évidence des éléments d'interface (ils doivent avoir un identifiant stable, à prévoir dans P3). La fiche ci-dessous reste comme référence pour plus tard. La sauvegarde (F4) reste nécessaire au jeu, indépendamment.

### Idées concrètes
1. **Un scénario est un fichier JSON** qui donne : la carte de départ (générée à partir d'une graine et de paramètres, ou une sauvegarde faite à la main), l'argent, les bâtiments débloqués, des modificateurs de règles (pas d'eau à peindre, mauvaises herbes deux fois plus rapides, année de sécheresse), des objectifs datés et des conditions d'échec.
2. **Objectifs à une, deux ou trois étoiles.** Par exemple : 9 trous ouverts avant la 3e année (une étoile), plaisir au-dessus de 7 (deux), 20 membres Gold (trois).
3. **Des objectifs lisibles par la machine.** Un registre de mesures (trous ouverts, argent, plaisir, membres par niveau, place en tournoi…) que les objectifs combinent. Les objectifs sont vérifiés à chaque fin de mois.
4. **Dix scénarios de départ proposés** :
   1. « Premier trou » (tutoriel 1) ;
   2. « Neuf trous rentables » ;
   3. « Le club négligé » : remettre en état un parcours abîmé (D2) ;
   4. « Links dans le vent » (D1) ;
   5. « L'île » : peu de terrain, de l'eau partout ;
   6. « Sécheresse » (D1 et D2) ;
   7. « Le désert » : beaucoup d'argent, des golfeurs difficiles ;
   8. « La ligue du mardi » : 40 membres inscrits (D5) ;
   9. « Le trou signature » : un trou dans le Top 100 ;
   10. « Le championnat » : accueillir un tournoi avec cut (B18).
5. **Le tutoriel est une suite de quatre ou cinq scénarios guidés.** Des étapes déclenchées par les actions du joueur (« peins un green ») et des éléments d'interface mis en évidence. Votre pro sert de guide, comme le conseiller de l'original.
6. **Chaque scénario est aussi un test.** On enregistre une partie gagnante (le journal des commandes du joueur, F4). La CI la rejoue en accéléré dans `dotnet test` et vérifie que le scénario reste gagnable après un changement d'équilibrage. La simulation déterministe rend ça possible, et c'est un gain pour tout le développement.
7. **Un éditeur dans le bac à sable** : un bouton « Enregistrer comme scénario » et un formulaire d'objectifs.
8. **La progression est gardée dans le profil** : étoiles et scénarios débloqués.

### Questions concrètes
| # | Question | Ma proposition |
|---|---|---|
| Q1 | Ces dix scénarios, ou as-tu d'autres thèmes en tête ? | partir de ces dix, trier en séance |
| Q2 | Cartes faites à la main ou générées ? | générées avec une graine, puis retouchées à la main et sauvegardées |
| Q3 | Tutoriel imposé au premier lancement, ou proposé ? | proposé, avec un rappel discret |
| Q4 | L'éditeur de scénarios ouvert aux joueurs, ou outil interne ? | interne d'abord, ouvert avec le partage (D11) |
| Q5 | Votre pro comme voix du tutoriel ? | oui, il reprend le rôle du conseiller de l'original |

### Effets sur les autres boucles et features
- **Tutoriel (A6)** : il est fait du même moteur.
- **Sauvegarde (R7)** : c'est un prérequis, puisque l'état de départ d'un scénario est une sauvegarde.
- **Partage (D11), plus tard** : un scénario se partage comme un parcours.
- **Toutes les autres boucles** fournissent du contenu de scénario (D1, D2, D5, tournois).
- **Bac à sable** : même moteur, sans objectifs.
- **UI (F7)** : panneau d'objectifs, écran de victoire, mise en évidence d'éléments. Les éléments d'interface doivent donc avoir un identifiant stable, à prévoir dans P3.

### Coût en code
- **Simulation (≈ 4 lots).**
  - F4 sauvegarde : sérialiser le terrain, le gazon, les golfeurs, l'économie, le calendrier et les états du hasard. Test clé : sauver, recharger, avancer de N ticks, et retrouver la même somme de contrôle qu'une partie jamais interrompue. **Chaque nouveau module ajoute sa part de sauvegarde et son test** : c'est un coût récurrent de quelques heures par module.
  - F4 journal des commandes : toute action du joueur passe par une commande enregistrée. Les retouches de C01 (`World.Edits.cs`) en sont le début.
  - Moteur de scénario : objectifs, registre de mesures, modificateurs.
  - **Point de coût des tests de scénario** : trois années de jeu, c'est environ 43 000 ticks (≈ 1 780 ticks par mois). La marche est négligeable, mais des centaines de parties à ~72 coups planifiés à ~15 ms prendraient des dizaines de minutes (estimation). Les tests longs utiliseront le résolveur de coups simplifié qui existe déjà (`StubShotResolver`), et le vrai planificateur seulement sur des scénarios courts.
- **Tutoriel (≈ 2 lots)** : étapes, déclencheurs, mises en évidence.

### Coût en rendu
- Écrans de choix, d'objectifs et de victoire (F7). Vignettes produites par l'outil de captures de la CI.
- Rien de spécifique dans la 3D.

### Lots proposés (≈ 6, dont 2 de fondation F4)
1. F4 sauvegarde v1, avec le test de rechargement.
2. F4 journal des commandes et rejeu.
3. Moteur de scénario, deux scénarios et leurs tests de rejeu en CI.
4. Écrans de scénario et progression du profil.
5. Tutoriel : étapes et mises en évidence.
6. Les huit autres scénarios (contenu), au fil des boucles qui les rendent possibles.

---

## D12. Mode photo et caméra libre (avec A9)

> **Décidé (2026-10-24)** : **pas de mode photo pour l'instant.** On le laisse prêt sans l'activer : la caméra à modes (`CameraRig`, lot 1 ci-dessous) prévoit un mode « photo » désactivé. Ça ne coûte rien de plus, puisque ce lot est nécessaire de toute façon pour la caméra libre (A9) et le swing contrôlable (B17). Les lots 6 (mode photo, caméra de télévision) et l'album sont reportés. La caméra libre (A9) et les niveaux de détail restent prévus, après C02.

### Idées concrètes
1. **Une caméra à modes** :
   - *Iso* : la vue actuelle, orthographique, par défaut ;
   - *Libre* : perspective à la Cities: Skylines, déplacement au clavier et au bord de l'écran, orbite à la souris, inclinaison de 10° à 89°, hauteur de 2 m à 600 m, qui ne passe jamais sous le relief (`HeightField.Sample`) ;
   - *Suivi* : la caméra suit un golfeur, ou suit la balle en vol comme à la télévision ;
   - *Photo* : la caméra libre, avec les réglages de photo.
2. **Passage fluide entre iso et perspective.** Passer de l'orthographique à la perspective en douceur est délicat. Un court fondu, ou un travelling compensé, suffit.
3. **Le mode photo** :
   - le temps de la simulation s'arrête ;
   - réglages : champ de vision, profondeur de champ et mise au point (`CameraAttributesPractical`), exposition, heure du jour et météo *pour la photo seulement* (la simulation n'est pas touchée) ;
   - filtres de couleur (table de correction de l'`Environment`) ;
   - interface et noms masqués ;
   - rendu en 2× ou 4× par un `SubViewport` pour des images en 4K ;
   - photos enregistrées dans un album, en jeu.
4. **La caméra de la balle.** Sur un coup exceptionnel, une caméra de télévision suit la balle. C'est le SimFoto de l'original, en mouvement.
5. **La visite à pied** à 1,70 m : plus tard (question Q2), parce que la vue de près révèle les assets.
6. **L'album rejoint les accomplissements** : les photos Polaroid du tableau en liège de l'original viennent de là.

### Questions concrètes
| # | Question | Ma proposition |
|---|---|---|
| Q1 | La vue iso reste-t-elle la vue par défaut (ADR 0001), avec la caméra libre comme mode, ou passe-t-on en perspective par défaut ? | iso par défaut en première version : elle masque les niveaux de détail et garde la lisibilité de gestion |
| Q2 | La visite à pied dès la première version ? | plus tard, après C02 et la preuve P2 (personnages) |
| Q3 | Pause pendant le mode photo ? | oui |
| Q4 | Caméra de télévision automatique sur les grands coups ? | oui, désactivable comme SimFoto |
| Q5 | Partage des photos ? | fichiers sur le disque en première version ; capture Steam plus tard |

### Effets sur les autres boucles et features
- **Échelle du monde (C02)** : une caméra libre montre tout de suite si un arbre ou un golfeur est à la mauvaise taille. C02 doit passer avant.
- **Qualité des assets (R2, R5)** : de près, chaque modèle se juge. Le mode photo est une exigence de qualité autant qu'une feature.
- **Swing contrôlable (B17)** : il demande une caméra derrière le golfeur. Même système de caméra, donc du code partagé.
- **Analyse de coup (B4)** : une vue en perspective le long de la ligne de jeu.
- **Météo (D1)** et **golf de nuit (D13)** : ce sont eux qui font les belles photos.
- **Hors de la propriété** : en vue rasante, on voit au-delà des limites du terrain. Il faut un horizon : un anneau de relief simplifié autour de la carte et un décor de fond par thème (montagnes, mer, désert).
- **Marketing** : les captures Steam viendront de là.

### Coût en code
- Caméra à modes (≈ 1 lot) : `IsoCamera.cs` devient un `CameraRig` à états, avec des actions dans `InputMap` (configurables) au lieu des touches codées en dur.
- Caméra libre et suivi (≈ 1 lot) : déplacement, orbite, inclinaison, garde au-dessus du relief, lissage.
- Mode photo (≈ 1 lot) : interface, rendu haute résolution, album.

### Coût en rendu : le vrai poste
- **Le pire cas** est une vue rasante sur tout un 18 trous. Sans niveaux de détail :
  - terrain : à ~2 sommets par cellule et ~100 000 cellules, de l'ordre de 800 000 triangles (estimation) ;
  - arbres : leur nombre dépend de C02. Avec 20 000 arbres de 340 triangles, ça fait ~7 millions de triangles (estimation), trop pour le Steam Deck et lourd pour une RTX 2080.
- **Niveaux de détail du terrain (≈ 1 lot)** : deux résolutions par bloc de 32 × 32 cellules. Un `ArrayMesh` accepte des LOD que Godot choisit selon la taille à l'écran.
- **Niveaux de détail des arbres (≈ 1 lot)** : couper le `MultiMesh` unique en un `MultiMesh` par bloc (utile aussi pour ne reconstruire que les blocs retouchés), avec des distances de visibilité (`visibility_range`) et, au loin, un arbre simplifié en quelques plans (« imposteur »).
- **Horizon (≈ 1 lot)** : anneau de relief simplifié et décor par thème.
- **Ombres** : en perspective, la lumière directionnelle a besoin d'ombres en cascades (4 tranches, ~400 m), soit 1 à 2 ms sur une RTX 2080 (estimation), à réduire sur le Steam Deck.
- **Mesure** : ajouter une scène « vue rasante » au banc de FPS (`FpsBench.cs`, `Mesurer FPS.bat`) pour mesurer ce pire cas sur la machine de Guillaume.

### Lots proposés (≈ 6, dont 3 de fondation F6)
1. `CameraRig` à modes et `InputMap`.
2. Caméra libre en perspective et suivi.
3. F6 : LOD du terrain.
4. F6 : LOD des arbres, un `MultiMesh` par bloc.
5. F6 : horizon et décor par thème ; scène « vue rasante » au banc de FPS.
6. Mode photo et caméra de télévision.

---

## Récapitulatif (après les décisions du 2026-10-24)

| Boucle | Lots | Dont fondations | Coût de simulation | Coût de rendu | Dépend de |
|---|---|---|---|---|---|
| D1 Météo et saisons, avec M1 à M9 retenus | 5 à 6 | F1, F2 | faible ; planification par vent fort à mesurer dans P1 | faible : ciel, soleil qui tourne, particules, paramètres de shader | |
| Arbres vivants (dans C02) | 4 | | faible : croissance mensuelle, index spatial des arbres | faible : `MultiMesh` par bloc, jeunes plants en `bpy` | C02, F1, F6 (`MultiMesh` par bloc) |
| D2 Entretien, sans mauvaises herbes | 5 | F3 | faible si la mise à jour est étalée et l'usure n'agit pas sur la marche | faible ; réécriture moyenne du shader (F3) | F1, F2 ; D1 conseillé |
| D5 Ligues à divisions, avec votre pro | 4 | F5 | faible ; note du parcours en arrière-plan | nul | B10 (handicap), F1, F7 |
| Tutoriel de construction | 2 | | nul | nul | F7 (P3) |
| Caméra à modes et LOD (A9, photo prête) | 5 | F6 | nul | **élevé** : LOD, horizon, ombres en cascades | C02, F7 |
| **Total** | **≈ 25 à 26** | | | | |

Les scénarios (D7) et le mode photo (D12) sortent de la première version ; la sauvegarde (F4, 2 lots) reste à faire pour le jeu, hors de ce total.

## Ordre proposé
1. **P1 (parcours chargé)** reste la première preuve : la météo ajoute du travail au planificateur.
2. **C02, devenu « Arbres et échelle du monde »**, avec les arbres vivants : il fixe le nombre d'arbres, donc le besoin de niveaux de détail.
3. **F1 et F2**, puis **D1** avec les côtés positifs retenus.
4. **D2** avec **F3** : il touche le shader de terrain, à valider sur les captures.
5. En parallèle, sur des zones de code disjointes : **B10 et F5**, puis **D5** ; la preuve **P3** (UI), puis le **tutoriel**.
6. **Caméra à modes et LOD (F6)**, puis la caméra libre (A9).
