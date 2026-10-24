# Recensement des features de Sid Meier's SimGolf (2002)

Inventaire de ce que contient le jeu original, sans aucune décision sur Golferoo. Rédigé le 2026-10-24.

## Comment lire ce document

**Sources**, citées entre crochets à la fin des lignes :
- **[M]** le manuel imprimé (EA, 2002, 32 pages), tel que résumé dans `docs/MANUAL_NOTES.md` d'OpenSGolf. Le manuel ne donne aucun chiffre.
- **[E]** l'exécutable `golf.exe`, décompilé et décodé par le projet [OpenSGolf](https://github.com/crspytopgn/opensgolf) (fichiers `docs/DECODE_*.md`, `EXE_COSTS.md`, `PUBLISHER_EXE_NOTES.md`, `GAMELOGIC.md`). Ce sont les règles et les chiffres les plus fiables qu'on ait.
- **[Z]** les fichiers du jeu dans `sim_golf.zip` (version 1.0 avec le patch 1.02 et un no-CD), lus comme données, rien d'exécuté.
- **[X]** les sources externes : Wikipedia, GameSpot (test et news du patch), Giant Bomb, MobyGames, PCGamingWiki, TV Tropes. Les liens sont en fin de document.

**Fiabilité** : ✔ lu dans le code ou dans le manuel ; ≈ déduit (raisonnement ou mesure à l'œil) ; ? incertain ou contradictoire entre sources. Sans marque, c'est ✔.

L'argent de l'exe est stocké en unités de 100 $ ; ce document donne directement les dollars.

---

## 1. Fiche d'identité

- Développé par Firaxis Games (avec Maxis), édité par Electronic Arts, sorti le 23 janvier 2002 en Amérique du Nord et le 12 juillet 2002 en Europe. Windows uniquement : aucune source ne mentionne de version Mac. [X]
- Conception : Sid Meier et Bing Gordon. [X]
- Solo uniquement. [X]
- Vue isométrique en 800 × 600 fixe, terrain en tuiles. [X] [E]
- Ton : humour et voix en simlish à la *The Sims*, golfeurs avec traits, relations et histoires. Devise de conception affichée par le jeu : un trou doit « avoir l'air dur et se jouer facilement ». [X] [E]
- Patchs : 1.01 (échange web des parcours de championnat, correction d'un crash en tournoi) ; 1.02 (présent dans le zip) ; 1.03 (crash sur les têtes féminines dans l'écran de personnalisation). [X] [Z]

## 2. Structure de jeu et modes

### 2.1 Menu titre [M] [E]
Six entrées : Start New Game, Continue Saved Game, Play a Championship, Sandbox Mode, Select a Theme, Exit. Un clic sur le logo ouvre les crédits (≈).

### 2.2 Carrière (Start New Game) [M] [E]
- Choix de la **difficulté** : Easy, Moderate, Difficult, Impossible (0 à 3).
- Choix d'une des **16 propriétés** sur la carte du monde, puis on arrive directement sur le terrain avec un pavillon (clubhouse) déjà posé.
- Argent de départ : 100 000 $. Seules les quatre propriétés les moins chères sont abordables au départ.
- On peut plus tard **acheter une autre propriété** avec ses gains (carte du monde, F6 ou Shift+W) ; on ne peut alors plus revenir à l'ancienne. Un parcours acheté reste marqué sur la carte (épingle magenta).
- Pas de fin « gagnée » : la partie se termine si le conseil d'administration vous renvoie (trois fins d'année de suite en négatif). Les objectifs sont les accomplissements (section 13.5) et le classement Top 10 Designers.

