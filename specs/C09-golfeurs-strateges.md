# Golfeurs stratèges : lire le trou et jouer selon son style
Statut : proposé
Créé : 2026-10-26

## But
Retour de jeu de Guillaume (2026-10-26) : une paire a vu sa balle s'arrêter en lisière de forêt, puis a retapé dans la forêt coup après coup, la balle retombant toujours au même endroit ; ils ont fini par avancer vers le green en laissant leur balle près des arbres. Les golfeurs doivent prendre des décisions logiques (jouer le fairway plutôt que viser droit au trou à travers une forêt), et des golfeurs différents doivent jouer différemment (imaginatif, long frappeur, amateur de draw ou de fade…).

**Cause, vérifiée dans le code.** `PhysicalShotResolver.Plan` simule ses candidats sans arbres (« La planification les ignore : elle ne contourne pas les arbres, elle les subit », déjà noté comme suite de C02). Le golfeur choisit donc la ligne droite à travers la forêt, la balle tape les arbres et retombe à leur pied, et il recommence jusqu'à `World.MaxStrokes` (12) : balle ramassée, il part vers la sortie. Le rendu (`ActorLayer`) dessine encore une balle ramassée, d'où la balle « restée » près de la forêt. Deux autres limites aggravent le cas : le coût d'un point de chute est sa distance à vol d'oiseau au trou (une balle derrière la forêt « paraît » proche), et les directions essayées ne s'écartent que de 20° de la ligne du trou (impossible de ressortir de côté vers le fairway).

Ce qui prouve la réussite : sur le dogleg, depuis la balle de la capture de Guillaume, le golfeur ressort vers le fairway au lieu de retaper dans les arbres ; depuis le tee, il vise l'angle du fairway ; deux golfeurs de même niveau mais de profils différents jouent des coups visiblement différents sur le même trou.

## Périmètre et hors périmètre
**Dans le périmètre**
- Le planificateur des golfeurs (membres et golfeurs ordinaires) voit les arbres, sort de la forêt, lit le trou par le jeu et non à vol d'oiseau.
- Des profils de golfeurs qui changent les choix (club, cible, risque, effet), pas seulement la dispersion.
- Rendu : balle ramassée cachée, profil visible sur la fiche du membre, capture qui compare les profils.
- La note du parcours (`CourseRater`, même résolveur) suit d'elle-même : elle notera le trou joué intelligemment.

**Hors périmètre**
- Votre pro : le joueur décide pour lui (C08), rien ne change dans son mode visée.
- Les erreurs de jugement (un golfeur faible qui lit mal le trou, un têtu qui retente le coup héroïque) : à reparler après le lot 3, si les golfeurs semblent trop parfaits.
- Pousses, outils de pose, échelle des arbres et des personnages, contours lissés : traités ailleurs.
- Humeur des membres selon leurs coups : chantier de l'humeur.

## Conception
**Lot 1 : le planificateur voit les arbres** (`Shots/`)
- Les simulations de planification passent les arbres à `BallSimulator` en mode *espérance* : un tronc arrête la balle comme à l'exécution ; une couronne ne tire pas au hasard, elle cumule la probabilité d'arrêt (`TreeCollision.StopProbability`) le long du vol. Le coût d'un candidat devient p × coût(arrêté au pied) + (1 − p) × coût(passé). Sans hasard, la planification reste déterministe et hors RNG du monde.
- Directions essayées élargies jusqu'à ±90° quand la ligne du trou est bouchée, et une sortie basse (lancement à moitié de l'angle du club, comme le chip de C08) pour passer sous les branches.
- Rendu : une balle ramassée n'est plus dessinée.
- Banc : planifier un coup sur le dogleg avec ses arbres, limite à fixer à 2 fois le temps actuel.

**Lot 2 : lire le trou** (`Shots/`, `Pathing/`)
- Une carte de jeu par drapeau : pour chaque cellule, la distance au trou par un chemin que la balle peut suivre (forêts, bosquets et hors-limites bloquent ; l'eau se survole mais on n'y atterrit pas), comme un champ de flux mais pour la balle. Calculée à la demande et mise en cache comme `FlowFieldCache`, refaite quand le drapeau bouge ou que le terrain ou les arbres changent.
- Le coût d'un point de chute devient une estimation des coups restants : distance par la carte de jeu, plus une pénalité de lie (rough, bunker, sous les arbres). Sur un dogleg, le coin du fairway vaut alors mieux qu'un rough derrière la forêt, même s'il est plus loin à vol d'oiseau.
- Sommes de contrôle et calibrage de `SkillLevels` (test de calibrage sur proto-par4) recalculés une fois, dans ce lot.

