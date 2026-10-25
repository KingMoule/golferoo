# Handicap, moteur de score et ligues
Statut : proposé
Créé : 2026-10-25

## But
La fiche D5 de `docs/boucles-v1.md`, avec les décisions du 2026-10-24 (divisions avec montées et descentes, votre pro part de la plus basse, seuls les scores de ligue comptent pour son handicap, classement en brut) et celles du 2026-10-25 (section « Décisions »), ramenée à ce que le jeu porte aujourd'hui : un seul trou, des paires anonymes qui jouent toutes aussi bien, aucun membre, aucune humeur, aucune UI de gestion (P3 pas encore faite).
1. **Un moteur de score partagé (F5)** : carte de score, brut, net, stableford, match play, contre le par, classement avec ex aequo. Les tournois (B18) et les autres formats (B20) le reprendront tel quel.
2. **Des golfeurs qui ne jouent pas tous pareil** : un niveau de jeu par golfeur, qui règle sa dispersion et sa longueur.
3. **Le parcours se note seul (B10)** : un golfeur fictif scratch et un golfeur bogey jouent le trou ; on en tire la note du parcours et le slope, comme sur une vraie carte de score.
4. **Le handicap se calcule comme dans la réalité**, en simplifié (World Handicap System) : moyenne des meilleurs écarts sur les dernières parties de ligue, rapportés à la note du parcours.
5. **La ligue du club** : les membres qui ont une bonne opinion du club s'y inscrivent ; une journée par semaine d'avril à août, avec un format qui change d'une semaine à l'autre ; une finale en septembre, puis les montées et les descentes. Autant de divisions de 8 que le nombre d'inscrits le permet, une seule sinon. Votre pro joue en classement depuis la dernière division.

Ce qui prouve la réussite : sur une saison simulée, les meilleurs joueurs finissent en tête de la division 1 et gagnent plus souvent la finale, le handicap de chaque membre retrouve à quelques points près celui de son niveau de jeu, et le nombre de divisions suit le nombre d'inscrits.

## Périmètre et hors périmètre
**Dans le périmètre**
- `Scoring/` dans la sim : carte de score et formats, classement.
- Niveau de jeu par golfeur, passé au résolveur de coups ; par du trou.
- `Rating/` : note du parcours et slope par simulation, handicap des joueurs.
- `League/` : membres du club et leur opinion du club, inscriptions, divisions, calendrier et formats tournants, finale, ajustement des divisions, report d'une journée sous l'orage, votre pro en classement.
- Rendu : classement, carte de score du dernier tour, handicap du pro au HUD, annonce de la finale et de l'ajustement, avec des `Control` simples comme le panneau des consignes de C05.

**Hors périmètre, et où ça ira**
- **Progression de votre pro** (décision 3) : elle se fera par l'expérience, coup par coup (faire des draws en match pour mieux réussir les draws), et le joueur la contrôle. Elle demande des effets de balle (draw, fade), que la physique n'a pas encore. Chantier à part, à cadrer : en C06, le pro garde un niveau fixe et faible.
- Ce qui fait bouger l'opinion du club (humeur, prix, état du parcours) : avec l'humeur et l'adhésion. En C06, l'opinion de chaque membre est tirée de la graine et ne bouge pas.
- Membres qui jouent les autres jours, adhésion, cotisations, fidélité (idée 7) : avec l'adhésion.
- Formats par équipes (scramble, greensome, foursome, quatre balles) : ils changent la façon de jouer de la paire (choisir la meilleure balle, jouer en alterné), donc la machine à états des golfeurs. Avec B20.
- Parcours de plusieurs trous : le moteur de score compte les trous qu'a le parcours (un aujourd'hui) et marchera tel quel avec dix-huit.
- Rivalités (idée 6, piste B12 pas encore triée) ; vitrine des trophées et tableau d'affichage (avec les bâtiments) ; feuille de départs (Q3 : les paires de ligue partent simplement les premières).
- Thème et composants communs de l'UI : preuve P3 ; les écrans de C06 seront rhabillés par elle.

