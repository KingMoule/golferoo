# Handicap, moteur de score et ligues
Statut : proposé
Créé : 2026-10-25

## But
La fiche D5 de `docs/boucles-v1.md`, avec les décisions du 2026-10-24 (divisions avec montées et descentes, votre pro part de la plus basse, seuls les scores de ligue comptent pour son handicap, classement en brut), ramenée à ce que le jeu porte aujourd'hui : un seul trou, des paires anonymes qui jouent toutes aussi bien, aucun membre, aucune UI de gestion (P3 pas encore faite).
1. **Un moteur de score partagé (F5)** : carte de score, brut, net, stableford, classement avec ex aequo. Les tournois (B18) et les formats (B20) le reprendront tel quel.
2. **Des golfeurs qui ne jouent pas tous pareil** : un niveau de jeu par golfeur, qui règle sa dispersion et sa longueur.
3. **Le parcours se note seul (B10)** : un golfeur fictif scratch et un golfeur bogey jouent le trou ; on en tire la note du parcours et le slope, comme sur une vraie carte de score.
4. **Le handicap se calcule comme dans la réalité**, en simplifié (World Handicap System) : moyenne des meilleurs écarts sur les dernières parties de ligue, rapportés à la note du parcours.
5. **La ligue du club** : des membres répartis en trois divisions, des journées de ligue sur la saison d'avril à septembre, un classement en brut, des montées et des descentes à la fin de la saison. Votre pro joue en classement depuis la division la plus basse et progresse.

Ce qui prouve la réussite : sur une saison simulée, les meilleurs joueurs finissent en tête de la division 1, le handicap de chaque membre retrouve à quelques points près celui de son niveau de jeu, et votre pro, parti en division 3, peut monter.

## Périmètre et hors périmètre
**Dans le périmètre**
- `Scoring/` dans la sim : carte de score, brut, net, stableford, classement.
- Niveau de jeu par golfeur, passé au résolveur de coups ; par du trou.
- `Rating/` : note du parcours et slope par simulation, handicap des joueurs.
- `League/` : membres, divisions, calendrier, journées, classement de saison, montées et descentes, votre pro et sa progression, report d'une journée sous l'orage.
- Rendu : classement, carte de score du dernier tour, handicap du pro au HUD, avis de fin de saison, avec des `Control` simples comme le panneau des consignes de C05.

**Hors périmètre, et où ça ira**
- Parcours de plusieurs trous : le moteur de score compte les trous qu'a le parcours (un aujourd'hui) et marchera tel quel quand il y en aura dix-huit.
- Adhésion, cotisations, fidélité d'un membre inscrit (idée 7) : avec l'économie et l'adhésion.
- Rivalités (idée 6, piste B12 pas encore triée) et humeur : avec l'humeur.
- Vitrine des trophées et tableau d'affichage (objets `bpy`) : avec les bâtiments.
- Finale de saison, match play, scramble (B20), stableford comme format de ligue : le moteur les calcule, la ligue de C06 n'en joue pas (choix 4).
- Feuille de départs (Q3) : les paires de ligue partent simplement les premières le jour de ligue.
- Thème et composants communs de l'UI : preuve P3 ; les écrans de C06 seront rhabillés par elle.
- Jouer soi-même les coups du pro : jamais demandé ; le pro joue seul, comme les membres.