**Lot 3 : profils de golfeurs** (`Golfers/`, `League/`)
- Un profil est fait de quatre réglages du planificateur : prudence (poids des pénalités et des coups ratés sur les flancs), puissance (préférence pour le driver et le bois, un peu plus de vitesse et de dispersion), effet favori (aucun, draw ou fade, avec sa maîtrise), imagination (candidats en plus : effets autour d'un obstacle, sortie basse, trouée entre deux arbres, attaque du drapeau).
- Archétypes nommés (choix 2) : prudent, long frappeur, joueur de draw, joueur de fade, imaginatif, fonceur. Chaque membre reçoit le sien de la graine ; les golfeurs ordinaires aussi.
- Les membres jouent draw et fade selon leur profil (choix 3), avec l'exécution de C08 (`ShotShapes`) ; cela lève une limite écrite au hors périmètre de C08.
- Le niveau reste le handicap : un profil change la manière, et son effet sur le score dépend du trou (un long frappeur aime un par 5 ouvert, un prudent un trou étroit).

**Lot 4 : rendu** (`game/src/`)
- Profil sur la fiche du membre (panneau J) et dans l'info-bulle d'un golfeur.
- Capture `c09-profils` : depuis le tee du dogleg, les trajectoires prévues de chaque archétype au même niveau, et la sortie de forêt depuis la balle de la capture de Guillaume.

## Choix à trancher par Guillaume
1. **Comment le golfeur lit le trou.** (a) Carte de jeu par drapeau (lot 2) : marche sur tout trou, y compris ceux que le joueur dessine, un calcul par drapeau. (b) Points de visée posés par l'architecte sur chaque trou : simple mais à refaire à chaque retouche, et oublié sur les trous du joueur. (c) Prévoir deux coups d'avance par simulation : le plus juste, mais environ 50 fois plus de simulations par coup. **Recommandation : (a)**, la seule qui suit les trous que le joueur construit sans rien lui demander.
2. **Forme des profils.** (a) Six archétypes nommés, chacun réglé sur les quatre réglages. (b) Traits combinables librement (prudent et long frappeur, etc.). (c) Réglages continus tirés au hasard, sans nom. **Recommandation : (a)** : un nom se lit d'un coup d'œil sur la fiche, et les quatre réglages permettent de passer à (b) plus tard sans rien refaire.
3. **Les membres jouent-ils draw et fade ?** (a) Oui, selon leur profil : c'est ce qui rend « le joueur de draw » visible. (b) Non : les profils ne changent que la cible, le club et le risque. **Recommandation : (a)**. Coût : la note du parcours et les sommes de contrôle bougent, recalculées au lot 2 et 3.

## Critères d'acceptation
1. Lot 1 : sur le dogleg avec ses arbres, depuis une balle en lisière avec la forêt entre elle et le trou, 100 golfeurs de niveau bogey (graines différentes) ne finissent jamais deux coups de suite au pied des arbres depuis le même point, et aucun n'atteint `MaxStrokes` (test).
2. Lot 1 : la planification reste déterministe (même graine, même somme de contrôle) et le banc de planification passe sa limite (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`).
3. Lot 1 : une balle ramassée n'apparaît plus dans les captures.
4. Lot 2 : sur le dogleg, la part des drives d'un golfeur scratch qui s'arrêtent sur le fairway monte d'au moins 20 points par rapport à la mesure faite avant le lot (test qui affiche les deux).
5. Lot 2 : le test de calibrage de `SkillLevels` passe, et la note du parcours du dogleg ne monte pas (les golfeurs jouent mieux, pas plus mal).
6. Lot 3 : à niveau égal sur le dogleg, sur 200 trous chacun, le long frappeur prend le driver au départ plus souvent que le prudent ; le joueur de draw joue un draw sur plus de la moitié de ses coups pleins ; le prudent finit avec moins de pénalités que le fonceur (test).
7. Lot 3 : chaque membre a un profil stable d'une partie à l'autre (même graine, même profil).
8. Lot 4 : la capture `c09-profils` montre des trajectoires différentes par archétype ; le profil apparaît sur la fiche du membre.
9. Tous lots : `dotnet test Golferoo.sln`, `dotnet format Golferoo.sln --verify-no-changes` et les quatre jobs de CI verts.

## Niveau de contrôle
complet (lots 1 à 3 : décisions des golfeurs, sommes de contrôle, note du parcours), léger (lot 4).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
