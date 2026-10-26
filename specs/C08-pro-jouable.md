# Votre pro : jouer ses coups et progresser par l'expérience
Statut : proposé
Créé : 2026-10-26

## But
La décision 3 de C06 (`specs/C06-handicap-ligues.md`) : la progression de votre pro est contrôlée par le joueur, quelle que soit la situation, par l'expérience de chaque compétence (pour mieux réussir un draw, il faut en faire en match), et non par des points à répartir comme dans SimGolf. Aujourd'hui le pro joue seul, au niveau fixe d'un handicap 36, et la balle ne tourne pas.
1. **Un vrai trou pour jouer** (demande de Guillaume du 2026-10-26) : la scène par défaut est un trou dessiné pour être joué, pas la scène de mesure des FPS. Un dogleg où le draw et le fade servent, des arbres posés comme sur un vrai parcours, un green défendu.
2. **Des effets de balle dans la physique** : draw et fade courbent la balle en vol et changent un peu son roulement.
3. **Le joueur joue les coups du pro** : club, point visé, force et effet, avec la trajectoire prévue (vent compris, décision D1 Q4) et la dispersion du pro affichées.
4. **Le pro progresse par l'expérience** : chaque coup joué fait progresser la compétence qu'il emploie, plus en match qu'à l'entraînement ; son niveau, son handicap et sa division en découlent.

Ce qui prouve la réussite : sur le vrai trou, un draw contourne l'angle du dogleg qu'un coup droit ne passe pas ; un pro qui ne joue que des draws pendant une saison simulée progresse en draw et pas en fade ; son handicap de ligue baisse avec l'expérience.

