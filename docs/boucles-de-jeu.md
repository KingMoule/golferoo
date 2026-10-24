# Nouvelles boucles de jeu : analyse une par une

Mis à jour : 2026-10-24. Chantier R03, suite de `docs/pistes-features.md` (groupe D), demandée par Guillaume : analyser chaque boucle en profondeur, avec ses effets sur les autres features et la façon de l'intégrer sous Godot. Les risques R1 à R7 et les efforts S à XL sont ceux de `docs/faisabilite-features.md`.

Rien n'est décidé ici. Chaque fiche finit par une recommandation que Guillaume tranche. Les cinq boucles recommandées pour la première version sont détaillées dans `docs/boucles-v1.md` ; les questions de fin de document sont mises de côté (Guillaume, 2026-10-24).

## En bref
- Les 13 boucles se rangent en quatre familles. Celles qui **changent le terrain lui-même** (météo, entretien, écologie, nuit) nourrissent directement le cœur du jeu : construire, regarder, corriger. Ce sont les plus solides.
- Deux boucles **dépendent de pistes déjà acceptées** et coûtent peu une fois celles-ci faites : les ligues (handicap, B10) et les scénarios (tutoriel, A6).
- Deux boucles **multiplient la simulation** et passent après la preuve P1 : la chaîne de clubs (plusieurs parcours) et, dans une moindre mesure, les événements privés (plus d'agents).
- Le refus des prix fixés (B7) ne bloque aucune boucle. La réputation et les événements passent par l'humeur, l'affluence et des offres à prix donné, pas par un tarif.
- **Recommandation pour une première version** : météo et saisons (D1), entretien du gazon (D2), ligues (D5), scénarios (D7) et mode photo (D12). **Ensuite** : réputation (D3), événements (D4), école de golf (D6), écologie (D8), golf de nuit (D13). **Plus tard** : rivaux (D9), chaîne de clubs (D10), partage (D11).

## Les quatre familles

| Famille | Boucles | Ce qu'elles touchent |
|---|---|---|
| Le terrain vivant | D1 météo et saisons, D2 entretien, D8 écologie, D13 golf de nuit | la surface, la balle, le rendu |
| La vie du club | D4 événements, D5 ligues, D6 école de golf | les golfeurs, le calendrier, le personnel |
| Le club dans le monde | D3 réputation, D9 rivaux, D10 chaîne de clubs | l'affluence, la carte du monde |
| Autour du jeu | D7 scénarios, D11 partage, D12 mode photo | les modes, les fichiers, la caméra |

## D1. Météo et saisons

**La boucle.** Chaque jour a sa météo, tirée de façon déterministe selon la région et la saison. Le joueur la voit venir (prévisions sur quelques jours) et s'adapte : fermer le parcours sous l'orage, déplacer les drapeaux par grand vent, avancer un tournoi. L'année gagne un rythme : printemps humide, été sec, automne coloré, et un hiver où le parcours ferme et où l'on construit sans golfeurs.

**Simulation.**
- Un état météo par jour : vent (direction, force, rafales), pluie, température. Il tient en quelques octets et ne coûte rien au tick.
- Le vent entre dans `BallSimulator` comme une force sur la balle en vol. Le planificateur de coups utilise le même simulateur, donc les golfeurs tiennent compte du vent sans règle à part. Ce qu'on voit reste ce que la physique fait.
- La pluie modifie l'humeur, l'affluence et le sol : un sol mouillé roule moins (frottement et rebond par surface, déjà dans la table des surfaces).
- L'hiver est un état du calendrier : pas d'arrivées, entretien réduit, travaux possibles. Le calendrier de l'original (8 mois, de mars à octobre) se prolonge naturellement.

**Godot.**
- Ciel et lumière : `ProceduralSkyMaterial` ou `PhysicalSkyMaterial`, soleil qui tourne avec l'heure, `Environment` par type de temps (brouillard, nuages).
- Pluie et neige : `GPUParticles3D` attachées à la caméra, donc d'un coût fixe quelle que soit la taille de la carte.
- Sol mouillé, neige, feuillage d'automne : de simples paramètres du shader de terrain et du shader des arbres (couleur par instance dans le `MultiMesh`). Pas d'assets en plus.
- Vent visible : drapeau qui flotte (il existe), arbres qui ploient par un léger décalage de sommets dans leur shader, manche à air sur les tees.

**Effets sur les autres features.**
- Analyse de coup (B4) et swing contrôlable (B17) : il faut montrer le vent sur la trajectoire prévue. C'est gratuit puisque la prévision vient du même simulateur.
- Tournois avec cut (B18) : la météo du jour fait des surprises, et un orage peut reporter un tour.
- Évaluation SGA : le temps de jeu cible varie avec la météo ; il faut l'en corriger pour ne pas punir un parcours venteux.
- Plusieurs départs (B1) et drapeaux mobiles : ils deviennent des outils de réponse au vent.
- Entretien (D2) : l'arrosage dépend de la pluie. C'est le couplage le plus fort.
- Vitesse du temps (A1) : l'avance rapide doit s'arrêter avant un orage annoncé.

**Risques et effort.** Aucun risque de performance (R1 épargné). Le seul risque est l'équilibrage : un vent trop fort rend les golfeurs malheureux sans que le joueur y puisse rien. Effort **M**.

**Version minimale.** Vent sur la balle, pluie qui fait baisser l'affluence, couleurs des saisons, hiver fermé.

**Recommandation : première version.** C'est la boucle la moins chère au regard de ce qu'elle change, et elle montre la 3D.

## D2. Entretien du gazon (agronomie)

**La boucle.** Le terrain s'use et se soigne. Le passage des golfeurs et des voiturettes tasse l'herbe, les coups arrachent des mottes, la sécheresse jaunit le fairway. Le joueur règle la hauteur de tonte, l'arrosage et la vitesse des greens, et répartit son personnel. Il voit le résultat à l'écran (bandes de tonte, plaques jaunes) et dans le jeu (greens rapides, fairways fermes).

**Simulation.**
- Un état par cellule : santé, hauteur, humidité, en quelques octets. Pour une carte de ~100 000 cellules, c'est environ 0,5 Mo.
- La mise à jour n'a pas besoin d'être faite à chaque tick : on traite une tranche de la carte par tick (par exemple 1/100), pour un coût fixe et faible.
- L'usure s'incrémente là où les golfeurs marchent et là où les coups partent : la sim connaît déjà ces positions.
- L'état modifie les propriétés de la surface : un green tondu court roule plus vite (le frottement du putt), un fairway sec et ferme rebondit plus. **Le « Firm Fairway » peint de l'original devient un état qu'on obtient par l'entretien**, ce qui est plus riche. On garde la surface peinte pour ne pas retirer d'outil.
- Les mauvaises herbes de l'original s'y rangent : une herbe malade les laisse pousser.
- Le jardinier de l'original prend du relief : il tond, arrose et répare selon des consignes. On évite la microgestion avec des **politiques** (« greens rapides pour le tournoi », « économiser l'eau ») que le personnel applique seul.

