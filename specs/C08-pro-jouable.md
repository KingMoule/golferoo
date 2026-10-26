# Votre pro : jouer ses coups et progresser par l'expérience
Statut : livré
Créé : 2026-10-26

## But
La décision 3 de C06 (`specs/C06-handicap-ligues.md`) : la progression de votre pro est contrôlée par le joueur, quelle que soit la situation, par l'expérience de chaque compétence (pour mieux réussir un draw, il faut en faire en match), et non par des points à répartir comme dans SimGolf. Aujourd'hui le pro joue seul, au niveau fixe d'un handicap 36, et la balle ne tourne pas.
1. **Un vrai trou pour jouer** (demande de Guillaume du 2026-10-26) : la scène par défaut est un trou dessiné pour être joué, pas la scène de mesure des FPS. Un dogleg où le draw et le fade servent, des arbres posés comme sur un vrai parcours, un green défendu.
2. **Des clubs et des trajectoires** : six familles de clubs, du wedge (précis, court, haut, beaucoup de spin) au driver (long, moins précis, bas, peu de spin) ; quatre trajectoires : droit, draw, fade, chip.
3. **Le joueur joue les coups du pro**, comme dans l'original avec un peu plus de profondeur : club, point visé, trajectoire et force, avec la trajectoire prévue (vent compris, décision D1 Q4) et la dispersion du pro affichées.
4. **Le pro progresse par l'expérience, sur deux paliers** : chaque coup fait progresser la ou les compétences générales qu'il emploie ; un coup réussi fait aussi progresser la ou les compétences spécialisées. On gagne toujours un peu en jouant, plus en réussissant de beaux coups. L'entraînement est plafonné par année, la ligue compte double sans plafond.

Ce qui prouve la réussite : sur le vrai trou, un draw contourne l'angle du dogleg qu'un coup droit ne passe pas ; un pro qui ne joue que des draws pendant une saison simulée progresse en draw et pas en fade ; son handicap de ligue baisse avec l'expérience.

