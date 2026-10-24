# Critique C : contre-expertise des analyses A et B

Analyste C, 2026-10-24, ronde 2. Les trois analyses concluent à Godot. Restent deux désaccords : le langage de la
simulation (A : Rust en GDExtension, B : bibliothèque C# pure testée par `dotnet test`, C : GDScript typé) et la 2D iso
contre la 3D dès le départ. Fait établi par le coordinateur : Guillaume a pour login GitHub KingMoule, pas `crspytopgn`.
**La relicence d'OpenSGolf n'est donc pas acquise**, ce qui fait tomber mon second choix.

## 1. Mesure pour départager les langages (≈ 10 minutes, dans ce conteneur)

J'ai écrit le même banc dans les trois langages : grille 128 × 128 avec 15 % d'obstacles, 40 destinations, un champ de
flux BFS par destination (la conception que propose A au §5), puis 500 golfeurs qui suivent leur champ pendant 1 000 ticks.
Machine : 4 cœurs. Deux passes par langage ; les cartes diffèrent un peu car les générateurs aléatoires ne sont pas les mêmes.

| Langage | Construire 1 champ BFS | 500 golfeurs, 1 tick | Commande |
|---|---|---|---|
| GDScript (Godot 4.7.2, typé) | 4,9 ms | 0,22 à 0,24 ms | `timeout 120 godot --headless --script res://tests/bench_path.gd` |
| C# (.NET 8, Release, 2e passe) | 0,31 ms | 0,011 ms | `dotnet run -c Release` |
| Rust (`rustc -O`) | 0,17 à 0,20 ms | 0,007 à 0,008 ms | `rustc -O main.rs && ./bench` |
| GDScript + `AStarGrid2D` natif | 500 chemins A* en 152 à 161 ms, soit 0,3 ms par chemin (longueur moyenne 92) | | même script |

Lecture :
- Sur ce type de boucle, **GDScript est 15 à 30 fois plus lent que C#**, et C# reste dans un facteur 1,5 à 2 de Rust.
- En valeur absolue, GDScript **tient** le remake. Marcher coûte 0,2 ms par tick, dans un budget de 87 ms. Le risque est
  ailleurs : recalculer les 40 champs après une édition de terrain prend environ 200 ms en GDScript, soit un à-coup
  visible, contre 12 ms en C#. Même risque pour la planification de coups par échantillonnage en vitesse ×16 (risque R3
  de B), que je n'ai pas mesurée.
- Installer .NET : le CDN `builds.dotnet.microsoft.com` est **refusé par le proxy** (403), mais `apt-get install
  dotnet-sdk-8.0` a fonctionné en 25 s. Premier essai de compilation : erreur `CS0246 List<>` parce que les
  `ImplicitUsings` manquaient, corrigée en une ligne. C'est une erreur typique d'agent, attrapée à la compilation.
- Je n'ai pas testé Godot .NET en headless (binaire « mono » distinct, faute de temps). Ce n'est pas bloquant dans
  l'architecture de B, puisque la simulation se teste sans Godot.

## 2. Analyse A (Godot + simulation Rust en GDExtension)

