# Plan de l'alpha jouable

Mis à jour : 2026-10-28. Chantier R04. Guillaume a demandé (2026-10-28) l'analyse de ce qui manque pour une **version alpha jouable et fonctionnelle**, découpée en chantiers ordonnés, avec les décisions raisonnables prises par la session et une courte liste de décisions qui lui reviennent. C12 est livré (`specs/C12-parcours-plusieurs-trous.md` : parcours de N trous, « Le neuf » par défaut, P1 tenue au banc sur 18 trous, nouveau trou en jeu). **Guillaume a tranché les six décisions le 2026-10-28** (dernière section) ; le plan en tient compte. Rien n'est codé ; aucun chantier n'est lancé.

Les coûts sont en **lots** (un lot ≈ une PR d'agent), comme dans `docs/boucles-v1.md`.

## En bref
- Après C12, Golferoo est un **bac à sable de simulation** solide : terrain, arbres vivants, météo et saisons, gazon, golfeurs stratèges, ligue, pro jouable, caméra, sauvegarde, parcours de plusieurs trous. Il lui manque ce qui en fait un **jeu** : un but, de l'argent, des golfeurs qui viennent seuls, leurs réactions, et une interface qu'on utilise sans connaître les touches.
- Le cœur de l'original est la boucle courte **construire un trou, regarder les golfeurs le jouer, lire leurs réactions, corriger** (`docs/pistes-features.md`). Aujourd'hui, on construit et on regarde ; on ne lit aucune réaction et rien ne récompense une correction. L'alpha ferme cette boucle.
- **Douze chantiers, C13 à C24, environ 34 lots**, en quatre vagues. Le chemin critique est C15 (arrivée des golfeurs) → C16 (humeur et adhésion) → C17 (bâtiments) → C19 (tutoriel v2) → C22 (préparation). L'interface (C13), l'économie (C14), les finitions (C21) et les golfeurs animés (C23) avancent en parallèle dès le départ.
- Décisions de Guillaume (2026-10-28) : alpha pour lui seul, carrière en difficulté facile, propriété générée, golfeurs animés à partir de ses fichiers Blender, son minimal, distribution par `.zip`.

## Ce qu'on appelle l'alpha
**Définition.** Guillaume (seul testeur de l'alpha, décision 1) joue comme un joueur qui ne connaîtrait pas le code :
1. lance l'exécutable Windows, choisit « Nouvelle partie » en difficulté **facile** ;
2. arrive sur une **propriété générée** et boisée, avec un pavillon et de l'argent ;
3. construit ses premiers trous **à la souris**, avec le coût affiché ;
4. voit des golfeurs **animés** arriver seuls, payer, jouer, montrer ce qu'ils pensent, et pour certains devenir membres ;
5. corrige son parcours d'après leurs réactions, pose quelques bâtiments, gère le gazon, suit la ligue et joue son pro ;
6. sauvegarde, quitte, reprend ;
7. tient **deux heures** et une **première année de jeu** sans plantage, avec une raison de continuer (objectifs du club) et une façon de perdre (faillite).

**Ce que l'alpha n'est pas** : complète, équilibrée finement, traduite, belle de près, ni publique. Elle sert à savoir si la boucle est amusante, et d'abord si l'économie tient.

**Critère de sortie de l'alpha** (vérifiable) :
- une partie headless de deux années de jeu à ×16, avec arrivées, achats et constructions scriptés, tourne en CI sans exception, avec une somme de contrôle stable et une trésorerie qui reste dans une bande attendue (C22) ;
- Guillaume joue une séance de deux heures en partant du menu, sans console ni touche à connaître ;
- toutes les fonctions jouables sont accessibles à la souris ; les touches restent des raccourcis.

## État après C12 : ce qui existe
| Domaine | Ce qui est là | Où |
|---|---|---|
| Terrain | 8 surfaces peintes, relief, flaques, beauté par cellule | `Terrain/`, `Brush.cs` (touches B, F1 à F9) |
| Arbres vivants | essences, croissance, saisons, chute, garder, raser, planter, **coûts déjà calculés** (`TreeCosts`, cumulés dans `TreeSpending` sans portefeuille) | `Trees/`, `TreeTool.cs` |
| Temps et météo | calendrier, saisons, météo du jour, vent sur la balle, pluie | `Time/`, `Weather/` |
| Gazon | hauteur, tonte, stimp, usure, consignes, tondeuses visibles | `Turf/`, `TurfPanel.cs` |
| Golfeurs | profils de C09, planificateur qui voit les arbres, champs de flux | `Golfers/`, `Shots/`, `Pathing/` |
| Parcours (C12) | N trous, enchaînement, départs espacés, attente du groupe devant, nouveau trou en jeu | `Course/` (C12) |
| Ligue et handicap | 40 membres **générés d'office**, divisions, finale, note du parcours | `League/`, `Rating/`, `LeaguePanel.cs` |
| Pro | coups visés, compétences, progression | `World.Pro.cs`, `ProAim.cs`, `ProPanel.cs` |
| Tutoriel | construire un premier trou (C07) | `Tutorial/` |
| Caméra | iso, libre, suivi, horizon, de trou en trou (C12) | `CameraRig.cs` |
| Sauvegarde | F5, F8, autosave quotidienne, format binaire versionné | `Save/`, `SaveSlots.cs` |
| Distribution | export Windows en artefact de CI, banc FPS | `ci.yml` |
| Assets | bâtiments (pavillon, pro shop, snack-bar, hangar, garage, abri de practice, maisons), mobilier, véhicules en `.glb` | `game/assets/` |

## Ce qui manque pour l'alpha
| # | Manque | Pourquoi c'est bloquant | Chantier |
|---|---|---|---|
| M1 | **Interface** : tout passe par une trentaine de touches (B, F1 à F10, G, H, J, L, O, [, ]…) et un libellé de débogage en haut de l'écran ; pas de thème ni de composants communs (P3 jamais faite, risque R3) | un testeur ne trouve rien ; chaque nouvel écran ferait le sien | C13 |
| M2 | **Argent** : aucun portefeuille ; seuls les arbres ont un coût, cumulé sans effet | sans coût ni revenu, aucun choix n'a de prix | C14 |
| M3 | **Arrivée des golfeurs** : ils apparaissent par la touche G ou par la ligue ; pas de pavillon dans la sim ni de file au départ | le parcours ne vit pas seul | C15 |
| M4 | **Réactions** : aucune humeur, aucune pensée, aucun abandon ; l'opinion des membres est fixe (C06) ; pas de fiche de golfeur ni d'info-bulle (reportée depuis C08) | la moitié « lire les réactions, corriger » de la boucle n'existe pas | C16 |
| M5 | **Adhésion** : les 40 membres de la ligue existent dès le départ | la progression du club ne se gagne pas | C16 |
| M6 | **Bâtiments** : des modèles, mais rien dans la sim ni d'outil de pose | pas de dépense d'investissement ni de service | C17 |
| M7 | **Une partie** : pas de menu, pas de nouvelle partie, de propriété de départ, d'objectif ni de fin ; le jeu démarre sur un trou chargé par défaut | on ne « commence » pas une partie | C18 |
| M8 | **Tutoriel** : il s'arrête au premier trou valide, avant les golfeurs, l'argent et les réactions | il n'enseigne pas la boucle | C19 |
| M9 | **Son** : aucun | un jeu muet paraît cassé (décision 5) | C20 |
| M12 | **Golfeurs** : silhouettes procédurales en `MultiMesh`, sans animation | la boucle se lit sur les golfeurs : marche, swing, réaction (décision 4) | C23 |
| M13 | **Propriété** : on part de « Le neuf » ou d'un trou fixe | une carrière part d'un terrain vierge, différent à chaque partie (décision 3) | C24 |
| M10 | **Finitions reportées** : dispersion des coups, bordures en escalier, eau en escalier, drapeau qui scintille, mineurs de l'audit C09, banc du changement de jour qui frôle 5 ms | défauts que les testeurs remarqueront tout de suite | C21 |
| M11 | **Sortie** : pas de numéro de version visible, pas de journal de plantage, pas de rapport de bogue, pas de politique de compatibilité des sauvegardes, pas de test de partie longue | on ne peut ni diagnostiquer ni rassurer un testeur | C22 |

**Volontairement hors de l'alpha** (gardés pour la bêta) : tournois avec cut (B18), départs multiples (B1), swing contrôlable (B17), besoins des golfeurs (faim, soif, fatigue), personnel géré, immobilier et célébrités, VIP, histoires, landmarks et décors coupés (C1 à C6), évaluation SGA complète, journal des commandes et rejeu, sauvegardes nommées multiples au-delà de trois emplacements, Mac, Linux et Steam Deck, traduction anglaise, difficultés au-delà de « facile », testeurs extérieurs. Chacun s'appuie sur ce que l'alpha construit (pavillon, humeur, argent, interface) et aucun n'est nécessaire pour juger la boucle.

## Les chantiers
Chaque chantier sera cadré par le skill `cadrer` au moment de le lancer ; les lots ci-dessous sont des estimations. Les règles du dépôt tiennent : tout nouvel état de la sim entre dans `Checksum` et `Save` avec un cas `SaveTests` ; la sim ne connaît pas Godot.

### C13. Fondation d'interface (F7, preuve P3) — ≈ 4 lots
Le préalable de tout écran. **Thème** Godot unique et une poignée de **composants** (panneau, liste, tableau, fiche, jauge, bouton d'outil) avec un écran témoin capturé en CI. **Barre du haut** : argent (vide jusqu'à C14), date et saison, météo et prévision, vitesse et pause. **Dock d'outils** à gauche : terrain (surfaces), relief, arbres, trous, bâtiments (vide jusqu'à C17), gazon ; chaque outil montre son raccourci. **Journal des messages** regroupés par importance (A3). Les panneaux existants (`TurfPanel`, `LeaguePanel`, `ProPanel`, `TutorialPanel`) passent sur les composants. Chaque élément porte un **identifiant stable** (le tutoriel le cible).
- Lots : 1. thème, composants et écran témoin ; 2. barre du haut et dock, outils existants branchés ; 3. migration des quatre panneaux ; 4. journal des messages, info-bulles d'outils, échelle d'interface.
- Critère phare : une fumée Godot active chaque outil par son bouton, sans touche ; capture `c13-interface`.
- Dépend de : rien (zone `game/src` disjointe de la sim). **Démarre dès la fin de C12.**

### C14. Économie (B8 partiel) — ≈ 3 lots
Un **portefeuille** et un **grand livre** par catégorie dans la sim. **Dépenses** : peinture par surface et par cellule, relief par mètre cube, arbres (branchement de `TreeSpending` existant), nouveau trou, bâtiments (C17), **entretien mensuel** par trou et par tondeuse, intérêts sur le découvert. **Revenus** : **green fee** payé à la fin de chaque trou, selon l'humeur (C16, valeur neutre en attendant) et le niveau de membre, comme l'original ; le joueur ne fixe pas les prix (B7 refusé le 2026-10-24). **Découvert** permis avec confirmation, sans limite tant que le parcours a moins de trois trous (original). Rapport mensuel dans le grand livre.
- Lots : 1. portefeuille, grand livre, coûts de construction et d'entretien ; 2. green fee et intérêts, test de rentabilité (un 9 trous fréquenté finit l'année en positif avec la graine de référence) ; 3. écran des finances et coût affiché sous le pinceau (après C13).
- Dépend de : C12 ; lot 3 de C13. Lots 1 et 2 **en parallèle de C13**.

