# Progression de votre pro par l'expérience (C25)
Statut : proposé
Créé : 2026-10-29

## But
Votre pro progresse lentement, sur plus de dix saisons, et chaque niveau gagné change un aspect précis et mesurable de ses coups. Chaque coup apporte de l'expérience aux habiletés qu'il a sollicitées, selon la qualité du résultat, et non plus selon un « réussi ou raté ». Ce système remplace les deux paliers et le niveau continu de C08 (`specs/C08-pro-jouable.md`, décision 4 et section « Compétences et expérience »).

Ce qui prouve la réussite : un draw long et bien visé au fer long fait progresser Fers longs, Draw, Distance et Précision, chacune selon sa propre qualité ; un pro qui ne joue que des draws progresse en draw et pas en fade ; il faut plus de dix saisons types pour qu'une habileté travaillée atteigne le niveau 50.

## Décisions de Guillaume (cadrage du 2026-10-29, rondes 1, 2 et 2 bis)
1. **Quatre groupes d'habiletés** : les clubs (sept familles), les trajectoires, les qualités du coup et les situations (rough, bunker, vent, green).
2. **Progression très longue** : plus de dix saisons pour amener une habileté travaillée au niveau maximal.
3. **Un gain fixe par niveau**, affiché (exemple : Fers longs niv. 12 = dispersion −12 % par rapport au départ). La longueur de la progression vient du coût en XP de chaque niveau, qui augmente.
4. **XP selon la qualité** : XP = 10 × qualité, de 0 à 1, avec qualité = (1 − écart / seuil)², qui récompense surtout les très bons coups. Seule l'habileté du club a un plancher, **5 %** (0,5 XP pour un coup complètement raté). Les autres habiletés peuvent tomber à 0. L'XP n'est jamais négative.
5. **Pénalité de lie pour tous les golfeurs** : le rough et le bunker gênent le coup lui-même (perte de vitesse et dispersion en plus) selon le niveau du golfeur ; les habiletés « sortie de rough » et « sortie de bunker » du pro la réduisent.
6. **Vent et green par l'aperçu** : à bas niveau, la trajectoire prévue (vent) et la ligne prévue du putt (pente) sont faussées, et deviennent de plus en plus justes en montant. La physique du coup ne change pas.
7. **Plafond : un pro du circuit**. Au niveau 50 de toutes ses habiletés, le pro joue comme un handicap +4. Au niveau 0, comme un handicap 36.
8. Reprises de C08 sans changement : la ligue compte double, sans plafond ; l'entraînement est plafonné par habileté et par année (remise à zéro au 1er avril) ; un gimme ne rapporte rien ; aucune rouille.

## Périmètre et hors périmètre
- Dans le périmètre :
  - `src/Golferoo.Sim/Pro/` : habiletés, niveaux, qualité des coups, attribution de l'XP, effets ;
  - `PhysicalShotResolver` et `BallSimulator` : pénalité de lie ;
  - jeu : fiche du pro, aperçus de visée et de putt, avis Birdie.
- Hors périmètre : équipement, leçons et tournois (chantiers à venir, qui liront les niveaux) ; rouille ; habiletés des autres golfeurs (ils gardent leur handicap de C06) ; lecture des sauvegardes d'avant C25 (aucune alpha distribuée).

## Conception