## Périmètre et hors périmètre
**Dans le périmètre**
- Un trou par 4 en dogleg (fichier de trou), chargé par défaut ; une option de lancement qui met le pro au départ d'une partie d'entraînement.
- Effet latéral dans `BallSimulator` (draw, fade) ; coups droits identiques au bit près.
- `Pro/` dans la sim : compétences, expérience, niveau ; commande de coup du pro ; partie d'entraînement.
- Rendu : mode de visée (caméra derrière le pro, choix du club, de l'effet et de la force, trajectoire prévue, ellipse de dispersion), fiche du pro, avis de progression.

**Hors périmètre, et où ça ira**
- Jauge de swing (puissance et timing, piste B17) : la visée de l'original d'abord ; la jauge optionnelle plus tard, sur la même commande.
- Coups bas, coups hauts, lob, punch sous les arbres : d'autres compétences, plus tard, sur le même modèle.
- Rouille (une compétence qui baisse sans pratique) : non, sauf demande.
- Équipement du pro, leçons aux élèves (école, D8), tournois (B18) : chantiers à venir, qui liront les compétences.
- Les membres et golfeurs ordinaires ne jouent pas d'effet : leur planificateur reste celui de C06.
- Le renfort du vent de travers (`WindGainAcross = 6`, qui imitait l'effet absent) reste tel quel : le revoir changerait toutes les sommes de contrôle de C04 à C06.
- Parcours de plusieurs trous : le vrai trou est un trou de plus, le moteur reste à un trou.

## Conception
**Le vrai trou** (`data/holes/`)
- Un par 4 d'environ 380 m en dogleg vers la gauche : un coup droit depuis le tee finit dans le rough ou les arbres de l'angle, un draw coupe l'angle ; le green est défendu par un bunker à gauche et de l'eau à droite, si bien qu'un fade sert vers un drapeau à droite (le drapeau change de place chaque jour, `PinPicker` de C05).
- Des arbres posés comme objets de la sim (C02) en lisière et à l'angle, quelques centaines au plus, pas les 5 000 de la mesure FPS.
- Le jeu le charge par défaut ; `proto-par4.json` reste pour les tests et le banc existants (leurs sommes de contrôle ne bougent pas). La mesure FPS (`--fps-bench`) garde sa scène.
- Option `-- --pro` : le monde démarre avec le pro au départ d'une partie d'entraînement, en mode visée, pour tester vite.
- Aucun asset nouveau : drapeau, marques de départ, arbres et bunkers sont dans le dépôt ; le catalogue de Guillaume n'a ni golfeur ni club (golfeur et balle restent procéduraux).

**Effet de balle** (`Shots/BallSimulator.cs`, `Shots/Club.cs`)
- `ShotShape` : droit, draw, fade (valeurs dans la somme de contrôle). Un réel `curve` (−1 fade plein à +1 draw plein, pour un droitier) entre dans `Run`.
- En vol, une accélération latérale perpendiculaire à la vitesse horizontale : a = curve × K × (vz, 0, −vx), qui pousse vers la gauche pour un draw. Sans `curve`, la boucle est la même au bit près.
- Calibrage au lot 2 : un driver en draw plein courbe d'environ 15 m à 220 m, un fer 7 d'environ 8 m ; un draw roule un peu plus (environ +10 %), un fade un peu moins.
- Les golfeurs ordinaires ne jouent que droit ; le planificateur ne change pas.

**Compétences et expérience** (`src/Golferoo.Sim/Pro/`)
- Six compétences : long jeu (driver, bois 3), fers (fer 5 à fer 9), petit jeu (wedge), putting, draw, fade.
- `ProSkills` : l'expérience de chaque compétence (un entier). Chaque coup joué en apporte à sa famille de club et, s'il porte un effet, à cet effet. Un coup de ligue compte double (« en match »).
- Niveau d'une compétence = un handicap équivalent, de 36 au départ vers 2, avec des rendements décroissants : h = 2 + 34 × exp(−xp / τ). Il se traduit en dispersion par `SkillLevels.FromHandicap` de C06. τ est calibré au lot 3 pour qu'une saison de pratique régulière (ligue et entraînement) amène une compétence travaillée vers un handicap 18.
- Exécution d'un coup du pro : direction et vitesse dispersées selon la famille de club ; l'effet ajoute une erreur sur `curve` selon la compétence de l'effet (un draw mal maîtrisé courbe trop ou pas assez).
- Le niveau global du pro (pour la carte de score, le handicap de départ d'une saison) est la moyenne des compétences de jeu ; le handicap WHS reste celui de C06, calculé sur ses cartes de ligue.

**Coup du pro** (`World`)
- Nouvel état `GolferState.AwaitingShot` (ajouté en fin d'énumération, pour la somme de contrôle) : quand c'est le tour du pro joué à la main, il attend la commande `World.PlayProShot(club, cible, force, effet)`. Comme toute commande, elle est rejouable et entre dans la somme de contrôle.
- Le jeu se met en pause quand le tour du pro arrive en mode visée ; la sim ne fait qu'attendre.
- Le pro peut aussi jouer seul (sans le joueur) : l'IA de C06 choisit le coup, droit, avec ses compétences ; l'expérience compte (voir question 2).
- Partie d'entraînement : `World.StartProRound()` ajoute un groupe d'un seul golfeur, le pro, hors ligue ; ses cartes ne comptent pas pour son handicap (décision du 2026-10-24 : seuls les scores de ligue comptent).

**Rendu** (`game/src/`)
- Mode visée : caméra derrière le pro, molette ou flèches pour le club, clic sur le point visé, force et effet au clavier ; trajectoire prévue tirée du même simulateur (vent compris), ellipse de dispersion selon les compétences ; Entrée joue le coup.
- Fiche du pro (touche O) : chaque compétence avec son niveau et sa barre d'expérience, le handicap, la division ; un bouton « partie d'entraînement ».
- Avis quand une compétence gagne un niveau entier (« Draw : handicap 24 »).

**Lots (5 PR)**
1. **Le vrai trou** : fichier du dogleg, arbres posés, chargé par défaut, option `--pro` (qui démarre une partie où le pro joue seul tant que le lot 3 n'est pas là) ; captures. (léger)
2. **Effets de balle** : `ShotShape`, `curve` dans `BallSimulator`, calibrage ; tests (draw à gauche, fade à droite, courbure par club, droit identique au bit près) et banc. (complet)
3. **Compétences et coup du pro** : `Pro/`, `AwaitingShot`, `PlayProShot`, partie d'entraînement, expérience et niveau, pro joué par l'IA ; tests sur une saison simulée et déterminisme. (complet)
4. **Jouer le pro** : mode visée, trajectoire prévue, dispersion, pause ; captures. (léger)
5. **Fiche du pro et progression** : touche O, avis, HUD ; captures ; clôture. (léger)

## Questions à Guillaume
1. **Où le pro gagne-t-il de l'expérience ?** Avec un seul trou, la ligue ne lui donne qu'environ 25 coups par saison. *Recommandation : en ligue et en partie d'entraînement, un coup de ligue comptant double.*
2. **Si le joueur laisse le pro jouer seul, apprend-il ?** *Recommandation : oui, mais l'IA ne joue que des coups droits : seul le joueur fait progresser le draw et le fade.*
3. **Comment joue-t-on un coup ?** *Recommandation : la visée de l'original (club, point visé, force, effet, trajectoire prévue) ; la jauge de swing plus tard.*
4. **Les six compétences** (long jeu, fers, petit jeu, putting, draw, fade) **vous conviennent-elles ?** *Recommandation : oui, les coups bas et hauts plus tard.*

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent l'effet (un draw finit à gauche d'un coup droit de même visée, un fade à droite ; courbure du driver entre 10 et 20 m ; sans effet, trajectoire identique au bit près), le vrai trou (il se charge ; depuis le tee, un draw bien visé passe l'angle là où un coup droit de même portée touche les arbres ou le rough), l'expérience (un coup de ligue compte double ; un pro qui ne joue que des draws progresse en draw et pas en fade ; le handicap d'une compétence part de 36 et ne descend pas sous 2), la commande (le pro attend en `AwaitingShot` ; mêmes commandes, même somme de contrôle) et la partie d'entraînement (ses cartes n'entrent pas dans le handicap).
2. Les trajectoires et sommes de contrôle des tests existants de C01 à C06 sont inchangées.
3. Banc : le tick à 200 golfeurs reste sous 2 ms ; un changement de jour reste sous 5 ms ; la trajectoire prévue se calcule en moins de 2 ms.
4. Captures de la CI (lignes `THUMB`) : le vrai trou vu d'ensemble, le mode visée avec une trajectoire en draw, la fiche du pro. Guillaume juge, et joue le trou avec `Lancer.bat`.
5. Fumée Godot (`SMOKE OK`) ; `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (lots 2 et 3 : physique du coup, état de partie) ; léger pour les lots 1, 4 et 5 (contenu et rendu)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
