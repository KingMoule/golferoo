# Faisabilité des features de SimGolf sous Godot

Mis à jour : 2026-10-24. Chantier R02. Revue, feature par feature, de `docs/recensement-sim-golf.md` à la lumière du prototype C01 (`docs/prototype-c01-rapport.md`) et de l'ADR 0001. Aucune décision de conception ici : on dit ce qui est faisable, ce qui reste à prouver et ce que ça coûte. La suite (nouvelles features, boucles de jeu, approfondissement) s'appuiera sur ce document.

## En bref
- **Rien dans le recensement n'est bloqué par Godot 4.7 .NET.** Rendu 3D, UI, audio, sauvegarde et export couvrent tout ce que faisait l'original, et la simulation en C# pur n'a aucune limite propre au moteur.
- **Les vrais risques sont ailleurs, dans trois zones que C01 n'a pas touchées :**
  1. **Un parcours plein.** C01 a fait jouer une seule paire. Les 200 golfeurs du banc *se promènent* entre quelques points fixes : ils ne jouent pas. 18 trous, 76 paires qui marchent chacune vers sa balle, des bâtiments et des bancs comme destinations, et des retouches du terrain pendant la partie : rien de ça n'est mesuré. Deux chiffres du rapport inquiètent : un drive planifié coûte environ 15 ms, et une retouche de surface reconstruit **tous** les champs de flux en cache (13 ms chacun sur une carte de 512²).
  2. **Les personnages animés.** Golfeurs, employés, pro, célébrités, animaux : C01 a une silhouette qui glisse. Le script Blender fait bien les objets, mal les personnages animés. La filière prévue (packs CC0 animés, normalisés par script) n'est pas encore essayée.
  3. **Le volume d'interface et de contenu.** Une quinzaine d'écrans et de rapports, une soixantaine de pensées, des histoires en quatre scènes, 96 pros, 21 célébrités, la musique et les voix. Tout est faisable, mais c'est là que partira l'essentiel du temps. Et rien du contenu d'EA ne se reprend : textes, noms parodiques, voix et thème « The Sims » sont à créer.
- **Proposition** : trois preuves courtes avant de construire en largeur (section « Preuves proposées »). P1, le parcours chargé, est la plus importante : c'est la seule qui pourrait remettre en cause une décision d'architecture (les champs de flux).

## Ce que C01 a prouvé, et ce qu'il laisse ouvert

| Prouvé par C01 | Preuve |
|---|---|
| Simulation C# pure, déterministe, testée en CI, sans `using Godot` | 84 tests, `WorldTests.SameSeed_SameChecksum`, `ArchitectureTests` |
| Terrain fin (cellules de 4 m) qui ne paraît pas « en cases » : relief Catmull-Rom, fondu des surfaces par shader | verdict de Guillaume, captures |
| Rendu tenu : 632 FPS à 200 golfeurs en 1080p sur RTX 2080 | `fps-result.txt` |
| Pinceau de surface et de relief, reconstruction des seuls blocs de 32 × 32 touchés | `Brush.cs`, `TerrainRenderer.RebuildChunks` |
| Vol, rebond, roulement, eau et pénalité sur la surface lissée ; putt | `BallSimulator`, `ShotTests` |
| Arbres en `MultiMesh`, asset produit par script `bpy` dans le cloud | `TreeLayer.cs`, `tools/blender/tree_lowpoly.py` |
| Godot headless en CI (fumée, captures), export Windows depuis Linux | `.github/workflows/ci.yml` |

| Laissé ouvert | Pourquoi ça compte |
|---|---|
| Plusieurs groupes qui *jouent* en même temps | le coût réel est la planification des coups et les champs vers chaque balle, pas la marche |
| Carte de la taille d'un 18 trous à l'échelle réelle | environ 1,5 × 1 km, soit ~375 × 250 cellules ; dépend de C02 |
| Retouche du terrain pendant une partie chargée | tous les champs en cache sont reconstruits à chaque retouche de surface |
| Interface de gestion (Control, thèmes, listes, graphiques) | aucun écran dans C01 |
| Personnages animés | silhouette qui glisse |
| Sauvegarde et chargement d'une partie | absente |
| Son | absent |
| Exports Mac, Linux, Steam Deck | seul Windows est construit et mesuré |

