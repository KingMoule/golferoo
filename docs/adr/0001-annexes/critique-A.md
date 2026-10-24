# Contre-expertise A : critique des analyses B et C

Analyste A, 2026-10-24, ronde 2. Fait donné par l'orchestrateur : le login GitHub de Guillaume est **KingMoule**, pas `crspytopgn`. Mon analyse le disait déjà (§2, auteur du dépôt). Il fait tomber une condition de C (voir plus bas).

## 0. Une mesure neuve qui tranche le débat sur le langage

Les trois analyses parlent de performance sans mesurer la partie chère de la simulation. Les 10,4 ms de C pour 2 × 500 agents × 200 ticks portent sur des agents qui avancent en ligne droite vers un trou. Or le coût de SimGolf est ailleurs : dans la recherche de chemin par inondation (`golfer.rs::path_dir`, rejouée par golfeur et par pas) et dans la visée par échantillonnage (`planner.rs`, boucles `samples` × 8 directions, l. 1308-1330).

J'ai écrit **le même algorithme** dans les deux langages : inondation à 8 voisins pondérée, file circulaire, même grille pseudo-aléatoire, mêmes sommes de contrôle (2540 et 6460). Godot 4.7.2 headless et `rustc -O` tournent dans ce conteneur. Sources : le dossier `bench/` du scratchpad.

| Carte | Rust | GDScript typé (Godot 4.7.2) | Rapport |
|---|---|---|---|
| 50 × 50 | 0,138 ms | 8,07 ms | × 58 |
| 128 × 128 | 1,22 ms | **99,2 ms** | × 81 |

Une seule recherche de chemin sur une carte de 128 tuiles de côté coûte plus d'un tick de jeu (87 ms) en GDScript. Recalculer 40 champs de flux après une modification du terrain prend environ 4 s en GDScript, contre 50 ms en Rust. Le C# n'a pas été mesuré ; en JIT, je l'attends dans un facteur 1,5 à 3 de Rust, mais c'est une supposition.

## 1. Analyse B (Godot 4.7, simulation en C# pure, 3D dès le départ)

**Objection la plus forte : B confond « rendu 3D » et « terrain continu dans la simulation », et le second coûte la parité.**
- B demande une « heightmap continue (plus seulement des paliers de tuiles) » et un « navmesh si le terrain devient libre » (§3.1 points 2-3). Pourtant, son architecture garde des « surfaces (23 types) ».
- Les règles décodées de SimGolf sont **par tuile**, et pas seulement à l'affichage. `course.rs` porte une table par type de tuile (rebond, roulement, lie : `TILE_ROWS = 23`, `data.rs`), des hauteurs entières aux coins (51 × 51) et des murets entre tuiles de hauteurs différentes. Le vol de balle, la visée, les classes de trous et la recherche de chemin lisent cette grille.
- Passer en continu, c'est réécrire la balistique et la visée sans référence, donc perdre la seule source de chiffres (OpenSGolf) au moment où on en a le plus besoin.
- Rien n'oblige ce couplage : un rendu 3D avec une caméra iso par défaut peut afficher une simulation à tuiles et hauteurs aux coins (mon §5, point 7). B a raison sur la vue, pas sur le modèle.

**Hypothèse critique de B** : « la 3D low-poly coûte moins cher en art qu'un set iso à 4 rotations » (Parkitect). Ce n'est démontré nulle part dans B. C'est vrai si l'on dessine des sprites à la main, faux si l'on pré-rend des sprites iso depuis des modèles 3D (le même modèle sert alors aux deux). L'argument de l'art ne départage donc pas « 3D dès le départ » et « 2D iso pré-rendue ». Ce qui départage, c'est la caméra libre et le zoom jusqu'au golfeur : là, B a raison.

**Erreurs factuelles** (vérifiées) :
- « WGSL remplacé par WESL » en Bevy 0.20 : approximatif. Les notes de 0.20 (bevy.org, 2026-10-08) décrivent WESL comme « a standardized extension of WGSL ». Seul l'ancien dialecte maison de Bevy disparaît ; un WGSL simple fonctionne toujours. Les changements de syntaxe BSN, eux, sont exacts.
- `flight.rs` « constantes marquées inconnu (yards par unité) » : exact (l. 2-3). J'ajoute que le pas de 87 ms y est lui aussi marqué « placeholder ».
- « sg-core 44 797 lignes » contre 45 125 chez moi : écart sans portée.