**Objection la plus forte : A paie deux chaînes d'outils pour un bénéfice qui a disparu.** Rust se justifiait surtout
par la réutilisation de `sg-core`. Or A conclut lui-même « rien en code » si Golferoo n'est pas sous GPL (§5), et la
relicence n'est pas acquise. Il reste donc la vitesse brute, mais mon banc montre que C# fait presque aussi bien
(0,011 contre 0,007 ms par tick). Le coût, lui, reste entier :
- gdext est en 0.5.5 (2026-10-13). Son export web est « still a work-in-progress and is not yet well tested » :
  vérifié sur [docs.rs/godot](https://docs.rs/godot) le 2026-10-24.
- Ce sont deux langages, deux systèmes de build et une couche de liaison de 1 000 à 2 500 lignes (estimation de A) sur
  une API que les modèles connaissent mal. A le reconnaît.
- Rust sur console via un prestataire Godot exige des cibles de compilation spécifiques sous NDA. Aucune des trois
  analyses ne l'a vérifié.

**Hypothèse critique** : la rigueur du compilateur Rust vaut une liaison expérimentale et une seconde chaîne d'outils
pour des agents. C'est vrai par rapport à GDScript, pas par rapport à C#, qui a un typage statique, un compilateur
strict et `dotnet test`.

**Erreurs factuelles vérifiées** :
- « Godot 4.6 sorti en février 2026 » : la page officielle donne le **26 janvier 2026**
  ([godotengine.org/blog/release](https://godotengine.org/blog/release)). A l'avait signalé comme non vérifié. Erreur mineure.
- §3.4 : « il y a des contributeurs : une relicence demande l'accord de chacun ». Rien ne l'étaye. Le clone est
  superficiel et `git shortlog` ne montre qu'un auteur, `crspytopgn`. A le note pourtant lui-même au §2. Le nombre de
  détenteurs de droits reste inconnu, ni établi ni exclu.
- §4 : la simulation en GDScript serait « trop lente sans cache agressif ». C'est en partie démenti : avec les champs
  de flux que A propose lui-même, 500 golfeurs coûtent 0,22 ms par tick en GDScript. La vraie limite tient au recalcul
  des champs et à la planification des coups, pas à la marche.

**Ce que A a vu et que j'avais manqué** : l'état de `sg-core` comme fondation. L'orchestration de la simulation est dans
`simgolf/src/app.rs`, mêlée au rendu. La règle dépend de la position à l'écran (`sx == -1` dans `golfer.rs`). Il y a un
état global (`mapsize::GLOBAL`, `data::CURRENT`), une table fixe de 152 golfeurs, et seulement 14 tests pour 4 214 lignes
de `golfer.rs`. A a aussi mesuré la recherche de chemin d'OpenSGolf (0,21 à 1,39 ms par appel). J'avais fait de
« Rust sur `sg-core` » mon second choix. C'était trop optimiste, même avec une relicence : `sg-core` est un code à
porter, pas une base à étendre. Je le concède entièrement.

## 3. Analyse B (Godot + bibliothèque C# pure, 3D dès le départ)

**Objection la plus forte : la 3D dès le départ est un pari sur le budget art et sur la vérification visuelle, présenté
comme une évidence.** B affirme qu'« en 2D iso, chaque objet doit être dessiné en 4 orientations × états × saisons ×
thèmes ». C'est faux en pratique : OpenSGolf rend déjà ses sprites isométriques depuis des modèles 3D générés en code
(`packs/opensgolf-free/README.md` : « sprite sheets … rendered offline by `tools/freeset_art/build.py` from 3D models
made in code »). Faire de la 2D iso n'oblige donc pas à dessiner à la main. En revanche, la 3D temps réel ajoute ce que
des agents vérifient le plus mal sans yeux humains : éclairage, caméra libre, squelettes et retargeting, shader de
terrain, LOD. Aucun test headless ne dit qu'une scène 3D est « lisible et jolie ». Enfin, B écrit que la 3D low-poly
coûte « moins cher en art qu'un set iso à 4 rotations ». C'est une croyance (Parkitect), pas une mesure. Son propre
risque R1 est le prototype qui doit trancher, et il passe avant la décision, pas après.

**Hypothèse critique** : le produit est un tycoon « à la Two Point » destiné à Steam, et le web ne compte pas. Si
Guillaume veut le web (comme OpenSGolf, qui tourne en navigateur et sur iPhone par ce biais), le C# dans Godot est exclu
aujourd'hui. B le dit (R5), mais en fait une question secondaire alors qu'elle conditionne son choix de langage.

**Erreurs factuelles vérifiées (mineures)** :
- « Le binaire fait environ 100 Mo » : il fait 146 414 384 octets décompressé (78 Mo zippé) pour Godot 4.7.2 Linux,
  mesuré ici. La version .NET est encore plus lourde (je crois, non mesuré).
- « Une API qui casse tous les 4 à 6 mois » (Bevy) : d'après [bevy.org/news](https://bevy.org/news/), 0.17 le
  2025-09-30, 0.18 le 2026-01-13, 0.19 le 2026-06-19, 0.20 le 2026-10-08, soit **3,5 à 5 mois**. Cela renforce son
  argument. J'ajoute qu'en pratique, Bevy 0.20 a refusé de compiler ici avec rustc 1.97.0 (il exige 1.97.1).

**Ce que B a vu et que j'avais manqué** :
- **Le langage de la simulation est un choix séparé de celui du moteur, et C# est le meilleur pour des agents.** C'est
  le langage que les modèles connaissent le mieux après TypeScript. Il a un typage statique et une compilation qui
  attrape les erreurs, `dotnet test` se lance en secondes sans Godot, et il va 15 à 30 fois plus vite que GDScript
  (mesuré ci-dessus). Surtout, il élimine le piège que j'avais trouvé moi-même : avec une erreur de script,
  `godot --headless --script` **ne se termine jamais**. Une simulation testée par `dotnet test` ne passe plus par cette
  boucle fragile. Je n'avais pas tiré cette conséquence de mon propre essai.
- La couverture du recensement par couche (§3.3) : environ deux tiers de la parité sont de la simulation pure, donc
  le langage de la simulation pèse plus que celui de la vue.
- Les fixtures produites localement depuis OpenSGolf, sans jamais lier son code : une bonne idée de parité sans GPL,
  sous réserve d'avis juridique.

## 4. Ma recommandation change-t-elle ? Oui, sur le langage ; non, sur le moteur.

**Nouvelle position : Godot 4.7, simulation dans une bibliothèque C# pure (sans référence à Godot) testée par
`dotnet test`, vue et UI en C# et GDScript dans Godot.** Je me range au langage de B, pour trois raisons :
1. **Mesurée** : 15 à 30 fois GDScript sur la boucle qui grossira (champs de flux, planification de coups). Cela donne
   de la marge pour « simulation plus profonde » sans extension native.
2. **Pour des agents** : une boucle compiler → tester en secondes, sans Godot et sans le piège du process qui ne quitte
   pas. C# est un langage que les modèles écrivent bien, et l'erreur typique (un `using` manquant) est attrapée à la
   compilation.
3. **Pour les coûts et les risques** : aucune liaison expérimentale (contre A) et une seule chaîne d'outils, gratuite
   (MIT et .NET sous licence MIT).

Ce que je maintiens contre B : **ne pas décider la 3D dans cet ADR.** La simulation de B est indépendante de la vue, donc
la décision peut attendre le prototype R1 (un à deux jours), avec un critère explicite : un agent produit un trou lisible
en 3D low-poly avec caméra iso, et Guillaume le compare à des sprites iso rendus depuis les mêmes modèles. Je penche vers
la 3D à caméra iso fixe par défaut, car elle mène à la vue libre sans réécriture. Mais c'est un penchant, pas une mesure.

Conditions qui me feraient revenir en arrière :
- **Le web est une cible indispensable** : simulation en GDScript typé (ma position initiale), avec un lanceur de tests
  sous `timeout` et des champs de flux étalés sur plusieurs ticks.
- **Golferoo accepte la GPL** : A redevient pertinent (Rust + gdext pour porter `sg-core`), sous réserve du prototype
  d'export web.
- **Le prestataire console ne prend pas en charge le C#** (à demander à W4 Games ou à Lone Wolf) et les consoles sont
  prioritaires : GDScript, ou Unity en dernier recours.

**Nouveau second choix** : Godot en GDScript typé partout. Il remplace « Rust sur `sg-core` », que l'absence de
relicence et les constats de A écartent.

Fichiers de mesure : `/tmp/claude-0/-home-claude-golferoo/887068cd-4948-56e2-b8ad-47d016ab30bf/scratchpad/godot/proj/tests/bench_path.gd`,
`/tmp/claude-0/-home-claude-golferoo/887068cd-4948-56e2-b8ad-47d016ab30bf/scratchpad/csbench/Program.cs`,
`/tmp/claude-0/-home-claude-golferoo/887068cd-4948-56e2-b8ad-47d016ab30bf/scratchpad/rsbench/main.rs`.
