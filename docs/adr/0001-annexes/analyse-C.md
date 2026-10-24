# Analyse C : coûts, risques et conduite du projet par agents IA

Analyste C, 2026-10-24. Angle principal : coûts, licences, propriété intellectuelle, pérennité, et efficacité réelle d'agents
Claude (orchestrateur Opus, codeurs Sonnet) sur chaque moteur, dans un projet mené par une seule personne qui ne code pas
elle-même et fusionne les PR. Tous les critères du brief sont couverts, mais pondérés par cet angle.

## 1. Recommandation

**Choix 1 : Godot 4.7 (stable), GDScript typé, avec une couche de simulation pure (sans nœuds) testée headless en CI.**
Une extension Rust (GDExtension via godot-rust) reste une porte de sortie, à n'ouvrir que pour un point chaud mesuré.

**Choix 2 : moteur maison en Rust, sur la base d'OpenSGolf / `sg-core`**, mais **seulement** si l'une de ces deux conditions
est remplie : (a) Guillaume est l'auteur d'OpenSGolf (dépôt `crspytopgn`, auteur unique) et peut donc le relicencier, ou
(b) Guillaume accepte que Golferoo soit lui aussi sous GPL, ce qui ferme en pratique les consoles et la fermeture du code.

**Confiance : moyenne.** Forte sur l'élimination d'Unreal, d'Unity et de Bevy pour ce projet précis ; moyenne sur
Godot face au Rust maison, car l'écart dépend de deux réponses que seul Guillaume a : qui détient le copyright d'OpenSGolf,
et les consoles sont-elles un objectif réel.

Pourquoi, en trois phrases :
1. **Coût et licence nuls et stables** : Godot est sous MIT, sans redevance ni siège ; Unity a encore changé ses prix en
   janvier 2026, Unreal prélève 5 % au-delà de 1 M$, et la GPL d'OpenSGolf contaminerait Golferoo si on reprend son code.
2. **Boucle de vérification d'agent prouvée dans ce conteneur** : Godot 4.7.2 se télécharge en 2 s (78 Mo), importe un
   projet et lance une suite de tests GDScript headless en moins de 10 s, scènes en texte diffable, code de sortie exploitable
   par la CI. Aucun éditeur graphique n'est requis pour vérifier.