### C15. Pavillon et arrivée des golfeurs — ≈ 3 lots
Le **pavillon** devient un objet de la sim (emprise, porte, niveau) : premier bâtiment, avec le modèle générique que C17 étendra. Les golfeurs **arrivent seuls** au pavillon selon l'attrait du club (nombre de trous ouverts, note du parcours, réputation simple = moyenne glissante de l'humeur de sortie, météo du jour) ; **file d'attente** de 8 au plus ; départ **par paires** dès que le premier départ est libre (règles de départ de C12) ; sortie au pavillon. Un **vivier persistant** de visiteurs, d'où sortiront les membres (C16). La touche G ne sert plus qu'en mode débogage.
- Lots : 1. bâtiment dans la sim, emprise et champ de flux vers la porte ; 2. arrivées, file et départs, test de flux (un 9 trous attire entre X et Y groupes par jour selon la note, bornes fixées au cadrage) ; 3. rendu du pavillon, choix de la paire au clic (C6), capture.
- Dépend de : C12. Prévoir au cadrage le **banc P1 avec arrivées continues** : C12 mesure 36 groupes posés, C15 doit montrer que le flux reste sous ce plafond.

### C16. Humeur, pensées et adhésion — ≈ 4 lots
Le chantier qui ferme la boucle. **Humeur** de −10 à +10 par golfeur. Environ **quinze événements** pour l'alpha, choisis parmi la soixantaine de l'original pour ce que la sim sait déjà mesurer : putt difficile réussi, putt facile raté, coup décevant, balle dans l'eau ou en bunker, balle contre un arbre, pas d'endroit sûr où viser (planificateur), attente au départ, jeu lent, presque touché par une balle, vue panoramique (`BeautyMap`), gazon abîmé (usure de C05), arbre remarquable, beau trou, marche longue sans chemin. **Pensées** en bulle au-dessus des golfeurs (texte court, en français). Sous zéro, le golfeur **abandonne** et rentre au pavillon. **Fiche du golfeur** et **info-bulle** au survol (reportée depuis C08). **Comptage des réactions par trou** : le rapport qui dit au joueur quoi corriger. **Adhésion** : points après une partie, niveaux Member, Silver, Gold, Platinum ; une démission est définitive. **La ligue recrute parmi les vrais membres** au lieu de 40 membres générés d'office.
- Lots : 1. humeur, événements, abandon, tests (même graine, même somme ; un trou avec de l'eau devant le green fait baisser l'humeur moyenne) ; 2. adhésion et ligue branchée sur les membres réels ; 3. pensées en bulle, info-bulle et fiche (après C13) ; 4. rapport des réactions par trou et carte des réactions sur le terrain.
- Dépend de : C15. L'**opinion fixe** des membres de C06 devient l'humeur cumulée : point de migration à traiter au lot 2.