## Conception
**Moteur de score** (`src/Golferoo.Sim/Scoring/`, sans Godot)
- `ScoreCard` : par et coups de chaque trou (balle ramassée = 12 coups, `World.MaxStrokes`), brut, net, points stableford, résultat contre le par.
- `MatchResult` : deux cartes comparées trou par trou (gagné, partagé, perdu).
- `Leaderboard` : tri stable par une clé, ex aequo au même rang (« T3 »).
- Le par du trou entre dans le fichier de trou (`"par": 4`) ; sans champ, il se déduit de la longueur du tee au drapeau (barèmes de l'USGA : jusqu'à 230 m par 3, jusqu'à 430 m par 4, au-delà par 5).

**Niveau de jeu** (`Golfers/Golfer.cs`, `Shots/`)
- `Golfer.Skill` : un réel qui multiplie les écarts types du club (direction et vitesse) et réduit un peu la vitesse de balle des moins bons. `Skill = 1` est le golfeur actuel : à ce niveau, trajectoires et sommes de contrôle de C01 à C05 restent identiques au bit près.
- `ShotRequest` porte le niveau. La planification reste la même pour tous ; seule l'exécution change.
- Deux niveaux de référence calibrés au lot 1 sur le trou du prototype : **scratch** (handicap 0) et **bogey** (handicap 20 environ, un coup de plus que le par par trou en moyenne). Les niveaux intermédiaires s'interpolent entre eux, et au-delà jusqu'au handicap 54. Un seul nombre en C06 ; la progression du pro le découpera plus tard en compétences.

**Note du parcours et handicap** (`src/Golferoo.Sim/Rating/`)
- `CourseRater.Rate(terrain, seed)` : fonction pure. Un golfeur scratch et un golfeur bogey jouent chacun le trou 50 fois, sans vent, sol sec, consignes du jour ; la moyenne du scratch donne la note du parcours, l'écart avec le bogey donne le slope (formule de l'USGA : slope = 5,381 × (note bogey − note scratch), ramené à 18 trous, borné de 55 à 155).
- **Coût** (fiche D5) : environ 50 × 2 × 4 coups × 15 ms, soit environ 6 s. Le jeu lance le calcul sur un fil à part (`Task.Run` sur une copie du terrain) et rend le résultat à la sim par une commande `World.SetCourseRating(rating)`. Comme toute commande, elle est rejouable et entre dans la somme de contrôle : la sim reste déterministe sans attendre le calcul. Les tests appellent `CourseRater` directement.
- Quand : au lancement, après une retouche du terrain (une fois les retouches finies depuis 10 s) et à chaque changement de saison. Une partie garde la note du jour où elle est jouée.
- Handicap (WHS simplifié) : écart d'une partie = (brut − note) × 113 / slope, ramené à 18 trous ; index = moyenne des 8 meilleurs écarts sur les 20 dernières parties de ligue (moins de parties : barème WHS, aucun index avant 3 parties), borné de 0 à 54. Handicap de jeu = index × slope / 113 + (note − par), ramené au nombre de trous. Avec un seul trou, l'index est bruité ; il se resserrera avec un vrai parcours.

**Ligue** (`src/Golferoo.Sim/League/`)
- `Member` : identifiant, nom tiré de la graine (prénom et initiale), niveau, opinion du club (0 à 100), cartes de ligue jouées. Le club compte 40 membres générés à la création du monde (handicaps de 2 à 36), plus votre pro.
- **Inscriptions** au 1er avril : un membre s'inscrit si son opinion du club atteint 60 (décision 2). Votre pro est toujours inscrit.
- **Divisions** : autant de divisions de 8 que les inscrits en remplissent (21 inscrits : deux divisions, de 11 et 10), une seule s'il y en a moins de 16. La première saison, elles sont rangées par niveau ; ensuite, par l'ajustement de la saison passée, les nouveaux inscrits et votre pro en bas.
- **Calendrier** : une journée de ligue par semaine de 5 jours (le 5 et le 10 de chaque mois) d'avril à août, soit 10 journées ; la finale le 5 septembre ; l'ajustement le lendemain. Sous l'orage (`Sky.HeavyRain`), la journée est reportée au lendemain.
- **Formats tournants** (décision 1), joués en brut pour qu'une montée récompense un vrai progrès ; le handicap reste affiché. Un cycle de quatre : stroke play, stableford, match play (les deux golfeurs d'une paire s'affrontent, paires tirées au sort dans la division), contre le par (chaque trou gagné, partagé ou perdu contre le par).
- **Points de saison** : à chaque journée, classement de la division selon le format (en match play, les vainqueurs devant, départagés au brut) ; chacun marque le nombre de joueurs classés derrière lui, un demi-point par ex aequo.
- **Finale** (décision 4) : les quatre premiers de chaque division jouent un stroke play ; le vainqueur de la division 1 est champion du club. Puis **ajustement** : les deux premiers de chaque division montent et les deux derniers descendent (rien avec une seule division).
- Rythme : une paire ne part que quand la précédente a quitté le green. Au matin d'une journée, la sim crée les paires de ligue (dernière division d'abord) avant les autres golfeurs ; le lot 3 mesure si elles tiennent dans une journée de 6 min, et une journée qui déborde finit le lendemain matin.
- Votre pro joue seul, au niveau fixe d'un handicap 36 environ (hors périmètre : sa progression).
- Tout passe au changement de jour ou par les cartes rendues : rien par tick.