### 2.3 Effets de la difficulté [E]
- Humeur de départ des golfeurs plus basse et événements négatifs plus forts (certains malus n'existent qu'au-dessus d'Easy).
- Golfeurs plus exigeants : un golfeur n'adhère qu'avec plus de points ; la patience en file d'attente baisse.
- Charges (salaires, entretien des trous) plus fréquentes : période de 1024 / (difficulté + 2) ticks.
- Plus de mauvaises herbes : elles poussent plus souvent et naissent plus souvent sous les golfeurs mécontents.
- Autant de « verrues » (château d'eau, antenne, pompe à pétrole) sur le terrain que le niveau de difficulté.
- Évaluation SGA plus stricte. Sur Easy et Moderate, trois accomplissements remplacent les trous Challenge, Heroic et Strategic par le premier dogleg droit, le premier dogleg gauche et le premier par 5.
- Sur Easy, la première année seulement, un conseiller (votre pro) donne des conseils de construction.

### 2.4 Sandbox [M] [E]
Argent illimité (le jeu stocke 1 000 000 $ ; une autre branche stocke davantage, ?), les 16 propriétés disponibles, tout le terrain possédé, tous les bâtiments débloqués, 14 landmarks disponibles. Pas de visiteurs VIP, pas de conseil d'administration, pas d'offre SGA.

### 2.5 Play a Championship [M] [E]
Mode « golf seulement » : on choisit la difficulté, un parcours sauvegardé « pour championnat » (`.cse`) et un pro (`.pro`), puis un tournoi démarre immédiatement contre un plateau de pros célèbres. Pas d'accomplissements ni de salaires ; on revient au menu à la fin. Les parcours se partageaient via un **échange web** sur le site officiel (patch 1.01). [X]

### 2.6 Thèmes (Select a Theme) [M] [Z]
Un thème change les personnages, les dialogues et les histoires, pas les règles. Le zip contient :

| Thème | Contenu |
|---|---|
| Standard | 10 histoires, 4 golfeurs spéciaux (`.glf` : Joe Pro, I.M. Picky, Ivana Richman, J.P. Bigdome), `progolfers.dta` (96 pros), `celebrities.dta` (21 célébrités) |
| More Stories | 27 histoires |
| Firaxis | 3 histoires, 15 personnages `.chr` (l'équipe Firaxis), 4 `.glf`, un pro `Golfmonkey.pro` |
| The Sims | 6 personnages `.chr` (Bob Newbie, Mortimer Goth, « The Donald »…) |
| Championship | dossier des pros (`Gary Golf.pro`) et parcours sauvegardés pour le mode championnat |

Un thème peut aussi fournir son propre `progolfers.dta`. Le format `.chr` (sprites et répliques, environ 40 Ko) n'est pas décodé.

## 3. Le monde

### 3.1 Les 16 propriétés [E] (table issue des données, non revérifiée dans le décompilé, ≈)

| Région | Parcours | Thème | Situation | Relief | Bonus annoncé |
|---|---|---|---|---|---|
| Monterey | Ocean's Edge | Parkland | côtier | vallonné | Scenic Cypress |
| San Diego | Dolphin Coast | Parkland | côtier | plat | Dolphins |
| Rocky Mtns. | Jurassic Springs | Parkland | intérieur | accidenté | Free Hotel |
| Las Vegas | Ace in the Hole | Desert | intérieur | plat | Fun Fun Fun |
| Phoenix | Coyote Flats | Desert | intérieur | plat | Free Spa |
| Hawaii | Flamingo Shores | Tropical | côtier | vallonné | Scenic Waterfall |
| Oahu | Island Palms | Tropical | île | plat | Japanese Garden |
| Nova Scotia | Windy Point | Links | côtier | vallonné | Scenic Lighthouse |
| Northeast | Ravenwood Farms | Parkland | intérieur | vallonné | Civil War Battlefield |
| Carolina | Christmas Pines | Parkland | intérieur | plat | Free Putting Green |
| Ireland | County Kincaide | Links | intérieur | accidenté | Leprechauns |
| Scotland | Harold's Keep | Links | côtier | plat | Free Castle |
| Wales | Thistle Runes | Links | île | vallonné | Stonehenge |
| Spain | Sangria Bay | Desert | côtier | vallonné | Scenic Vineyards |
| Florida | Ocean Grove | Tropical | côtier | plat | Free Pro Shop |
| Jamaica | Scorpion Cove | Tropical | intérieur | vallonné | Scenic Statues |

- À chaque partie, les 16 emplacements de la carte tirent un site au hasard (le premier est toujours Parkland). Prix des emplacements : 50 000, 60 000, 70 000, 80 000, 120 000, 150 000, 200 000, 250 000, 300 000, 400 000, 500 000, 600 000, 700 000, 800 000, 900 000 et 1 000 000 $. [E]
- Superficie (acres) croissante avec le prix, plus grande à l'intérieur des terres, plus petite sur une île. [E]
- Carte du monde : épingle jaune pour une propriété abordable, grise sinon, magenta si déjà achetée. [M]
- L'effet de la colonne « bonus » dans le code n'est pas identifié (?).

### 3.2 Génération du terrain [E]
- Carte de 50 × 50 tuiles. Le terrain est généré à partir du site : relief (plat, vallonné, accidenté), rivage sur un côté (côtier) ou tout autour (île), étangs, broussailles, bois, marais, eau peu profonde.
- Des éléments scéniques propres au site (rochers, ponts naturels, ossements, vignes, rhododendrons…) et des landmarks pré-placés.
- Le terrain possédé au départ est un carré centré ; le reste est « à vendre » (affiché en noir) jusqu'à son achat.
- Les bords de la propriété s'affichent comme une falaise texturée.

### 3.3 Acheter du terrain [M] [E]
- Le terrain est découpé en 9 parcelles de 16 × 16 tuiles. Une parcelle n'est achetable qu'après l'accord du commissaire du comté (VIP, section 11) ; l'écran Buy Land s'ouvre alors.
- Le prix dépend des tuiles encore à vendre et augmente fortement à chaque achat (2^n par tuile tirée).

### 3.4 Le temps [E]
- Calendrier de **8 mois par an** (mars à octobre), 30 jours affichés par mois, début en 2001.
- Une partie avance en ticks d'environ 87 ms ; un mois dure environ 89 secondes réelles. Pause (Shift+P). Pas de contrôle de vitesse trouvé dans l'original.
- Un tournoi de 18 trous dure plus de deux années de jeu en temps simulé : il n'y a pas d'accélération.

## 4. Construction du parcours

### 4.1 Terrains peignables [M] [Z] [E]
Menu Terrain : Tee, Green, Fairway, Firm Fairway, Rough, Deep Rough, Sandtrap, Waste Bunker, Pot Bunker, Stream, Brush, Rocks, Water, Trees, Scenic Trees, plus les chemins.

| Terrain | Comportement |
|---|---|
| Fairway | surface de référence |
| Firm Fairway | la balle rebondit plus haut et roule plus loin |
| Rough / Deep Rough | l'herbe gêne le contact ; le deep rough plus encore |
| Sandtrap | appui incertain, club non posé |
| Waste Bunker | gazon plus long, l'élévation compte |
| Pot Bunker | dépression profonde |
| Brush | difficile d'en sortir |
| Rocks | dévient la balle au hasard |
| Stream / Water | balle irrécupérable (pénalité) |
| Trees | dévient les longs coups et retiennent les balles |
| Scenic Trees | idem, et plaisent aux golfeurs |

- L'exe connaît 23 types de surface avec, pour chacun : rebond, friction, pénalité, coût de peinture, effort de marche et classe. [E]
- **Noms par thème** : en Desert, Rough devient « Desert », Deep Rough devient « Rough » et le Stream un « Ravine » ; en Links, le Stream est un « Burn » et le Brush du « Gorse ». Les arbres changent aussi : Parkland arbre, pin, palmier ; Desert arbre de Josué, cactus, palmier. [M] [Z]
- Chaque bouton de terrain affiche une info-bulle avec prix, description, visage d'humeur et jauges de rebond et de roulement. [E]
- Coût par tuile différent par terrain (l'eau est la plus chère) ; l'annulation rend le montant payé. [E]
- Les arbres plantés poussent en une seconde environ (animation de croissance) ; la taille affichée dépend des voisins. [E]
- Les mauvaises herbes changent de sprite par thème : pissenlit (Parkland, Links), flaque de pétrole (Desert), herbe sèche (Tropical), plus du chiendent sur les zones jouées. [E]

### 4.2 Relief [M] [E]
- Monter ou descendre un sommet, un carré ou une zone (raccourcis `=` et `-`, mode E pour la grille d'élévation). Hauteurs bornées ; les propriétés accidentées descendent plus bas.
- Les pentes créent des murets de soutènement, les cascades se forment entre plans d'eau à niveaux différents.
- **Analyze Golf Shot** (touche `/`) : montre les trajectoires possibles depuis un point, pour un golfeur complet et pour des golfeurs sans longueur, sans précision ou sans imagination.

### 4.3 Créer un trou [M] [E]
- Un trou = un tee (une tuile) + un green avec le trou (outil Green). On construit un trou à la fois, 18 au maximum, et il faut **ouvrir** un trou (touche H ou clic sur son drapeau) avant de construire le suivant.
- Une **ligne blanche** relie tee et trou et montre la ligne que tenteront la plupart des golfeurs.
- Pendant la construction, un golfeur fictif « joue » le trou pour calculer : orientation du tee, longueur réelle, dogleg gauche ou droite, montée ou descente, et trois piquets de distance (100, 150, 200 yards) sur le fairway.
- **Par** calculé selon la longueur : 2 jusqu'à 50 yards, 3, 4 au-delà de 249, 5 au-delà de 474, 6 au-delà de 625. Un dogleg ajoute 25 yards ; chaque niveau de Driving Range en retire 25 sur les trous longs. Le joueur ne choisit pas le par.
- Marques de tee rouges (par 3), blanches (par 4) ou bleues (par 5+). Le drapeau flotte selon le plaisir moyen du trou.
- **Tricky green** : Tab avec l'outil Green donne un green piégeux (texture dédiée).
- Effacer le tee ou le trou d'un trou ouvert le ferme. La **carte de routage** (Shift+R) permet de renuméroter les trous par clic droit.
- Chaque ouverture annonce le trou (montée, descente, dogleg) et peut débloquer un bâtiment (section 5.2).

### 4.4 Aménagements (panneau Amenities) [M] [E]
Ball Washer, Pathway, Home Site, Benches, Scenic Bridge, Landmarks, Flower Bed, Scenic Trees, et Undo. Il n'existe ni fontaine à eau, ni poubelle, ni toilettes dans le jeu.

| Aménagement | Prix | Effet |
|---|---|---|
| Chemin | 100 $ la tuile | les golfeurs le préfèrent et y marchent plus vite ; sert à relier les bâtiments au pavillon ; un chemin mal relié s'affiche en terre ; sur l'eau, il devient un pont |
| Banc (5 modèles) | 200 $ | repose les golfeurs fatigués (humeur +1 s'ils l'étaient vraiment) |
| Parterre de fleurs (5 modèles, se raccordent entre eux) | 500 $ | plaît aux golfeurs qui le voient ; un parterre envahi de mauvaises herbes déplaît |
| Lave-balles | 5 000 $ | à moins de 3 tuiles du tee, réduit d'un tiers l'erreur de direction du coup suivant ; peut ralentir le jeu |
| Pont décoratif (8 modèles) | 10 000 $ | pont sur l'eau, plaît aux golfeurs |
| Arbres décoratifs (saule, prunier, cornouiller, érable du Japon, cyprès, pêcher…) | ≈ 2 500 $ | plaisent aux golfeurs |
| Landmark | 5 000 à 20 000 $ | voir section 6 |
| Home Site | voir section 7 | vente immobilière |

- Tab fait tourner un objet avant de le poser. Clic droit deux fois pour supprimer. Les petits objets sont remboursés à la suppression, les bâtiments jamais. [M] [E]

## 5. Bâtiments

### 5.1 Règles générales [M] [E]
- Le **pavillon (clubhouse)** est donné avec chaque propriété ; on peut le déplacer en payant.
- Un bâtiment n'est **opérationnel que relié au pavillon par un chemin continu** (sinon pas de revenu et un avertissement mensuel à partir de Moderate).
- Chaque type de bâtiment (du Putting Green à l'Airstrip) n'existe qu'**une fois**. Le reconstruire fait monter son **niveau** (0, 1, 2) : c'est l'amélioration, au coût de 1,5 fois le prix. Les améliorations ne sont possibles qu'au-delà de 9 trous.
- Les bâtiments tombent du ciel à l'endroit choisi. [X]
- Le terrain sous l'emprise ajoute un coût de défrichage (arbres, rochers, eau). Le bâtiment arrête la balle qui le touche.

### 5.2 Liste, déblocage et effets [M] [E]

| Bâtiment (générique) | Nom par thème | Débloqué à l'ouverture du trou | Prix | Effet | Revenu par visite |
|---|---|---|---|---|---|
| Putting Green | | 2 | 10 000 $ | améliore l'imagination (le putting) des golfeurs qui en ont | 400 $, 800 $ au niveau 2 |
| Snack Bar | Pub (Links) | 3 | 15 000 $ | nourrit les golfeurs affamés | 500 $ |
| Pro Shop | | 4 | 20 000 $ | les golfeurs précis améliorent leur équipement (précision) | 600 $ / 1 000 $ |
| Tennis Court / Swim Club (?) | Stable (Links), Spa (Desert), Swim Club (Tropical) | 5 | 30 000 $ | les golfeurs commencent de meilleure humeur | aucun |
| Driving Range | | 7 | 25 000 $ | les longs frappeurs gagnent en longueur ; raccourcit le par des trous longs | 800 $ / 1 200 $ |
| Cart Garage | | 8 | 40 000 $ | voiturettes : jeu plus rapide | aucun |
| Marina | Church (Links), Helipad (Desert) | 9 | 100 000 $ | valeur des home sites + un tiers par niveau, chance d'attirer une célébrité | aucun |
| Resort Hotel | | 11 | 250 000 $ | les golfeurs se fatiguent moins en fin de parcours (endurance) | aucun |
| Airstrip | Castle (Links), Casino (Desert), Theme Park (Tropical) | 12 | 500 000 $ | augmente le green fee de chaque trou (cumulatif) | aucun |

- Les trous **6, 10 et 18** ne débloquent pas de bâtiment mais font monter le **rang** du club : Municipal (moins de 6 trous), Daily Fee ou Golf Club (6 à 9 : personnel qualifié), Country Club (10 à 17 : améliorations des bâtiments), Championship (18). [E]
- Portée de visite : Snack Bar 8 tuiles, Putting Green et Pro Shop 5, Driving Range 6. [E]
- Le jeu nomme ce bâtiment de niveau 5 « Swim Club » dans sa table et « Tennis Court » dans le manuel ; Wikipedia parle de court de tennis. Le dossier Desert contient des sprites de tennis jamais utilisés. ?

## 6. Landmarks [M] [E]

- 16 landmarks achetables, 3 verrues que seul le générateur place :

| # | Landmark | Effet (n° mod 4) |
|---|---|---|
| 0 | Garden sundial | Happy Golfers |
| 1 | Traditional barn | No Dandelions |
| 2 | Civil War cannon | Skill Upgrade |
| 3 | Ancient Stonehenge rock | Happy Endings |
| 4 | Operating water mill | Happy Golfers |
| 5 | Unusual rock face | No Dandelions |
| 6 | Civil War statue | Skill Upgrade |
| 7 | New England lighthouse | Happy Endings |
| 8 | Peaceful Buddha | Happy Golfers |
| 9 | Dutch windmill | No Dandelions |
| 10 | Historic equestrian statue | Skill Upgrade |
| 11 | Easter Island head | Happy Endings |
| 12 | Exquisite pagoda | Happy Golfers |
| 13 | Hatteras lighthouse | No Dandelions |
| 14 | Ornate oriental house | Skill Upgrade |
| 15 | Dinosaur tar pit | Happy Endings |
| 16-18 | Water tower, radio antenna, oil pump | verrues : humeur −2 quand on les voit |

- Effets dans un rayon qui grandit avec le numéro (de 2,7 à 7,7 tuiles environ) : pensées heureuses ; pas de pissenlits (un golfeur mécontent ne fait plus pousser de mauvaise herbe) ; progression rapide des compétences de votre pro ; histoires qui finissent bien.
- Prix : (5 × n + 25) × 200 $, soit 5 000 $ à 20 000 $. Gratuit une fois s'il a été offert.
- **Obtention** : seuls certains sont disponibles au départ ; les autres sont offerts par l'héritière Ivana Richman ou débloqués par la fin heureuse d'une histoire (chaque histoire donne un landmark précis selon la première lettre de son fichier).
- Les landmarks, fleurs et arbres décoratifs sont aussi vus par les golfeurs : un coup d'œil donne une pensée positive.

## 7. Immobilier : Home Sites et célébrités [M] [E]
- L'outil Home Site pose un lot de 2 × 2 tuiles. Il faut plus de membres Silver ou mieux que de lots, et une **valeur de lot** suffisante.
- Valeur du lot : proximité de l'eau et des arbres, plaisir du meilleur trou voisin, trous Top 100 et Top 18 ; malus si proche d'un autre bâtiment, d'un trou peu amusant, trop près d'un green, d'un fairway ou du hors-limite, ou loin du parcours. La Marina l'augmente.
- Poser un lot le **vend aussitôt** au quart de sa valeur (revenu « Home Sites »). Le détruire coûte un rachat.
- La valeur évolue avec le temps : panneau « à vendre », puis maison en construction, puis maison.
- Une **célébrité** (21 dans `celebrities.dta` : parodies comme « Bubba Klinton », « Britney Aguilera », « Michael Nyke ») peut acheter une maison de vacances sur un lot de grande valeur. Elle y habite, se promène et fait son geste signature avec un court jingle. Ça ne rapporte rien mais plaît aux golfeurs (+1). [Z] [X]
- L'aide clavier liste Shift+B « sell building lot » (vendre un lot ; effet exact ?). La vue « Home Site Value » de la carte (Shift+R) montre la valeur de chaque lot.

## 8. Personnel [M] [E]

| Emploi de base | Version qualifiée | Rôle | Salaire de base / qualifié (par échéance) |
|---|---|---|---|
| Club Pro (greeter) | Celebrity | accueille et encourage les golfeurs (+1) | 300 $ / 700 $ |
| Ranger | Marshal | presse les golfeurs lents (ils marchent plus vite) | 200 $ / 300 $ |
| Groundskeeper | Turf (Lawn) Technician | arrache les mauvaises herbes, zone de travail plus large | 200 $ / 400 $ |
| Soda Vendor | Refreshment Consultant (Refresher) | vend des boissons aux assoiffés (+200 $ par vente) | 200 $ / 500 $ |

- 20 employés au maximum. Le personnel qualifié exige un club de rang Daily Fee (6 trous ou plus) ; il marche plus vite, travaille mieux et a ses propres animations.
- Chaque employé a un **poste** et une zone de travail autour (4 tuiles, 6 pour le jardinier, 8 pour le technicien). Actions : Move (déplacer le poste), Fire (licenciement 2 500 $), Rename. Fiche : date d'embauche, total payé, compteur d'actions (golfeurs accueillis, pressés, boissons servies, herbes arrachées).
- Votre **pro** (Gary Golf par défaut) est aussi un employé gratuit posté au pavillon, qui fait les quatre métiers.
- La vue « Employees » de la carte montre les zones couvertes.
- TV Tropes et MobyGames parlent de « security » ; aucun emploi de ce nom n'existe dans l'exe. [X] ?

## 9. Les golfeurs

### 9.1 Arrivée et parties [E]
- Seuls les golfeurs **invités** viennent : 12 au départ (trois de chacune de quatre classes de compétences), puis un de plus à chaque montée de niveau d'adhésion (un ami du nouveau promu).
- Ils attendent au pavillon (8 au plus) et partent **par paires** dès que le premier tee est libre. Un clic sur le pavillon permet de **choisir soi-même la paire** qui part.
- Ils jouent tous les trous ouverts dans l'ordre ; le plus loin du trou joue en premier ; ils attendent que la zone d'arrivée soit libre.

### 9.2 Compétences et profils [M] [E]
- Trois compétences principales : **Length, Accuracy, Imagination**. Elles déterminent les trous qui les mettent à l'épreuve.
- Chaque golfeur a un nom, un âge, un sexe, une situation familiale, des traits de personnalité et des goûts (« aime… ») affichés sur sa **fiche** (golfer card) : jauges d'humeur, d'énergie, de faim, de soif, carte de score du jour sur 18 trous, citation selon l'humeur.
- 152 golfeurs simulés au plus en même temps ; un vivier de 75 identités persistantes pour l'adhésion.

### 9.3 Besoins [M] [E]
- **Faim** et **soif** montent avec le temps (la faim plus vite en Tropical) ; au-delà d'un seuil, le golfeur se plaint (−1) et cherche un snack-bar ou un vendeur de soda.
- **Fatigue** : cherche un banc, monte plus vite sans chemin ou en pente ; le Resort Hotel la réduit, les voiturettes aussi.
- Avant de partir d'un tee, le golfeur fait des détours vers snack-bar, banc, lave-balles, putting green, pro shop ou driving range selon ses besoins et ses goûts, et paie.

### 9.4 Humeur, pensées et commentaires [M] [E]
- Humeur de −10 à +10, affichée rouge, jaune ou verte. Elle baisse un peu après chaque trou (plus vite selon la difficulté).
- Une soixantaine d'**événements** modifient l'humeur, chacun avec une pensée en bulle. Exemples : putt difficile réussi (+1), putt facile raté (−1), coup décevant (−2), pas d'endroit sûr où viser (−2), presque touché par la balle d'un autre groupe (−3), pas de chemin là où il veut marcher (−2), vue panoramique (+1), balle contre un arbre (−1), balle dans un obstacle (−1), voit une verrue (−2), jeu lent (−2), mauvaises herbes (−2), maison de célébrité (+1), aime ou n'aime pas ce trou (+1 / −2), décision stratégique intéressante (+1), voit un golfeur en colère (−3), animaux (+1), nouveau club (+1), bon coup mais n'a pas vu où la balle est allée (−2).
- Les petites contrariétés ne comptent que si elles se répètent ; un golfeur mécontent peut gâcher l'humeur de son partenaire.
- Un golfeur en dessous de zéro **abandonne** (un membre démissionne définitivement). Raisons : trop fatigué, assoiffé, affamé, attente trop longue, parcours à améliorer, clubs jetés dans le lac. Il peut piquer une **crise** qui contrarie ceux qui la voient.
- Un événement négatif peut faire naître une **mauvaise herbe** sous le golfeur.
- Les commentaires sont comptés par trou et par type : ils alimentent le rapport Player Comments et les stats de trou.
- La **première fois** qu'un type de réaction arrive, le jeu explique ce qu'il signifie (désactivable).
- Noms et pensées peuvent s'afficher au-dessus des golfeurs (Préférences, Shift+N).

### 9.5 Déplacements [E]
- Recherche de chemin : préfère les chemins, évite l'eau, cède le passage, se plaint de marcher hors chemin ou en pente raide.
- Voiturettes dès qu'un Cart Garage existe (plus rapides par niveau).

### 9.6 Le coup [E] [M]
- Le golfeur planifie chaque coup : portée maximale selon ses compétences et son lie, échantillonnage de points d'arrivée autour de la ligne du trou, score selon la proximité du trou, les obstacles et le risque du coup suivant.
- Choix du club (Driver, bois 3 et 4, fers 2 à 9, lob wedge, sand wedge, putter), crochet ou slice aléatoire selon les compétences, draw, fade, backspin, coup bas sous les arbres.
- Vol de balle avec courbe, arbres et bâtiments, rebond et roulement selon le terrain et la pente, trou, chute dans l'eau, hors-limite, pénalités. Réaction du golfeur selon l'endroit où la balle s'arrête.
- **SimFoto** : photo automatique des coups exceptionnels (désactivable). [X] [E]

## 10. Adhésion (membership) [M] [E]
- Niveaux : Visitor, **Member, Silver, Gold, Platinum**. Une démission est définitive.
- Après une partie, le golfeur gagne des points (trous qu'il a aimés, bonne humeur, niveau du bâtiment qui attire les membres) ; il monte d'un niveau à 2, 4, 8 puis 16 points. Chaque promotion invite un ami.
- Giant Bomb ne cite que trois niveaux (basic, silver, gold) ; le code en a quatre avec Platinum. [X]
- Les Gold paient +200 $ par trou, les Platinum +500 $. Les Silver et plus permettent les home sites.
- Écran **Membership Roster** (F9) : effectifs par niveau, démissions, liste d'attente. Message « nouveau membre » (désactivable). Le jeu prévient si l'adhésion décline.

## 11. Visiteurs spéciaux (VIP) [M] [E] [Z]
Annoncés « is playing your course today », ils jouent avec une paire et rendent un verdict à la fin :

| Visiteur | Fichier | S'il est satisfait (humeur ≥ 3 au dernier trou) |
|---|---|---|
| J.P. Bigdome, PDG | `J.P.Bigdome.glf` | investit 5 000 $ (10 000 $ si très content) et entre au conseil ; 8 fois au plus |
| I.M. Picky, commissaire du comté | `I.M.Picky.glf` | autorise l'achat de terrain (pas sur une île) |
| Ivana Richman, héritière | `Ivana Richman.glf` | offre un landmark (gratuit une fois) |

- Ils viennent quand la note de plaisir (Easy, Moderate) ou de compétence (Difficult, Impossible) dépasse un seuil qui monte à chaque visite réussie. S'ils sont déçus, ils disent à quel seuil ils reviendront.
- Un quatrième VIP, la **célébrité internationale** qui achèterait une maison, existe dans le texte mais son code est inaccessible ; les célébrités achètent les maisons par une autre voie (section 7).

## 12. Sim-stories (histoires de golfeurs) [M] [E] [Z]
- Deux golfeurs **compatibles** jumelés vivent une histoire : un titre et quatre scènes, chaque scène une réplique et des réponses classées de la meilleure à la pire.
- Le nom du fichier code les conditions (sexes, situation familiale, traits, âges) et le landmark offert. Exemples de titres : Opening Day (toujours la première paire), Love on the Links, Spies, Male Bonding, Bad Chemistry, Whodunnit, New Dad, Teenagers, Jealousy, Promotion, Goldfish.
- Une scène avance quand la paire est ensemble sur un trou et que l'humeur est assez haute ; une scène ratée recule.
- Une **fin heureuse** ajoute un cœur au compteur du club, une entrée au journal, une musique, et débloque un landmark. Le landmark Happy Endings aide les histoires.
- Les fiches de golfeur ont des boutons View Story et Next Chapter (≈ ; l'écran d'histoire n'est pas dans le décompilé).

## 13. Votre pro et le golf jouable [M] [E] [X]

### 13.1 Personnage
- Le joueur est représenté par un pro, **Gary Golf** par défaut, qui parle en tant que conseiller.
- Éditeur (Customise) : nom, sexe, couleur de peau, cheveux, pantalon, chemise, visage, adulte ou enfant, silhouette, citations personnalisées, biographie ; sauvegarde et chargement de personnages, export pour le mode championnat. Fiche : parties jouées, handicap, meilleur score.

### 13.2 Compétences
- Dix compétences : Power Hitter, Long Driver, Accurate Driver, Accurate Irons, Accurate Putter, Draw Shot, Fade Shot, High Backspin Shot, Recovery Skill, Luck (plus Positive Attitude et Mental Toughness, cachées). Chaque point vaut +10 %.
- 10 points au départ, +3 points par accomplissement. Les compétences montent aussi sur un coup réussi (plafond selon le rang du club) et peuvent baisser après un coup raté dans un obstacle.

### 13.3 Jouer
- **Practice Round** : il faut 2 trous ouverts ; le pro joue sans payer.
- On vise sur le terrain avec une ligne de trajectoire et on choisit le type de coup : **Straight, Fade, Draw, High Backspin, Low Punch**. Fade, draw et backspin sont refusés depuis un obstacle. Les petits coups près du trou sont joués automatiquement.
- On peut continuer à construire et modifier le terrain pendant que son pro joue (source communautaire, ?). [X]

### 13.4 Défis (matches)
- Un pro célèbre de niveau proche peut lancer un **défi** avec une **mise par trou** (match play). On gagne ou on paie la mise selon le résultat. Une victoire donne un accomplissement ; une défaite baisse le niveau des prochains défis.
- 96 pros dans `progolfers.dta` (noms parodiques comme « Tiger Forest »), des débutants à une seule compétence aux stars. [Z]

### 13.5 Accomplissements (F10) [E]
22 trophées, chacun gagné une fois avec une photo Polaroid et une plaque sur un tableau en liège :
1st Challenge hole, 1st Heroic hole, 1st skill upgrade, 1st Tournament, 1st Strategic hole, First match victory, First 9+ hole course, 1st Top 100 hole, 1st $500,000 Tournament, 1st Classic hole, 1st Top 18 hole, First tournament victory (9+ holes), 1st Grand Slam course (9+), 1st $1,000,000 Tournament, First 18 hole course, First tournament victory (18 holes), 1st Grand Slam course (18), Grand Slam Victory Parkland, Desert, Tropical et Links, 1st 100 star rating.
Le tableau montre aussi les trois prochains objectifs. Les Grand Slam Victory semblent impossibles à obtenir à cause d'un seuil de prix inatteignable (bug de l'original, ≈).

## 14. Évaluation SGA et tournois [M] [E]

### 14.1 Évaluation SGA (F7)
Dix critères notés de 0 à 10 (un 0 rend le parcours inacceptable) : longueur du parcours, nombre de trous, temps de jeu (cible 235 minutes), plaisir, variété, trous scéniques, trous de longueur, de précision, d'imagination, installations. La cible dépend du rang du club.

### 14.2 Classes de trous
Selon les compétences qu'un trou exige : **Breather** (aucune), **Freeway** (longueur), **Precise** (précision), **Creative** (imagination), **Challenge** (longueur + précision), **Heroic** (longueur + imagination), **Strategic** (précision + imagination), **Classic** (les trois). Deux trous consécutifs de même classe ou même dogleg lassent les golfeurs.

### 14.3 Tournois
- En juillet, si le parcours passe l'évaluation, la SGA propose un tournoi avec un premier prix calculé sur la note, le rang et le nombre de trous. Noms d'événements par note : Jr. Qualifying School, SGA Qualifying School, Jr. Tour Event et Championship, SGA Amateur Championship, Senior SGA Tour Event et Championship, SGA Tour Event, SGA Players Championship, SGA Championship, Mini Slam, Grand Slam.
- On lance le tournoi par le bouton Begin Tournament. Une **liste de préparation** propose des recommandations à cocher : tours et cabines de télévision, greens lissés, deep rough, pars de tournoi, +3 points de compétence.
- Format : un seul tour en **départ en shotgun**, deux joueurs par trou (36 sur 18 trous). Votre pro joue avec un partenaire contre des pros célèbres tirés selon la difficulté. Pendant le tournoi les golfeurs marchent plus vite, le trou est plus petit et le putting plus exigeant.
- **Leaderboard** en direct, puis écran de résultats. Prix décroissants (2/3 du précédent) pour les places payées ; le prix de votre pro va au club. On ne peut pas sauvegarder le parcours pendant un tournoi ; on peut l'annuler (offre perdue).

## 15. Notes et indicateurs du club [M] [E]
- **Fun rating** (bonheur moyen) et **Skill rating** (mise à l'épreuve de la longueur, de la précision et de l'imagination), affichés en haut avec l'argent.
- Ligne de notation : balles de golf, étoiles (note du parcours), **cœurs** (fins heureuses), drapeaux par trou.
- Trous **Top 100** et **Top 18** : green fee plus élevé (+200 $ chacun), accomplissements.
- **Aura** du parcours : carte des zones qui produisent des réactions favorables, neutres ou défavorables.
- **Top 10 Designers** : classement envoyé en fin d'année si le club y entre (fichier `top10.sve`) ; **Best Scores** : meilleurs scores par trou. [Z] [E]

## 16. Économie [M] [E]

### 16.1 Revenus
- **Green fee** payé à la fin de chaque trou : humeur du golfeur, doublée avec l'amélioration de green fee, +Top 100, +Top 18, +niveau d'Airstrip, +bonus Gold ou Platinum. Le joueur **ne fixe pas** de prix.
- Ventes : snack-bar, putting green, pro shop, driving range, vendeur de soda.
- Home sites (vente au quart de la valeur), investissement des PDG, prix de tournoi, mises de défis gagnées.

### 16.2 Dépenses
- Construction (terrain, relief, bâtiments, aménagements), terrain, nouvelles propriétés.
- Salaires et entretien des trous (100 $ par trou) à chaque échéance ; les petits clubs ne paient qu'une partie des échéances de salaire.
- Intérêts de 2 % par mois sur une dette. Aucun entretien des bâtiments, aucune taxe, aucun prêt.
- On peut dépenser à découvert avec une confirmation ; au tout début (moins de 3 trous), sans limite.

### 16.3 Conseil d'administration et rapports
- Fin d'année négative : « concerned » (deux ans pour revenir au positif), « very worried » (un an), puis **licenciement**, fin de la partie. Une année positive remet le compteur à zéro.
- **Financial Report** (F4) : Greens Fees, Home Sites, Food/Drink, Build course, Facilities, Salaries, Maint./Interest, Other, sur 8 années.
- **Bilan de fin d'année** : cette année contre la précédente pour l'argent, le plaisir, la compétence et l'adhésion, et les faits marquants (trous ouverts, bâtiments, matches gagnés, Top 100, Top 18, places en tournoi, PDG au conseil, fins heureuses, terrain acheté, landmark offert).

## 17. Faune, ambiance et décor [E] [Z]
- Neuf animaux selon le thème et l'habitat : élan, grue, flamant, mouton, crocodile, serpent, monstre de Gila, grand géocoucou, canard. Ils fuient les golfeurs et les balles et plaisent aux golfeurs (+1). Désactivables.
- Survols : montgolfière (qui atterrit), voilier, rapace par thème, dirigeable (à partir de la deuxième année).
- Eau : profondeur, rides sur la rive, rochers et coraux, dauphins en eau scénique, fontaines, cascades, éclaboussures.
- Éclairage propre à chaque thème (`*Lighting.txt`).

## 18. Interface et rapports [M] [E]
- **Dock** en bas à gauche avec panneaux : Build Course (terrain), Elevation, Add Buildings (9 lots avec images grisées, en couleur, améliorées), Amenities, Employees, Golfers (liste, clic = caméra sur le golfeur), Player (votre pro).
- **Menu Information** : Repeat Last Message, Course Report (F1), Player Comments (F2), Routing Map, Histograph (F3), SGA Evaluation (F7), Financial Report (F4), Membership Roster (F9), Professional Accomplishments (F10), World Map (F6), Best Scores, Top 10 Designers. F5 : vue d'ensemble (routage, aura, valeur des lots, employés). F8 : aide clavier.
- **Menu System Functions** : sauvegarder, charger, annuler un match ou un tournoi, sauvegarder son pro pour le championnat, renommer le parcours, préférences, sauvegarder le parcours pour le championnat, quitter.
- **Préférences** (6 cases) : noms des golfeurs à l'écran ; photos des grands coups ; explication complète des commentaires ; message pour les nouveaux membres ; pensées des golfeurs à l'écran ; animaux et habitats.
- **Raccourcis** : g green et tee, f fairway, r rough, s sable, t arbres, w eau, p chemin, b bancs, e élévation, `=` et `-` relief, Tab rotation, H ouvrir un trou, z et x zoom, Shift+P pause, Shift+T masquer les arbres, Shift+N noms, `/` analyse de coup, `?` répéter le dernier message.
- Caméra : 4 rotations, zoom, défilement. Les tests de l'époque reprochent un défilement lent et l'absence de raccourci pour sauter de trou en trou. [X]
- Sauvegardes nommées, avec autosave ; écran de chargement avec emblème du club. [E]
- Messages : bandeau défilant (ticker), boîtes de dialogue avec portrait, bulles de pensée, photos SimFoto (F10 les sauvegarde).
- Conseiller (Easy, première année) et un tutoriel inachevé caché dans le code : **le jeu livré n'a pas de tutoriel**. [E]

## 19. Son et musique [E] [Z] [X]
- Musique par thème (Parkland, Desert, Tropical, Links, divers), courts jingles jazz lors d'événements (amitié, fin heureuse), ambiance de nature le reste du temps.
- Voix simlish des golfeurs (hommes, femmes, émotions), répliques des célébrités, sons d'interface, de golf et d'effets (caisse, applaudissements, construction, arbres qui poussent). Son positionné à l'écran (panoramique selon la position).

## 20. Contenu coupé ou inaccessible [E]
Présent sur le disque ou dans le code mais jamais proposé au joueur :
- Landmarks : chapelle, jardins suspendus, voie ferrée, jardin de rocaille, ruines de cathédrale, gargouille, pyramide, volcan, second cadran solaire.
- Décors : bambou, pin noir, étang à carpes koï, source chaude, oasis, fontaine du désert, hélicoptère, gopher, etc.
- Accessoires de maisons de célébrités : arcade, barbecue, jacuzzi, manoir, sac de frappe, cinq voitures de sport.
- Un court de tennis Desert, un pont Links en quatre couches, deux bancs de plus, un chariot à boissons et une voiturette de ranger.
- Code mort : visiteur célébrité, mode de green fee double, tutoriel en deux visites guidées, écran de sélection de paire (code sans appel).

## 21. Limites et bugs connus de l'original [E] [X]
- Carte limitée à 50 × 50 tuiles et 18 trous, résolution fixe de 800 × 600.
- Bugs relevés par OpenSGolf : triangles noirs aux cascades, bâtiment facturé deux fois, améliorations grisées un niveau trop tôt, un nouveau golfeur écrase un golfeur lent, crash des têtes féminines (corrigé par le patch 1.03), arbres absents au zoom maximal.
- La parcelle de terrain déjà achetée peut être facturée à nouveau ; l'annonce de record du parcours ne se déclenche jamais.
- Tests de l'époque : crash possible en tournoi sur un parcours trop grand, messages qui se chevauchent et disparaissent, dialogues répétitifs, parcours vite encombrés, golfeurs petits et sommaires, clics accidentels pendant une compétition.

## 22. Points contradictoires ou à vérifier
- **Tennis Court ou Swim Club** : le manuel et Wikipedia disent court de tennis, la table de l'exe et les textes du conseiller disent Swim Club. Il faudrait une capture du jeu.
- **Chiffres de départ** : 100 000 $ est déduit (la fonction de nouvelle partie n'a pas été décompilée) ; l'argent par difficulté est inconnu.
- **Landmarks disponibles au départ** : inconnu.
- **Effet des « bonus » de propriété** (Free Hotel, Free Spa, Dolphins…) : inconnu dans le code.
- **Modifier le terrain pendant le jeu de son pro** et la mention d'un agent de sécurité : vient de TV Tropes, non confirmé par le code.
- **Mac** : aucune source n'atteste d'une version Mac.
- Les chiffres de l'exe sont ceux de la version décompilée par OpenSGolf (« publisher exe ») ; la version 1.02 du zip peut différer sur des détails.

## Sources
- OpenSGolf, dépôt [crspytopgn/opensgolf](https://github.com/crspytopgn/opensgolf) : `docs/MANUAL_NOTES.md`, `PUBLISHER_EXE_NOTES.md`, `GAMELOGIC.md`, `EXE_COSTS.md`, `DECODE_SOCIAL.md`, `DECODE_WORLD2.md`, `DECODE_TOURNAMENTS.md`, `DECODE_HOMES.md`, `DECODE_MENUS.md`, `UI_PANELS.md`, `UI_SCREENS.md`, `UI_SCREENS2.md`, `CUT_CONTENT.md`, `FIXES.md`, `PARITY.md`.
- Fichiers du jeu : `sim_golf.zip` (dossiers `Themes`, `Interface`, `Docs/readme.txt`, `Flics`, `Sounds`).
- [Wikipedia, Sid Meier's SimGolf](https://en.wikipedia.org/wiki/Sid_Meier%27s_SimGolf)
- [GameSpot, test de SimGolf](https://www.gamespot.com/reviews/simgolf-review/1900-2843958/) et [patch 1.01](https://www.gamespot.com/articles/sid-meiers-simgolf-patch-available/1100-2844355/)
- [PCGamingWiki, patch 1.03](https://community.pcgamingwiki.com/files/file/64-sid-meiers-simgolf-patch/)
- [Giant Bomb](https://www.giantbomb.com/games/3030-15377/)
- [MobyGames](https://www.mobygames.com/game/5700)
- [TV Tropes](https://www.tvtropes.org/pmwiki/pmwiki.php/VideoGame/SidMeiersSimGolf) (source communautaire, à prendre avec prudence)
- Le wiki Fandom de SimGolf n'a pas pu être consulté (OpenSGolf signale déjà le même blocage).