### C17. Bâtiments et aménagements — ≈ 3 lots
Outil de **pose** avec emprise, rotation d'un quart de tour, coût, vente à moitié prix. Pour l'alpha, quatre bâtiments à partir des modèles existants : **pavillon** (déjà posé, améliorable d'un niveau), **snack-bar** et **pro shop** (les golfeurs y passent avant le premier départ et au virage selon leur profil, et paient), **hangar d'entretien** (abrite les tondeuses de C05, D2 Q3). Aménagements simples : bancs, poubelles, chemins. Sans besoins de faim ni de soif (hors alpha) : un service visité donne un bonus d'humeur et un revenu.
- Lots : 1. bâtiments génériques et pose dans la sim, invalidation des champs de flux limitée aux blocs touchés (règle de C12 lot 4) ; 2. visites et ventes ; 3. outil de pose dans le dock et rendu.
- Dépend de : C15 (modèle de bâtiment), C14 (coûts), C13 (dock).

### C18. Une partie du début à la fin — ≈ 3 lots
**Menu titre** : nouvelle partie, continuer, charger, options, quitter. **Nouvelle partie** : une **propriété générée** par C24 à partir d'une graine affichée, avec le pavillon posé ; « Le neuf » reste accessible comme parcours de démonstration. **Difficulté** (décision 2) : un jeu de paramètres en données (argent de départ, coûts, exigence des golfeurs, vitesse de baisse de l'humeur, seuil de faillite) ; seul **« Facile »** est proposé dans l'alpha (argent de départ généreux, golfeurs indulgents), les autres niveaux viendront quand l'économie sera réglée. **Menu pause** (Échap). **Options** : résolution, plein écran, échelle d'interface, volume (C20), pause automatique sur les messages importants. **Trois emplacements de sauvegarde** plus l'autosave, avec date et aperçu. **Objectifs du club** : une liste d'étapes à cocher qui donne une direction (ouvrir 3, 6 puis 9 trous ; 10 membres ; un membre Gold ; finir l'année en positif ; faire monter son pro d'une division) avec un petit déblocage à chaque étape (bâtiments, argent). **Fin de partie** par faillite : trésorerie sous un seuil pendant six mois de jeu de suite (l'original attend trois années, trop long pour une séance). **Bilan de fin d'année**.
- Lots : 1. menus, nouvelle partie, difficulté, emplacements ; 2. objectifs, déblocages, faillite, bilan annuel (sim : tests de déclenchement) ; 3. options et pause automatique.
- Dépend de : C13, C14, C24 ; objectifs de membres après C16.

