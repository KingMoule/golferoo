# ADR 0001 : moteur de Golferoo

Statut : accepté (questions tranchées par Guillaume le 2026-10-24)
Date : 2026-10-24
Chantier : C00

## Contexte
Golferoo doit offrir au minimum les features de Sid Meier's SimGolf (2002), recensées dans `docs/recensement-sim-golf.md` : un jeu de gestion de parcours avec des centaines de golfeurs simulés, un terrain peignable avec relief, une UI de gestion dense, une économie et un pro jouable. Viendront ensuite la modernisation, plus de profondeur et de nouvelles boucles de jeu. Le code est écrit surtout par des agents Claude, et Guillaume fusionne.

Le projet [OpenSGolf](https://github.com/crspytopgn/opensgolf) réimplémente SimGolf en Rust (environ 105 000 lignes, v0.5.3). Il est sous **GPL-2.0-or-later**, et son auteur n'est pas Guillaume.

Trois analystes indépendants ont étudié la question, chacun sous un angle : architecture et réutilisation du code (A), produit et avenir du jeu (B), coûts, licences et développement par agents (C). Chacun a ensuite critiqué les deux autres. Tout est dans `docs/adr/0001-annexes/`.

## Décision proposée
**Godot 4.7 (.NET) pour le rendu, l'UI et les outils. Toute la simulation est dans une bibliothèque C# pure, sans dépendance à Godot, testée headless par `dotnet test`.**

- Projet `Golferoo.Sim` (net10) : terrain à tuiles, golfeurs, besoins, pathfinding par champs de flux, économie, sauvegarde. Aucun état global, et rien qui dépende de l'écran.
- Projet `Golferoo.Sim.Tests` : tests unitaires et parties simulées en accéléré. C'est la boucle de vérification des agents, sans éditeur ni licence.
- Projet Godot : il lit l'état de la simulation et le dessine, puis renvoie les commandes du joueur. Les scènes sont en `.tscn`, donc en texte.
- OpenSGolf sert de **savoir**, pas de code : on reprend ses docs `DECODE_*`, ses chiffres et ses règles décrites, mais on ne copie ni ne lie aucune ligne. Aucun fichier du jeu original d'EA n'est distribué.
- Le second choix est **Unity 6**, avec la même architecture. Il passe premier seulement si les consoles deviennent la priorité dès la première sortie.

## Pourquoi
1. **Les trois analystes convergent sur Godot.** Il gère la 2D et la 3D, une UI de gestion dense (Control, thèmes), des scènes en texte diffable et le mode headless. Sa licence MIT ne permet aucun changement de règles après coup. Unity a tenté le Runtime Fee en 2023 et a monté ses prix de 5 % le 2026-01-12. Unreal prend 5 % au-delà de 1 M$.
2. **Le C# pour la simulation, mesuré plutôt que supposé.** Les trois langages ont été mesurés sur le même code avec les mêmes sommes de contrôle :

   | Mesure | GDScript typé | C# .NET 10 | Rust |
   |---|---|---|---|
   | Recherche de chemin 128×128 (A) | 99,2 ms | | 1,22 ms |
   | Tick à 500 golfeurs, champs de flux (C) | 0,22 ms | 0,011 ms | 0,007 ms |
   | Un champ de flux 128×128 (C) | 4,9 ms | 0,31 ms | 0,19 ms |
   | Planification d'un coup, plancher (B) | 146-159 µs | 2,4 µs | 2,9 µs |

   GDScript est de 15 à 80 fois plus lent, et il a un piège de CI : une erreur dans un script chargé par un autre sort avec le code 0. Le C# est à moins de deux fois le Rust. Il évite une seconde chaîne d'outils et une couche de liaison (le gdext 0.5.5 n'a qu'un export web expérimental), et les modèles le connaissent bien.
3. **Rust n'a d'intérêt que pour lier `sg-core`, et ce gain est faible.** L'analyse A montre que `sg-core` n'est pas une fondation. La boucle de simulation et la sauvegarde sont dans `simgolf/src/app.rs`, mêlées au rendu. Les règles dépendent de la position à l'écran, l'état global est partagé, et la simulation a peu de tests (14 tests pour les 4 214 lignes de `golfer.rs`). Sa vraie valeur, ce sont environ 11 700 lignes de docs, qu'on peut reprendre sans contrainte de licence. Lier ce code rendrait tout Golferoo GPL, ce qui ferme les consoles (SDK sous NDA) et le code fermé.
4. **Moteur maison, Bevy, web, Unreal : écartés.** Un moteur maison obligerait à refaire l'UI, l'animation et les outils. Bevy casse son API tous les 3 à 5 mois et son UI est faible (et Bevy 0.20 exigeait rustc 1.97.1 dans l'essai). La stack web est excellente pour les agents, mais elle n'a ni consoles ni éditeur pour les artistes. Unreal est disproportionné pour un tycoon et peu adapté aux agents (Blueprints binaires).

## Tranché par Guillaume
- 2026-10-24 : **Golferoo est un jeu à code fermé.** Aucun code d'OpenSGolf n'est lié ni copié ; seuls ses docs et ses chiffres servent de référence. La variante Rust + `sg-core` est écartée.
- 2026-10-24 : **rendu en 3D stylisée (low-poly)**, parce qu'elle ouvre plus de possibilités pour les features modernes. La caméra par défaut est iso orthographique, avec rotation et zoom libres. Le terrain de la simulation reste une grille avec hauteurs aux coins (analyse A), mais **beaucoup plus fine que l'original** pour plus de liberté créative sur les trous (facteur à fixer par le prototype, de l'ordre de 2 à 4 sous-cases par tuile d'origine). Les règles décodées sont adaptées à cette échelle. Le rendu lisse le relief et fond les surfaces avec des bords courbes : la grille ne se voit pas. La balle suit la surface lissée. Coût attendu : un champ de flux C# mesuré à 0,31 ms en 128×128 passe vers 5 ms en 512×512, acceptable avec un cache par destination ; le prototype le mesure.
- 2026-10-24 : **pas de version jouable dans le navigateur.** L'absence d'export web du C# dans Godot 4.7 est donc sans objet.
- 2026-10-24 : **pas de consoles.** Cibles : PC Windows, Mac, Linux et Steam Deck. Unity n'a plus d'avantage décisif ; il reste le second choix si Godot bloquait.

## Vérifications avant de construire (chantier suivant)
1. **Prototype d'un trou** (2 jours environ) : `Golferoo.Sim` minimal (grille fine, rendu lissé, 200 golfeurs, champs de flux) testé par `dotnet test` en CI, dessiné par Godot en 3D low-poly avec la caméra iso. On mesure le temps par tick, le temps d'un agent pour faire une modification visuelle, le coût de production d'un asset 3D, et on vérifie que le terrain ne paraît pas « en cases ».
2. Vérifier que `godot --headless` en version .NET tourne en CI GitHub Actions et sort avec un code non nul quand un test échoue.

## Conséquences
- Stack à reporter dans `CLAUDE.md` après fusion : `dotnet test` pour le test rapide, Godot headless pour la suite complète en CI.
- Les docs d'OpenSGolf deviennent une référence citée. Une règle de dépôt suffit : aucun fichier d'OpenSGolf ni d'EA n'est copié dans `golferoo`.