**Rendu** (`game/src/UI/`)
- Touche J (L, K et les autres lettres proches sont prises) : un panneau avec le classement de chaque division (rang, nom, handicap, points), la prochaine journée et son format, la note du parcours et le slope.
- Carte de score du dernier tour d'un joueur au clic sur son nom.
- HUD : « Votre pro : division 2, handicap 31,4 ». Avis pour la finale, le champion et l'ajustement (« Votre pro monte en division 1 »).

**Lots (5 PR)**
1. **Moteur de score, niveau de jeu et par** : `Scoring/` (stroke play, stableford, match play, contre le par, classement), `Golfer.Skill`, par du fichier de trou, calibrage des niveaux scratch et bogey ; tests et banc. (complet)
2. **Note du parcours et handicap** : `CourseRater`, commande `SetCourseRating`, calcul en arrière-plan dans le jeu, index WHS simplifié ; tests et banc. (complet)
3. **Membres et journées de ligue** : membres et opinion, inscriptions, divisions, calendrier, formats tournants, report, points de saison ; tests sur une saison simulée. (complet)
4. **Finale et ajustement** : finale de septembre, champion, montées et descentes, divisions recalculées à chaque saison selon les inscrits ; tests sur deux saisons. (complet)
5. **Écrans de ligue** : panneau J, carte de score, HUD du pro, avis ; captures. (léger)

## Décisions de Guillaume (2026-10-25)
1. **Une partie joue les trous du parcours** (un seul aujourd'hui), **avec un format de ligue qui change d'une semaine à l'autre** : stroke play, match play, stableford, et d'autres qui fonctionnent (ici : contre le par ; les formats par équipes attendent B20).
2. **Les membres s'inscrivent à la ligue parce qu'ils ont une opinion positive du club.**
3. **La progression du pro est contrôlée par le joueur, quelle que soit la situation**, par l'expérience de chaque compétence : pour mieux réussir un draw, il faut en faire en match, plutôt que des points à répartir comme dans SimGolf. À voir plus tard, dans un chantier à part.
4. **Une finale en septembre, puis l'ajustement des divisions.** S'il n'y a pas assez de membres pour plusieurs divisions, on n'en garde qu'une.

## Choix à trancher par Guillaume
1. **Qui joue les coups de votre pro ?** « Contrôlée par le joueur » peut vouloir dire que le joueur choisit lui-même chaque coup du pro (club, visée, effet), ou seulement que sa progression vient de ce qu'il fait en match.
   - A : en C06, le pro joue seul, comme les membres ; jouer ses coups soi-même et sa progression par l'expérience forment un chantier à part, qui ajoutera les effets de balle. Gain : C06 reste une affaire de score et de ligue.
   - B : C06 ajoute déjà un mode où le joueur choisit le club et la visée de chaque coup du pro. Perte : une interface de visée et un lot de plus, sans les effets de balle qui donnent son sens à la progression.
   - **Recommandé : A**, parce que les effets de balle et l'expérience vont ensemble et méritent leur propre cadrage.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent le score (brut, stableford, match play, contre le par ; ex aequo au même rang ; balle ramassée comptée 12), le niveau (sur 200 parties, un golfeur bogey finit en moyenne entre 0,7 et 1,3 coup au-dessus du scratch ; le scratch entre 0 et 0,4 au-dessus du par), la note (même terrain et même graine, même note ; un bunker devant le green augmente la note ; slope entre 55 et 155), le handicap (aucun index avant 3 parties ; 20 parties d'un niveau donné retrouvent son handicap à 3 points près), la ligue (seuls les membres d'opinion au moins 60 s'inscrivent ; 15 inscrits donnent une division, 16 en donnent deux ; une saison a 10 journées dont les formats suivent le cycle ; un orage reporte la journée ; la finale réunit les quatre premiers de chaque division ; l'ajustement fait monter deux joueurs et en descendre deux par frontière de division) et le déterminisme (mêmes commandes, dont `SetCourseRating`, même somme de contrôle).
2. Au niveau 1 et sans ligue, les trajectoires et sommes de contrôle des tests existants de C01 à C05 sont inchangées.
3. Banc : le tick à 200 golfeurs reste sous 2 ms ; un changement de jour, journée de ligue comprise, reste sous 5 ms ; `CourseRater` est mesuré et ne tourne jamais sur le fil de la sim.
4. Captures de la CI (lignes `THUMB`) : le panneau de ligue après une saison simulée, une carte de score, le HUD du pro. Guillaume juge.
5. Fumée Godot (`SMOKE OK`) ; la note du parcours arrive par la commande sans figer le jeu ; une retouche du terrain la change.
6. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (lots 1 à 4 : physique du coup, état de partie, calculs de score) ; léger pour le lot 5 (rendu)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