**Godot.**
- Le shader de terrain lit déjà une texture d'une texel par cellule pour les surfaces. On en ajoute une seconde pour l'état (santé, hauteur, sens de tonte). Plaques jaunes, herbe haute et **bandes de tonte** se dessinent dans le shader, sans géométrie en plus. Les bandes de tonte sont un marqueur visuel fort des parcours modernes.
- Arroseurs : petits objets avec des particules, actifs seulement à l'écran.
- Tondeuses : véhicules rigides qui suivent un trajet, sans squelette. Faciles en script `bpy`.

**Effets sur les autres features.**
- Météo (D1) : la pluie arrose, la canicule assèche.
- Tournois (B18) : la liste de préparation de l'original (greens lissés, deep rough) devient de vraies actions d'entretien, qui prennent du temps et coûtent.
- Personnel : le jardinier et le technicien gagnent en intérêt ; les zones de travail de l'original deviennent des tournées.
- Plaisir et évaluation : un parcours mal tenu déplaît, ce qui donne une raison de dépenser une fois le parcours construit.
- Économie : l'eau et l'entretien deviennent des postes de dépense. C'est le budget d'entretien de la piste B8, encore à trier.
- Écologie (D8) : l'eau, les engrais et les pesticides sont le levier écologique.

**Risques et effort.** Risque R1 faible si la mise à jour est étalée. Risque de conception : la microgestion, d'où les politiques. Effort **M**, **L** si l'on fait les véhicules d'entretien animés.