## Conception
**Moteur de score** (`src/Golferoo.Sim/Scoring/`, sans Godot)
- `ScoreCard` : par et coups de chaque trou (balle ramassée = 12 coups, `World.MaxStrokes`), brut, net (brut moins le handicap de jeu), points stableford.
- `Leaderboard` : tri stable par une clé (brut, net ou points), ex aequo au même rang (« T3 »).
- Le par du trou entre dans le fichier de trou (`"par": 4`) ; sans champ, il se déduit de la longueur du tee au drapeau (barèmes de l'USGA : jusqu'à 230 m par 3, jusqu'à 430 m par 4, au-delà par 5).

**Niveau de jeu** (`Golfers/Golfer.cs`, `Shots/`)
- `Golfer.Skill` : un réel qui multiplie les écarts types du club (direction et vitesse) et réduit un peu la vitesse de balle des moins bons. `Skill = 1` est le golfeur actuel : à ce niveau, trajectoires et sommes de contrôle de C01 à C05 restent identiques au bit près.
- `ShotRequest` porte le niveau. La planification reste la même pour tous ; seule l'exécution change.
- Deux niveaux de référence calibrés au lot 1 sur le trou du prototype : **scratch** (handicap 0) et **bogey** (handicap 20 environ, un coup de plus que le par par trou en moyenne). Les niveaux intermédiaires s'interpolent entre eux, et au-delà jusqu'au handicap 54.

**Note du parcours et handicap** (`src/Golferoo.Sim/Rating/`)
- `CourseRater.Rate(terrain, seed)` : fonction pure. Un golfeur scratch et un golfeur bogey jouent chacun le trou 50 fois, sans vent, sol sec, consignes du jour ; la moyenne du scratch donne la note du parcours, l'écart avec le bogey donne le slope (formule de l'USGA : slope = 5,381 × (note bogey − note scratch), ramené à 18 trous, borné de 55 à 155).
- **Coût** (fiche D5) : environ 50 × 2 × 4 coups × 15 ms, soit environ 6 s. Le jeu lance le calcul sur un fil à part (`Task.Run` sur une copie du terrain) et rend le résultat à la sim par une commande `World.SetCourseRating(rating)`. Comme toute commande, elle est rejouable et entre dans la somme de contrôle : la sim reste déterministe sans attendre le calcul. Les tests appellent `CourseRater` directement.
- Quand : au lancement, après une retouche du terrain (une fois les retouches finies depuis 10 s) et à chaque changement de saison. Une partie garde la note du jour où elle est jouée.
- Handicap (WHS simplifié) : écart d'une partie = (brut − note) × 113 / slope, ramené à 18 trous ; index = moyenne des 8 meilleurs écarts sur les 20 dernières parties de ligue (moins de parties : barème WHS, aucun index avant 3 parties), borné de 0 à 54. Handicap de jeu = index × slope / 113 + (note − par), ramené au nombre de trous.
- Avec un seul trou, l'index est bruité (un trou ramené à 18) ; il se resserrera de lui-même avec un vrai parcours.

