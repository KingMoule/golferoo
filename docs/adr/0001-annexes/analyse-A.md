# Analyse A : choix du moteur de Golferoo (angle : architecture et réutilisation du code)

Analyste A, 2026-10-24. Angle principal : architecture technique, découplage de `sg-core`, réutilisation d'OpenSGolf, scalabilité de la simulation. Tous les critères et toutes les options du brief sont couverts.

## 1. Recommandation

**Choix 1 : Godot 4.x comme coque (rendu, UI, entrées, audio, export), avec la simulation écrite en Rust dans une crate headless (`golferoo-sim`) branchée par GDExtension (godot-rust `godot` 0.5).**
Le cœur du jeu est une bibliothèque Rust pure qui ne connaît ni Godot ni l'écran : testée par `cargo test`, pilotable en CLI, sauvegardée en serde. Godot ne fait que l'afficher et lui transmettre les ordres du joueur. Une couche de liaison mince (`golferoo-godot`) fait le lien.

**Choix 2 : Bevy (tout Rust), avec la même crate `golferoo-sim`.** Si le prototype Godot du §5 échoue (export web de gdext, ou friction de la liaison), on garde la crate de simulation telle quelle et on remplace seulement la coque.

**Confiance : moyenne-haute** sur l'architecture (simulation Rust headless séparée de la coque). C'est ce que le code d'OpenSGolf montre de mieux et de plus mal fait. **Moyenne** sur Godot face à Bevy pour la coque : l'écart se joue surtout sur l'UI dense de gestion, l'outillage art et les consoles, où Godot gagne, et sur la productivité des agents tout-texte, où Bevy gagne.

**Sur OpenSGolf, en une phrase** : à prendre comme **documentation de référence** (11 700 lignes de docs, chiffres, routines décodées), et comme **code à lier seulement si Guillaume accepte que Golferoo soit sous GPL** (ou obtient une licence de l'auteur, qui n'est pas Guillaume). Dans les deux cas, ni la crate de jeu `simgolf` (miniquad) ni la structure de la simulation ne servent de fondation pour la suite.

## 2. Ce que j'ai vérifié

### Fichiers lus (par extraits)
- `docs/adr/0001-annexes/brief.md` (en entier), titres de `docs/recensement-sim-golf.md` et §9.2, §9.5, §9.6.
- OpenSGolf : `Cargo.toml` (workspace), `crates/*/Cargo.toml`, `crates/sg-core/src/lib.rs`, `course.rs` (en-tête), `golfer.rs` (en-tête, `struct Golfer` l. 84-92, `struct Club` l. 319-345, `Club::tick` l. 1593, `path_dir` l. 2594-2640), `mapsize.rs` (en-tête), `data.rs` (en-tête), `tests/parity.rs`, `tests/course_sizes.rs` (bench l. 202-245), `crates/simgolf/src/app.rs` (`struct SaveGame` l. 322, `struct App` l. 513-620, `club_tick` l. 2334, `step_game` l. 2579), `docs/NEXT_STEPS.md` (début), `CONTRIBUTING.md` (licence), `.github/workflows/rust.yml` (étapes), `packs/opensgolf-free/pack.toml`.

### Commandes et résultats
| Commande | Résultat |
|---|---|
| `wc -l` sur `crates/sg-core/src/**/*.rs` | 45 125 lignes, 67 modules ; les plus gros : `golfer.rs` 4 214, `land.rs` 2 227, `freeset/sprite.rs` 2 043, `unpack.rs` 1 702, `planner.rs` 1 494 |
| `wc -l` sur `crates/simgolf/src` | 59 401 lignes, 94 fichiers ; `app.rs` 4 753, `main.rs` 3 919 ; 48 fichiers importent miniquad |
| `cargo --version` | cargo 1.97.0 (2026-06-30) |
| `cargo test -p sg-core --release` | build 1 min 24 s, **237 tests unitaires OK, 1 ignoré**, plus 6 + 2 + 2 tests d'intégration OK ; 1 min 53 s au total |
| `cargo test -p sg-core --release --test course_sizes -- --ignored --nocapture` (bench) | voir tableau ci-dessous |
| `grep -rc '#[test]'` par module | `golfer.rs` 14 tests pour 4 214 lignes, `extras.rs` 13, `land.rs` 9, `economy.rs` 9, `planner.rs` 0 dans le fichier |
| `grep static/thread_local` dans sg-core | états globaux : `mapsize::GLOBAL`, `data::CURRENT`, `vfs::VFS`, `lang`, `fixes::ON` |
| `git log` / auteur | clone superficiel (1 commit), auteur `crspytopgn` (dépôt `github.com/crspytopgn/opensgolf`), **pas Guillaume** (Golferoo : KingMoule / Claude). Les PR viennent de branches `claude/…` : OpenSGolf est lui aussi développé par des agents |

Bench de la simulation des golfeurs (`Club::tick`, 40 000 ticks, 18 trous, partie neuve, machine de la session) :