### C19. Tutoriel v2 et conseiller — ≈ 2 lots
Le tutoriel de C07 continue après le premier trou : **ouvrir le trou**, voir arriver la première paire, toucher le premier green fee, **lire une pensée** et corriger le trou qu'elle désigne, poser un snack-bar. Ensuite, la première année, **votre pro conseille** (bulles courtes déclenchées par l'état : premier abandon, trésorerie basse, trou jamais joué, gazon usé).
- Lots : 1. étapes de la suite du tutoriel, sur les identifiants stables de C13 ; 2. conseils de la première année.
- Dépend de : C16, C17, C18. **Le dernier chantier de contenu**, parce qu'il décrit l'interface finale.

### C20. Son minimal — ≈ 2 lots (décision 5, retenu)
Une banque de sons **libres de droits** (CC0) : frappe selon le club, balle dans le trou, dans l'eau, contre un arbre, ambiance (oiseaux, vent, pluie selon la météo), clics d'interface, caisse au green fee ; une ou deux musiques libres pour le menu et le jeu, réglables. Bus audio et volumes dans les options.
- Dépend de : C13 (options). Parallèle à tout le reste.

### C21. Finitions reportées — ≈ 3 lots
Le reste à faire relevé dans `docs/handoff.md` : dispersion des coups (C08), lissage des bordures de fairway, green et bunker, eau en escalier, scintillement du drapeau, mineurs de l'audit C09 (fonceur moins pénalisé que le neutre, dispersion de l'artiste, bannière du champion, légende de `c09-profils`), marge du banc du changement de jour (5 ms en CI), et les deux constats ouverts de C12 : journée de ligue de 13,4 jours sur 18 trous, handicap faussé par un trou fermé.
- Lots : 1. rendu (bordures, eau, drapeau) ; 2. jeu (dispersion, profils) ; 3. performance (banc, champs de flux).
- Dépend de : C12. **Démarre dès la fin de C12**, en parallèle de C13 et C14, sur des fichiers disjoints par lot.