**Ligue** (`src/Golferoo.Sim/League/`)
- `Member` : identifiant, nom tiré de la graine (prénom et initiale), niveau, cartes de ligue jouées. 23 membres générés à la création du monde (handicaps de 2 à 36), plus votre pro.
- Trois divisions de 8 joueurs, rangées au départ par niveau ; votre pro est le huitième de la division 3.
- Calendrier : une journée de ligue tous les 5 jours d'avril à septembre, soit 12 journées par saison (le 5 et le 10 de chaque mois). Au matin d'une journée, la sim crée les paires de ligue (division 3 d'abord) avant les autres golfeurs ; elles jouent le parcours et rendent leurs cartes. Sous l'orage (`Sky.HeavyRain`), la journée est reportée au lendemain.
- Rythme : une paire ne part que quand la précédente a quitté le green, et 12 paires tiennent peut-être mal dans une journée de 6 min. Le lot 3 le mesure ; une journée qui déborde finit simplement le lendemain matin, avant les autres golfeurs.
- Classement de la journée en brut dans chaque division ; points de saison = nombre de joueurs battus dans la division, un demi-point par ex aequo. Fin septembre : les deux premiers montent, les deux derniers descendent.
- Votre pro (`League.Pro`) part avec un niveau faible (handicap 36 environ) et progresse à chaque partie de ligue jouée (choix 3). Seules ses parties de ligue comptent pour son handicap, comme pour les membres.
- Tout passe au changement de jour ou par les cartes rendues : rien par tick.

**Rendu** (`game/src/UI/`)
- La ligue s'ouvre avec la touche J (L, K et les autres lettres proches sont prises). Un panneau : classement de chaque division (rang, nom, handicap, points), prochaine journée, note du parcours et slope.
- Carte de score du dernier tour d'un joueur au clic sur son nom.
- HUD : « Votre pro : division 3, handicap 31,4 ». Avis en fin de saison (« Votre pro monte en division 2 »).

**Lots (4 PR)**
1. **Moteur de score, niveau de jeu et par** : `Scoring/`, `Golfer.Skill`, par du fichier de trou, calibrage des niveaux scratch et bogey ; tests et banc. (complet)
2. **Note du parcours et handicap** : `CourseRater`, commande `SetCourseRating`, calcul en arrière-plan dans le jeu, index WHS simplifié ; tests et banc. (complet)
3. **Ligue** : membres, divisions, calendrier, journées, report, classement de saison, montées et descentes, votre pro et sa progression ; tests sur une saison simulée. (complet)
4. **Écrans de ligue** : panneau J, carte de score, HUD du pro, avis de fin de saison ; captures. (léger)

## Choix à trancher par Guillaume
1. **Une partie sur un parcours d'un seul trou.**
   - A : une partie joue tous les trous du parcours, un seul aujourd'hui. Le handicap est ramené à 18 trous. Gain : rien de jetable, tout marchera avec dix-huit trous. Perte : un tour de ligue dure un trou, et un index sur un trou est bruité.
   - B : en attendant, une partie de ligue joue le même trou 9 fois. Gain : des scores plus lisibles. Perte : du code à retirer plus tard, et 9 fois plus de temps sur le trou unique.
   - C : attendre un chantier « parcours de plusieurs trous » avant la ligue. Perte : C06 recule derrière un chantier lourd (R1).
   - **Recommandé : A**, parce que rien ne sera à défaire et que le jeu n'a pour l'instant qu'un trou de démonstration.
2. **Les membres de la ligue.**
   - A : 23 membres générés par la graine, avec un niveau fixe, qui ne viennent que les jours de ligue. Gain : simple, et la ligue a des têtes stables. Perte : on ne les croise pas les autres jours.
   - B : les mêmes, mais les paires créées les autres jours (touche G) piochent aussi parmi eux. Perte : leurs parties hors ligue ne comptent pas, ce qui peut surprendre.
   - **Recommandé : A** ; l'adhésion fera venir les membres les autres jours.
3. **La progression de votre pro.**
   - A : automatique, un peu de niveau à chaque partie de ligue jouée, avec un plafond. Gain : aucune UI, il peut monter en une ou deux saisons. Perte : le joueur n'y est pour rien.
   - B : des points d'expérience à répartir (précision, longueur, putting), comme dans l'original. Perte : un écran de plus et un niveau à plusieurs composantes.
   - C : niveau fixe en C06. Perte : il ne montera jamais, ce qui casse la boucle.
   - **Recommandé : A**, B pourra s'y greffer avec l'écran du pro de P3.
4. **La fin de saison.**
   - A : classement aux points sur 12 journées, deux montées et deux descentes par division, pas de finale. Gain : simple. Perte : pas de grand jour.
   - B : la même saison plus une finale en septembre entre les quatre premiers de chaque division. Perte : un format de plus à tester, qui doublonne avec les tournois (B18).
   - **Recommandé : A**, la finale viendra avec les tournois.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent le score (brut, net, stableford ; ex aequo au même rang ; balle ramassée comptée 12), le niveau (sur 200 parties, un golfeur bogey finit en moyenne entre 0,7 et 1,3 coup au-dessus du scratch ; le scratch entre 0 et 0,4 au-dessus du par), la note (même terrain et même graine, même note ; un bunker devant le green augmente la note ; slope entre 55 et 155), le handicap (aucun index avant 3 parties ; 20 parties d'un niveau donné retrouvent son handicap à 3 points près), la ligue (une saison simulée de 12 journées : chaque membre a 12 cartes sauf report ; deux montées et deux descentes par division ; un orage reporte la journée ; votre pro gagne du niveau à chaque partie) et le déterminisme (mêmes commandes, dont `SetCourseRating`, même somme de contrôle).
2. Au niveau 1 et sans ligue, les trajectoires et sommes de contrôle des tests existants de C01 à C05 sont inchangées.
3. Banc : le tick à 200 golfeurs reste sous 2 ms ; un changement de jour, journée de ligue comprise, reste sous 5 ms ; `CourseRater` est mesuré et ne tourne jamais sur le fil de la sim.
4. Captures de la CI (lignes `THUMB`) : le panneau de ligue après une saison simulée, une carte de score, le HUD du pro. Guillaume juge.
5. Fumée Godot (`SMOKE OK`) ; la note du parcours arrive par la commande sans figer le jeu ; une retouche du terrain la change.
6. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
complet (lots 1 à 3 : physique du coup, état de partie, calculs de score) ; léger pour le lot 4 (rendu)

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