**Habiletés** (`ProSkill`, 18 valeurs ; l'ordre entre dans la somme de contrôle)

D = distance visée (du départ au point visé). L'écart est mesuré entre l'arrêt de la balle et le point visé : écart latéral par rapport à la ligne de visée, écart en profondeur le long de cette ligne. Chaque seuil vit dans un fichier de données (`data/pro/progression.json`) et se calibre au lot 4.

| Habileté | Gagne de l'XP sur | Écart mesuré, seuil (qualité 0 au seuil) | Effet d'un niveau |
|---|---|---|---|
| Driver, Bois, Fers longs (fer 5), Fers moyens (fer 7), Fers courts (fer 9), Wedge | chaque coup de ce club | écart total ≤ 15 % de D ; × 1 dans le lie visé, × 0,5 dans le rough, × 0 avec une pénalité ; plancher 5 % | dispersion du club, direction et distance |
| Putter | chaque putt hors gimme | putt rentré : qualité 1 ; sinon écart ≤ max(1 m, 20 % de D) ; plancher 5 % | dispersion du putt |
| Précision | tout coup hors putt | écart latéral ≤ 6 % de D | erreur de direction |
| Dosage | approche à force partielle (force < 0,9), et putt | écart en profondeur ≤ 8 % de D | erreur sur la force |
| Distance | driver, bois et fer long à pleine force, sans pénalité | portée obtenue : 0 à 85 % de la portée du club, 1 à 100 % et plus | vitesse de balle |
| Draw, Fade | coup joué avec cet effet | écart entre la courbure obtenue et la courbure voulue ≤ 100 % de la courbure voulue | erreur sur la courbure |
| Chip | coup en chip | écart total ≤ 15 % de D | erreur sur l'abaissement et le roulement |
| Sortie de rough, Sortie de bunker | coup joué depuis ce lie | écart total ≤ 15 % de D | pénalité de lie : vitesse et dispersion |
| Jeu dans le vent | coup avec un vent ≥ 3 m/s | qualité du club × min(1, vent / 10 m/s) | justesse de la trajectoire prévue |
| Lecture du green | putt avec une pente le long de la ligne | écart latéral au trou ≤ max(0,5 m, 10 % de D) | justesse de la ligne prévue |

- Pour Distance et Draw/Fade, l'écart est l'écart relatif de la grandeur mesurée (portée, courbure), et non la distance à la cible.
- Pour une habileté à rampe (Distance), on applique la même règle : qualité = (part du chemin parcouru entre le seuil et l'idéal)².
- Un coup peut nourrir plusieurs habiletés. Chacune reçoit son propre XP, sans partage. Exemple : un draw au fer long à pleine force alimente Fers longs, Draw, Distance et Précision ; il n'alimente pas Dosage (pleine force), ni Vent si le vent est sous 3 m/s.

**Niveaux et coût**
- Les niveaux vont de 0 à 50. Le coût pour passer du niveau n au niveau n+1 est `cost(n) = a + b × n`, avec a et b calibrés au lot 4 : une saison type amène une habileté travaillée vers le niveau 10 à 15, et le niveau 50 demande au moins dix saisons types.
- Les fractions d'XP sont gardées en entiers à l'échelle 1/100, pour que la somme de contrôle reste exacte.
- **Gain fixe par niveau** : chaque effet est interpolé linéairement entre sa valeur au niveau 0 (golfeur 36) et sa valeur au niveau 50 (golfeur +4). Par exemple, la dispersion d'un club suit `SkillLevels.FromHandicap(36 − 0,8 × niveau)`. `FromHandicap` doit accepter les handicaps négatifs jusqu'à +4 (aujourd'hui, la fonction borne à 0).
- Niveau global du pro (handicap affiché) : la moyenne des sept habiletés de club, traduite en handicap. Le handicap WHS reste celui de C06, calculé sur ses cartes de ligue.

**Pénalité de lie (tous les golfeurs)**
- Depuis le rough ou le bunker : la vitesse de balle perd un pourcentage, et les écarts types sont multipliés par un facteur. Les deux valeurs dépendent du niveau du golfeur, ou, pour le pro, de son habileté « sortie de… ». Elles sont en données.
- Le choix du coup (C09) prévoit déjà ces lies ; il doit maintenant voir la vitesse perdue dans son espérance.
- Les cibles de C21 (critère 4 : fairways et greens en régulation par niveau) restent vraies : les écarts types de base sont recalés.

**Aperçus (jeu)**
- Le biais de l'aperçu est déterministe par coup : il est tiré d'un hachage du tick et du numéro de coup, sans toucher au RNG du monde, et son amplitude décroît linéairement avec le niveau.
- Vent : la trajectoire prévue est calculée avec un vent faussé (direction et force).
- Green : la ligne du putt est calculée avec une pente faussée.
- Niveau 50 : biais nul.

**Jeu**
- La fiche du pro (`ProPanel`) montre les 18 habiletés en quatre groupes. Pour chacune : le niveau, la barre d'XP du niveau, l'effet actuel et celui du niveau suivant.
- Un niveau gagné publie un message Birdie (« Fers longs : niveau 13, dispersion −13 % »).

**Sauvegarde** : XP et XP d'entraînement de l'année par habileté. Ces données entrent dans `World.Save` et `World.Checksum`, avec un cas `SaveTests`, et la version de sauvegarde augmente. L'XP de C08 n'est pas convertie.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Sim : 18 habiletés, qualité par habileté, attribution, plancher, ligue et plafond d'entraînement, niveaux et coût, effets des clubs, Précision, Dosage, Distance, trajectoires ; `FromHandicap` jusqu'à +4 ; Save et Checksum | complet |
| 2 | Sim : pénalité de lie pour tous, Sortie de rough et Sortie de bunker, espérance du choix, recalage de C21 critère 4 | complet |
| 3 | Jeu : aperçus faussés (vent, pente), Jeu dans le vent et Lecture du green, fiche du pro, avis Birdie ; captures `c25-fiche` et `c25-apercu` | léger |
| 4 | Calibrage : saisons simulées, a et b, seuils et plafond d'entraînement en données | complet |

Les lots 1 et 2 touchent des fichiers différents (`Pro/` d'un côté, `Shots/` de l'autre) et peuvent avancer en parallèle. Le lot 3 vient après le lot 1, et le lot 4 en dernier.

## Choix à trancher par Guillaume
Aucun : la grille a été validée le 2026-10-29. Les seuils et le rythme se règlent en données au lot 4.

## Critères d'acceptation
1. Test : le draw au fer long, à pleine force, de 170 m, arrêté à 3 m du point visé, donne de l'XP à Fers longs, Draw, Distance et Précision, chacune égale à 10 × sa qualité calculée à la main ; il n'en donne ni à Dosage ni aux autres habiletés.
2. Test : la qualité suit (1 − écart / seuil)² pour chaque habileté (valeurs à 0, à la moitié et au seuil) ; un coup complètement raté donne 0,5 XP au club et 0 aux autres ; aucune XP n'est négative.
3. Test : un coup de ligue compte double ; l'entraînement s'arrête au plafond de l'année et repart au 1er avril ; un gimme ne donne rien.
4. Test : une saison simulée de draws seulement fait progresser Draw et pas Fade ; même graine, même XP au centième.
5. Test : chaque niveau d'une habileté de club réduit sa dispersion mesurée (2 000 coups) du même pas, à ± 1 point près ; au niveau 50, le pro joue comme un +4 sur « Le neuf » (note de parcours de C06, ± 1,5) ; au niveau 0, comme un 36 (± 2).
6. Test : depuis le rough, un golfeur perd de la portée et disperse davantage qu'au fairway, d'autant moins que son niveau est bon ; au niveau 50 de Sortie de rough, la pénalité du pro est au moins réduite de moitié ; le critère 4 de C21 passe toujours.
7. Test du lot 4 : une habileté travaillée à chaque coup d'une saison type (ligue et entraînement au plafond) termine la saison entre les niveaux 10 et 15 ; il faut au moins dix saisons types pour atteindre le niveau 50.
8. Fumée : au niveau 0, l'écart entre l'aperçu et le coup joué sans dispersion, par vent de 8 m/s, dépasse un seuil ; il est nul au niveau 50 ; l'aperçu ne consomme pas le RNG du monde (même somme de contrôle avec ou sans aperçu).
9. Fumée : un niveau gagné publie un message Birdie, et la fiche affiche le niveau, la barre d'XP et l'effet actuel ; captures `c25-fiche` et `c25-apercu`.
10. `SaveTests` échoue si on oublie l'XP d'une habileté ; quatre jobs de CI verts ; `dotnet format` sans changement ; le banc tient (tick moyen sous 2 ms, 99e centile sous 5 ms).

## Niveau de contrôle
Complet pour les lots 1, 2 et 4 (simulation et état de partie), léger pour le lot 3 (écran).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans `docs/chantiers.md`
- [ ] `specs/C08-pro-jouable.md` renvoie à C25 pour la progression