| Carte | ms par tick (moy.) | tick le plus lent | golfeurs en jeu (moy. / max) | recherche de chemin coin à coin |
|---|---|---|---|---|
| 50 × 50 (original) | 0,161 | 15,95 ms | 4,5 / 12 | 0,210 ms |
| 74 × 74 | 0,163 | 15,25 ms | 5,8 / 12 | 0,417 ms |
| 98 × 98 | 0,158 | 11,09 ms | 6,6 / 12 | 0,752 ms |
| 128 × 128 | 0,155 | 12,53 ms | 6,0 / 12 | 1,388 ms |

Lecture : le tick moyen est minuscule, mais le bench ne monte qu'à 12 golfeurs (club débutant), pas aux 152 places du jeu. La recherche de chemin croît en O(n²) avec le côté de la carte. Les pointes à 11-16 ms viennent sans doute de la recherche de visée (`planner`, un seul golfeur par tick, drapeau `plan_busy`) : c'est une supposition, je ne l'ai pas profilée.

### Sources web (consultées le 2026-10-24)
- docs.rs, crate `godot` (godot-rust/gdext) : **v0.5.5 du 13 octobre 2026**, cible Godot 4 (`api-4-x`), export web par la feature `experimental-wasm`, « still a work-in-progress and is not yet well tested ». Vérifié : https://docs.rs/godot
- Godot 4.6 sorti en février 2026 (https://app.cinevva.com/news/2026-02-27-godot-4-6-released) et Godot 4.7 vers juin 2026 (https://gamedev.net/news/godot-47-released-r4011/, Phoronix via linux.org). **Cru, pas vérifié** : je n'ai vu que les titres des résultats.
- Bevy 0.19 : sortie relayée le 2026-06-22 (https://gamedev.net/news/4045-bevy-019-released/), crates `bevy_input` 0.19.1 sur docs.rs. Le contenu des notes de version n'est pas vérifié.
- Unity, https://unity.com/pricing-updates : Personal jusqu'à 200 000 $ de revenus et financements ; Pro à 2 310 $/an par poste, +5 % au 12 janvier 2026 ; annonce du 10 novembre 2025. Runtime Fee annulée en 2024 (blog Unity « Unity is canceling the Runtime Fee »). Vérifié sur la page tarifs, sauf le détail de la FAQ Runtime Fee.
- Ce que je crois sans l'avoir vérifié ici : Godot sous licence MIT, consoles via des tiers (W4 Games et des sociétés de portage) ; Bevy sous MIT/Apache-2.0, sans éditeur stable, avec des ruptures d'API à chaque version mineure (environ tous les 3-4 mois) ; Unreal à 5 % au-delà de 1 M$.

## 3. Ce que le code d'OpenSGolf montre vraiment

### 3.1 `sg-core` est découplé de la fenêtre, pas de l'architecture de l'exe
- **Bon** : aucune dépendance fenêtre, GPU ou audio (`Cargo.toml` : fontdue, lewton, nanomp3, miniz, serde, toml, rhai, sha2, image-webp). La crate compile pour wasm (la version navigateur l'utilise). Les types de simulation sont `serde` : `SaveGame` (dans `simgolf`) n'est qu'un agrégat de types `sg-core` (`Terrain`, `Land`, `Club`, `Course`, `Economy`, `ExeRng`…). La simulation est entière, au tick fixe (87 ms) et à graine (`ExeRng`) : elle est déterministe, ce qui est précieux pour les tests, le replay et un éventuel multijoueur en lockstep. Les tables sont surchargeables en TOML (`data.rs`) et le scripting rhai est sandboxé (`script.rs`) : le modèle de modding est sain.
- **Moins bon, et c'est l'essentiel pour nous** :
  1. **L'orchestration de la simulation n'est pas dans `sg-core`.** La boucle (`App::step_game`, `club_tick`, `staff_tick`, `resident_tick`) vit dans `simgolf/src/app.rs`, au milieu de l'état GPU : `App` mélange `textures: HashMap<PathBuf, Option<TextureId>>`, caméra, batches de rendu, `Club`, `Economy`, employés et RNG. `club_tick` recopie l'argent, l'année et la difficulté dans `Club`, ouvre des popups et joue des sons au milieu des règles. Sans réécrire environ 1 500 à 3 000 lignes de `app.rs`, il n'existe donc pas de « jeu complet headless » réutilisable tel quel.
  2. **La fidélité à l'exe passe avant la modélisation.** `golfer.rs` reproduit la table de 152 golfeurs de l'exe (`SLOTS = 152`, champs commentés par offset `+0x08`, adresses `0x59e7b8`). Le golfeur porte même sa **position écran** (`sx`, `sy`, « set by the drawing code ») et la règle en dépend : `sx == -1` (golfeur hors écran) change le comportement en fin de trou (l. 1869) et l'enregistrement des coups (l. 2920). Le bench doit forcer `sx = 400` pour simuler. C'est une fuite du rendu vers la règle, héritée de l'original.
  3. **État global de processus** : taille de carte (`mapsize::GLOBAL`, avec une surcharge par thread pour les tests), tables de données (`AtomicPtr<Data>`), VFS, langue. Une seule partie par processus, ce qui gêne la simulation de plusieurs parcours en parallèle (serveur, rapports, IA d'adversaires, tests parallèles).
  4. **Une couverture de tests inégale.** Les décodeurs sont bien testés (formats, zip, deflate64, wav, gsm). La simulation l'est peu : 14 tests pour les 4 214 lignes de `golfer.rs`, aucun test dans `planner.rs`. La parité est vérifiée surtout par des hachages dorés (`tests/parity.rs` : cours de démo et balistique, comparés à l'ancien port C++) et par des journaux (`SG_GOLFER_LOG`). C'est solide contre la régression, peu utile pour comprendre ou faire évoluer une règle.
  5. Environ 40 % de `sg-core` ne sert à rien à Golferoo : décodeurs de l'original (`unpack`, `bink`, `flc`, `gsm`, `sve`, `zip`, `inflate64`, `vfs`, `hires`…), utiles seulement pour une fonction « importer ma copie de SimGolf » qu'un jeu commercial ne voudra pas.

### 3.2 Inventaire de réutilisation
| Partie d'OpenSGolf | Lignes (≈) | Verdict pour Golferoo |
|---|---|---|
| `docs/DECODE_*.md`, `GAMELOGIC.md`, `PUBLISHER_EXE_NOTES.md`, `EXE_*.md`, `PARITY*.md` | 11 700 | **À consulter** : c'est la vraie valeur. Règles, formules et constantes (prix, salaires, besoins, humeurs, classes de trous, SGA). Les faits et les mécaniques ne sont pas protégés par le droit d'auteur, leur expression l'est |
| `flight.rs`, `planner.rs`, `golfer.rs` (coup, visée, humeur), `economy.rs`, `ratings.rs`, `tournament.rs`, `stories.rs`, `thoughts.rs`, `staff.rs`, `visitors.rs`, `vips.rs`, `weather.rs` | ≈ 14 000 | **Liable sous GPL** ; sinon **à réécrire** depuis les docs. À liaison égale, il faudra de toute façon **porter** ces fichiers vers un modèle propre (golfeur sans position écran, pas de table fixe, état non global) avant d'y greffer de la profondeur |
| `land.rs`, `course.rs`, `terrain.rs`, `heightmap.rs`, `tracts.rs` (génération, terrain par tuiles, hauteurs aux coins) | ≈ 4 500 | **À porter** : le modèle à tuiles de 50 × 50 avec hauteurs aux coins colle au remake, mais c'est la première chose que la 3D ou la vue libre remettront en cause (résolution, pentes continues) |
| `data.rs`, `packs.rs`, `script.rs` (TOML + rhai) | ≈ 2 000 | **Modèle à copier** (format de packs, validation, bac à sable) ; liable sous GPL |
| Décodeurs de l'original, `vfs`, `freeset/*` (art généré en CPU) | ≈ 18 000 | **Inutiles** pour Golferoo (on ne livre pas les assets d'EA) |
| `crates/simgolf` (miniquad, UI maison, effets) | 59 400 | **Inutile** comme fondation : rendu 2D isométrique à la main, UI immédiate maison, `App` fourre-tout |
| `packs/opensgolf-free` (19 Mo, GPL-2.0+) | | Placeholder possible sous GPL seulement |
| CI (`rust.yml` : fmt, clippy, test, build, web) | | **Modèle à copier** |

### 3.3 Ce que la suite exigera et que `sg-core` ne prévoit pas
- **Plus de 152 golfeurs** : la table fixe et les index `g ^ 1` (le partenaire de jeu est l'index voisin, l. 2603 de `golfer.rs`) supposent des paires dans un tableau fixe.
- **Cartes plus grandes ou plus fines** : le plafond est de 200 × 200 tuiles (`mapsize::MAX`), et le coût du pathfinding grandit en O(n²) par appel.
- **Vue libre ou 3D** : les hauteurs sont des niveaux entiers aux coins (`HEIGHT_STEP_PX = 5`, en pixels écran) ; le modèle de pente est pensé pour l'isométrique 800 × 600.
- **Plusieurs parcours ou simulation côté serveur** : l'état global (`mapsize`, `data`, `vfs`) l'empêche dans un même processus.
- **Nouvelles boucles de gameplay** : il faudrait un point d'extension de systèmes ; aujourd'hui, ajouter un système veut dire éditer `club_tick` dans `app.rs`.
- **Modding** : TOML + rhai, c'est déjà bien. C'est la partie la plus directement transposable.

Aucun de ces points n'est un défaut d'OpenSGolf, dont le but est la parité avec l'exe. Mais ils montrent que même sous GPL, `sg-core` est un **point de départ à porter**, pas une fondation à étendre.

### 3.4 Licence : ce que la GPL implique (je ne suis pas juriste)
- Lier `sg-core` (statiquement, ou dynamiquement dans le même processus) fait de Golferoo une œuvre dérivée : **le jeu entier doit être distribué sous GPL-2.0+**, source comprise, y compris la coque Godot ou Bevy (MIT et Apache sont compatibles).
- **Vendre reste possible** (Steam, itch, web). Fermer le code, non. Les assets (art, musique) peuvent garder une licence propre s'ils sont séparés du code, mais un pack chargé et exécuté comme script rhai se discute.
- **Consoles** : les SDK sont sous NDA et la GPL interdit d'ajouter des restrictions, donc c'est en pratique exclu. **iOS App Store** : historiquement incompatible (affaire VLC, 2011). Android et Google Play : sans souci.
- **Unity** + `sg-core` : on combine du GPL avec un runtime propriétaire qui n'est pas une « bibliothèque système », donc c'est non distribuable sauf exception accordée par l'auteur.
- L'auteur (`crspytopgn`) n'est pas Guillaume et il y a des contributeurs : une relicence demande l'accord de chacun, ou au moins d'un auteur principal qui détient presque tout.
- **Réécrire depuis les docs** (procédure « salle blanche » : les agents codeurs reçoivent une spec en français tirée des docs, pas les fichiers `.rs`) évite la GPL sur le code. Le risque résiduel tient aux tables de constantes extraites de l'exe d'EA : des faits, en principe non protégés, mais le nom SimGolf et le style visuel sont à éviter. À valider par un juriste avant toute commercialisation.

## 4. Analyse par option

Notes de 1 (mauvais) à 5 (excellent). Critères du brief : C1 couverture des features, C2 anticipation de la suite, C3 réutilisation et licence, C4 productivité des agents, C5 plateformes, coûts et pérennité, C6 performance, outillage art et coût des assets.

| Option | C1 | C2 | C3 | C4 | C5 | C6 | Total /30 |
|---|---|---|---|---|---|---|---|
| **Godot 4 + sim Rust (GDExtension)** | 5 | 5 | 4 | 4 | 4 | 4 | **26** |
| Bevy + sim Rust | 4 | 4 | 5 | 5 | 3 | 3 | **24** |
| Godot 4, sim en C# | 5 | 4 | 3 | 4 | 4 | 4 | 24 |
| Godot 4, sim en GDScript | 4 | 3 | 2 | 3 | 4 | 3 | 19 |
| Web TS (Pixi/Three) + sim Rust en wasm | 4 | 3 | 4 | 4 | 3 | 3 | 21 |
| Fork d'OpenSGolf (miniquad, moteur maison) | 5 | 2 | 3 (GPL imposée) | 3 | 3 | 2 | 18 |
| Moteur maison neuf (Rust, wgpu + egui) | 3 | 3 | 4 | 4 | 3 | 2 | 19 |
| Unity 6 (C#) | 5 | 5 | 2 | 2 | 5 | 5 | 24 |
| Unreal 5 | 3 | 4 | 1 | 1 | 4 | 3 | 16 |

Les totaux ne pèsent pas les critères. Pour Golferoo, je pèse plus lourd C4 (le développement est mené par des agents) et C2 (la suite compte plus que le remake).

### Godot 4 + simulation Rust (recommandé)
- **Brancher la simulation** : une crate `golferoo-godot` (cdylib) expose une classe `SimWorld` (`#[derive(GodotClass)]`) avec `tick(n)`, `apply(cmd)`, `snapshot()` et des getters par lot (positions des golfeurs en `PackedVector3Array`, tuiles modifiées depuis le tick précédent). Estimation : 1 000 à 2 500 lignes de liaison, stables, et la règle n'y vit jamais. Si l'on lie `sg-core` (sous GPL), c'est la même chose avec des adaptateurs autour de `Club`, `Course` et `Land`, mais il faut d'abord sortir l'orchestration de `app.rs` (point 1 du §3.1).
- **C1** : `Control`, `Container` et `Theme` couvrent l'UI dense (rapports, tableaux, graphes). `TileMapLayer` couvre la vue isométrique 2D ; un `MeshInstance3D` généré depuis la grille de hauteurs couvre la 3D. La sauvegarde reste du serde côté Rust.
- **C2** : passer à la 3D ou à une vue libre ne touche que la coque. Les packs TOML et le scripting restent côté Rust. Pour le multijoueur, le lockstep déterministe vient de la simulation entière et seedée ; Godot apporte en plus le réseau haut niveau. Le partage de parcours est un fichier serde.
- **C3** : on garde la parité Rust avec OpenSGolf, on peut porter des fichiers de règles ou les réécrire depuis les docs, et la GPL reste un choix ouvert : Godot est MIT, donc les deux voies restent possibles.
- **C4** : `.tscn` et `.tres` sont du texte diffable. `godot --headless` fait tourner des scènes en CI. Mais 90 % des tests sont `cargo test` sur la crate de simulation, sans Godot. Côté faiblesses, les modèles connaissent moins bien l'API gdext 0.5 que GDScript ou Unity C#, et les scènes ne se valident pas sans lancer Godot.
- **C5** : PC, Mac, Linux, Steam Deck et Android sont mûrs ; iOS aussi. **Web avec gdext : expérimental** (docs.rs, 2026-10). Consoles via des tiers, et à condition de ne pas être sous GPL. Pas de redevance.
- **C6** : la simulation est native ; le rendu Godot suffit largement pour un tycoon. Les assets passent par le pipeline glTF et Blender standard.

### Bevy + simulation Rust (second choix)
- **Brancher** : quasi nul. `SimWorld` devient une `Resource` et un système appelle `tick` dans `FixedUpdate`. `sg-core` se brancherait en environ 200 lignes. On peut ensuite migrer des golfeurs en entités ECS, mais rien ne l'oblige.
- **Pour** : tout est code Rust, donc diffable ; tests headless natifs (`MinimalPlugins`) ; un seul langage et un seul outil (`cargo`) pour les agents ; licence MIT/Apache ; web par wasm.
- **Contre** : l'UI dense de gestion est le point faible historique de Bevy (`bevy_ui` est rudimentaire, `bevy_egui` fonctionnel mais d'aspect « outil »). Pas d'éditeur pour placer décors et animations. Les ruptures d'API à chaque version imposent une migration tous les 3-4 mois (les agents y sont bons, mais c'est du coût permanent). Consoles quasi inexistantes. Les modèles connaissent mal les dernières versions de Bevy (0.17-0.19), dont l'API a bougé.

### Godot 4 en C# ou en GDScript (sans Rust)
- C# : bonne productivité, les modèles connaissent bien C#, tests via xUnit hors moteur possibles si la simulation est une bibliothèque .NET séparée. Mais **aucune réutilisation de code d'OpenSGolf** (réécriture depuis les docs), et l'export web de Godot en C# reste limité (crainte non vérifiée en 2026-10 ; à contrôler). Option tout à fait viable si l'on renonce au code Rust.
- GDScript : simulation de centaines d'agents avec des recherches de chemin en inondation, interprétée, ce qui est trop lent sans cache agressif. Tests (GUT, GdUnit) moins confortables. À réserver à la colle UI.

### Stack web TypeScript + simulation Rust en wasm
- `sg-core` compile déjà en wasm32 (la version navigateur d'OpenSGolf le prouve) : `wasm-bindgen` plus Pixi pour l'isométrique, Three ou Babylon pour la 3D. C'est l'option où la réutilisation est la plus directe après Bevy, avec une UI DOM et React excellente pour la gestion dense et des modèles très forts en TS.
- Contre : Steam et le bureau passent par Electron ou Tauri ; consoles impossibles ; mobile moyen ; deux langages et deux chaînes d'outils ; la performance de rendu 3D est limitée pour une suite ambitieuse. Bonne option de **prototype**, moins bonne de **produit**.

### Fork d'OpenSGolf ou de `sg-core` comme moteur maison
- Pour : features à parité presque gratuites (c'est déjà un SimGolf jouable, multiplateforme, web compris).
- Contre : **GPL imposée** (pas de consoles, pas d'iOS, pas de fermeture). Une fondation calquée sur l'exe (152 places, carte à tuiles, position écran dans la règle, état global) qu'il faudrait démonter avant toute « simulation plus profonde ». Un rendu et une UI maison de 59 000 lignes sur miniquad (OpenGL 2/ES 3) à maintenir soi-même, sans éditeur ni pipeline 3D. Golferoo deviendrait un fork divergent d'un projet actif, avec des fusions amont de plus en plus coûteuses. **À écarter comme fondation, à garder comme oracle** : lancer OpenSGolf à côté pour comparer un comportement.

### Moteur maison neuf (Rust, wgpu + egui ou winit)
Toute la liberté et le tout-texte, mais on réécrit ce que Godot ou Bevy donnent (rendu, animation, audio, entrées, export mobile et web, UI). Le coût d'entrée est disproportionné pour une petite équipe, et il n'y a pas d'outillage art. Je ne le recommande pas.

### Unity 6
- Pour : outillage art et animation de premier plan, consoles et mobile mûrs, C# très connu des modèles, beaucoup de références de tycoons.
- Contre : la simulation Rust ne peut passer que par une ABI C (`cdylib` + P/Invoke, `csbindgen`), avec du marshalling, sous IL2CPP sur toutes les plateformes, et **incompatible avec `sg-core` sous GPL**. Les scènes et prefabs sont du YAML diffable mais fragile (GUID, `.meta`), et la boucle de vérification exige l'éditeur ou le batchmode, qui est lourd en CI. Coût : Pro à 2 310 $/an par poste au-delà de 200 k$, et un historique de confiance entamé (Runtime Fee, 2023-2024).

### Unreal 5
Surdimensionné pour un tycoon isométrique : C++ et Blueprints binaires (non diffables), CI lourde, et redevance de 5 % au-delà de 1 M$ (cru). Une liaison Rust est possible mais pénible. À écarter.

### Synthèse : coût de brancher une simulation Rust (ou `sg-core`) derrière chaque coque
| Coque | Mécanisme | Colle estimée | Compatible GPL (`sg-core`) | Tests de la simulation sans moteur |
|---|---|---|---|---|
| Godot 4 | GDExtension (crate `godot` 0.5, cdylib) | 1 000 à 2 500 lignes | Oui (Godot MIT) | Oui (`cargo test`) |
| Bevy | appel direct (Resource + système) | 200 à 500 lignes | Oui (MIT/Apache) | Oui |
| Web TS | `wasm-bindgen` + TS typé généré | 500 à 1 500 lignes | Oui, si tout le bundle est GPL | Oui |
| Unity | ABI C, P/Invoke, `csbindgen`, IL2CPP | 1 500 à 3 000 lignes + marshalling | **Non** sans exception de l'auteur | Oui |
| Unreal | bibliothèque statique + module C++ | 2 000 lignes et plus | Non en pratique | Oui |
| Fork OpenSGolf | aucun (déjà en place) | 0, mais 1 500 à 3 000 lignes d'`app.rs` à démêler pour la testabilité | GPL imposée | Partiel aujourd'hui |

Dans tous les cas, **le travail préalable est le même** : sortir l'orchestration de la boucle hors de l'état de rendu (§3.1). C'est la vraie dette, et elle ne dépend pas du moteur.

### Couverture des features du recensement par couche (option recommandée)
| Feature (recensement) | Couche | Remarque |
|---|---|---|
| §2 modes, difficulté, sandbox, championnat | `golferoo-sim` (scénario) + UI Godot | Les règles de difficulté sont des tables TOML |
| §3 propriétés, génération du terrain, achat de terrain, temps | `sim/model` + `sim/data` | Génération seedée, testable par hachage |
| §4.1-4.2 terrains peignables et relief | `sim/model::Terrain` (commandes `Paint`, `Raise`) + rendu Godot (`TileMapLayer` ou mesh) | Diff de tuiles par tick vers la coque, jamais la grille entière |
| §4.3 création de trous, §14.2 classes de trous | `sim/systems/holes` | Le calcul des classes est une fonction pure de la grille |
| §5-7 bâtiments, landmarks, immobilier | `sim/model` + `sim/data` | Effets sous forme de modificateurs déclarés en TOML |
| §8 personnel | `sim/systems/staff` + `nav` | Mêmes champs de flux que les golfeurs |
| §9 golfeurs : arrivée, besoins, humeur, pensées, déplacements, coup | `sim/systems/*`, `nav`, `flight`, `planner` | La partie la plus lourde ; LOD de simulation |
| §10-12 adhésion, VIP, sim-stories | `sim/systems/social` | Les histoires sont des `Event` que l'UI raconte |
| §13 le pro jouable | `sim` (commandes `Aim`, `Swing`) + entrée Godot | La visée en aperçu est une requête pure sur `World` |
| §14-16 SGA, tournois, notes, économie, conseil | `sim/systems/{ratings,tournament,economy}` | Rapports = requêtes sur un historique stocké dans `World` |
| §17 faune et décor | coque (pur visuel) + petit état dans `sim` | Ce qui n'influence pas la règle reste côté Godot |
| §18 interface et rapports | Godot `Control` | Données fournies par des requêtes `SimWorld` |
| §19 son et musique | Godot | Déclenché par des `Event` |
| Sauvegarde | `sim/save` (serde + version) | L'aller-retour est testé en CI |

## 5. Architecture proposée (option recommandée)

```
golferoo/
  crates/
    golferoo-sim/      # règles pures : pas de Godot, pas d'E/S, pas d'état global
      model/           # World { terrain, holes, golfers, staff, economy, calendar, rng }
      systems/         # arrivée, besoins/humeur, visée (planner), vol de balle, économie, notes, tournois, histoires
      nav/             # champs de flux par destination, invalidés par zone à l'édition
      data/            # tables TOML (prix, terrains, bâtiments) + validation (modèle data.rs)
      script/          # rhai sandboxé (modèle script.rs) pour packs et scénarios
      save/            # serde + version + migrations
    golferoo-cli/      # headless : `golferoo-cli sim --course x.toml --days 30 --seed 7` → JSON de métriques
    golferoo-godot/    # GDExtension : SimWorld, commandes, instantanés par lot ; aucune règle
  godot/               # projet Godot : scènes .tscn texte, UI, rendu iso/3D, audio
  specs/  docs/
```

Règles de conception, tirées des défauts observés dans OpenSGolf :
1. **La simulation n'importe rien du rendu.** La visibilité à l'écran n'entre jamais dans une règle (contre-exemple : `sx == -1` dans `golfer.rs`). Si une règle de l'original en dépend, on la reformule (par exemple « golfeur proche de la caméra » devient un paramètre explicite de LOD de simulation).
2. **Un seul point d'entrée** : `World::step(&mut self, cmds: &[Command]) -> Vec<Event>`. Les popups, sons et messages sont des `Event` que la coque consomme (contre-exemple : `club_tick` ouvre des popups).
3. **Aucun état global** : taille de carte, tables et langue sont des champs de `World` ou des `Arc<Data>` passés explicitement, ce qui permet plusieurs mondes par processus (tests parallèles, rapports de parcours, serveur).
4. **Déterminisme** : entiers ou virgule fixe pour la règle, RNG seedé par sous-système (OpenSGolf a déjà séparé `exe_rng`, `deco_rng` et `look_rng` ; c'est la bonne idée). Le hash d'état par tick, comme `SG_GOLFER_LOG`, sert aux tests de non-régression et au lockstep.
5. **Golfeurs sans plafond fixe** : `Vec` ou `SlotMap`. La simulation détaillée se fait par LOD (golfeurs lointains en pas grossiers).
6. **Pathfinding** : l'inondation de `path_dir` se refait à chaque appel (1,4 ms sur 128 × 128, `Vec` alloués à chaque appel). Les destinations sont peu nombreuses (tees, greens, clubhouse, bâtiments) : un **champ de flux par destination**, recalculé seulement quand le terrain change dans sa zone, rend le coût par golfeur O(1). Estimation : 40 destinations × 1,4 ms = 56 ms pour tout recalculer après une édition, et 0 en régime établi. Des centaines de golfeurs sur une carte de 200 × 200 tiennent alors largement dans un tick de 87 ms.
7. **Terrain** : grille de tuiles typées plus hauteurs aux coins pour le remake (compatible avec les chiffres de l'original), derrière une interface `Terrain` qui permettra une grille plus fine ou une heightmap continue pour la 3D.

**Ce qui vient d'OpenSGolf** :
- Si Golferoo **n'est pas** GPL (cas par défaut que je suppose) : rien en code. Les agents reçoivent des specs (`specs/*.md`) rédigées à partir de `docs/*.md` et des chiffres. OpenSGolf sert d'oracle de comportement (on lance son binaire, ou son bench, et on compare des statistiques : coups moyens par trou, humeur, revenus par mois) plutôt que des hachages ligne à ligne.
- Si Golferoo **est** GPL : on vendore `sg-core` (pas `simgolf`), on garde ses tests, et l'on porte module par module vers `golferoo-sim` (en commençant par `flight`, puis `planner`, `economy` et `ratings`) en ajoutant des tests de propriétés à chaque port.

**Tests headless** :
- Unitaires et propriétés dans `golferoo-sim` (`cargo test`, quelques secondes) : vol de balle, choix du club, prix, notes.
- Scénarios : `golferoo-cli` simule N jours sur un parcours de référence, avec des assertions sur des bandes statistiques (le golfeur moyen joue entre X et Y au trou 3).
- Hash d'état par tick à seed fixe contre les régressions involontaires, régénéré explicitement quand une règle change.
- Godot : une poignée de tests de fumée en `godot --headless --script` (chargement des scènes, aller-retour de sauvegarde), plus un test de liaison qui vérifie que `SimWorld` expose les bons types.
- CI : `cargo fmt`, `clippy`, `cargo test`, puis l'export Godot headless (Linux, Windows, web) en tâche séparée.

**Ordre de construction proposé** (chaque étape vérifiable sans Godot sauf la dernière) :
1. `golferoo-sim` : terrain, commandes de peinture, sauvegarde ; tests d'aller-retour.
2. Vol de balle et visée (depuis `docs/` d'OpenSGolf) ; tests contre des valeurs dorées (`tests/parity.rs` en donne pour le vol).
3. Golfeurs (arrivée, marche avec champs de flux, coup, fin de trou) ; bench de 300 golfeurs.
4. Économie, notes, personnel ; scénario CLI « 1 an » avec des bandes statistiques.
5. Coque Godot : vue isométrique, peinture, panneau de rapports, sur le prototype du §6.

## 6. Risques et vérification la moins chère

| Risque | Gravité | Vérification qui tranche |
|---|---|---|
| L'export web de gdext est expérimental (docs.rs 2026-10) : si le web est une plateforme cible, Godot + Rust peut buter | Haute si le web est cible, faible sinon | **Prototype d'un jour** : projet Godot 4.7 + crate gdext minimale qui appelle un `tick` de 1 000 agents, exporté en web (avec et sans threads), puis mesure de la taille, du démarrage et du FPS |
| Les agents écrivent mal du gdext 0.5 (API récente, peu présente dans les données d'entraînement) | Moyenne | Dans le même prototype, un agent codeur écrit seul la liaison `SimWorld` à partir de la doc ; on compte ses itérations ratées. Au-delà d'un jour, on bascule la coque vers Bevy ou on écrit la colle en GDScript |
| L'UI dense de gestion coûte cher dans Godot quand elle est écrite par des agents sans éditeur | Moyenne | **Demi-journée** : un agent écrit en `.tscn`/`.tres` texte le panneau « Rapports » (onglets, tableau triable, graphe) et une capture headless le vérifie |
| La GPL est découverte trop tard (code copié de `sg-core` par un agent) | Haute | Décision de Guillaume avant le premier chantier de règles ; consigne dans `CLAUDE.md` : « ne pas lire `crates/` d'OpenSGolf, seulement `docs/` », et un hook qui refuse les chemins `opensgolf/crates` si l'on choisit la salle blanche |
| Simulation de centaines de golfeurs trop lente avec la visée et les chemins | Faible en Rust | **Un jour** : bench `golferoo-sim` avec 300 golfeurs sur 128 × 128 et des champs de flux ; objectif < 5 ms par tick en moyenne et < 20 ms au pire (repère OpenSGolf : 0,16 ms en moyenne et 16 ms au pire avec 12 golfeurs) |
| Bevy (plan B) : ruptures d'API et UI faible | Moyenne | Ne concerne que la coque ; la crate de simulation ne dépend pas de Bevy. Coût de bascule borné à la coque |
| Dérive « parité à tout prix » qui reproduit l'exe au lieu de concevoir | Moyenne | Écrire dans la spec du remake que la parité se juge sur des statistiques de jeu, pas sur la structure de l'exe |

### Prototype combiné proposé (2 jours d'agent au plus, avant l'ADR définitif)
- **Jour 1, sans moteur** : crate `proto-sim` (hors OpenSGolf, en salle blanche) avec une grille de 128 × 128, 300 agents, des champs de flux vers 40 destinations et un tick de 87 ms simulé ; `cargo bench` ou un test `--ignored` qui imprime les ms par tick, comme le bench d'OpenSGolf.
- **Jour 2, coque** : la même crate derrière gdext dans Godot 4.7 (vue 2D, 300 sprites, un panneau `Control` de statistiques), exportée sous Linux et en web ; en parallèle, la même crate derrière Bevy 0.19 (vue 2D, `bevy_egui`) pour comparer à effort égal.
- **Critères de décision** : export web fonctionnel ou non ; nombre de corrections ratées de l'agent par coque ; lignes de colle ; temps de CI ; ressenti de Guillaume sur l'UI produite.

### Conséquences pour le workflow du dépôt (si l'option est retenue)
- `CLAUDE.md`, section Stack : installer = `rustup` + Godot 4.7 headless ; test rapide = `cargo test -p golferoo-sim <filtre>` ; suite complète = `cargo test --workspace` + export Godot headless en CI ; lint = `cargo fmt`, `clippy`, `gdformat` (déjà prévu par le hook).
- Les agents codeurs travaillent presque toujours dans `crates/golferoo-sim`, où tout est vérifiable en quelques secondes sans éditeur ; les chantiers de coque Godot sont plus rares, plus petits, et contrôlés par capture headless.

## 7. Questions que seul Guillaume peut trancher

1. **Open source sous GPL ou jeu commercial à code fermé ?** C'est la question qui conditionne tout le §3.4. Avec la GPL, on peut lier et porter `sg-core` et gagner des mois sur les règles, mais on renonce aux consoles et sans doute à iOS. Sans GPL, on travaille en salle blanche depuis les docs.
2. **Contacter l'auteur d'OpenSGolf (`crspytopgn`) ?** Pour une collaboration, une exception de licence, voire une double licence. Et si ce n'est pas lui, savoir si un lien public avec ce projet est souhaité.
3. **Plateformes cibles au lancement et plus tard** : PC et Steam Deck seulement ? Web (ce qui pèse contre gdext) ? Mobile ? Consoles (ce qui pèse contre la GPL et pour Godot via des tiers ou Unity) ?
4. **2D isométrique au lancement, 3D plus tard, ou 3D dès le départ ?** L'architecture proposée supporte les deux, mais le budget art n'est pas le même.
5. **Quelle fidélité ?** « Mêmes règles et mêmes chiffres que l'exe » (on porte `golfer.rs` presque tel quel) ou « même expérience » (on réécrit proprement, en validant par des statistiques) ?
6. **Budget art et animation** : assets faits par une personne, achetés, ou générés ? Ce choix pèse sur l'outillage (éditeur Godot ou Unity contre pipeline tout-code de Bevy).
7. **Multijoueur ou partage de parcours** : envisagé sérieusement ? Si oui, le déterminisme strict (§5, point 4) devient une exigence dès le premier chantier.
