# Contre-expertise B : critique des analyses A et C

Analyste B, 2026-10-24, ronde 2. Je ne modifie pas `analyse-B.md` ; ce qui change dans ma position est en section 4.

## 0. Vérifications faites pour cette ronde

Toutes dans `scratchpad/benchB/`, sur la machine de la session (4 cœurs).

**Banc « planification de coup »** : le même noyau écrit trois fois, en GDScript typé (Godot 4.7.2 headless, le binaire téléchargé par C), en C# (.NET 10.0.113, Release) et en Rust (`--release`). Le noyau évalue 64 trajectoires candidates (boucle de vol entière façon `flight.rs`), lit une case de la grille et calcule un score. 20 000 coups, même résultat de contrôle (`acc=7860`) dans les trois langages :

| Langage | 20 000 coups | µs par coup |
|---|---|---|
| GDScript typé | 2 918 à 3 186 ms (2 essais) | 146 à 159 |
| C# .NET 10 | 47,3 ms | 2,4 |
| Rust | 58,9 ms | 2,9 |

Le vrai planificateur d'OpenSGolf fait bien plus : `planner.rs` l. 1256-1469, passes de 2 à 8 échantillons, coupure vers 250 essais (`sims + 3 * kept * samples > 250`), et chaque essai simule un vol complet avec rebond et roulement. Mon noyau est donc un **plancher** ; l'écart entre langages (×50 à ×65) est la donnée utile.

**Godot, code de sortie sur erreur de script** : une erreur de type dans un script **dépendant**, chargé par `load()` ou `preload()` depuis le lanceur, affiche `SCRIPT ERROR: Parse Error…` mais le processus sort avec **exit 0** (deux variantes testées). Une erreur dans le script principal donne exit 1. Ni l'un ni l'autre ne s'est bloqué chez moi. Le blocage vu par C dépend donc de la configuration ; le piège général, c'est **une CI verte malgré une erreur de compilation**. À l'inverse, `dotnet build` a refusé mon premier `Program.cs` (`using System` oublié) avec un code non nul.

**Outillage .NET dans le conteneur** : le téléchargement direct depuis Microsoft est refusé par le proxy (`builds.dotnet.microsoft.com`, 403). En revanche, `apt-get update && apt-get install dotnet-sdk-10.0` fonctionne (paquet Ubuntu, 10.0.113). C'est une friction réelle, mais qui se contourne.

**gdext** : docs.rs confirme la crate `godot` 0.5.5 du 13 octobre 2026, avec `experimental-wasm` « not yet well tested ». Le fait cité par A est exact.

**Auteur d'OpenSGolf** : le login GitHub de Guillaume est KingMoule, pas `crspytopgn` (fait transmis par l'orchestrateur). L'API GitHub du dépôt OpenSGolf n'est pas accessible depuis cette session, donc je ne peux pas compter ses contributeurs.

## 1. Analyse A (Godot + simulation Rust en GDExtension)

**Objection la plus forte : l'avantage de Rust n'existe que sous GPL, et A lui-même suppose le cas non-GPL par défaut.** En section 5, A écrit : « Si Golferoo n'est pas GPL (cas par défaut que je suppose) : rien en code ». Dans ce cas, Rust ne réutilise pas plus d'OpenSGolf que C# : les codeurs réécrivent depuis `docs/` dans les deux langages. Pourtant le tableau de A donne C3 = 4 à « Godot + Rust » et C3 = 3 à « Godot + C# », avec la justification « aucune réutilisation de code d'OpenSGolf ». C'est incohérent avec sa propre hypothèse. Une fois cet avantage retiré, il reste les coûts de Rust dans Godot :
- une couche de liaison de 1 000 à 2 500 lignes (estimation de A), qui n'existe pas en C#, puisque le C# est le langage natif de la couche Godot ;
- deux chaînes d'outils (cargo + Godot) au lieu d'une (dotnet + Godot .NET) ;
- une API gdext 0.x que les modèles connaissent mal (A le reconnaît) ;
- un web expérimental, dont A fait son risque n° 1 tout en recommandant l'option.

Le banc montre que C# et Rust jouent dans la même catégorie de vitesse (2,4 contre 2,9 µs par coup, sans optimisation particulière ni dans l'un ni dans l'autre) : la performance ne départage pas.

**Hypothèse critique** : que la GPL reste une vraie option. Or l'auteur n'est pas Guillaume. Relicencier exige l'accord de `crspytopgn` (et d'éventuels contributeurs, que je n'ai pas pu compter), et un Golferoo GPL ferme les consoles et le code fermé. Plus cette porte se ferme, plus le choix de Rust devient un coût sans contrepartie.