### C22. Préparation de l'alpha — ≈ 2 lots
**Version** visible au menu et dans les sauvegardes (`0.1.0-alpha.N`). **Journal** dans `user://logs` et rapport de bogue en une touche (capture, sauvegarde et journal dans un dossier daté, à glisser dans le fil). **Compatibilité des sauvegardes** : à partir de la première alpha distribuée, chaque version lit la précédente (une migration par changement de format). **Partie longue headless** en CI : deux années de jeu à ×16 avec `StubShotResolver`, arrivées, constructions et achats scriptés ; elle vérifie l'absence d'exception, la somme de contrôle au rechargement et une trésorerie dans une bande. C'est aussi le premier outil d'**équilibrage** (coûts, green fees, arrivées). **Liste de vérification** de la séance de test et notes de version. Distribution : le `.zip` de l'artefact de CI `golferoo-windows` (décision 6), avec le numéro de version dans son nom.
- Dépend de : tout le reste ; le test de partie longue peut commencer dès C14 et grossir avec chaque chantier.

### C23. Golfeurs animés (preuve P2) — ≈ 3 lots (décision 4)
Point de départ : **les fichiers Blender de Guillaume** (un golfeur qui marche et qui frappe). Le jeu en demande pour l'alpha :
| Animation | Source | Usage |
|---|---|---|
| marche | Guillaume | tous les déplacements |
| swing complet | Guillaume | drive, bois, fers ; le chip en version raccourcie (vitesse et amplitude) |
| putt | à faire | sur le green |
| attente | à faire | file au pavillon, attente du groupe devant, partenaire qui regarde |
| joie, déception | à faire | réactions de C16 (bon putt, balle dans l'eau) |

Les trois manquantes viennent soit de Guillaume, soit d'un pack CC0 (Quaternius, Universal Animation Library, déjà repéré dans `docs/assets-3d.md`) reciblé sur son squelette par script `bpy`.
**Le risque technique** (R2) : les golfeurs sont aujourd'hui un `MultiMesh`, qui ne sait pas animer un squelette. Deux voies, départagées par la mesure du lot 1 : un `Skeleton3D` et un `AnimationTree` par golfeur visible (simple, coûteux au-delà de quelques centaines), ou des **animations cuites en texture** lues par le shader d'un `MultiMesh` (plus de travail, coût quasi nul). Critère P2 : 200 golfeurs animés à l'écran au-dessus de 60 FPS sur la RTX 2080 en 1080p, mesuré par Guillaume.
- Lots : 1. filière `bpy` (import, échelle, normalisation, export `.glb`), golfeur animé en jeu sur la marche et le swing, banc de 200 golfeurs ; 2. putt, attente et réactions, déclenchement par l'état du golfeur dans `ActorLayer` ; 3. variété (teintes de tenue par profil, tailles), et repli si la mesure le demande.
- Dépend de : rien dans la sim ; réactions branchées sur C16 au lot 2. **Démarre dès qu'on a les fichiers.**

### C24. Génération de la propriété — ≈ 3 lots (décision 3)
Une propriété **générée à partir d'une graine** : relief doux (bruit à plusieurs octaves, une ou deux collines, un creux), un cours d'eau ou un étang, boisements d'âges variés par `WoodlandGenerator` (existant), quelques arbres remarquables, une clairière pour le pavillon près d'un bord, et une surface jouable assez grande pour 9 trous (de l'ordre de 1 200 × 800 m, comme « Le neuf »). Un seul thème (Parkland) dans l'alpha. **Garanties testées** : même graine, même carte ; le pavillon est accessible ; au moins N couloirs de 400 m tiennent sans eau ni pente excessive ; pas de flaque permanente au pavillon.
- Lots : 1. relief, eau et clairière (sim, tests de garanties sur 100 graines) ; 2. boisement et arbres remarquables, budget d'arbres tenu (5 000 au banc) ; 3. aperçu de la graine dans l'écran de nouvelle partie (avec C18), captures `c24-*` sur trois graines.
- Dépend de : C12. Parallèle à C13 et C14 ; C18 s'appuie dessus.

## Ordre et vagues
| Vague | Chantiers | Lots | Pourquoi ensemble |
|---|---|---|---|
| 1 (maintenant) | **C13** interface ∥ **C14** économie (lots 1 et 2) ∥ **C21** finitions ∥ **C23** golfeurs animés (lot 1, dès les fichiers reçus) | ≈ 12 | quatre zones disjointes : UI, sim (`Economy/`), réglages de jeu, filière d'assets et `ActorLayer` |
| 2 | **C15** pavillon et arrivées, puis **C16** humeur et adhésion ; C14 lot 3 ; **C24** génération ; C23 lots 2 et 3 | ≈ 13 | le chemin critique ; la génération et les animations à côté |
| 3 | **C17** bâtiments ∥ **C18** partie complète ∥ **C20** son | ≈ 8 | bâtiments dans la sim et le dock, menus et objectifs côté jeu, son à part |
| 4 | **C19** tutoriel v2, puis **C22** préparation | ≈ 4 | décrivent et vérifient le jeu fini |
| | **Total** | **≈ 34** | (C22 allégé d'un demi-lot : un seul testeur) |

Le chemin critique est **C15 → C16 → C17 → C19 → C22**, soit environ 14 lots en série. Au rythme de C09 à C11 (un chantier de 3 à 5 lots par jour ou deux), c'est de l'ordre de **deux à trois semaines**, sans compter les séances de jeu de Guillaume, qui sont le vrai goulot (estimation).

**Jalons de jeu pour Guillaume**, pour juger tôt plutôt qu'à la fin :
- fin de la vague 1 : tout se fait à la souris, tout coûte quelque chose ;
- fin de C23 lot 1 : un golfeur animé marche et frappe en jeu ;
- fin de C16 : **la boucle est fermée**, première vraie séance de jeu (sans menu ni bâtiments) ; c'est le moment de dire si c'est amusant ;
- fin de la vague 3 : carrière complète depuis le menu, sur une propriété générée, en facile ;
- C22 : alpha.

## Décisions prises par la session
Guillaume a délégué les choix de cadrage (2026-10-27 et 2026-10-28). Celles-ci sont prises ; il peut revenir sur chacune.
1. **L'alpha ferme la boucle de l'original avant d'ajouter des boucles.** Tournois avec cut, départs multiples et swing contrôlable, triés « oui » le 2026-10-24, passent en bêta : ils approfondissent un jeu qui doit d'abord exister.
2. **L'interface d'abord en parallèle, pas en premier.** C13 démarre avec C14 et C21 au lieu de bloquer la vague : la sim de l'économie n'a pas besoin d'écran pour ses tests.
3. **Économie à la manière de l'original** : green fee par trou selon l'humeur et le niveau de membre, prix non réglables (B7 refusé), découvert permis. Prêts et budgets détaillés (B8) en bêta.
4. **Quinze événements d'humeur**, pas soixante : ceux que la sim mesure déjà. Les autres viendront avec les systèmes qui les produisent (besoins, personnel, VIP).
5. **La ligue recrute parmi les membres gagnés** : les 40 membres générés d'office disparaissent d'une nouvelle partie. « Le neuf » et les tests gardent un club pré-rempli pour ne pas perdre la couverture de C06.
6. **Faillite après six mois de jeu sous le seuil**, au lieu de trois années : une séance de test doit pouvoir la rencontrer.
7. **Français seulement** pour l'alpha ; les textes sont rangés en données (`data/text/`) pour la traduction plus tard (risque R4).
8. **Windows seulement**, par l'export de CI qui existe. Mac, Linux et Steam Deck en bêta (R6).
9. **Sauvegardes** : aucune migration avant la première alpha distribuée ; ensuite, chaque version lit la précédente. Les formats de C12 à C21 cassent librement.
10. **Les preuves deviennent des chantiers** : P1 est faite par C12 ; P3 devient C13 ; P2 devient C23.

## Risques
| Risque | Où | Parade |
|---|---|---|
| R1 revient avec les arrivées continues et les visites de bâtiments | C15, C17 | banc P1 avec flux continu au cadrage de C15 ; destinations fixes en champs de flux en cache (une par bâtiment), invalidation limitée aux blocs touchés |
| L'équilibrage (coûts, fees, arrivées) rend la partie triviale ou impossible | C14 à C18 | partie longue headless dès C14 (C22), avec une bande de trésorerie attendue ; chiffres en données, pas en code |
| R3, volume d'interface | tous | C13 d'abord ; tout écran passe par ses composants ; capture en CI par écran |
| R4, textes des pensées et des conseils | C16, C19 | une centaine de phrases courtes en données, rédigées par lot et relues par Guillaume |
| Le tutoriel décrit une interface qui bouge encore | C19 | placé en dernière vague, sur des identifiants stables |
| R2 : des golfeurs animés trop coûteux en nombre | C23 | mesure à 200 golfeurs au lot 1 ; repli par animations cuites en texture dans un `MultiMesh` |
| Une graine produit une propriété injouable | C24 | garanties testées sur 100 graines ; la graine reste affichée pour la rejouer |

## Décisions de Guillaume (2026-10-28)
1. **Public** : Guillaume seul pour l'instant. Pas de rapport de bogue partageable ni de notes pour des tiers ; la séance de deux heures est la sienne.
2. **Partie** : carrière, avec une difficulté **facile** pour commencer, pour tester les boucles d'économie. Les paramètres de difficulté sont en données (C18) ; les autres niveaux attendent que l'économie soit réglée.
3. **Propriété** : **générée** (chantier C24, environ 3 lots en plus).
4. **Golfeurs animés avant l'alpha**, à partir de ses fichiers Blender d'un golfeur qui marche et qui frappe (chantier C23). On commence par ces deux animations ; le putt, l'attente et deux réactions s'ajoutent au lot 2.
5. **Son** : recommandation retenue, son minimal libre de droits (C20).
6. **Distribution** : le `.zip` de l'artefact de CI.

## Suite
Les chantiers C13 à C24 entrent dans `docs/chantiers.md` au statut « proposé ». La vague 1 (C13, C14, C21, puis C23 dès réception des fichiers Blender) se cadre au signal de Guillaume.