**Version minimale.** Usure et santé par cellule, effet sur le roulement, vitesse des greens réglable, bandes de tonte, et une politique d'entretien par zone.

**Recommandation : première version**, avec D1. Les deux ensemble donnent un parcours vivant qui change d'un jour à l'autre, ce que l'original n'avait pas.

## D3. Réputation et presse

**La boucle.** Le club a une réputation auprès de plusieurs publics : les gens du coin, les touristes, les joueurs exigeants. Les golfeurs laissent des avis à partir de leurs pensées. Une fois par an, des magazines publient des classements (meilleur nouveau parcours, meilleur trou signature). Le joueur lit les avis, voit ce qui plaît ou fâche, et vise une place.

**Simulation.**
- Les pensées sont déjà comptées par trou et par type dans l'original (Player Comments). Un avis est un résumé tiré de ces compteurs : aucune nouvelle mesure n'est nécessaire.
- La réputation par public règle l'affluence et les profils de golfeurs qui viennent. C'est un changement par rapport à l'original, où seuls les invités viennent : il faut choisir si la réputation remplace les invitations ou s'ajoute à elles (question de conception pour la séance).
- Les classements de magazines prolongent le Top 10 Designers, le Top 100 et le Top 18 de l'original.
- Le journaliste peut être un VIP de plus (piste B15).

**Godot.**
- Un fil d'avis et une page de magazine : de l'UI (R3).
- La couverture du magazine est une photo du plus beau trou, prise par le même mécanisme que SimFoto (une caméra dans un `SubViewport`, rendue en image). C'est peu cher et très parlant.

**Effets sur les autres features.**
- Prix non fixés (B7 non) : la réputation ne peut pas jouer sur un tarif. Elle joue sur l'affluence, sur la part de membres Gold et Platinum (qui paient plus par trou) et sur les offres de tournois et d'événements.
- Textes des avis : mêmes morceaux que les pensées (A4), donc le même travail de contenu (R4).
- Écologie (D8) et événements (D4) en profitent : une certification ou un mariage réussi font parler du club.

**Risques et effort.** R3 et R4. Effort **M**.

**Version minimale.** Avis tirés des compteurs de pensées, une note par public, un classement annuel.

**Recommandation : ensuite.** Elle donne un but lisible, mais elle a besoin d'une UI solide (P3) et de textes variés (A4) pour ne pas sembler mécanique.

## D4. Événements privés

**La boucle.** Le club reçoit des demandes : tournoi d'entreprise, tournoi caritatif, mariage sur le green du 18. Chacune a ses exigences et son prix proposé. Le joueur accepte ou refuse, prépare (tentes, parcours fermé une demi-journée) et en subit les effets : les membres râlent si on leur ferme le parcours, l'événement réussi rapporte argent et réputation.

**Simulation.**
- Un calendrier de réservations. Chaque type d'événement a des exigences calculables : un mariage veut une vue (la sim sait déjà ce qui est « scénique »), un tournoi d'entreprise veut des groupes de quatre et un jeu rapide, un tournoi caritatif attire des célébrités.
- Le départ en shotgun existe déjà pour les tournois. Les groupes de quatre sont la piste B11, encore à trier : un tournoi d'entreprise les rend nécessaires.
- Les invités d'un mariage sont des agents qui ne jouent pas : marche et attente seulement, donc peu chers pour la sim.
- Le prix est fixé par l'offre, pas par le joueur : c'est compatible avec B7 non.

**Godot.**
- Accessoires (tentes, tables, arche, podium) en script `bpy` : objets rigides, R5.
- Les invités sont des personnages animés (R2), mais avec peu d'animations (marcher, se tenir debout, applaudir).
- Un écran de calendrier (R3).

**Effets sur les autres features.**
- Adhésion : fermer le parcours aux membres fait baisser leur humeur. C'est la tension de la boucle.
- Tournois (B18) : même mécanique de départ et de classement, donc du code partagé.
- Réputation (D3) : un événement réussi ou raté fait parler.
- Météo (D1) : un mariage sous la pluie, c'est une histoire.