**Erreur factuelle** : rien de faux sur les faits que j'ai vérifiés (gdext 0.5.5, dates Unity, mesures OpenSGolf cohérentes avec les miennes : 249 `#[test]` dans la crate, 238 dans `src/`). Une affirmation n'est pas étayée : « il y a des contributeurs » (§3.4). Le clone est superficiel et ne montre qu'un auteur ; C a lu la même chose. Ce n'est pas faux, c'est invérifié, et ça pèse sur la faisabilité d'une relicence.

**Ce que A a vu et que j'avais manqué** :
- **la dette est dans l'orchestration, pas dans le moteur** : la boucle de simulation vit dans `simgolf/src/app.rs`, mêlée à l'état GPU ; le golfeur porte sa position écran (`sx == -1` change une règle) ; l'état est global (`mapsize::GLOBAL`, `data::CURRENT`). C'est la meilleure preuve que même `sg-core` n'est pas une fondation, ce que je n'avais affirmé que par la forme du code (unités de l'exe) ;
- **le bench réel d'OpenSGolf** (0,16 ms par tick en moyenne, mais seulement 12 golfeurs ; pointes à 16 ms) et le **pathfinding en O(n²) recalculé à chaque appel**, avec le remède des champs de flux par destination. Je le reprends tel quel ;
- **la faible couverture de tests de la simulation** (14 tests pour 4 214 lignes de `golfer.rs`) : les « fixtures » que je proposais de tirer d'OpenSGolf auront moins de valeur que je ne le pensais ; il faudra des bandes statistiques, comme le propose A ;
- les règles de conception (`World::step(cmds) -> events`, aucun état global, RNG par sous-système) valent pour n'importe quel langage.

## 2. Analyse C (Godot + GDScript typé)

**Objection la plus forte : GDScript est 50 à 65 fois plus lent que C# ou Rust sur le calcul central du jeu, et la mesure de C ne testait pas ce calcul.** C conclut « de la marge pour quelques centaines de golfeurs » à partir de 200 000 déplacements de vecteurs en 10 ms. C'est la boucle la plus légère du jeu. Le coût d'un tycoon de golf est ailleurs : la planification de coup (§9.6 du recensement, environ 250 essais de vol complets par coup dans `planner.rs`). Mon banc donne un plancher de 150 µs par coup en GDScript, pour un noyau environ 10 fois plus léger que le vrai. Extrapolation, à confirmer : de l'ordre de **1 à 2 ms par coup planifié** en GDScript, contre 0,03 ms en natif. Conséquences :
- en jeu à ×16, avec un coup planifié par tick comme l'exe, on brûle une large part d'un cœur ; ça passe encore ;
- les **outils** ne passent plus : le rapport de parcours d'OpenSGolf « simulates hundreds of rounds » (commentaire de son `Cargo.toml`), l'analyse de coup `/`, le golfeur fictif qui joue chaque trou à la construction, les tournois à 36 joueurs. 500 tours de 18 trous, c'est environ 36 000 coups, soit environ 1 minute en GDScript contre environ 1 seconde en C#. Une « simulation plus profonde » (critère 2 du brief) aggrave l'écart.
Le plan de C (« Rust pour un point chaud mesuré ») est donc sûr de servir, et sur le cœur même de la simulation, pas sur une périphérie. C'est le pire moment pour changer de langage.

**Hypothèse critique** : que le web soit une cible. C'est la seule raison de C pour écarter le C#. Si Guillaume répond « PC, Mac, Deck d'abord », l'argument tombe. Et le web resterait possible plus tard pour une démo, au pire par un core séparé.

**Erreurs factuelles** :
- le second choix de C repose sur la condition (a) « Guillaume est l'auteur d'OpenSGolf ». C'est **faux** : son login est KingMoule. Il ne reste que la condition (b), la GPL acceptée ;
- « Godot affiche bien l'erreur, mais le processus ne se termine jamais » : je ne le reproduis pas sur 4.7.2. Chez moi, une erreur dans un script dépendant donne **exit 0**, ce qui est plus grave pour la CI : l'échec passe inaperçu sans `grep 'SCRIPT ERROR'`. Le remède de C (`timeout`, `--check-only`) est nécessaire mais ne suffit pas ; il faut aussi échouer sur toute ligne `SCRIPT ERROR`. Un core compilé (C# ou Rust) n'a pas ce trou, parce que le compilateur échoue avant tout test.

**Ce que C a vu et que j'avais manqué** :
- **la boucle Godot réellement mesurée** dans ce conteneur : binaire de 78 Mo téléchargé en 2 s, import en 7 s, tests headless en moins de 10 s, aucun compte. Je l'avais affirmé, C l'a prouvé ;
- **les réglages de typage strict** (`untyped_declaration = error`) et le skill de test avec `timeout` : à reprendre même avec un core C#, pour le GDScript de colle d'UI ;
- **le risque de contamination par traduction** : un agent qui lit un `.rs` d'OpenSGolf et le « traduit » produit probablement une œuvre dérivée. D'où le hook qui refuse `opensgolf/crates/`. **Ce risque touche aussi ma proposition de fixtures tirées de `sg-core`** (R7 de mon analyse) : je la retire au profit de l'oracle par statistiques de A et des docs seules ;
- **marques et contenu EA** : pas de « SimGolf » dans le nom, contrôle CI des chaînes du jeu original ;
- **le coût de chaîne Rust** : Bevy 0.20 exige le rustc 1.97.1, sorti juste avant. Cela confirme ma réserve sur Bevy.

## 3. 2D iso contre 3D dès le départ : où je concède

A et C proposent « 2D iso maintenant, 3D plus tard dans le même moteur ». Mon objection tient : le même moteur ne rend pas le passage gratuit. Il faut refaire tout l'art (sprites contre modèles), la couche de vue, le picking, le rendu du relief et de l'eau. Ce que le moteur garde, c'est le core et l'UI, qui seraient gardés de toute façon dans les trois architectures.

Je concède deux points :
1. **Personne n'a mesuré le coût de l'art 3D pour une équipe sans artiste**, moi compris. Le golfeur animé en 3D (squelette, cycles de marche et de swing) est le poste le plus cher, et c'est celui que les agents savent le moins produire. C a raison de mettre le coût des assets en risque élevé.
2. Le compromis que j'aurais dû écrire nettement : **rendu 3D avec caméra orthographique iso et 4 angles au départ, low-poly**. Il ressemble à SimGolf, garde le pipeline 3D, et ne rend pas obligatoire la caméra libre. La décision ne porte donc pas sur le moteur mais sur la direction artistique. Elle revient à Guillaume, après le prototype R1 de mon analyse (un trou, des arbres, un golfeur), à faire **dans les deux styles** pour comparer le coût réel.

## 4. Ma recommandation change-t-elle ?

**Sur le fond, non : Godot 4.7, core de simulation en C# pur testé par `dotnet test`, Godot .NET pour la vue et l'UI.** Elle sort même renforcée :
- contre C : le banc montre que le GDScript ne tient pas le cœur de calcul du jeu à moyen terme, et que les erreurs de compilation GDScript peuvent laisser une CI verte ;
- contre A : sans GPL, Rust n'apporte plus de réutilisation, et il ajoute une couche de liaison et une deuxième chaîne d'outils pour une vitesse équivalente à C#. Le fait « Guillaume n'est pas `crspytopgn` » rend la GPL moins probable.

**Ce que j'intègre** :
- les règles de conception de A (aucun état global, `step(commands) -> events`, rien de l'écran dans les règles, champs de flux, golfeurs sans plafond) ;
- la salle blanche de C (docs seulement, hook sur `crates/`) : je retire mes fixtures tirées de `sg-core` ;
- en CI, l'échec sur `SCRIPT ERROR` et un `timeout` sur toute commande Godot ;
- dans `CLAUDE.md`, `apt-get install dotnet-sdk-10.0` comme installation (le script de Microsoft est bloqué par le proxy).

**Ce qui renverserait ma recommandation** :
- Guillaume obtient l'accord de `crspytopgn` ou accepte la GPL : alors A gagne (Rust et `sg-core` porté), et je m'y rallie ;
- le web devient une cible de premier rang : alors aucun core compilé n'est propre aujourd'hui (C# : pas d'export ; gdext : expérimental). La voie de C (tout GDScript) est la seule exportable, au prix de la vitesse. Il faudrait alors un core GDScript limité au jeu courant, et les outils lourds (rapport de parcours) bridés ou différés.

**Ma confiance** passe de « moyenne » à « moyenne-haute » sur le choix C# contre Rust et GDScript, toujours conditionnée aux plateformes cibles. Elle reste moyenne sur la 3D dès le départ, faute d'un essai d'art.