## Périmètre et hors périmètre
**Dans le périmètre**
- Un trou par 4 en dogleg (fichier de trou), chargé par défaut ; une option de lancement qui met le pro au départ d'une partie d'entraînement.
- Familles de clubs, trajectoires (droit, draw, fade, chip) et spin dans l'exécution d'un coup du pro ; les coups des autres golfeurs restent ceux de C06 au bit près.
- `Pro/` dans la sim : compétences générales et spécialisées, expérience, plafond annuel ; commande de coup du pro ; partie d'entraînement.
- Rendu : mode de visée (caméra derrière le pro, choix du club, de l'effet et de la force, trajectoire prévue, ellipse de dispersion), fiche du pro, avis de progression.

**Hors périmètre, et où ça ira**
- Jauge de swing (puissance et timing, piste B17) : la visée de l'original d'abord ; la jauge optionnelle plus tard, sur la même commande.
- **Le pro qui accueille les membres** autour du club-house et des installations et leur remonte le moral (Guillaume, 2026-10-26) : avec l'humeur des membres, qui n'existe pas encore (chantier de l'humeur et de l'adhésion). En C08, le pro ne joue que lorsque le joueur joue ses coups.
- Coups bas, coups hauts, lob, punch sous les arbres : d'autres trajectoires, plus tard, sur le même modèle.
- Rouille (une compétence qui baisse sans pratique) : non, sauf demande.
- Équipement du pro, leçons aux élèves (école, D8), tournois (B18) : chantiers à venir, qui liront les compétences.
- Les membres et golfeurs ordinaires ne jouent ni effet ni spin : leur planificateur et leur exécution restent ceux de C06.
- Le renfort du vent de travers (`WindGainAcross = 6`, qui imitait l'effet absent) reste tel quel : le revoir changerait toutes les sommes de contrôle de C04 à C06.
- Parcours de plusieurs trous : le vrai trou est un trou de plus, le moteur reste à un trou.

## Conception
**Le vrai trou** (`data/holes/`)
- Un par 4 d'environ 380 m en dogleg vers la gauche : un coup droit depuis le tee finit dans le rough ou les arbres de l'angle, un draw coupe l'angle ; le green est défendu par un bunker à gauche et de l'eau à droite, si bien qu'un fade sert vers un drapeau à droite (le drapeau change de place chaque jour, `PinPicker` de C05).
- Des arbres posés comme objets de la sim (C02) en lisière et à l'angle, quelques centaines au plus, pas les 5 000 de la mesure FPS.
- Le jeu le charge par défaut ; `proto-par4.json` reste pour les tests et le banc existants (leurs sommes de contrôle ne bougent pas). La mesure FPS (`--fps-bench`) garde sa scène.
- Option `-- --pro` : le monde démarre avec le pro au départ d'une partie d'entraînement, en mode visée, pour tester vite.
- Aucun asset nouveau : drapeau, marques de départ, arbres et bunkers sont dans le dépôt ; le catalogue de Guillaume n'a ni golfeur ni club (golfeur et balle restent procéduraux).

**Clubs, trajectoires et spin** (`Shots/`)
- Six familles, sur la table actuelle : wedges (Wedge), short iron (fer 9), mid iron (fer 7), long iron (fer 5), bois (bois 3), driver. Plus le club est long, plus il porte et plus il disperse (déjà le cas des écarts types de `Clubs`).
- `ShotShape` : droit, draw, fade, chip (valeurs dans la somme de contrôle).
  - Draw et fade : en vol, une accélération latérale perpendiculaire à la vitesse horizontale, a = curve × K × (vz, 0, −vx) (vers la gauche pour un draw, joueur droitier). Calibrage au lot 2 : un driver en draw plein courbe d'environ 15 m à 220 m, un fer 7 d'environ 8 m.
  - Chip : lancement plus bas (environ la moitié de l'angle du club), peu de spin, la balle roule ; pour les wedges et les fers courts près du green.
- Spin : au premier rebond, la vitesse horizontale perd une part propre au club, forte pour les wedges (la balle s'arrête vite), faible pour le driver (elle roule) ; un draw roule un peu plus, un fade un peu moins.
- Ces effets ne s'appliquent qu'aux coups du pro : sans forme ni spin, la boucle de `BallSimulator` est la même au bit près, et les autres golfeurs gardent l'exécution de C06.

**Compétences et expérience** (`src/Golferoo.Sim/Pro/`)
- **Générales**, qui gagnent de l'expérience à chaque coup de leur famille : long jeu (driver, bois), fers (long, mid, short iron), petit jeu (wedges, chips), putting. Leur niveau règle la dispersion (direction et vitesse) de la famille.
- **Spécialisées**, qui ne gagnent de l'expérience que sur un coup réussi : distance (un coup de driver, bois ou fer long qui atteint au moins 95 % de la portée du club), précision (la balle s'arrête dans le lie visé, fairway ou green, et près de la cible : à moins de 8 % de la distance jouée), draw, fade et chip (un coup réussi de cette trajectoire). Distance donne un peu de vitesse de balle (jusqu'à +8 %) ; précision resserre encore la dispersion ; draw, fade et chip règlent l'erreur sur la courbure ou le lancement (une trajectoire mal maîtrisée courbe trop ou pas assez).
- Points : un coup d'entraînement vaut 1, un coup de ligue 2 (« en match »). Plafond : l'entraînement apporte au plus un nombre de points par compétence et par année de jeu (calibré au lot 3), remis à zéro au 1er avril ; la ligue n'a pas de plafond.
- Niveau d'une compétence = un handicap équivalent, de 36 au départ vers 2, avec des rendements décroissants : h = 2 + 34 × exp(−xp / τ), traduit en dispersion par `SkillLevels.FromHandicap` de C06. τ et le plafond sont calibrés au lot 3 pour qu'une saison complète (ligue et entraînement au plafond) amène une compétence générale travaillée vers un handicap 20.
- Le niveau global du pro (handicap de départ d'une saison) est la moyenne des compétences générales ; le handicap WHS reste celui de C06, calculé sur ses cartes de ligue.

**Coup du pro** (`World`)
- Nouvel état `GolferState.AwaitingShot` (ajouté en fin d'énumération, pour la somme de contrôle) : quand c'est le tour du pro, il attend la commande `World.PlayProShot(club, cible, trajectoire, force)`. Comme toute commande, elle est rejouable et entre dans la somme de contrôle.
- Le pro ne joue jamais seul (Guillaume, 2026-10-26) : le jeu se met en pause quand son tour arrive, en ligue comme à l'entraînement ; la sim ne fait qu'attendre.
- Partie d'entraînement : `World.StartProRound()` ajoute un groupe d'un seul golfeur, le pro, hors ligue ; ses cartes ne comptent pas pour son handicap (décision du 2026-10-24 : seuls les scores de ligue comptent).

**Rendu** (`game/src/`)
- Mode visée : caméra derrière le pro, choix du club parmi les six familles, clic sur le point visé, trajectoire (droit, draw, fade, chip) et force au clavier ; trajectoire prévue tirée du même simulateur (vent compris), ellipse de dispersion selon les compétences ; Entrée joue le coup.
- Fiche du pro (touche O) : chaque compétence avec son niveau et sa barre d'expérience, le handicap, la division ; un bouton « partie d'entraînement ».
- Avis quand une compétence gagne un niveau entier (« Draw : handicap 24 »).

**Lots (5 PR)**
1. **Le vrai trou** : fichier du dogleg, arbres posés, chargé par défaut, partie d'entraînement et option `--pro` (le pro y joue avec l'IA de C06 jusqu'au lot 3) ; captures. (léger)
2. **Clubs, trajectoires et spin** : `ShotShape`, courbure, chip et spin dans l'exécution d'un coup choisi, calibrage ; tests (draw à gauche, fade à droite, courbure par club, wedge qui s'arrête plus vite qu'un driver, chip qui roule, coups de C06 identiques au bit près) et banc. (complet)
3. **Compétences et coup du pro** : `Pro/` (générales et spécialisées, réussite d'un coup, plafond annuel, ligue double), `AwaitingShot`, `PlayProShot` ; tests sur une saison simulée et déterminisme. (complet)
4. **Jouer le pro** : mode visée, trajectoire prévue, dispersion, pause ; captures. (léger)
5. **Fiche du pro et progression** : touche O, avis, HUD ; captures ; clôture. (léger)

## Décisions de Guillaume (2026-10-26)
1. **Expérience** : l'entraînement est plafonné par année, pour ne pas gagner en enchaînant les parties ; les coups de ligue comptent double, sans limite.
2. **Le pro ne joue pas seul** : il accueille les membres et leur remonte le moral autour du club-house et des installations (plus tard, avec l'humeur).
3. **La visée de l'original, avec un peu plus de profondeur** : six familles de clubs, du wedge (précis, court) au driver ; visée et trajectoire voulue (droit, draw, fade, chip) ; les wedges montent plus haut avec plus de spin, les clubs longs moins.
4. **Deux paliers de compétences** : chaque coup fait progresser la ou les compétences générales ; un coup réussi (assez long, dans le lie voulu, près de la cible) fait aussi progresser la ou les spécialisées.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent l'effet (un draw finit à gauche d'un coup droit de même visée, un fade à droite ; courbure du driver entre 10 et 20 m ; sans effet, trajectoire identique au bit près), le vrai trou (il se charge ; depuis le tee, un draw bien visé passe l'angle là où un coup droit de même portée touche les arbres ou le rough), l'expérience (un coup compte pour sa compétence générale, réussi ou non ; seul un coup réussi compte pour une spécialisée ; un coup de ligue compte double ; l'entraînement s'arrête au plafond de l'année et repart au 1er avril ; un pro qui ne réussit que des draws progresse en draw et pas en fade ; le handicap d'une compétence part de 36 et ne descend pas sous 2), les trajectoires (un wedge s'arrête plus près de son point de chute qu'un driver ; un chip roule plus qu'un coup droit du même club), la commande (le pro attend en `AwaitingShot` et ne joue jamais seul ; mêmes commandes, même somme de contrôle) et la partie d'entraînement (ses cartes n'entrent pas dans le handicap).
2. Les trajectoires et sommes de contrôle des tests existants de C01 à C06 sont inchangées.
3. Banc : le tick à 200 golfeurs reste sous 2 ms ; un changement de jour reste sous 5 ms ; la trajectoire prévue se calcule en moins de 2 ms.
4. Captures de la CI (lignes `THUMB`) : le vrai trou vu d'ensemble, le mode visée avec une trajectoire en draw, la fiche du pro. Guillaume juge, et joue le trou avec `Lancer.bat`.
5. Fumée Godot (`SMOKE OK`) ; `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (lots 2 et 3 : physique du coup, état de partie) ; léger pour les lots 1, 4 et 5 (contenu et rendu)

## Clôture
- [x] Critères vérifiés (1 à 3 et 5 par les tests, le banc, la fumée et la CI ; audit indépendant des lots 2 et 3 conforme, une réserve corrigée ; 4 : captures `pro-aim` et `pro-sheet`, à juger par Guillaume, qui joue le trou avec `Lancer.bat`)
- [x] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
