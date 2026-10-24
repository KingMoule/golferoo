# Analyse B : choix du moteur, sous l'angle du produit et de l'avenir du jeu

Analyste B, 2026-10-24. Angle principal : à quoi ressemble un SimGolf moderne dans trois ans, et quel moteur ne bloque pas cette suite. Tous les critères du brief sont couverts.

## 1. Recommandation

**Premier choix : Godot 4.7, en 3D stylisée dès le départ, avec une simulation écrite comme une bibliothèque C# pure (sans dépendance à Godot) testée en headless par `dotnet test`, et Godot (C# + un peu de GDScript pour l'UI) comme couche de rendu, d'entrée et d'outils.**

**Second choix : Unity 6 (LTS courante), même architecture (simulation en assembly C# pure, Unity comme couche de présentation).** C'est le moteur qui a fait le plus de tycoons récents. Il passe second à cause des formats d'éditeur moins diffables, de la boucle de vérification qui dépend de l'éditeur, et du risque de licence (historique du Runtime Fee de 2023).

**Confiance : moyenne-haute** sur « moteur généraliste 3D plutôt que 2D iso ou moteur maison ». **Moyenne** sur Godot contre Unity : l'écart est réel mais pas énorme, et trois réponses de Guillaume peuvent le renverser (consoles en priorité ? web indispensable ? Golferoo en GPL ?). Section 7.

À éviter : partir d'un fork d'OpenSGolf comme base du jeu. C'est le chemin le plus rapide vers la parité, et celui qui bloque le plus la suite (section 3.5).

Ce qui ferait changer ma recommandation :
- **consoles prioritaires dès la première sortie** : Unity 6 passe premier ;
- **Golferoo en GPL, gratuit**, ou accord de licence de l'auteur d'OpenSGolf : Godot avec un core Rust (gdext) qui reprend `sg-core` ;
- **web indispensable** : Godot en GDScript typé (core compris), ou une stack TypeScript si le web est la cible principale ;
- **2D pixel-art voulue** : Godot quand même, en 2D ; la recommandation tient.

## 2. Ce que j'ai vérifié

### Fichiers lus (par extraits)
- `docs/adr/0001-annexes/brief.md` en entier.
- `docs/recensement-sim-golf.md` : tous les titres, puis les sections 1, 4 (construction), 9 à 21 (golfeurs, adhésion, VIP, sim-stories, pro jouable, SGA, économie, UI, limites).
- OpenSGolf (`/home/claude/crspytopgn/opensgolf/`) : `README.md` (début, roadmap), `Cargo.toml`, `crates/sg-core/Cargo.toml`, `crates/simgolf/Cargo.toml`, liste des modules de `sg-core/src`, début de `sg-core/src/flight.rs` et de `mapsize.rs`, extraits de `docs/NEXT_STEPS.md`, `docs/MODDING.md`, `docs/HD.md` et `docs/ENHANCED.md`.

### Commandes et mesures
- `wc -l` : `sg-core` 44 797 lignes de Rust, `simgolf` 59 401. Environ 238 `#[test]` dans `sg-core`.
- `sg-core` dépend de fontdue, lewton, nanomp3, miniz_oxide, serde, toml, rhai, sha2, image-webp : la bibliothèque mêle **règles du jeu et décodeurs des formats propriétaires d'EA** (bink, flc, sve, sprites, wav…).
- `flight.rs` : vol de balle en **arithmétique entière fidèle à l'exe** (1 tuile = 1 024 unités, tick de 87 ms), avec des constantes marquées « inconnu » (yards par unité). `mapsize.rs` : carte en tuiles, 50 à l'origine, 200 au maximum.
- `simgolf` : rendu par miniquad (OpenGL bas niveau), fontdue, cpal ; le rendu est un moteur de sprites 2D isométriques construit autour de l'art de 2002 (HD = upscale ×4 de cet art, Enhanced = même art mieux filtré).
- `git log` : clone superficiel, un seul auteur visible (`crspytopgn`), adresse différente de celle de Guillaume. Je ne sais pas s'il y a d'autres contributeurs (donc qui pourrait relicencier).
- `packs/opensgolf-free` : 19 Mo, présenté par `MODDING.md` comme un set de remplacement « simple placeholder art ».

### Sources web (consultées le 2026-10-24)
- Godot, blog des versions ([godotengine.org/blog/release](https://godotengine.org/blog/release)) : 4.7 sortie le 18 juin 2026, 4.7.1 le 14 juillet, 4.7.2 le 18 août 2026. **Vérifié.**
- Export web de Godot 4 en C# : toujours **non supporté** d'après une réponse du forum datée du 17 avril 2026, qui disait que ça « may not make it into 4.7 » ([forum.godotengine.org](https://forum.godotengine.org/t/is-there-an-update-on-exporting-c-projects-to-web/128821?page=2)). La page de doc 4.7 que j'ai récupérée n'en dit rien. **Vérifié comme non annoncé, à reconfirmer.**
- Unity ([unity.com/runtime-fee](https://unity.com/runtime-fee), annonce du 10 novembre 2025) : Personal gratuit jusqu'à 200 000 $ de revenus et de financement, écran de démarrage optionnel avec Unity 6 ; Pro à 2 310 $ par siège et par an, +5 % au 12 janvier 2026 ; Havok retiré de Pro à partir de 6.3. Runtime Fee annulé (blog Unity, septembre 2024). **Vérifié.**
- Bevy ([bevy.org/news](https://bevy.org/news/)) : 0.18 (13 janvier 2026), 0.19 (19 juin 2026), **0.20 (8 octobre 2026)**. Toujours pas d'éditeur de scènes ; le format `.bsn` est prévu ; chaque version casse l'API (WGSL remplacé par WESL, syntaxe BSN modifiée en 0.20) ([bevy.org/news/bevy-0-20](https://bevy.org/news/bevy-0-20/)). **Vérifié.**
- Unreal : 5 % de redevance au-delà de 1 M$ de revenus bruts par produit ([unrealengine.com/license](https://www.unrealengine.com/en-US/license)). **Ce que je crois** (titre de la page vu, contenu non relu).
- Cities: Skylines II et Unity DOTS : le studio a reconnu en 2026 avoir trop compté sur des technologies Unity pas mûres ([automaton-media, 2026-03-19](https://automaton-media.com/articles/newsjp/20260319-430623/), [Windows Central](https://www.windowscentral.com/gaming/cities-skylines-ii-runs-horribly-a-deep-dive-into-unity-explains-why)). **Vérifié pour le titre, résumé de seconde main.**

### Ce que je crois sans l'avoir revérifié aujourd'hui
- Moteurs des tycoons récents : Two Point Hospital, Campus et Museum sur Unity ; Parkitect sur Unity ; Cities: Skylines 1 et 2 sur Unity ; Planet Coaster 1 et 2 et Planet Zoo sur Cobra (moteur maison de Frontier) ; RollerCoaster Tycoon 3 sur un moteur maison, OpenRCT2 en C++ maison ; The Golf Club et PGA Tour 2K21 (HB Studios, avec un éditeur de parcours) sur Unity. Slay the Spire 2 est passé de Unity à Godot (C#).
- Godot sur consoles : pas d'export officiel (licences des SDK) ; il passe par des prestataires (W4 Games, Lone Wolf Technology…). Je ne sais pas si le C# y est bien pris en charge en 2026.
- Les SDK de consoles sont sous NDA et incompatibles en pratique avec la GPL.

## 3. Ce qu'un SimGolf moderne exige du moteur

### 3.1 Le produit visé
Le recensement décrit un jeu de 2002 en 800 × 600, sur une carte de 50 × 50 tuiles, avec 152 golfeurs au plus et 4 rotations de caméra. Les tests de l'époque lui reprochaient des golfeurs « petits et sommaires », un défilement lent et des parcours vite encombrés (§21). Les tycoons qui ont réussi depuis tracent la cible :

| Référence | Ce qu'elle a apporté au genre | Ce qu'elle impose au moteur |
|---|---|---|
| Two Point Hospital / Campus / Museum | lisibilité, humour, agents expressifs, UI de gestion dense et claire, sorties console | 3D stylisée, animation de personnages, UI riche, consoles |
| Planet Coaster 2 / Planet Zoo | construction libre, terrain sculpté, partage de créations (Workshop), agents nombreux | caméra libre, terrain en heightmap continue, éditeur in-game, sérialisation partageable |
| Parkitect | l'héritier de RCT, en 3D low-poly produite par une petite équipe | prouve que la 3D low-poly coûte **moins cher** en art qu'un set iso à 4 rotations |
| Cities: Skylines 1 / 2 | modding massif (code et assets) ; contre-exemple de performance en 2 | API de mod, chargement d'assets tiers, simulation scalable |
| The Golf Club / PGA 2K (designer) | éditeur de parcours, partage en ligne, physique de balle crédible | physique de balle 3D, pente lisible, caméra de coup |
| OpenRCT2 / OpenTTD | la parité fidèle et le modding de données | (ce que fait déjà OpenSGolf) |

Un SimGolf moderne, c'est donc, à mon sens :
1. **Vue** : 3D stylisée, caméra iso par défaut (pour garder l'âme de SimGolf) mais rotation libre, zoom fluide jusqu'au niveau du golfeur, caméra « suivre la balle » pour les coups du pro et les SimFoto. En 2D iso, chaque objet doit être dessiné en 4 orientations × états × saisons × thèmes (4 thèmes, plus les « Enhanced » d'OpenSGolf : météo, hiver, foules). En 3D, un modèle suffit pour toutes les vues.
2. **Terrain** : heightmap continue (plus seulement des paliers de tuiles), peinture de surfaces au pinceau avec des bords doux, eau à plusieurs niveaux avec des cascades (bug des « triangles noirs » §21 : c'est un problème de rendu 2D), arbres instanciés en nombre. Cartes plus grandes que 50 × 50 (OpenSGolf monte déjà à 200).
3. **Agents** : de quelques centaines à plusieurs milliers (spectateurs de tournoi, personnel, faune), chacun avec besoins, humeur, pensées et relations. Pathfinding sur grille, ou sur navmesh si le terrain devient libre.
4. **Balle** : simulation déterministe de vol, rebond et roulement sur pente, avec vent et types de coup (§9.6, §13.3). Elle sert à trois choses : les golfeurs IA (des milliers de coups planifiés par seconde en simulation accélérée), l'analyse de coup `/` et le pro jouable. **Elle doit être écrite par nous, pas confiée au moteur physique** : déterminisme, tests et vitesse en headless.
5. **UI de gestion dense** : une dizaine de rapports (F1 à F10), des fiches de golfeur, des listes triables, des histogrammes, des info-bulles avec des jauges (§18). C'est le poste qui coûte le plus en code dans un tycoon, et c'est là que les moteurs diffèrent le plus.
6. **Nouvelles boucles probables** : saisons et entretien (greenkeepers, arrosage, état du gazon), événements et tournois télévisés, carrière multi-clubs, partage et notation de parcours (déjà amorcé dans le patch 1.01 et OpenSGolf 0.3), défis asynchrones (« bats mon score sur mon parcours »), pro jouable approfondi (jusqu'à une partie à plusieurs en local ou en ligne), mods de données et de scripts.
7. **Plateformes** : PC (Steam) d'abord, Mac et Steam Deck presque gratuits avec un bon moteur. Le genre vit aussi sur consoles (Two Point, Planet Coaster Console Edition), et la tablette lui va bien. Le web sert de démo, rarement de produit.

### 3.2 Exigences moteur qui en découlent
| # | Exigence | Poids produit |
|---|---|---|
| E1 | 3D temps réel solide (terrain, instancing, eau, éclairage par thème), caméra libre | fort |
| E2 | Mode 2D/iso possible sans changer de moteur (option de repli si Guillaume veut du pixel-art) | moyen |
| E3 | Système d'UI riche, thémable, avec texte riche et localisation | fort |
| E4 | Simulation dans un langage rapide, typé, testable sans moteur | fort |
| E5 | Animation de personnages (squelettes, retargeting), pipeline Blender | fort |
| E6 | Outils in-game (éditeur de terrain) faciles à construire ; outils pour les artistes | fort |
| E7 | Modding : chargement de packs d'assets et de scripts à l'exécution | moyen-fort |
| E8 | Export PC, Mac, Linux/Deck ; consoles possibles ; mobile possible | moyen-fort |
| E9 | Formats texte diffables, CI headless, bonne connaissance du moteur par les modèles | fort (développement par agents) |
| E10 | Licence sûre et pérenne, compatible avec une vente commerciale | fort |

### 3.3 Couverture du recensement : ce qui dépend vraiment du moteur
La plupart des features de SimGolf sont de la **simulation pure** : le moteur n'y change rien, pourvu que le core soit testable hors moteur. Le moteur pèse sur la présentation et les outils.

| Feature du recensement | Couche | Dépendance au moteur |
|---|---|---|
| §3 propriétés, génération de terrain, achat de terrain | sim + rendu du terrain | rendu de heightmap, eau |
| §4.1 terrains peignables (23 surfaces) | sim + shader de terrain | splatmap ou texture array, pinceau |
| §4.2 relief, murets, cascades | sim + maillage | génération de maillage à l'exécution |
| §4.3 création de trou, ligne blanche, piquets | sim + overlay | lignes et décals en surimpression |
| §4.4, §5, §6 aménagements, bâtiments, landmarks | sim + placement | instanciation, rotation, aperçu fantôme |
| §8 personnel | sim | aucune |
| §9.1 à 9.5 golfeurs, besoins, humeur, pathfinding | sim | animation de foule, bulles de pensée |
| §9.6 coup planifié, vol de balle | sim (Ball) | rendu de trajectoire, caméra de suivi |
| §10 à §12 adhésion, VIP, sim-stories | sim + UI | dialogues avec portraits |
| §13 pro jouable, personnalisation | sim + UI + avatar | personnage modulaire (têtes, tenues) |
| §14, §15 SGA, tournois, leaderboard, aura | sim + UI | carte de chaleur en surimpression |
| §16 économie, rapports sur 8 ans | sim + UI | graphiques, tableaux |
| §17 faune, survols, ambiance | sim légère + rendu | animation, particules, éclairage par thème |
| §18 dock, menus F1 à F10, raccourcis, caméra | UI + entrée | système d'UI, remapping des touches |
| §19 son positionné, musique par thème | audio | bus audio, spatialisation |
| Sauvegarde, autosave | sim | aucune (sérialisation du core) |

Conclusion : environ deux tiers du travail de parité se trouvent dans le core et ne dépendent pas du moteur. Le dernier tiers (terrain, UI dense, animation, outils de construction) est précisément ce qui grossit avec la modernisation. C'est donc là qu'il faut choisir le moteur.

### 3.4 Phasage produit qui en découle
1. **Parité jouable** (« Classique ») : core complet, terrain 3D simple, UI fonctionnelle, art placeholder low-poly.
2. **Modernisation** : direction artistique, caméra libre, cartes plus grandes, ergonomie (sauts de trou en trou, défilement rapide : les reproches de 2002).
3. **Profondeur** : saisons et entretien, carrière multi-clubs, tournois télévisés, pro jouable approfondi.
4. **Communauté** : partage de parcours (Workshop), défis asynchrones, mods de données puis de scripts.
5. **Extension de plateformes** : consoles, tablette.
Le moteur doit servir les cinq phases sans migration. Un moteur qui ne sert que la phase 1 coûte une réécriture en phase 2.

### 3.5 Le piège « rapide pour le remake »
OpenSGolf joue déjà SimGolf, sur 5 plateformes. Le forker donnerait la parité en quelques semaines. Mais :
- son rendu est un moteur de sprites 2D bâti sur l'art d'EA (§2). Sans cet art, il reste un set placeholder. Passer à la 3D voudrait dire réécrire `simgolf` (59 k lignes), soit l'essentiel du jeu ;
- ses règles sont fidèles à l'exe jusqu'à l'arithmétique entière et aux ticks de 87 ms. C'est parfait pour la parité, et c'est un carcan pour la modernisation (balle en unités inconnues, monde en tuiles) ;
- tout est **GPL-2.0-or-later** : Golferoo deviendrait GPL tout entier, sans consoles (SDK sous NDA), sans code fermé ni anti-triche, et chaque fork pourrait redistribuer le jeu gratuitement ;
- l'UI est faite à la main sur miniquad : chaque nouveau rapport, chaque nouvelle boucle coûte du code d'UI de bas niveau.
OpenSGolf est le meilleur **document de référence** possible pour Golferoo, mais une mauvaise **fondation**.

## 4. Analyse par option

Notes de 1 (mauvais) à 5 (excellent). C1 à C6 sont les critères du brief ; « Suite » détaille C2 sous mon angle.

| Option | C1 Features | C2 Suite (3D, mod, multi) | C3 Réutilisation / licence | C4 Productivité agents | C5 Plateformes / licence moteur | C6 Perf, art | Total /30 |
|---|---|---|---|---|---|---|---|
| **Godot 4.7 + core C#** | 4 | 4 | 4 | 4 | 4 | 4 | **24** |
| Godot 4.7 tout GDScript | 4 | 3 | 4 | 4 | 5 | 3 | 23 |
| Godot + GDExtension Rust (gdext) | 4 | 4 | 3 (5 si GPL) | 3 | 3 | 4 | 21 |
| **Unity 6** | 5 | 5 | 4 | 3 | 4 | 5 | **26 − risques** → voir note |
| Fork OpenSGolf / moteur maison Rust | 5 (remake) | 1 | 2 (GPL) | 4 | 3 | 2 | 17 |
| Bevy 0.20 | 3 | 3 | 4 | 2 | 3 | 3 | 18 |
| Web TS + Three/Babylon | 3 | 3 | 4 | 4 | 3 | 2 | 19 |
| Unreal 5 | 3 | 4 | 4 | 1 | 4 | 4 (art) / 2 (coût) | 18 |
| Moteur maison neuf (C++/Rust + libs) | 3 | 3 | 4 | 3 | 3 | 2 | 18 |

**Note sur Unity** : sur le papier, Unity a le meilleur total, parce qu'il est la référence du genre. Je le classe pourtant second, pour deux raisons que la grille additive pondère mal. (a) C4 compte double dans ce projet : tout le code est écrit par des agents sans éditeur graphique, et les scènes, prefabs et méta-fichiers YAML de Unity sont fragiles à éditer en texte (GUID, `fileID`). Les tests et les builds passent par un éditeur licencié en mode batch, plus lourd en CI. (b) Le risque de licence n'est pas une note mais une queue de distribution : Unity a déjà tenté une fois de changer les règles après coup (2023), et un moteur MIT ne peut pas le faire. Si Guillaume vise les consoles en priorité, Unity repasse premier (section 7).

### 4.1 Godot 4.7 (C#, GDScript ou GDExtension)
- **Pour le produit** : moteur 2D **et** 3D natif, donc E1 et E2 sont tous deux couverts. Le pivot iso vers la caméra libre n'est qu'un réglage de caméra. Terrain : pas de terrain intégré, mais Terrain3D (addon C++, MIT) existe ; je crois qu'il est maintenu et compatible 4.x, à vérifier. De toute façon, notre terrain sera une heightmap à surfaces de jeu (23 types), plus simple à générer nous-mêmes en `ArrayMesh` qu'à plier un terrain générique. MultiMesh pour les arbres et la foule.
- **UI (E3)** : les Control, conteneurs, thèmes, RichTextLabel et la traduction intégrée (fichiers CSV/PO) sont l'un des points forts de Godot : l'éditeur lui-même est écrit avec. C'est adapté à une UI de gestion dense.
- **Agents (E4)** : 150 à 2 000 agents ne posent aucun problème en C# dans une bibliothèque pure (structures de tableaux, pas un nœud par agent). En GDScript pur, les centaines d'agents passent, mais la planification de coups par échantillonnage (§9.6) multipliée par l'avance rapide devient le goulot. D'où le core en C#.
- **Animation, art (E5, E6)** : import glTF depuis Blender sans friction, AnimationTree, retargeting. Les outils pour artistes sont plus pauvres que Unity (pas d'Asset Store comparable), compensés par Blender. Les outils in-game (éditeur de terrain) se font dans le jeu lui-même avec les mêmes Control : c'est notre cas d'usage.
- **Modding (E7)** : chargement de `.pck` et de ressources à l'exécution, glTF chargeable au runtime. Le scripting de mods pourrait reprendre l'idée d'OpenSGolf (données TOML + un langage de script sandboxé côté core C#, par exemple MoonSharp ou NLua, à évaluer) plutôt que d'exposer du GDScript non sandboxé.
- **Agents IA (E9)** : `.tscn`, `.tres` et `project.godot` sont en texte lisible et diffable ; `godot --headless` fait tourner des tests et les exports en CI ; le binaire fait environ 100 Mo, sans licence ni compte. Les modèles connaissent bien Godot, mais avec un piège réel : la confusion Godot 3 / Godot 4 dans le code généré. On s'en protège avec un fichier de conventions et la vérification headless.
- **Plateformes (E8)** : Windows, macOS, Linux/Deck, Android et iOS en natif. Consoles via prestataires, payants (non vérifié aujourd'hui). **Limite C# : pas d'export web en octobre 2026** (forum, avril 2026). Web = démo hors périmètre, ou version GDScript plus tard.
- **Licence (E10)** : MIT, fondation à but non lucratif. Aucun frais, aucun risque de changement rétroactif.
- **Variantes** : *tout GDScript* : le plus simple, web possible, mais simulation plus lente et typage optionnel, que les agents respectent mal sans lint strict. *GDExtension Rust (gdext)* : le seul chemin qui permette de **lier `sg-core` tel quel**, au prix de la GPL pour tout le jeu. Sinon, Rust n'apporte que la familiarité avec OpenSGolf, avec un double build plus complexe pour les agents.

### 4.2 Unity 6
- **Pour le produit** : le moteur le mieux prouvé du genre (Two Point, Parkitect, Cities, The Golf Club). Terrain, URP/HDRP, Animator, Timeline, Cinemachine pour la caméra de coup, Asset Store pour accélérer l'art (arbres, eau, personnages), consoles en standard, Steam Workshop et modding éprouvés (BepInEx, Addressables).
- **UI** : UI Toolkit (UXML/USS, en texte) est adapté aux UI de gestion ; uGUI reste plus documenté.
- **Agents IA** : C# excellemment connu des modèles. Mais scènes et prefabs en YAML à GUID, `.meta` obligatoires, beaucoup de réglages qui ne s'expriment que dans l'éditeur. Tests via Unity Test Framework en `-batchmode` : il faut une licence activée en CI. La boucle « un agent modifie, la CI vérifie » est plus lente et plus fragile qu'avec Godot.
- **Licence** : Personal gratuit jusqu'à 200 000 $, puis 2 310 $ par siège et par an (vérifié). Le Runtime Fee est annulé, mais le précédent existe.
- **Risque produit** : DOTS/ECS est encore le piège qu'a connu Cities: Skylines II. Avec la simulation dans une assembly C# pure, on n'en a pas besoin.

### 4.3 Moteur maison, fork d'OpenSGolf ou de `sg-core`
- **Fork complet** : le meilleur délai jusqu'à la parité, le pire pour la suite (section 3.5). Il revient à refaire OpenSGolf avec un autre nom, et OpenSGolf existe déjà, gratuit.
- **`sg-core` seul + nouveau rendu** : `sg-core` contient les règles **et** les décodeurs des formats d'EA (sans utilité pour Golferoo, qui n'utilisera pas l'art d'EA). Ses règles sont écrites en unités de l'exe. Les reprendre, c'est hériter de la GPL et d'un modèle de monde en tuiles. Intérêt réel : une **suite de tests de référence** (« pour ce trou, le par vaut X »). On peut en tirer des fixtures de données sans lier le code (voir 5.3).
- **Moteur maison neuf (Rust + wgpu, ou C++)** : tout est à faire (UI dense, animation, import, outils). Pour une équipe d'agents, l'UI de gestion maison est un gouffre : OpenSGolf l'illustre bien avec ses dizaines de modules `*_ui.rs`.

### 4.4 Bevy 0.20
ECS bien adapté à la simulation d'agents, Rust (familier par OpenSGolf), MIT/Apache. Mais pas d'éditeur en octobre 2026, une API qui casse tous les 4 à 6 mois (0.20 vient encore de changer les shaders et BSN), une UI de gestion encore jeune (Feathers en construction), et peu de jeux commerciaux 3D de cette taille. Pour un projet de plusieurs années, l'entretien des migrations ronge le budget. Les modèles connaissent mal les versions récentes, ce qui produit beaucoup de code obsolète. À revoir dans deux ans.

### 4.5 Stack web (TypeScript + Three.js ou Babylon.js)
Excellente boucle pour les agents (TS typé, Vitest, Playwright, tout en texte), UI de gestion en HTML/CSS (le meilleur outil d'UI dense qui existe), partage de parcours par URL immédiat. Mais desktop via Electron ou Tauri, consoles impossibles en pratique, pas d'éditeur pour les artistes, animation et rendu de terrain à assembler soi-même, et des performances plafonnées pour des milliers d'agents et des arbres instanciés (les Web Workers compliquent la simulation). Bon choix pour un prototype ou pour un outil compagnon (galerie de parcours), pas pour le jeu Steam visé.

### 4.6 Unreal 5
Rendu et outils pour artistes au sommet, terrain (Landscape) et foules (Mass) puissants. Mais trop lourd pour un tycoon stylisé : C++ et Blueprints binaires (`.uasset`) non diffables, donc la pire boucle pour des agents ; builds lents ; UI de gestion (UMG) laborieuse ; 5 % de redevance au-delà de 1 M$ (à reconfirmer). Mauvais rapport pour ce produit.

### 4.7 Autres pistes
- **Stride (C#, MIT)** : 3D en C#, scènes en YAML ; communauté et documentation trop minces, connaissance faible des modèles.
- **Defold** : excellent en 2D et sur mobile, trop limité en 3D et en UI dense.
- **Flax** : petit, C# et C++, licence avec redevance ; aucun avantage décisif.
Aucune ne bat Godot ou Unity pour ce produit.

## 5. Architecture proposée (Godot 4.7 + core C#)

### 5.1 Couches
```
golferoo/
  sim/            Golferoo.Sim (.NET, aucune référence à Godot)
    World/        heightmap, surfaces (23 types), objets, propriétés
    Course/       trous, par, classes de trous, routage, analyse de coup
    Ball/         vol, rebond, roulement, déterministe (fixed-point ou double + RNG seedé)
    Agents/       golfeurs, besoins, humeur, pensées, pathfinding, personnel, faune
    Social/       sim-stories, adhésion, VIP
    Economy/      green fees, ventes, salaires, conseil, rapports
    Events/       tournois, défis, accomplissements
    Save/         sérialisation versionnée (JSON ou MessagePack), migration
    Mods/         chargement des packs de données, API de script sandboxée
  sim.tests/      xUnit : tests unitaires + fixtures de référence + tests de simulation longue
  sim.cli/        outil headless : simuler N années, rendre des rapports, comparer à des fixtures
  game/           projet Godot (C#) : rendu, caméra, entrée, UI, audio, outils in-game
    view/         synchronise l'état de sim en nœuds (MultiMesh, golfeurs, balle)
    ui/           scènes .tscn des rapports, fiches, panneaux
    tools/        éditeur de terrain, de trous, de bâtiments
  content/        données (TOML/JSON) : surfaces, bâtiments, landmarks, histoires, textes
  art/            sources Blender, exports glTF
```
Règle : `game/` lit l'état de `sim/` et lui envoie des commandes (`PaintSurface`, `OpenHole`, `HireStaff`…), jamais l'inverse. La simulation avance à pas fixe ; la vue interpole. Le même flux de commandes sert à l'annulation, aux replays, aux tests et, plus tard, au multijoueur (lockstep) ou aux défis asynchrones (un parcours plus une graine plus des commandes).

### 5.2 Testabilité headless
- `dotnet test` sur `sim.tests` en quelques secondes, sans Godot : c'est le test rapide des codeurs.
- `sim.cli simulate --years 5 --seed 42` produit des rapports texte que l'on compare (golden files) : c'est un filet contre les régressions d'équilibrage.
- CI : `dotnet test`, puis `godot --headless --import` et un test de fumée (charger une partie, faire 1 000 ticks, capture d'écran comparée à une tolérance près), puis l'export Windows et Linux.
- Les scènes Godot restent minces (structure et thème) ; la logique d'UI est en C# testable. Les agents éditent `.tscn` et `.tres` en texte.

### 5.3 Ce qui vient d'OpenSGolf
| Élément | Reprise | Pourquoi |
|---|---|---|
| Règles, chiffres, formules (par selon la longueur, coûts, seuils d'humeur, événements, SGA, tournois) | **savoir** : réécrits depuis `docs/*.md` et le recensement | les faits ne sont pas protégeables, le code l'est (GPL) |
| Code Rust de `sg-core` | **non lié**, sauf si Golferoo devient GPL | GPL, unités de l'exe, décodeurs d'EA inutiles |
| Comportements de référence | **fixtures** : faire tourner `sgtool` ou `sg-core` localement (outil séparé, jamais distribué avec Golferoo) pour produire des cas « entrée → sortie » en JSON, puis tester notre core contre ces fixtures | parité vérifiable sans lier le code ; à valider par Guillaume côté juridique |
| Design du modding (packs, `pack.toml`, ordre de chargement) | **inspiration** | bon design, éprouvé |
| Art du pack libre | non | placeholder, GPL, style 2D |
| Art et sons d'EA | jamais | propriétaires |

La parité devient un **mode** (« Classique ») configuré par des données, et la modernisation un autre jeu de données sur le même core, et non l'inverse comme dans OpenSGolf.

## 6. Risques et vérification la moins chère pour chacun

| Risque | Vérification qui tranche | Coût |
|---|---|---|
| R1. La 3D stylisée coûte trop cher en art pour Guillaume | Prototype : un trou en heightmap, 6 surfaces peintes, 50 arbres en MultiMesh, un golfeur low-poly animé (asset CC0), caméra iso rotative. Comparer à une capture d'OpenSGolf | 1 à 2 jours d'agent |
| R2. Godot peine à afficher une UI dense (rapports, listes de 150 golfeurs) | Recréer le Course Report et la liste des golfeurs avec tri et info-bulles, mesurer le temps de frame | 1 jour |
| R3. Le core C# est trop lent pour l'IA de coups en avance rapide | Benchmark : 300 golfeurs, échantillonnage de 64 points par coup, ×16 en vitesse, dans `sim.cli` | une demi-journée |
| R4. Les agents produisent du Godot 3 ou cassent les `.tscn` | Confier à un codeur Sonnet un petit chantier UI avec la CI headless, compter les corrections | 1 chantier |
| R5. Pas d'export web en C# | Demander à Guillaume si le web est une cible ; sinon c'est sans objet. Sinon, suivre la feuille de route de .NET sur WASM dans Godot | 0 |
| R6. Consoles via prestataire incertaines pour Godot C# | Demander un devis ou une confirmation à W4 Games ou Lone Wolf ; si les consoles sont prioritaires et sans réponse, basculer sur Unity | quelques mails |
| R7. Fixtures tirées d'OpenSGolf jugées « œuvre dérivée » | Avis de Guillaume, ou question à l'auteur d'OpenSGolf (accord explicite) | 1 échange |
| R8. Parité incomplète en réécrivant au lieu de lier | Mesurer le temps d'un codeur pour réimplémenter §4.3 (création de trou et par) avec les tests | 1 chantier |

## 7. Questions que seul Guillaume peut trancher

1. **Commercial ou open source ?** Si Golferoo est GPL et gratuit, lier `sg-core` via Godot + gdext (Rust) devient le meilleur chemin, et l'avantage de Unity disparaît. Si Golferoo se vend, la GPL est à écarter. As-tu un lien avec l'auteur d'OpenSGolf (`crspytopgn`), qui pourrait accorder une autre licence ?
2. **2D iso ou 3D ?** Je recommande la 3D stylisée avec caméra iso par défaut. Si tu tiens au pixel-art 2D, Godot reste le bon choix (son 2D est excellent) ; seule la direction artistique change.
3. **Plateformes prioritaires** : Steam (PC, Mac, Deck) seulement ? Consoles à moyen terme (ce qui avantage Unity) ? Mobile ou tablette ? Web (ce qui exclut le C# dans Godot aujourd'hui) ?
4. **Budget art** : un artiste humain, des assets achetés (ce qui avantage l'Asset Store de Unity) ou des assets CC0 et générés ?
5. **Fidélité** : un mode « Classique » chiffre pour chiffre est-il un objectif, ou seulement l'esprit de SimGolf ? Ça décide de l'effort de fixtures (R7, R8).
6. **Multijoueur ou partage** : partage de parcours et défis asynchrones seulement (facile avec l'architecture par commandes) ou jeu en ligne synchrone (à prévoir dès le core : déterminisme strict) ?

Sources web consultées le 2026-10-24 : [Godot, versions](https://godotengine.org/blog/release) ; [forum Godot, export web C#](https://forum.godotengine.org/t/is-there-an-update-on-exporting-c-projects-to-web/128821?page=2) ; [doc Godot 4.7, export web](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_web.html) ; [Unity, plans et prix](https://unity.com/runtime-fee) ; [Unity, annulation du Runtime Fee](https://unity.com/en/blog/unity-is-canceling-the-runtime-fee) ; [Bevy, nouvelles](https://bevy.org/news/) ; [Bevy 0.20](https://bevy.org/news/bevy-0-20/) ; [Unreal, licence](https://www.unrealengine.com/en-US/license) ; [Automaton, Cities: Skylines II](https://automaton-media.com/articles/newsjp/20260319-430623/) ; [Windows Central, Cities: Skylines II](https://www.windowscentral.com/gaming/cities-skylines-ii-runs-horribly-a-deep-dive-into-unity-explains-why).