**Risques et effort.** R1 modéré (plus d'agents pendant l'événement), R2, R3. Effort **M**.

**Version minimale.** Tournois d'entreprise et caritatifs seulement, sans invités non joueurs.

**Recommandation : ensuite**, après la preuve P1 et les groupes de quatre.

## D5. Ligues de membres

**La boucle.** Le club organise des ligues : un jour par semaine, les membres inscrits jouent en net (score moins handicap), un classement court sur la saison et une finale la clôt. Le joueur choisit le format et les trous, et voit naître des rivalités.

**Simulation.**
- Le handicap (B10, accepté) est la seule vraie dépendance : sans lui, pas de score net. Avec lui, la ligue est un calendrier, un classement et quelques règles.
- Les rivalités alimentent les relations entre membres (piste B12, encore à trier) et les histoires.
- Une ligue fidélise : un membre inscrit démissionne moins vite.

**Godot.** Un écran de classement et une annonce de finale (R3, petits).

**Effets sur les autres features.**
- Handicap et mémoire (B10) : la ligue leur donne une raison d'être visible.
- Adhésion : un nouvel argument pour garder ses membres.
- Formats de jeu (B20, à trier) : skins, scramble et match play en équipe font de bons formats de ligue.

**Risques et effort.** Aucun risque technique. Effort **S** une fois B10 fait.

**Recommandation : première version**, avec B10. C'est le meilleur rapport coût sur intérêt de la liste.

## D6. École de golf

**La boucle.** Le club ouvre une école. Des juniors s'inscrivent, prennent des leçons avec le personnel ou avec votre pro, progressent saison après saison, et certains deviennent membres, puis pros. On les retrouve ensuite dans les tournois et les défis.

**Simulation.**
- La progression des compétences existe dans l'original (le pro gagne des points ; putting green, pro shop et driving range améliorent les golfeurs). L'école applique la même règle à des élèves, avec un rythme plus lent et des étapes.
- Les élèves qui passent pros entrent dans la liste des pros (96 dans l'original) et peuvent vous défier : c'est une boucle longue et attachante.
- Le pro jouable (B17) peut donner les leçons : ses compétences comptent.

**Godot.**
- Des enfants à l'écran (l'original proposait déjà un pro enfant) : un gabarit de personnage de plus, R2.
- Une fiche d'élève et un écran de promotion (R3).

**Effets sur les autres features.**
- Tournois avec cut (B18) : vos anciens élèves y jouent.
- Histoires : un élève qui gagne contre son professeur est une histoire toute faite.
- Bâtiments : le putting green et le driving range gagnent un usage.

**Risques et effort.** R2 (enfants), R4 (noms, fiches). Effort **M**.

**Recommandation : ensuite.** Elle est forte, mais elle prend son sens quand les tournois et les défis existent.

## D7. Scénarios

**La boucle.** Des parties courtes avec un objectif écrit et des règles propres : sauver un club en faillite en trois ans, construire un links sur une île sans arbres, faire un 9 trous rentable avec 50 000 $. On gagne, on perd, on recommence.

**Simulation.**
- Un scénario est un fichier de données : une carte de départ, des modificateurs de règles et des conditions de victoire vérifiées à chaque fin de mois.
- Il demande la sauvegarde de partie (R7) pour démarrer d'un état écrit à la main.
- La sim étant déterministe, **un scénario est aussi un test** : on le joue en accéléré dans `dotnet test` pour vérifier qu'il reste gagnable après un changement d'équilibrage. C'est un gain pour le développement, pas seulement pour le joueur.

**Godot.**
- Un écran de choix, un écran d'objectifs et un écran de victoire (R3).
- Un outil pour fabriquer les cartes de départ : le jeu lui-même en mode bac à sable, avec un bouton « enregistrer comme scénario ».

**Effets sur les autres features.**
- Tutoriel (A6, accepté) : le tutoriel est une suite de scénarios guidés. Faire l'un donne presque l'autre.
- Sandbox de l'original : même moteur de partie, sans objectif.
- Partage (D11) : un scénario se partage comme un parcours.

**Risques et effort.** R4 (écrire de bons scénarios), R7. Effort **M**, partagé avec A6.

**Recommandation : première version**, construite en même temps que le tutoriel.