3. **Il couvre la suite sans réécriture** : 2D isométrique aujourd'hui, 3D ou caméra libre demain dans le même moteur,
   UI dense native (Control, thèmes), export web/mobile/desktop gratuit, consoles via prestataires (W4 Games et d'autres),
   ce que ni la GPL ni un moteur maison ne permettent sans effort.

## 2. Ce que j'ai vérifié

### Fichiers lus (par extraits)
- `docs/adr/0001-annexes/brief.md` (entier).
- `docs/recensement-sim-golf.md` : titres (sections 1 à 22) et fiche d'identité. 800 × 600 isométrique en tuiles, solo,
  centaines de golfeurs simulés, terrain peignable, relief, sim-stories.
- OpenSGolf : `README.md` (licence l. 185-190, avertissement EA/Firaxis l. 174-178), `LICENSE` (GPL v2), `Cargo.toml`
  (`license = "GPL-2.0-or-later"` hérité par les 4 crates), `crates/sg-core/Cargo.toml` (deps : serde, toml, rhai, fontdue…),
  liste des modules de `crates/sg-core/src` (≈ 60 modules, 45 125 lignes, 249 `#[test]`),
  `packs/opensgolf-free/README.md` (set libre lui aussi GPL-2.0-or-later, polices en OFL),
  `docs/REVERSE_ENGINEERING.md` l. 1-30 (méthode : faits lus dans l'exe, jamais de code décompilé recopié),
  `.github/workflows/` (rust, android, pages, release).
- `git shortlog` d'OpenSGolf : **un seul auteur, `crspytopgn`**, clone superficiel (1 commit visible), dernier commit
  2026-10-22, fusion d'une branche `claude/magical-tesla-d1thkl` : OpenSGolf est lui-même développé avec des agents Claude.

### Essais dans le conteneur (≈ 8 minutes au total)

**Godot 4.7.2 headless** (dossier `scratchpad/godot/`) :
```
curl -sSL -o godot.zip https://github.com/godotengine/godot/releases/download/4.7.2-stable/Godot_v4.7.2-stable_linux.x86_64.zip
# 77 860 424 octets en 1,6 s ; un seul binaire, aucune dépendance installée
godot --headless --import                         # 7,2 s, crée .godot/ (cache, à ignorer dans git)
godot --headless --script res://tests/run_tests.gd
#   Godot Engine v4.7.2.stable.official.ed1daf0bf
#   2x500 agents x 200 ticks in 10.4 ms
#   RESULT: 0 failure(s)        -> exit 0, 0,18 s
```
Le projet tient en 4 fichiers texte écrits à la main sans éditeur : `project.godot`, `main.tscn` (scène texte, format 3),
`sim/golfer_sim.gd` (classe `GolferSim extends RefCounted`, RNG à graine, 500 golfeurs qui marchent vers un trou),
`tests/run_tests.gd` (`extends SceneTree`, `quit(fails)`).
- Test volontairement cassé : affiche `FAIL arrived=500`, **exit 1**. La CI voit l'échec.
- **Piège trouvé** : avec une erreur de type (`func bad() -> int: return "x"`), Godot affiche bien
  `SCRIPT ERROR: Parse Error: Cannot return a value of type "String" as "int"`, mais **le processus ne se termine jamais**
  (`timeout 20` → exit 124). Un agent qui lance la commande sans `timeout` reste bloqué. Remède : toujours
  `timeout 120 godot --headless …` et un lanceur de tests établi (GdUnit4 ou GUT) qui gère l'abandon, plus
  `--check-only` sur chaque script modifié. À inscrire dans le skill de test du dépôt.
- Bon point : le typage statique GDScript a attrapé l'erreur au chargement, pas à l'exécution.

**Bevy headless** (dossier `scratchpad/bevyprobe/`) :
```
cargo add bevy@0.20 --no-default-features   # puis cargo run --release
#   error: rustc 1.97.0 is not supported by the following package: bevy@0.20.0 requires rustc 1.97.1
cargo add bevy@0.19 --no-default-features   # 135 crates, 51 s (2 min 33 CPU), target/ = 291 Mo
#   arrived=500                                -> exit 0
```
Bevy 0.20, sorti le 2026-10-08, exige le rustc publié juste avant : la mise à jour de la chaîne d'outils est un passage
obligé à chaque version. Mon petit programme ECS (`MinimalPlugins`, `Query`, `ResMut`, `app.update()`) a compilé du premier
coup, mais il n'utilise que la partie la plus stable de l'API ; les ruptures portent sur le rendu, l'UI, les assets et
l'ordonnancement, que je n'ai pas testés.

Pas d'essai Unity ni Unreal : ni l'un ni l'autre ne s'installe sans compte et sans activation de licence, ce qui est en soi
un résultat pour une boucle d'agents en conteneur.

### Sources web (consultées le 2026-10-24)
| Fait | Source | Date de la source |
|---|---|---|
| Godot 4.7 sorti le 2026-06-18 ; 4.7.2 le 2026-08-18 ; 4.6 le 2026-01-26 ; 4.5 le 2025-09-15 | [godotengine.org/blog/release](https://godotengine.org/blog/release) | page lue le 2026-10-24 |
| Unity Personal gratuit jusqu'à 200 000 $ de revenus et financement ; Pro 2 310 $/an/siège ; hausse de 5 % au 2026-01-12 (annoncée le 2025-11-10) ; Havok retiré de Pro à partir de 6.3 ; splash optionnel en Unity 6 | [unity.com/pricing-updates](https://unity.com/pricing-updates), [unity.com/runtime-fee](https://unity.com/runtime-fee) | page lue le 2026-10-24 |
| Runtime Fee annulé (sept. 2024) | idem + [cinevva](https://app.cinevva.com/hi/news/2024-09-12-unity-runtime-fee-cancelled) | 2024-09-12 |
| Bevy 0.20 le 2026-10-08, 0.19 le 2026-06-19, 0.18 le 2026-01-13 ; ni éditeur ni 1.0 annoncés | [bevy.org/news](https://bevy.org/news/) | page lue le 2026-10-24 |
| W4 Consoles : port Switch 2 en bêta, « pas encore de certification complète », inclus dans l'offre « All Console Platforms » (prix non publié) | [w4games.com](https://www.w4games.com/blog/w4-games-news-1/w4-consoles-beta-support-for-the-nintendo-switch-2-87) | 2025-10-17 |
| Godot C# toujours pas exportable vers le web (4.7), prototype communautaire seulement | [cinevva, Godot ou Unity pour le web](https://app.cinevva.com/fr/guides/godot-vs-unity-web-games) | 2026-06-07, maj 2026-09-06 |
| Godot durcit sa politique sur le code IA **dans les contributions au moteur** (pas les jeux faits avec Godot) | [gamingonlinux](https://gamingonlinux.com/2026/07/godot-engine-to-get-stricter-on-ai-contributed-code) | 2026-07-01 |
| Unreal : redevance de 5 % au-delà du premier million de revenus brut par titre | [roadtovr](https://roadtovr.com/unreal-engine-royalty-free-first-1-million-revenue/), [conductatlas (EULA)](https://conductatlas.com/platform/unreal-engine/unreal-engine-eula/royalty-payment-obligation-5-above-1m/) | 2020 (seuil), EULA courante non relue en entier |

**Ce que je crois sans l'avoir vérifié aujourd'hui** : la licence MIT de Godot et Bevy (stable depuis des années) ; le fait
que les SDK consoles (Nintendo, Sony, Microsoft) sont sous NDA et incompatibles avec l'obligation GPL de publier tout le code
lié, ce qui explique qu'OpenTTD, ScummVM et consorts n'aient pas de version console officielle ; l'existence d'autres
prestataires de portage Godot (Lone Wolf Technology, Pineapple Works) ; la licence MIT de Pixi, Three.js et Babylon.

## 3. Analyse par option

### 3.1 Godot 4.7 (GDScript, éventuellement C# ou GDExtension Rust)
- **Coût** : 0 €, MIT, aucune activation, aucun compte. Seuls coûts : prestataire console (W4 ou autre, tarif sur devis) et
  signature de code (Apple, Windows) comme pour tout moteur.
- **Pérennité** : fondation à but non lucratif, cadence régulière (4.4 → 4.7 en quinze mois, maintenances mensuelles),
  code source disponible : le pire cas (abandon) laisse un moteur forkable. Pas de risque de changement de prix rétroactif.
- **Agents** : `.gd`, `.tscn`, `.tres`, `project.godot` sont du texte diffable ; un agent écrit une scène sans éditeur (essai
  ci-dessus). Les UID (`uid://…`) et `.import` ajoutent un peu de bruit ; `.godot/` est exclu de git. Connaissance des
  modèles : bonne sur Godot 4, avec une **erreur typique connue** : réflexes Godot 3 (`export var`, `yield`, `onready`,
  `KinematicBody2D`, `instance()`), qu'un fichier de règles et le typage strict attrapent. Réglage à imposer :
  `debug/gdscript/warnings/untyped_declaration = error` et consorts.
- **Choix du langage** : GDScript pour tout ce qui doit aller sur le web et sur console (C# n'exporte pas vers le web en
  4.7, et le support C# console dépend du prestataire). C# apporterait un meilleur outillage de test (xUnit) mais coupe le
  web : à écarter tant que Guillaume n'a pas renoncé au navigateur. GDExtension Rust : réserve pour un point chaud (A*, sim
  de 1 000 agents) ; il double la chaîne d'outils et complique web et consoles, donc pas au départ.
- **Couverture des features** : TileMapLayer (iso), dessin de terrain, NavigationServer2D/AStarGrid2D, Control pour l'UI
  dense, `FileAccess`/`ResourceSaver` ou JSON pour les sauvegardes, `AnimationPlayer`. Ma mesure : 200 000 déplacements de
  vecteurs en 10 ms en GDScript, soit de la marge pour quelques centaines de golfeurs à quelques ticks par seconde ; une
  simulation plus profonde pourra exiger une extension native, à mesurer.
- **Suite** : passage 3D dans le même moteur, modding par packs de ressources ou scripts chargés, réseau intégré
  (ENet/WebSocket/WebRTC).
- **Faiblesse** : pas de console sans tiers payant ; rendu 3D haut de gamme en retrait d'Unreal (sans importance ici).

### 3.2 Unity 6
- **Coût** : gratuit sous 200 000 $ (revenus **et** financement) ; au-delà 2 310 $/an **par siège** ; hausse de 5 % en
  janvier 2026 ; Havok retiré de Pro en 6.3. Historique de 2023 (Runtime Fee annoncé puis annulé en 2024) : le risque n'est
  pas le prix actuel mais la capacité de l'éditeur à le changer.
- **Agents** : C# est le langage le mieux connu des modèles de toute cette liste. Mais : scènes et prefabs en YAML géants
  avec `fileID`/GUID dans des `.meta`, fragiles à éditer à la main ; beaucoup de configuration ne se fait raisonnablement que
  dans l'éditeur ; le batch mode et `-runTests` existent, mais la CI exige une licence activée (GameCI + secrets), et
  l'éditeur pèse plusieurs Go. Dans un conteneur d'agent, la boucle « modifier → vérifier » est la plus lente des options.
- **Plateformes** : la meilleure couverture console « officielle », web correct.
- **Verdict** : bon moteur, mauvais ajustement à un projet piloté par agents sans éditeur, et risque commercial non nul.

### 3.3 Unreal Engine 5
- 5 % au-delà de 1 M$, éditeur et builds très lourds, Blueprints binaires (`.uasset`) non diffables, C++ à compilation
  longue. Surdimensionné pour un tycoon isométrique. **Écarté.**

### 3.4 Moteur maison sur OpenSGolf / `sg-core` (Rust)
- **Atout majeur** : 45 k lignes de règles déjà décodées et testées (249 tests), économie, golfeurs, humeurs, sim-stories,
  terrain, sauvegarde, scripting rhai, et une version jouable sur 5 plateformes. Aucun autre point de départ ne s'en approche.
  OpenSGolf prouve aussi qu'un jeu complet sans éditeur, écrit par agents Claude, est faisable en Rust.
- **La GPL-2.0-or-later, concrètement, pour Golferoo** :
  - Lier ou copier du code d'OpenSGolf (y compris `sg-core` comme crate) fait de Golferoo une œuvre dérivée : tout binaire
    distribué doit l'être sous GPL, avec le code source complet. Vendre reste permis ; fermer le code ne l'est pas.
  - **Consoles** : les SDK et les couches de portage sont sous NDA ; on ne peut pas publier leur code, donc on ne peut pas
    respecter la GPL. En pratique : pas de console. Les boutiques mobiles ajoutent des frictions (conditions d'usage
    jugées incompatibles avec la GPL v2 par la FSF dans le cas de l'App Store).
  - Le set graphique `opensgolf-free` est aussi GPL : le reprendre impose la même contrainte aux assets.
  - **Ce qui reste libre de réutilisation** : les *faits* (chiffres, tables, ordres, formules) ne sont pas protégeables par
    le droit d'auteur. On peut lire `docs/DECODE_*.md`, `GAMELOGIC.md`, `EXE_COSTS.md` comme on lirait un guide et
    réécrire le code soi-même. Mais un agent qui lit un fichier `.rs` puis « le traduit » en GDScript produit vraisemblablement
    une œuvre dérivée. Règle à fixer : les agents de Golferoo lisent les docs d'OpenSGolf, pas son code.
  - **Si Guillaume est `crspytopgn`** (auteur unique visible), il peut publier `sg-core` sous une seconde licence (MIT ou
    propriétaire) pour Golferoo, et tout ce qui précède tombe. C'est la question la plus rentable du dossier.
- **Coûts cachés** : tout ce qu'un moteur donne gratuitement est à faire et à maintenir par agents : UI (l'immédiat de
  miniquad), polices, texte, accessibilité, gestion des entrées, portages, 3D plus tard. miniquad/OpenGL n'a pas de chemin
  console ; un passage 3D signifie changer de rendu. La dette se paie à chaque nouvelle feature.
- **Agents** : le compilateur Rust est le meilleur relecteur de code d'agent de cette liste (types, emprunts, exhaustivité),
  `cargo test` headless est trivial. Coût : compilations longues, le contexte consommé par des erreurs de borrow-checker.

### 3.5 Bevy (0.19 / 0.20)
- MIT, gratuit, ECS adapté à des centaines d'agents, headless facile (essai ci-dessus).
- **Risque principal pour des agents** : 20 versions mineures en six ans, ruptures d'API à chaque version (tous les trois à
  quatre mois, la dernière le 2026-10-08), et des modèles entraînés sur un mélange de versions : l'erreur typique est du
  code d'une version antérieure qui ne compile plus. Mon essai a aussi montré l'exigence du rustc le plus récent. Pas
  d'éditeur ni de 1.0 annoncés. UI dense de gestion : `bevy_ui` reste le point faible pour un tycoon. Consoles : rien
  d'officiel. **Second rang derrière un Rust « maison » utilisant `sg-core`**, sans son avantage de réutilisation.

### 3.6 Stack web (TypeScript + Pixi.js, ou Three/Babylon)
- MIT, coût nul, TypeScript est le langage le mieux maîtrisé par les modèles avec C#, tests headless (Vitest) et CI
  triviales, UI dense en HTML/CSS, partage de parcours naturel. Pour une boucle d'agents, c'est la plus rapide.
- Contre : desktop par Electron/Tauri (poids, Steam acceptable), mobile par WebView, **consoles quasi impossibles**,
  simulation lourde limitée par le thread principal (Web Workers possibles), et passage 3D = changer de bibliothèque.
- Option sérieuse **si** Guillaume vise web + PC et jamais console.

### 3.7 Autres pistes
- **Rust sim (`sg-core` relicencié ou réécrit) + Godot en façade via GDExtension** : le meilleur des deux mondes sur le
  papier, mais deux langages, godot-rust encore en 0.x, web et console plus compliqués. À garder comme évolution du choix 1,
  pas comme départ.
- **Defold** (gratuit, consoles via la fondation) et **MonoGame/FNA** (C#, consoles prouvées : Stardew Valley) : crédibles,
  mais moindre connaissance des modèles et moins de briques prêtes pour une UI dense. Je ne les ai pas creusés.

### 3.8 Tableau de notation (1 à 5, 5 = meilleur ; pondération de l'angle C entre parenthèses)

| Critère (poids) | Godot GDScript | Unity | Unreal | Rust maison / sg-core | Bevy | Web TS |
|---|---|---|---|---|---|---|
| 1. Couverture features SimGolf (×1) | 4 | 5 | 4 | 5 (déjà fait) | 3 | 4 |
| 2. Anticipation (3D, modding, réseau) (×1) | 4 | 5 | 4 | 2 | 3 | 3 |
| 3. Réutilisation OpenSGolf et licence (×1,5) | 3 (savoir seul) | 3 (savoir seul) | 3 | 5 si relicence, 2 sinon | 3 (savoir) | 3 (savoir) |
| 4. Productivité agents IA (×2) | 4 | 2 | 1 | 4 | 2 | 5 |
| 5. Plateformes, coûts, pérennité (×2) | 4 | 3 | 3 | 2 (GPL : pas de console) | 2 | 2 |
| 6. Perf, outillage art, coût assets (×1) | 4 | 5 | 4 | 3 | 3 | 3 |
| **Total pondéré (sur 42,5)** | **32,5** | **29,5** | **24,5** | **29,5 / 25** | **21,5** | **28,5** |

Lecture : Godot gagne sans dominer nulle part, parce qu'il n'a aucune note éliminatoire. Avec relicence, le Rust maison
rejoint Unity à la deuxième place ; je le préfère à Unity comme second choix, car ses faiblesses (consoles, 3D) ne pèsent
que si Guillaume vise ces cibles, alors que celles d'Unity (licence activée, YAML, éditeur lourd) pèsent sur chaque PR.

## 4. Architecture proposée (Godot 4.7, GDScript)

```
golferoo/
  project.godot                    # warnings GDScript en erreur (typage strict)
  sim/            # COUCHE 1 : simulation pure. RefCounted uniquement, aucun Node, aucun accès écran ni audio.
    world.gd        # grille de tuiles, terrains, relief (PackedInt32Array / PackedFloat32Array)
    golfer.gd, needs.gd, mood.gd, stories.gd, economy.gd, staff.gd, tournament.gd
    pathing.gd      # AStarGrid2D (objet serveur, utilisable sans scène) ou A* maison
    rng.gd          # une RNG à graine par sous-système : rejouabilité et tests déterministes
    tick.gd         # SimClock.step(dt) : la seule entrée du temps
  data/           # COUCHE 0 : chiffres en JSON/TOML texte (coûts, seuils, tables), un commentaire de source par valeur
  save/           # sérialisation versionnée du World (JSON, puis binaire compressé si besoin)
  view/           # COUCHE 2 : présentation. Lit l'état de sim, émet des commandes ; jamais l'inverse.
    iso_map.tscn, golfer_sprite.tscn, camera.gd
  ui/             # COUCHE 3 : panneaux Control + Theme ; une scène .tscn par panneau, écrite en texte
  mods/           # packs de données et scripts (plus tard)
  tests/          # GdUnit4 (ou GUT) headless : sim/ et save/ en priorité, golden tests de parties simulées
```

- **Règle d'or** : `sim/` n'importe rien de `view/` ni de `ui/`. Un test de lint (grep en CI) le vérifie.
- **Commandes** : l'UI ne modifie pas l'état directement ; elle pousse `Command` (peindre, acheter, embaucher) que la sim
  applique au tick. Cela donne gratuitement l'annuler, la relecture de parties, et la base d'un futur multijoueur.
- **Tests headless** : `timeout 300 godot --headless --path . -s addons/gdUnit4/bin/GdUnitCmdTool.gd -a tests/`
  (à confirmer au prototype) ; en plus, un « soak test » : 10 ans de jeu simulés à graine fixe, comparés à une empreinte.
  Vérification visuelle ponctuelle : `godot --write-movie` ou capture d'écran en CI avec un rendu logiciel, à tester.
- **Ce qui vient d'OpenSGolf** : **du savoir et des données factuelles seulement**, sauf relicence. Concrètement : un agent
  « extracteur » lit `docs/DECODE_*.md`, `GAMELOGIC.md`, `EXE_COSTS.md`, `PARITY.md` et produit `data/*.json` avec la
  référence de la note source ; les codeurs écrivent `sim/` depuis la spec et ces données, sans ouvrir `crates/`.
  Si Guillaume relicencie `sg-core`, on peut aller plus loin : compiler `sg-core` en GDExtension (godot-rust) et ne
  réécrire que la présentation.
- **Ce qui ne vient jamais d'EA** : aucun asset, son, texte ni nom du zip `sim_golf.zip` ; ni « SimGolf » ni « Sid Meier »
  dans le nom ou le marketing (marques). Les mécaniques de jeu ne sont pas protégées ; l'expression (art, textes des
  pensées, noms des sim-stories, voix) l'est. Le recensement sert de cahier des charges, pas de source d'assets.
- **Skill de test du dépôt** : impose `timeout`, `--check-only` sur les scripts touchés, filtre `SCRIPT ERROR|FAIL`.

## 5. Risques principaux et vérification la moins chère

| # | Risque | Probabilité / impact | Vérification qui tranche | Coût |
|---|---|---|---|---|
| 1 | Reprendre par erreur du code GPL (copie ou traduction par un agent) et contaminer Golferoo | moyenne / très élevé | Demander à Guillaume s'il est `crspytopgn` ; sinon règle écrite dans `CLAUDE.md` + hook qui refuse la lecture de `opensgolf/crates/` par les codeurs | 5 min |
| 2 | GDScript trop lent pour une simulation profonde (1 000+ golfeurs, A* fréquents) | moyenne / moyen | Prototype d'un jour : grille 256×256, 1 000 golfeurs, A* sur AStarGrid2D, mesurer ms/tick en headless | 1 jour d'agent |
| 3 | Agents bloqués par la boucle Godot (process qui ne quitte pas, erreurs Godot 3) | élevée sans parade / moyen | Installer GdUnit4, écrire le skill de test avec `timeout`, faire écrire 3 panneaux UI à un codeur Sonnet et compter les retouches | ½ jour |
| 4 | UI dense de gestion pénible à écrire en `.tscn` sans éditeur | moyenne / moyen | Faire générer par un agent le panneau « golfeur » (fiche, besoins, pensées) en texte, avec capture d'écran headless | ½ jour |
| 5 | Consoles hors de portée (coût prestataire, certification) | faible si non visé / élevé si visé | Guillaume dit si les consoles sont un objectif ; si oui, demander un devis W4 / Lone Wolf avant d'écrire du code | 1 courriel |
| 6 | Fin de vie ou changement de cap de Godot | faible / moyen | Aucune action : MIT + fork possible ; suivre une version LTS de fait (une mineure par an) | 0 |
| 7 | Contenu EA dans le dépôt (asset, texte de pensée recopié du jeu) | moyenne / élevé | Hook ou test CI qui cherche des chaînes du jeu original (liste extraite une fois) dans `data/` et `ui/` | 2 h |
| 8 | Coût de production des assets (pas d'artiste) | élevée / élevé | Essai : refaire un arbre et un golfeur en art généré par code (approche du set libre d'OpenSGolf, réécrite) ou commander un pack, comparer | 1 jour |

## 6. Questions que seul Guillaume peut trancher

1. **Es-tu l'auteur (`crspytopgn`) et seul détenteur des droits d'OpenSGolf ?** Si oui, accepterais-tu de publier
   `sg-core` sous une licence permissive ou en double licence pour Golferoo ? Cette réponse change le second choix en
   candidat sérieux au premier.
2. **Open source ou commercial fermé ?** Golferoo sous GPL est compatible avec la vente sur Steam/itch, pas avec les
   consoles ni avec un code fermé.
3. **Plateformes cibles réelles** : PC/Mac/Steam Deck seulement ? Web (exclut Godot C#) ? Mobile ? Consoles (prestataire
   payant, NDA, kits de dev à acheter) ?
4. **Budget** : 0 € strict, ou un budget pour un prestataire console, un artiste, des licences d'assets, la signature de
   code (Apple 99 $/an, certificat Windows) ?
5. **2D isométrique fidèle d'abord, ou 3D dès le départ ?** Godot couvre les deux ; un moteur maison sur miniquad non.
6. **Rapport à l'original** : remake reconnaissable (risque de marques et de « look and feel ») ou jeu nouveau inspiré ?
   Cela fixe ce qu'on peut garder des noms, de l'UI et du ton.
7. **Tolérance au risque de rythme** : préfères-tu une option plus lente au départ mais sans dette (Godot), ou repartir de
   ce qui marche déjà (OpenSGolf) quitte à porter sa dette et sa licence ?