**Ce que B a vu et que j'avais manqué** :
- Le **produit** : caméra libre, suivi de balle, zoom jusqu'au golfeur, références du genre (Two Point, Planet Coaster, Parkitect). Mon analyse traitait la 3D comme « plus tard, seulement dans la coque », sans peser ce qu'elle apporte dès la parité (lisibilité, reproches de 2002 au §21 du recensement).
- Le mode « Classique » comme **jeu de données** sur un core moderne, plutôt que l'inverse. C'est meilleur que ma formulation « porter puis moderniser ».
- Le C# pur testé par `dotnet test` donne **la même testabilité headless** que ma crate Rust, avec un langage que les modèles connaissent mieux que l'API gdext 0.5. Je le concède : mon tableau sous-notait cette variante (24, à égalité avec Bevy).

## 2. Analyse C (Godot 4.7, GDScript typé, Rust en réserve)

**Objection la plus forte : la simulation en GDScript ne tient pas la charge réelle de SimGolf.** Preuve au §0 : × 58 à × 81 contre Rust sur l'algorithme même de la recherche de chemin du jeu, et 99 ms pour une seule recherche sur 128 × 128. Le risque n° 2 de C (« GDScript trop lent ») n'est pas de probabilité « moyenne » : il se réalise dès les cartes agrandies, qu'OpenSGolf propose déjà (74 à 128 tuiles). La parade de C, `AStarGrid2D`, est native et rapide. Mais les règles de marche de SimGolf (préférer les chemins, céder le passage, pente, coût du personnel, `path_rule` qui alterne selon le tick) devront être projetées dans ses poids. Et la visée (échantillonnage avec simulation de vol par candidat) n'a aucun équivalent natif. Il faudrait donc écrire la « porte de sortie Rust » de C dès les premiers chantiers de golfeurs. Autant partir avec.

**Hypothèse critique de C** : « le web est une cible, donc pas de C#, donc GDScript ». C'est une préférence de plateforme que Guillaume n'a pas exprimée. Et la contrainte vaut aussi contre Rust (gdext web expérimental) : le web pèse sur le langage de la simulation, il ne désigne pas GDScript à lui seul.

**Erreurs factuelles** (vérifiées) :
- Second choix, condition (a), « si Guillaume est l'auteur d'OpenSGolf (`crspytopgn`) » : **fausse**. Guillaume est KingMoule. Le second choix de C ne tient plus que par la condition (b), Golferoo sous GPL.
- `sg-core` aurait déjà la « sauvegarde » (§3.4) : inexact. La structure `SaveGame` et la boucle qui orchestre les ticks vivent dans `crates/simgolf/src/app.rs` (l. 322 et 2579), mêlées à l'état GPU. `sg-core` ne fournit que des types sérialisables. Cela compte pour C, qui envisage un « moteur maison sur `sg-core` » : il manque le monde headless.
- « ≈ 60 modules » : 67 `pub mod` dans `lib.rs`. Sans portée.

**Ce que C a vu et que j'avais manqué** :
- Godot ne quitte pas sur une erreur d'analyse de script en mode `--script` (exit 124 sous `timeout`). C'est un piège concret pour les agents, et je n'avais pas testé Godot du tout. À inscrire dans le skill de test, quel que soit le langage de la simulation.
- Bevy 0.20 (2026-10-08) exige rustc 1.97.1, alors que ce conteneur a 1.97.0. **Mon analyse donnait 0.19 comme version courante : c'est une erreur.** Elle renforce l'objection de churn que je portais moi-même contre Bevy.
- Godot se télécharge en 2 s, sans compte ; Unity et Unreal ne s'installent pas sans activation. C'est un argument C4 mesuré, que je n'avais qu'affirmé.
- La garde contre les **contenus d'EA** (chaînes du jeu recherchées en CI) et contre les marques. Je ne l'avais que mentionnée.
- Le rôle d'« agent extracteur » : docs d'OpenSGolf, puis `data/*.json` sourcés, puis codeurs qui n'ouvrent jamais `crates/`. C'est plus opérationnel que ma « salle blanche ».