## D8. Écologie

**La boucle.** Le terrain non joué peut devenir un habitat : prairie naturelle, zone humide, bois. La faune vient selon la qualité des habitats. L'eau et les produits de l'entretien (D2) pèsent sur un score écologique. Une certification (inventée, sans marque réelle) récompense un club exemplaire. La tension : un parcours très vert et rapide coûte en eau et en produits.

**Simulation.**
- Une carte des habitats calculée à partir des surfaces, comme l'aura : peu chère, recalculée de temps en temps.
- La faune de l'original apparaît déjà selon le thème et l'habitat. Ici, son nombre dépend de la qualité de l'habitat.
- Le score écologique s'appuie sur l'arrosage et les produits de D2. **Sans D2, cette boucle n'a presque rien à mesurer.**

**Godot.**
- Animaux animés (R2), en plus grand nombre que dans l'original. Les oiseaux en vol peuvent être de simples particules ou un `MultiMesh` animé par shader, peu chers.
- Une surcouche « habitats » dans le shader de terrain, comme l'aura.

**Effets sur les autres features.**
- Entretien (D2) : couplage fort, c'est le même budget.
- Réputation (D3) : la certification fait parler du club.
- Landmarks et décors coupés (C1, C2, acceptés) : l'étang à carpes koï et la source chaude sont des habitats tout trouvés.

**Risques et effort.** R2. Effort **M**.

**Recommandation : ensuite**, après D2.

## D9. Rivaux

**La boucle.** D'autres clubs existent sur la carte du monde et vous disputent les golfeurs, les tournois et les classements. Un rival ouvre un parcours voisin, attire vos membres Gold, et il faut répondre.

**Simulation.**
- Un rival ne peut pas être simulé en entier, ce serait une seconde partie complète (R1). Il est simulé de façon abstraite : une note, une affluence, des événements tirés au sort.
- Le risque est qu'il paraisse artificiel : un chiffre qui monte sans qu'on voie pourquoi.

**Godot.** La carte du monde et une fiche par rival (R3). On pourrait visiter un rival en vue fixe, mais il faudrait générer son parcours.

**Effets sur les autres features.** Réputation (D3), dont c'est le prolongement naturel ; tournois, que le rival peut vous prendre.

**Risques et effort.** Le risque principal est de conception. Effort **L** à cause de l'équilibrage.

**Recommandation : plus tard**, après D3.

## D10. Chaîne de clubs

**La boucle.** On garde ses propriétés au lieu de les quitter, comme l'imposait l'original, et on gère un groupe de clubs. C'est une progression de fin de partie.

**Simulation.**
- Faire tourner plusieurs parcours en entier multiplie R1. Il faut donc que seule la propriété active tourne en détail, et que les autres tournent sur un modèle abstrait, calibré sur leurs derniers mois réels.
- Les sauvegardes grossissent (R7).

**Godot.** On change de propriété par la carte du monde : chargement d'une scène par propriété, ce que Godot fait bien. Un tableau de bord du groupe (R3).

**Effets sur les autres features.** Carte du monde et achat de propriétés de l'original ; rivaux (D9), avec qui elle partage le modèle abstrait.

**Risques et effort.** R1, R7. Effort **L**.

**Recommandation : plus tard**, après P1, en même temps que D9.

## D11. Partage et défis

**La boucle.** Publier son parcours, jouer celui d'un autre, battre un score avec son pro, se mesurer sur un classement.

**Simulation.**
- Un parcours partagé est un fichier de données : le même format que la sauvegarde (R7), sans l'état de la partie.
- La sim déterministe rend un score **vérifiable** : on rejoue la graine et les coups, et on obtient le même résultat. C'est un vrai atout pour des classements honnêtes.

**Godot.**
- Steam Workshop et classements Steam par Steamworks.NET, une bibliothèque en C# pur qui marche sous Godot .NET. Il faut un compte partenaire Steam (environ 100 $ par jeu).
- Les fichiers partagés ne contiennent que des données, jamais de code : pas de risque de sécurité.

**Effets sur les autres features.** Mode championnat de l'original, qui faisait déjà jouer un parcours sauvegardé ; scénarios (D7) ; swing contrôlable (B17), qui rend les défis intéressants.