## Lecture des tableaux
- **Faisabilité** : ✅ déjà prouvé par C01 ; 🟢 sans risque, du Godot ou du C# courant ; 🟡 faisable, mais à prouver ou à mesurer avant de s'y engager ; 🔴 risque réel, une preuve dédiée est nécessaire.
- **Effort**, en lots comparables à ceux de C01 (un lot ≈ une PR d'agent) : **S** un lot ou moins ; **M** 2 à 4 lots ; **L** un chantier de 5 lots ou plus ; **XL** plusieurs chantiers.
- **Où** : *Sim* (`Golferoo.Sim`), *Godot* (rendu, UI, outils), *Assets* (Blender, packs), *Contenu* (textes, données, sons).

## Revue par section du recensement

### 2. Structure et modes

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Menu titre, crédits | 🟢 | S | Godot | scène `Control` |
| Carrière : difficulté, choix d'une propriété, argent de départ | 🟢 | M | Sim, Godot | la difficulté est une table de paramètres de la sim ; facile à tester |
| Changer de propriété, carte du monde | 🟢 | M | Godot | carte 2D ou 3D stylisée ; un niveau de chargement par propriété |
| Licenciement par le conseil, fin de partie | 🟢 | S | Sim | règle simple, testable en accéléré |
| Sandbox | 🟢 | S | Sim | paramètres de partie |
| Play a Championship (parcours et pro sauvegardés, tournoi immédiat) | 🟡 | M | Sim, Godot | dépend du format de sauvegarde et des tournois (section 14) |
| Échange des parcours (web, patch 1.01) | 🟡 | M | Godot | un fichier de parcours partageable est facile ; un service en ligne est un autre projet. Steam Workshop est la voie naturelle sur PC |
| Thèmes de personnages et d'histoires | 🟢 | M | Contenu | un thème = un dossier de données ; le thème « The Sims » et les personnages Firaxis ne se reprennent pas |

### 3. Le monde

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 16 propriétés : thème (Parkland, Desert, Tropical, Links), situation, relief | 🟢 | M | Sim, Assets | la génération est de la sim ; chaque thème demande sa palette et ses assets (section 17) |
| Génération du terrain (relief, rivage, étangs, bois, marais) | 🟡 | M | Sim | bruit et règles en C#, déterministe ; le risque est esthétique, pas technique. À juger sur captures |
| Taille de la carte | 🟡 | S | Sim | l'original fait 50 × 50 tuiles ; à l'échelle réelle de C02, un 18 trous demande bien plus. 375 × 250 cellules restent sous le banc de 512² ; à fixer avec C02 |
| Terrain « à vendre » en noir, bord en falaise | 🟢 | S | Godot | masque dans le shader de terrain, maillage de bordure |
| Achat de parcelles, prix croissant | 🟢 | S | Sim | |
| Calendrier de 8 mois, ticks, pause | ✅ | S | Sim | tick fixe à 20 Hz et accélération ×1 à ×16 déjà en place ; le rythme d'un mois est un réglage |

### 4. Construction du parcours

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Peindre les surfaces | ✅ | S | Sim, Godot | prouvé pour 7 surfaces ; l'original en a 23. Ajouter une surface = une ligne de table + une couleur dans le shader, mais le shader mélange par cellule : au-delà de ~16 surfaces, passer à une texture d'indices et un atlas |
| Firm fairway, rough, deep rough, waste et pot bunkers, brush | 🟢 | S | Sim | paramètres de rebond, friction et lie déjà modélisés par surface |
| Rocks (déviation aléatoire) | 🟢 | S | Sim | RNG déterministe existant |
| Stream, eau | ✅ | S | Sim, Godot | l'eau marche ; défaut connu du contour en escalier (suite n° 1 du rapport) |
| Noms et assets par thème | 🟢 | S | Contenu | table de libellés par thème |
| Info-bulles (prix, humeur, jauges) | 🟢 | S | Godot | `TooltipText` ou bulle maison |
| Coût par cellule, remboursement à l'annulation | 🟢 | S | Sim | le pinceau de C01 est gratuit et sans annulation ; une pile de commandes en sim donne l'annulation et le rejeu |
| Arbres qui poussent en une seconde, taille selon les voisins | 🟢 | S | Godot | échelle animée dans le `MultiMesh` ; la taille et la densité sont le sujet de C02 |
| Mauvaises herbes par thème | 🟢 | S | Sim, Assets | petits `MultiMesh` |
| Relief : sommet, carré, zone | ✅ | S | Sim, Godot | prouvé au pinceau ; les outils carré et zone sont des variantes |
| Murets de soutènement sur les pentes fortes | 🟡 | M | Godot, Assets | maillage généré là où la pente dépasse un seuil ; à prouver visuellement avec le relief lissé |
| Cascades entre plans d'eau | 🟡 | M | Sim, Godot | demande des plans d'eau à hauteurs différentes et une géométrie de chute ; C01 n'a qu'un niveau d'eau |
| Analyze Golf Shot (trajectoires possibles) | 🟢 | S | Sim, Godot | le planificateur de coups existe ; on dessine ses candidats. Coût de calcul à surveiller (voir R1) |
| Créer et ouvrir un trou, 18 au plus, renuméroter | 🟢 | M | Sim | le trou de C01 vient d'un JSON ; le construire en jeu est de la logique pure, bien testable |
| Ligne blanche tee-trou, golfeur fictif qui calcule dogleg, pente, piquets | 🟢 | S | Sim | réutilise le planificateur |
| Par calculé selon la longueur | 🟢 | S | Sim | seuils à recaler sur l'échelle réelle de C02 |
| Marques de tee, drapeau selon le plaisir | 🟢 | S | Godot | |
| Tricky green | 🟢 | S | Sim, Godot | pente du green plus marquée et texture |
| Aménagements : chemin, banc, fleurs, lave-balles, pont, arbres décoratifs | 🟢 | M | Sim, Assets | objets par script `bpy` (la filière de l'arbre) ; le chemin se raccorde comme une surface ; le pont sur l'eau demande un maillage qui suit la rive |
| Rotation avant pose, suppression, remboursement | 🟢 | S | Godot | |

### 5. Bâtiments

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Pavillon donné, déplaçable | 🟢 | S | Sim | |
| Opérationnel seulement relié par un chemin continu | 🟢 | S | Sim | parcours de graphe sur les cellules de chemin |
| Un exemplaire par type, trois niveaux, déblocage par trou | 🟢 | S | Sim | tables |
| 9 bâtiments, noms et modèles par thème | 🟡 | L | Assets | c'est le gros du travail : 9 bâtiments × 3 niveaux × 4 thèmes. Par script `bpy` (géométrie simple, low-poly) c'est faisable ; le volume se réduit en partageant les niveaux par ajouts (étage, auvent) |
| Chute du ciel à la pose | 🟢 | S | Godot | `Tween` |
| Coût de défrichage, bâtiment qui arrête la balle | 🟢 | S | Sim | obstacle dans `BallSimulator`, comme les arbres |
| Portée de visite et revenus | 🟢 | S | Sim | dépend des besoins (9.3) |

### 6. Landmarks

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 16 landmarks, 3 verrues, effets par rayon | 🟢 | S | Sim | règles simples |
| Modèles des 19 objets | 🟡 | M | Assets | statues et pagodes sont plus organiques que des bâtiments ; à essayer avec deux ou trois, en `bpy` et en pack CC0 |
| Obtention par VIP et par histoire | 🟢 | S | Sim | |
| Coup d'œil des golfeurs (ligne de vue) | 🟡 | S | Sim | lancer de rayons sur la grille de hauteurs ; coût à borner (pas chaque tick pour chaque golfeur) |

### 7. Immobilier

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Lots 2 × 2, valeur selon l'eau, les arbres, les trous voisins | 🟢 | S | Sim | carte de valeur calculée au besoin, comme l'aura |
| Vente immédiate, évolution panneau, chantier, maison | 🟢 | S | Sim, Assets | trois modèles par thème |
| Célébrité qui achète, habite, se promène, fait son geste | 🟡 | M | Sim, Assets, Contenu | dépend des personnages animés (R2) ; les 21 célébrités parodiques sont à réinventer |
| Vue « valeur des lots » | 🟢 | S | Godot | surcouche dans le shader de terrain (texture d'une texel par cellule, comme les surfaces) |

### 8. Personnel

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 4 métiers, version qualifiée, poste et zone de travail | 🟢 | M | Sim | agents qui marchent par champ de flux, comme les golfeurs |
| Fiche, compteurs, renvoi, renommage | 🟢 | S | Godot | |
| Votre pro qui fait les quatre métiers | 🟢 | S | Sim | |
| Vue des zones couvertes | 🟢 | S | Godot | surcouche |
| Modèles et animations des employés | 🟡 | M | Assets | même risque que les golfeurs (R2) |

### 9. Les golfeurs

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Arrivée des invités, attente au pavillon, départ par paires, choix de la paire | 🟢 | S | Sim | C01 a déjà les groupes et le départ |
| Ordre de jeu, attente de la zone d'arrivée | 🟢 | S | Sim | règle déjà présente sur un trou |
| Trois compétences, profils, traits, goûts, fiche | 🟢 | M | Sim, Godot | données et UI |
| 152 golfeurs simultanés, vivier persistant | 🔴 | M | Sim | voir R1 : 150 golfeurs *qui jouent* sur 18 trous n'ont jamais tourné. Le rendu, lui, tient (632 FPS à 200) |
| Faim, soif, fatigue, détours vers les services | 🟢 | M | Sim | besoins et choix d'une destination ; chaque destination fixe ajoute un champ de flux en cache (R1) |
| Humeur, ~60 événements, pensées en bulle | 🟢 | M | Sim, Godot, Contenu | règles simples ; les bulles sont des `Label3D` ou une couche 2D projetée. Les textes sont à écrire |
| Contagion de l'humeur, crises, abandon | 🟢 | S | Sim | |
| Mauvaise herbe sous un golfeur mécontent | 🟢 | S | Sim | |
| Comptage des commentaires par trou | 🟢 | S | Sim | |
| Explication à la première fois, noms au-dessus des têtes | 🟢 | S | Godot | |
| Chemins préférés, eau évitée, plainte hors chemin ou en pente | ✅ | S | Sim | le coût par surface des champs de flux le fait déjà ; la plainte est une règle |
| Céder le passage | 🟡 | S | Sim | les champs de flux ne gèrent pas les collisions entre agents ; une séparation locale simple suffit d'habitude, à vérifier à forte densité près des tees |
| Voiturettes | 🟡 | M | Sim, Assets | vitesse et chemins réservés : simple en sim ; voiturette animée avec deux golfeurs à bord : assets |
| Planification du coup (portée, échantillonnage, risque du coup suivant) | 🟡 | S | Sim | existe (`PhysicalShotResolver`), mais 15 ms par drive. À 76 groupes, plusieurs coups tombent dans le même tick : il faut étaler la planification sur plusieurs ticks ou réduire l'échantillonnage (R1) |
| Clubs, crochet, slice, draw, fade, backspin, coup bas sous les arbres | 🟢 | M | Sim | extension du modèle de vol existant ; le réglage fin de la physique était hors périmètre de C01 |
| Arbres et bâtiments sur la trajectoire, hors-limite | ✅ | S | Sim | arbres et eau faits ; hors-limite à ajouter |
| SimFoto | 🟢 | S | Godot | `Viewport.GetTexture().GetImage()` sur une caméra dédiée ; déjà utilisé par l'outil de captures |

### 10. Adhésion

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 5 niveaux, points, promotion qui invite un ami, démission définitive | 🟢 | S | Sim | règles et tests en accéléré |
| Écran Membership Roster, messages | 🟢 | S | Godot | |

### 11. VIP

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 3 VIP, seuils qui montent, verdict, effets | 🟢 | S | Sim, Contenu | personnages à réinventer (Bigdome, Picky, Richman sont d'EA) |

### 12. Histoires

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Compatibilité des paires, quatre scènes, avance et recul | 🟢 | S | Sim | une machine à états par histoire, données en JSON |
| Fins heureuses, cœurs, landmark débloqué | 🟢 | S | Sim | |
| Écran d'histoire (View Story, Next Chapter) | 🟢 | S | Godot | |
| Écrire les histoires | 🟡 | L | Contenu | l'original en a 37 ; aucune ne se reprend. Des agents peuvent rédiger, mais l'humour et la qualité se jugent à la lecture : c'est un travail éditorial |

### 13. Votre pro et le golf jouable

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Éditeur de personnage (sexe, peau, cheveux, vêtements, silhouette) | 🟡 | M | Godot, Assets | demande un personnage modulaire (têtes, coupes, tenues interchangeables) : c'est l'option la plus exigeante côté assets, liée à R2 |
| Dix compétences, progression | 🟢 | S | Sim | |
| Practice round, viser avec une ligne de trajectoire, choix du type de coup | 🟢 | M | Sim, Godot | le joueur fournit la cible au lieu du planificateur ; la trajectoire prévue se dessine avec le simulateur de balle existant (même fonction, donc ce qu'on voit est ce qui arrive) |
| Construire pendant que le pro joue | 🟢 | S | Sim | la sim gère déjà les retouches pendant que des golfeurs marchent |
| Défis en match play, 96 pros | 🟢 | S | Sim, Contenu | noms parodiques à réinventer |
| 22 accomplissements, tableau en liège | 🟢 | S | Sim, Godot | le bug des Grand Slam se corrige au passage |

### 14. Évaluation SGA et tournois

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Dix critères notés, classes de trous | 🟢 | M | Sim | calculs sur les trous et les statistiques ; à recaler sur l'échelle réelle (temps de jeu cible de 235 min) |
| Offre de tournoi, liste de préparation | 🟢 | S | Sim, Godot | |
| Tournoi en shotgun, 36 joueurs, leaderboard en direct | 🟡 | M | Sim | 18 groupes qui jouent en même temps : c'est R1 en plus petit. L'original mettait plus de deux années de jeu pour un tournoi ; l'accélération déjà en place règle ça si la sim tient à ×16 |
| Tours de télévision, cabines, greens lissés | 🟢 | S | Assets | objets de décor |

### 15. Notes et indicateurs

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Fun et Skill ratings, étoiles, cœurs, drapeaux | 🟢 | S | Sim, Godot | |
| Top 100, Top 18, Best Scores, Top 10 Designers | 🟢 | S | Sim | le Top 10 est un fichier local, comme l'original |
| Aura du parcours (carte des réactions) | 🟢 | S | Godot | surcouche dans le shader de terrain |

### 16. Économie

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Green fees, ventes, home sites, tournois, défis | 🟢 | S | Sim | les chiffres de l'exe sont connus ; à recaler sur l'échelle réelle |
| Salaires, entretien, intérêts, découvert | 🟢 | S | Sim | |
| Conseil d'administration | 🟢 | S | Sim | |
| Financial Report sur 8 ans, bilan de fin d'année | 🟢 | M | Godot | tableaux et graphiques en `Control` ; Godot n'a pas de composant de graphique intégré, un petit dessin maison par `_Draw` suffit |

### 17. Faune, ambiance, décor

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| 9 animaux qui fuient golfeurs et balles | 🟡 | M | Sim, Assets | comportement simple ; animaux animés : même filière que les personnages (R2) |
| Survols : montgolfière, voilier, rapace, dirigeable | 🟢 | S | Godot, Assets | trajectoires scriptées, objets rigides |
| Eau : rides sur la rive, rochers, dauphins, fontaines, éclaboussures | 🟢 | M | Godot | shader d'eau (distance au rivage dans la texture de surfaces) et `GPUParticles3D` |
| Éclairage par thème | 🟢 | S | Godot | un `Environment` et une lumière par thème |

### 18. Interface et rapports

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Dock avec panneaux (terrain, relief, bâtiments, aménagements, employés, golfeurs, pro) | 🟡 | M | Godot | aucune UI dans C01. Godot est bon pour ça (`Control`, conteneurs, thèmes), mais il faut fixer tôt un thème et des composants communs, sinon chaque écran fait le sien. Preuve P3 |
| ~12 écrans et rapports du menu Information, vue d'ensemble F5 | 🟢 | L | Godot | volume plus que difficulté, une fois P3 faite |
| Menu système, préférences, raccourcis | 🟢 | S | Godot | `InputMap` permet de rendre les raccourcis configurables |
| Caméra : rotations, zoom, défilement | ✅ | S | Godot | `IsoCamera` ; ajouter le saut de trou en trou que les tests de l'époque réclamaient |
| Sauvegardes nommées, autosave | 🟡 | M | Sim | la sim est déterministe et sans état global, ce qui aide ; il faut un format versionné (JSON ou binaire) et une sauvegarde rapide d'une grande carte. Testable en `dotnet test` (sauver, recharger, même somme de contrôle) |
| Ticker, dialogues avec portrait, bulles, photos | 🟢 | M | Godot | les portraits supposent des personnages à rendre en image (R2) |
| Conseiller, tutoriel | 🟢 | M | Godot, Contenu | l'original n'a pas de tutoriel ; c'est une amélioration à prévoir |
| Résolution libre | ✅ | S | Godot | le 800 × 600 fixe disparaît ; prévoir l'échelle d'UI pour le Steam Deck (1280 × 800) |

### 19. Son et musique

| Feature | Faisabilité | Effort | Où | Remarque |
|---|---|---|---|---|
| Musique par thème, jingles, ambiance | 🟢 | S | Godot, Contenu | `AudioStreamPlayer` et bus ; le contenu vient de banques libres ou d'un compositeur |
| Voix des golfeurs façon simlish | 🟡 | M | Contenu | le simlish est une marque d'EA : il faut notre propre charabia. Faisable par synthèse ou enregistrement, mais aucune source n'est encore choisie |
| Son positionné | 🟢 | S | Godot | `AudioStreamPlayer3D` |

### 20 et 21. Contenu coupé, limites et bugs de l'original
- Le contenu coupé (landmarks, décors, accessoires de maisons, code mort comme le visiteur célébrité ou le tutoriel) est une réserve d'idées pour l'étape suivante, pas une contrainte technique.
- Les limites de l'original (50 × 50 tuiles, 18 trous, 800 × 600) tombent avec la grille fine et la résolution libre. Les bugs listés ne se reproduisent pas puisque rien n'est repris du code.

## Risques classés

| # | Risque | Gravité | Ce qui le déclencherait | Parade connue |
|---|---|---|---|---|
| R1 | Simulation d'un parcours plein trop lente | 🔴 haute | 76 groupes qui jouent, chacun vers sa balle ; ~15 ms par drive planifié ; reconstruction de tous les champs en cache à chaque retouche de surface (une soixantaine de champs à environ 5 ms chacun sur 375 × 250 cellules, soit de l'ordre de 0,3 s par coup de pinceau, estimé à partir du banc de 512²) | étaler la planification sur plusieurs ticks ; marcher vers la balle en ligne droite ou par un champ vers le green plutôt qu'un champ par balle ; reconstruire les champs paresseusement et par tranches de temps ; réutiliser les tampons (suite n° 2 du rapport). Rien de ça ne change l'architecture tant que la mesure le confirme |
| R2 | Personnages animés hors de portée de la filière par script | 🟡 moyenne | golfeurs, employés, pro personnalisable, célébrités et animaux, tous animés | packs CC0 animés (Quaternius, KayKit) normalisés par `bpy`, squelette commun et `AnimationTree` ; en dernier recours un modèle animé acheté. À essayer avant de dessiner l'éditeur du pro |
| R3 | Volume d'UI sans fondation commune | 🟡 moyenne | quinze écrans faits chacun à sa façon par des agents | un thème Godot et une poignée de composants (panneau, liste, fiche, graphique) posés une fois, avec un écran témoin et sa capture en CI |
| R4 | Volume de contenu original | 🟡 moyenne | histoires, pensées, pros, célébrités, voix, musique ; rien ne se reprend d'EA | séparer contenu et code (données en JSON), faire rédiger par lots et relire ; prévoir des sources libres pour la musique et les sons |
| R5 | Assets en volume (bâtiments × niveaux × thèmes) | 🟢 basse | une centaine de modèles | la filière `bpy` marche ; le job CI « Assets » (suite n° 3 du rapport) devient indispensable avant d'en faire beaucoup |
| R6 | Exports Mac, Linux et Steam Deck | 🟢 basse | non construits | Godot .NET les exporte ; le Mac demande signature et notarisation (compte Apple Developer, environ 99 $ par an) et le Steam Deck une mesure de FPS sur l'appareil. Rien de bloquant avant une sortie |
| R7 | Sauvegarde d'une grande carte | 🟢 basse | carte de ~100 000 cellules, centaines d'agents | format versionné, test de rechargement par somme de contrôle |

## Preuves proposées avant de construire

| Preuve | But | Critère | Effort |
|---|---|---|---|
| **P1, parcours chargé** | lever R1 | 18 trous générés à l'échelle de C02, 76 paires qui jouent vraiment, retouches du terrain en continu : tick moyen < 2 ms et p99 < 5 ms (budgets de C01), aucune retouche > 16 ms, au banc et en CI | M, simulation seule, sans rendu ni UI |
| **P2, personnage animé** | lever R2 | un golfeur tiré d'un pack CC0, normalisé par script, qui marche, frappe, putte et réagit ; 200 à l'écran au-dessus de 60 FPS sur la RTX 2080 | S à M |
| **P3, écran de gestion** | lever R3 | un thème, le dock et deux écrans réels (fiche de golfeur, rapport financier) branchés sur la sim, captures en CI | S à M |

P1 vient après ou avec C02, puisqu'elle a besoin de l'échelle réelle des trous. P2 et P3 sont indépendantes et peuvent tourner en parallèle.

## Ce que la revue ne change pas
- L'ADR 0001 tient : aucune feature ne demande un autre moteur ni de sortir de la séparation simulation / rendu.
- Le facteur 2 tient : aucune feature ne réclame des cellules plus fines que 4 m. Le pinceau plus fin du facteur 4 n'est utile à aucune d'elles.