## 3. Mes propres erreurs, et ce que je concède

- Bevy 0.20 est la version courante, pas 0.19.
- Godot 4.6 est sorti le **26 janvier 2026** (godotengine.org/blog/release, lu ce jour), pas en février. Ma source était une date de dépêche.
- Mon tableau sous-notait « Godot + C# » : sur la testabilité headless, cette variante égale ma recommandation.

## 4. Ma recommandation change-t-elle ?

**Sur le fond, non. Sur la formulation, oui.**
- **Maintenu** : la simulation est une bibliothèque compilée, pure, sans Godot, et testée hors moteur. **GDScript est exclu pour la simulation** (mesure du §0). Il reste le bon langage pour la colle de l'UI, comme le propose C.
- **Changé** : entre **Rust (gdext)** et **C# (.NET)**, ce n'est plus un choix d'architecture mais un choix réglé par deux réponses de Guillaume :
  1. **Golferoo sous GPL** (ou exception obtenue de `crspytopgn`, qui n'est pas Guillaume) : **Rust**, sans discussion. On lie `sg-core`, on le porte module par module, et on garde ses tests comme oracle.
  2. **Golferoo fermé ou commercial** : la réutilisation de code tombe à zéro des deux côtés (salle blanche). L'avantage de Rust se réduit à des tests différentiels contre `sg-core` dans un outil de CI non distribué (B fait la même chose avec des fixtures JSON), et à une marge de performance que C# a probablement aussi. **C# devient alors mon premier choix**, pour la connaissance des modèles et l'outillage `dotnet test`. Une réserve : si le web est une cible, le C# Godot ne s'exporte pas vers le web en 4.7 (cinevva, mis à jour le 2026-09-06), et gdext n'y est qu'expérimental. Le web est donc risqué dans les deux cas, un peu moins avec Rust.
- **Sur la 3D** : je me rapproche de B pour la **vue** (3D stylisée avec caméra iso par défaut, à arbitrer avec le prototype d'art de B, R1). Je m'oppose à B sur le **modèle** : la simulation reste à tuiles typées avec hauteurs aux coins tant que la parité n'est pas atteinte.

La vérification la moins chère qui départage C# et Rust : le prototype d'un jour que proposent B (R3) et moi (§6). La recherche de chemin du §0 et une visée de 64 échantillons pour 300 golfeurs, codées en C# (`dotnet test`) et en Rust (`cargo test`) par le même codeur Sonnet. On compare le temps, le nombre de corrections ratées et le temps de CI.

## 5. Synthèse des trois positions après contre-expertise

| Question | A (moi, révisé) | B | C | Ce qui tranche |
|---|---|---|---|---|
| Moteur | Godot 4.7 | Godot 4.7 | Godot 4.7 | Accord |
| Langage de la simulation | Rust si GPL, sinon C# | C# | GDScript typé | GDScript exclu par la mesure du §0 ; Rust contre C# : décision de licence de Guillaume, puis prototype d'un jour |
| Vue | 3D stylisée possible dès le départ | 3D dès le départ | 2D iso d'abord | Prototype d'art de B (R1) et préférence de Guillaume |
| Modèle de terrain | Tuiles et hauteurs aux coins | Heightmap continue | Tuiles | Parité : tuiles, jusqu'à la sortie du mode Classique |
| OpenSGolf | Docs ; code seulement sous GPL | Docs et fixtures | Docs ; code si relicence | Guillaume n'est pas l'auteur : relicence = demande à `crspytopgn` |

## 6. Reproduire la mesure du §0

```
# scratchpad de la session A : bench/flood.rs, bench/gd/flood.gd, godot/Godot_v4.7.2-stable_linux.x86_64
rustc -O -o flood_rs flood.rs && ./flood_rs
timeout 60  godot --headless --path gd --import
timeout 120 godot --headless --path gd --script res://flood.gd
```
Le script GDScript est typé (`var c: int`, `PackedInt32Array`). Les mêmes graines et les mêmes sommes de contrôle garantissent que les deux versions font le même travail. 20 répétitions en GDScript, 200 en Rust.