**Risques et effort.** R6, R7. Effort **L**.

**Recommandation : plus tard**, vers la sortie. Mais le format de fichier de parcours doit être pensé dès la sauvegarde pour le rendre partageable.

## D12. Mode photo et visite libre

**La boucle.** Se promener sur son parcours à hauteur de golfeur, ou survoler le parcours librement, et prendre des photos réglées (cadrage, profondeur de champ, filtres). C'est la caméra libre de la piste A9 poussée au bout.

**Simulation.** Aucune. La marche au sol suit `HeightField.Sample`, la fonction de hauteur partagée.

**Godot.**
- Caméra libre en perspective, déplacement au sol par un `CharacterBody3D` simple ou un déplacement direct sur la hauteur.
- Profondeur de champ et exposition par `CameraAttributes`, photo en haute résolution par un `SubViewport`.
- Effort **S** pour la caméra et la photo.

**Effets sur les autres features.**
- C'est là que la qualité des assets se juge de près : arbres (C02), golfeurs (R2), bâtiments (R5). Le mode photo est une exigence de qualité autant qu'une feature.
- SimFoto et la couverture de magazine (D3) utilisent le même mécanisme.
- Marketing : les captures Steam viendront de là.

**Risques et effort.** R2 et R5 par ricochet. Effort **S**.

**Recommandation : première version**, avec la caméra libre (A9).

## D13. Golf de nuit

**La boucle.** Éclairer certains trous et le practice pour ouvrir le soir : balles lumineuses, ambiance différente, revenus en soirée, coût de l'électricité.

**Simulation.** Des heures d'ouverture plus longues et un coût d'énergie. Les golfeurs voient moins bien leur balle hors des zones éclairées, avec un effet sur l'humeur. La sim a besoin d'une heure du jour, qui vient de D1.

**Godot.**
- Le rendu Forward+ en grappes gère des centaines de lumières ponctuelles. Les lampadaires restent **sans ombres**, sinon le coût monte vite.
- Balles et drapeaux émissifs, halo par l'effet `glow` de l'`Environment`.
- Une mesure de FPS est nécessaire : une vue rasante de nuit avec beaucoup de lumières est le pire cas du rendu.

**Effets sur les autres features.** Météo (D1) pour le cycle jour et nuit ; économie ; mode photo (D12), car la nuit fait de belles images.

**Risques et effort.** Coût du rendu à mesurer. Effort **M**.

**Recommandation : ensuite**, une fois le cycle jour et nuit de D1 en place.

**Décidé (2026-10-24)** : abandonné. Pas de nuit dans le jeu, le golf se joue de jour.

## Récapitulatif

| Boucle | Dépend de | Effort | Risques | Recommandation | Tri |
|---|---|---|---|---|---|
| D1 Météo et saisons | | M | équilibrage | première version | |
| D2 Entretien du gazon | D1 (conseillé) | M | microgestion | première version | |
| D3 Réputation et presse | P3, A4 | M | R3, R4 | ensuite | |
| D4 Événements privés | P1, B11 | M | R1, R2, R3 | ensuite | |
| D5 Ligues de membres | B10 | S | | première version | |
| D6 École de golf | B17, B18 | M | R2, R4 | ensuite | |
| D7 Scénarios | sauvegarde (R7), A6 | M | R4, R7 | première version | |
| D8 Écologie | D2 | M | R2 | ensuite | |
| D9 Rivaux | D3 | L | conception | plus tard | |
| D10 Chaîne de clubs | P1, D9 | L | R1, R7 | plus tard | |
| D11 Partage et défis | sauvegarde (R7) | L | R6, R7 | plus tard | |
| D12 Mode photo et visite | A9 | S | R2, R5 | première version | |
| D13 Golf de nuit | D1 | M | rendu | ensuite | |

## Questions pour la séance
- **Affluence** : est-ce que la réputation (D3) remplace les invitations de l'original, ou s'y ajoute ? Ça change la façon dont le club grandit.
- **Hiver** : est-ce qu'on veut une saison fermée où l'on construit sans golfeurs, ou une année de jeu continue ?
- **Microgestion de l'entretien** (D2) : uniquement des politiques confiées au personnel, ou aussi des réglages fins par zone pour qui le veut ?
