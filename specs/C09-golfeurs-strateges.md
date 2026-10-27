# Golfeurs stratèges : lire le trou et jouer selon son style
Statut : validé
Créé : 2026-10-26

## But
Retour de jeu de Guillaume (2026-10-26) : une paire a vu sa balle s'arrêter en lisière de forêt, puis a retapé dans la forêt coup après coup, la balle retombant toujours au même endroit ; ils ont fini par avancer vers le green en laissant leur balle près des arbres. Les golfeurs doivent prendre des décisions logiques (jouer le fairway plutôt que viser droit au trou à travers une forêt), et des golfeurs différents doivent jouer différemment (artiste, long frappeur, amateur de draw ou de fade…).

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
- Directions essayées élargies à tout le tour (de 35° à 180°, tous les 30° environ) quand la ligne du trou est bouchée, et une sortie basse (lancement à moitié de l'angle du club, comme le chip de C08) pour passer sous les branches.
- Rendu : une balle ramassée n'est plus dessinée.
- Banc : rapport avec/sans arbres sur un drive ≤ 2,5.

**Lot 2 : lire le trou** (`Shots/`, `Pathing/`)
- Une carte de jeu par drapeau : pour chaque cellule, la distance au trou par un chemin que la balle peut suivre (forêts, bosquets et hors-limites bloquent ; l'eau se survole mais on n'y atterrit pas), comme un champ de flux mais pour la balle. Calculée à la demande et mise en cache comme `FlowFieldCache`, refaite quand le drapeau bouge ou que le terrain ou les arbres changent.
- Le coût d'un point de chute devient une estimation des coups restants : distance par la carte de jeu, plus une pénalité de lie (rough, bunker, sous les arbres). Sur un dogleg, le coin du fairway vaut alors mieux qu'un rough derrière la forêt, même s'il est plus loin à vol d'oiseau.
- Sommes de contrôle et calibrage de `SkillLevels` (test de calibrage sur proto-par4) recalculés une fois, dans ce lot.

**Lot 3 : profils de golfeurs** (`Golfers/`, `League/`, `Shots/`) ; démarre après la fusion de la #73, qui modifie `PhysicalShotResolver.cs` et la taille des arbres (calibrage sur les arbres définitifs)
- `Golfers/GolferProfile.cs` : `enum Archetype` (valeurs dans la somme de contrôle) et un réglage par archétype. Un profil neutre, attribué à personne, sert à `CourseRater` : la note dépend du trou, pas du tirage des membres. `ShotRequest` porte le profil.
- Le niveau reste le handicap : un profil change la manière, et son effet sur le score dépend du trou.
- Les six archétypes (cadrage avec Guillaume, 2026-10-27) :
  - **Prudent** : pénalités, lies difficiles et flancs pèsent plus dans le coût ; vise le centre du green, sort de la forêt de côté.
  - **Long frappeur** : driver et bois 3 vont 10 % plus loin, fers 5 et 7 inchangés, fer 9 et wedge 5 % moins loin ; dispersion normale ; préfère le driver au départ.
  - **Joueur de draw** et **joueur de fade** : chaque coup plein essaie le coup droit et son effet, puis garde le meilleur. Avec son effet : 5 % plus loin et dispersion −20 % ; en coup droit : dispersion +5 %. Exécution par `ShotShapes`, régularité de l'effet liée au niveau (`amountSigma` proportionnel au niveau). Deux fois plus de simulations de planification pour ces joueurs : le banc vérifie la limite.
  - **Artiste** (remplace l'imaginatif) : joue mieux dans un bel endroit, sans coups créatifs. Une *beauté* de 0 à 1 par cellule, calculée sur un rayon de 15 m : l'eau, la lisière des arbres, les fleurs au printemps (leur placement par hachage de cellule passe de `FlowerLayer` à la sim, le rendu le lit), et une liste de sources de beauté posées dans le monde (vide pour l'instant ; les objets de décor futurs s'y ajouteront sans toucher au calcul). Mise en cache, refaite quand le terrain, les arbres ou la saison changent. Exécution : dispersion × (1 − 0,25 × beauté au point de départ). Planification : coût d'un point de chute diminué de 5 m × beauté, si bien qu'il ne s'éloigne jamais du trou de plus de 5 m pour un bel endroit.
  - **Fonceur** : pénalités et flancs pèsent moins, vise le drapeau et la ligne courte au-dessus du coin.
- Attribution : le profil d'un membre vient d'un hachage de la graine et de son id, hors du flux de `GenerateMembers` (noms et handicaps inchangés) ; uniforme sur les six archétypes. Les golfeurs ordinaires tirent le leur du RNG du monde à leur arrivée.
- Les membres jouent draw et fade selon leur profil (choix 3) ; cela lève une limite écrite au hors périmètre de C08.

**Lot 4 : rendu** (`game/src/`)
- Profil sur la fiche du membre (`LeaguePanel.ShowCard` : « Nom | handicap 14,2 | joueur de draw ») et colonne « Style » dans les divisions si elle tient dans le panneau. L'info-bulle d'un golfeur sur le terrain est reportée : elle n'existe pas encore et demande le survol des golfeurs en 3D.
- Capture `c09-profils` : depuis le tee du dogleg, les trajectoires prévues de chaque archétype au même niveau, et la sortie de forêt depuis la balle de la capture de Guillaume.

## Choix à trancher par Guillaume
1. **Comment le golfeur lit le trou.** (a) Carte de jeu par drapeau (lot 2) : marche sur tout trou, y compris ceux que le joueur dessine, un calcul par drapeau. (b) Points de visée posés par l'architecte sur chaque trou : simple mais à refaire à chaque retouche, et oublié sur les trous du joueur. (c) Prévoir deux coups d'avance par simulation : le plus juste, mais environ 50 fois plus de simulations par coup. **Recommandation : (a)**, la seule qui suit les trous que le joueur construit sans rien lui demander.
2. **Forme des profils.** (a) Six archétypes nommés, chacun réglé sur les quatre réglages. (b) Traits combinables librement (prudent et long frappeur, etc.). (c) Réglages continus tirés au hasard, sans nom. **Recommandation : (a)** : un nom se lit d'un coup d'œil sur la fiche, et les quatre réglages permettent de passer à (b) plus tard sans rien refaire.
3. **Les membres jouent-ils draw et fade ?** (a) Oui, selon leur profil : c'est ce qui rend « le joueur de draw » visible. (b) Non : les profils ne changent que la cible, le club et le risque. **Recommandation : (a)**. Coût : la note du parcours et les sommes de contrôle bougent, recalculées au lot 2 et 3.

**Décisions de Guillaume (2026-10-26)** : les trois recommandations, soit 1 (a) carte de jeu par drapeau, 2 (a) six archétypes nommés, 3 (a) les membres jouent draw et fade selon leur profil.

**Cadrage des lots 3 et 4 (2026-10-27)** : réglages des styles décidés par Guillaume (voir lot 3) ; l'imaginatif devient l'artiste ; archétypes répartis uniformément ; info-bulle d'un golfeur reportée.

## Critères d'acceptation
1. Lot 1 : sur le dogleg avec ses arbres, depuis une balle en lisière avec la forêt entre elle et le trou, 100 golfeurs de niveau bogey (graines différentes) ne finissent jamais deux coups de suite au pied des arbres depuis le même point, et aucun n'atteint `MaxStrokes` (test).
2. Lot 1 : la planification reste déterministe (même graine, même somme de contrôle) et le banc de planification passe sa limite (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`).
3. Lot 1 : une balle ramassée n'apparaît plus dans les captures.
4. Lot 2 : sur le dogleg, la part des drives d'un golfeur scratch qui s'arrêtent sur le fairway monte d'au moins 20 points par rapport à la mesure faite avant le lot (test qui affiche les deux).
5. Lot 2 : le test de calibrage de `SkillLevels` passe, et la note du parcours du dogleg ne monte pas (les golfeurs jouent mieux, pas plus mal).
6. Lot 3 : à niveau égal sur le dogleg, sur 200 trous chacun, le long frappeur prend le driver au départ plus souvent que le prudent ; le joueur de draw joue un draw sur plus de la moitié de ses coups pleins ; le prudent finit avec moins de pénalités que le fonceur ; le long frappeur porte son drive plus loin que le prudent (test).
7. Lot 3 : chaque membre a un profil stable d'une partie à l'autre (même graine, même profil), et les noms et handicaps de `GenerateMembers` ne changent pas.
8. Lot 3 : la beauté vaut 0 au milieu d'un fairway sans eau ni arbres et monte près de l'eau ; une source de beauté ajoutée au monde la fait monter ; sur 200 coups depuis un même bel endroit, l'artiste disperse moins qu'un golfeur neutre du même niveau ; son point visé n'est jamais plus loin du trou (carte de jeu) que celui du golfeur neutre de plus de 5 m (test).
9. Lot 4 : la capture `c09-profils` montre des trajectoires différentes par archétype ; le profil apparaît sur la fiche du membre.
10. Tous lots : `dotnet test Golferoo.sln`, `dotnet format Golferoo.sln --verify-no-changes` et les quatre jobs de CI verts.

## Niveau de contrôle
complet (lots 1 à 3 : décisions des golfeurs, sommes de contrôle, note du parcours), léger (lot 4).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
