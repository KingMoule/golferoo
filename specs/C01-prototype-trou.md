# Prototype d'un trou en 3D low-poly
Statut : validé
Créé : 2026-10-24

## But
Prouver, avant de construire le jeu, que la stack de l'ADR 0001 tient ses promesses sur un seul trou jouable par des golfeurs simulés :
1. la chaîne complète marche : simulation C# pure testée par `dotnet test`, dessinée par Godot 4.7 .NET, le tout en CI ;
2. le terrain ne paraît pas « en cases » : relief lissé, surfaces aux bords courbes, sous toutes les rotations de caméra ;
3. les performances tiennent à la grille fine (champs de flux, tick à 200 golfeurs, reconstruction du terrain après une retouche) ;
4. on connaît le coût réel d'un asset 3D et d'une retouche visuelle faite par un agent.

Le prototype tranche aussi le **facteur de subdivision** (2, 3 ou 4 sous-cases par tuile d'origine) sur mesures et captures.

## Périmètre et hors périmètre
**Dans le périmètre**
- Un par 4 d'environ 400 yards sur une zone de 48 × 16 tuiles d'origine (soit 192 × 64 cellules au facteur 4), décrit dans un fichier texte versionné : hauteurs aux coins, surface par cellule, tee, green, trou.
- Surfaces : tee, green, fairway, rough, bunker, eau, arbres. Chemin optionnel s'il reste du temps.
- Golfeurs par paires qui jouent le trou de bout en bout : marche par champ de flux, coup planifié de façon simple, vol, rebond et roulement sur la surface lissée, eau et pénalité, putt, sortie.
- Un mode « charge » qui fait marcher 200 golfeurs en même temps, pour la mesure.
- Rendu 3D low-poly : terrain par blocs, fondu des surfaces par shader, eau, arbres, drapeau, golfeurs et balle interpolés entre deux ticks. Caméra iso orthographique, rotation par quarts et libre, zoom.
- Mesures automatiques et rapport `docs/prototype-c01-rapport.md`.

**Hors périmètre** : économie, besoins et humeur, bâtiments, UI de gestion, plusieurs trous, sauvegarde de partie, son, golf jouable par le pro, réglage fin de la physique, animations des golfeurs (une silhouette qui glisse suffit), localisation, builds Mac et Steam Deck (on vérifie seulement que l'export Linux se construit).

## Conception
**Arborescence**
- `Golferoo.sln`
- `src/Golferoo.Sim/` (bibliothèque, aucune référence à Godot) : `Terrain/` (grille, `HeightField` lissé, `Surface` et table de ses propriétés), `Pathing/` (champs de flux avec cache par destination), `Golfers/` (état, machine à états, groupes), `Shots/` (planification, vol, rebond, roulement), `World.cs` (tick fixe, RNG à graine, commandes).
- `tests/Golferoo.Sim.Tests/` : tests unitaires et partie complète en accéléré.
- `tests/Golferoo.Sim.Bench/` : console de mesure en Release, sortie en JSON.
- `game/` : projet Godot (`project.godot`, `.tscn` en texte, `Golferoo.Game.csproj` qui référence `Golferoo.Sim`). Lit un instantané du monde à chaque tick, envoie les commandes.
- `data/holes/proto-par4.json` : le trou du prototype.
- `.github/workflows/ci.yml` : `dotnet format --verify-no-changes`, `dotnet test`, banc, import et test de fumée Godot headless.

**Règles qui tiennent la promesse « pas en cases »**
- Une seule fonction de hauteur, `HeightField.Sample(x, z)` (interpolation bicubique ou Catmull-Rom des hauteurs aux coins), dans `Golferoo.Sim`. Le maillage Godot l'échantillonne et la balle la suit : ce qu'on voit est ce que la physique touche.
- Les surfaces restent discrètes dans la simulation (une par cellule). Le rendu les passe au shader dans une texture d'une texel par cellule, filtrée puis seuillée avec un bruit : les bords deviennent courbes sans changer les règles. La surface « vue » sous la balle et la surface simulée peuvent différer d'une demi-cellule ; le rapport dit si ça se remarque.
- Le maillage du terrain est plus fin que la grille (2 sommets par cellule ou plus) et découpé en blocs de 32 × 32 cellules, pour ne reconstruire que les blocs touchés.

**Temps** : tick de simulation fixe à 20 Hz, accéléré ×1 à ×16 ; le rendu interpole. Tout est déterministe à graine égale (même graine, même partie, même somme de contrôle).

**Risque connu** : `Golferoo.Sim` vise `net10.0` (ADR). Si Godot 4.7 .NET refuse qu'un projet `net10.0` référence la bibliothèque, on cible la version que Godot accepte pour les deux projets et on le note dans le rapport. Premier lot à vérifier.
_Levé au lot 1 (PR #6) : Godot 4.7.2 .NET charge `Golferoo.Game` et `Golferoo.Sim` en `net10.0`, test de fumée headless vert en CI._

## Choix à trancher par Guillaume
Aucun : tout est tranché ci-dessous. Les options étudiées pour les assets restent ici pour mémoire.

**Origine des assets 3D** (arbre, drapeau, golfeur, balle).
   - *A, procéduraux en code* : maillages générés par script, faits par les agents, sans coût ni licence, style homogène mais limité.
   - *B, pack libre CC0* (par exemple Kenney) : rapide et joli, mais pas notre style et pas une mesure de production.
   - *C, modélisés dans Blender* : la vraie chaîne d'un artiste, mais demande quelqu'un qui modélise.
   - *D, image vers 3D par IA* : on part d'une image 2D sous licence libre (CC0) ou dessinée par nous, un outil la convertit en modèle 3D, puis un script Blender lancé par un agent réduit les polygones et aplatit les couleurs pour le style low-poly. Outils possibles : TRELLIS (Microsoft, code ouvert, à faire tourner sur une carte NVIDIA) ou un service payant en ligne (Meshy, Tripo, Rodin). Les licences et la propriété des modèles produits sont à vérifier pour l'outil choisi avant tout usage dans le jeu. Jamais à partir des images du SimGolf d'EA.
   - Recommandation : *A* pour le terrain et les trois props, plus l'arbre en *D* pour comparer à l'écran le coût et le rendu. *B* sert de repère visuel ; *C* seulement si quelqu'un veut chronométrer un modèle. La mesure est le temps humain et agent par asset, notée dans le rapport.

## Tranché par Guillaume
- 2026-10-24 : **machine de référence** pour les FPS : son PC en 1080p. Pas de Steam Deck disponible : la tenue sur Steam Deck reste à vérifier plus tard (la CI ne mesure que la simulation).
- 2026-10-24 : **pinceau minimal** dans le prototype : peindre une surface et monter ou baisser le relief à la souris, sans coût ni annulation. Le lot 6 est donc dans le périmètre.
- 2026-10-24 : **assets** : terrain, drapeau, golfeur et balle générés par code (*A*) ; l'arbre converti d'une image 2D libre en 3D par TRELLIS (*D*), lancé sur le PC de Guillaume (carte NVIDIA) par une session Remote Control, puis simplifié par script Blender. Vérifier au lot 7 la mémoire vidéo demandée par TRELLIS et la licence de ses poids ; si le rendu déçoit, comparaison avec un mois de Meshy Pro (20 $, modèles privés avec usage commercial) ; si tout bloque, repli sur l'arbre en *A*.
- 2026-10-24 : TRELLIS abandonné (carte de Guillaume : RTX 2080, 8 Go, sous les ~16 Go demandés) ; l'arbre est construit par script Blender (`bpy`) dans le cloud, `tools/blender/tree_lowpoly.py`, d'après la comparaison de `docs/assets-3d.md`.

## Critères d'acceptation
1. `dotnet test` passe en local et en CI, avec au moins un test par module de `Golferoo.Sim` et une partie complète du trou par une paire de golfeurs en moins de 5 s de temps réel.
2. Déterminisme : deux parties à la même graine donnent la même somme de contrôle d'état (test dédié).
3. La CI échoue (code non nul) quand un test casse : prouvé par une PR ou un commit de démonstration au test volontairement faux, puis retiré. Même preuve pour le test de fumée Godot headless (ADR, vérification 2).
4. `Golferoo.Sim` ne référence pas Godot : un test ou une étape CI vérifie qu'aucun `using Godot` ni référence `GodotSharp` n'y figure.
5. Banc (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`), aux facteurs 2, 3 et 4, résultats en JSON dans le rapport :
   - tick moyen à 200 golfeurs en mode charge inférieur à 2 ms, p99 inférieur à 5 ms ;
   - un champ de flux sur la carte du trou inférieur à 5 ms, et sur une carte de 512 × 512 cellules inférieur à 20 ms ;
   - recalcul après une retouche de 5 × 5 cellules, champs de flux compris, inférieur à 16 ms.
6. Le jeu tourne à 60 FPS au moins sur le PC de Guillaume en 1080p avec 200 golfeurs, compteur affiché à l'écran et relevé dans le rapport.
7. Un script de captures produit 8 images (4 rotations × 2 zooms) dans `docs/prototype-c01/`. Guillaume juge sur ces images que le terrain ne paraît pas « en cases » ; son verdict est noté dans le rapport.
8. Une retouche visuelle confiée à un agent par un brief d'une ligne (exemple : « bunkers plus clairs et arbres plus ronds ») est faite et chronométrée ; le rapport donne la durée et les tokens dépensés.
9. Le rapport tranche le facteur de subdivision, chiffres et captures à l'appui, et liste ce qu'il faut changer dans l'ADR s'il y a lieu.
10. `CLAUDE.md`, section « Stack et commandes », donne les commandes réelles vérifiées.

## Plan (lots délégables)
1. Squelette : solution, projets, CI avec `dotnet test`, import Godot headless et test de fumée ; preuve du critère 3. Lève le risque `net10.0`.
2. Terrain sim : grille paramétrée par le facteur, `HeightField` lissé, surfaces, lecture du trou JSON, tests.
3. Champs de flux, golfeurs, tick, mode charge, banc.
4. Coup simplifié : planification par échantillonnage, vol, rebond, roulement, eau, putt ; partie complète en test.
5. Rendu Godot : maillage par blocs, shader de fondu, eau, caméra, golfeurs et balle interpolés, compteur FPS, script de captures.
6. Pinceau de surface et de relief.
7. Assets selon le choix 1, chronométrés.
8. Mesures, captures, retouche par agent, rapport, facteur choisi, `CLAUDE.md`.

Durée estimée : 3 jours avec le pinceau (2 selon l'ADR, sans lui).

## Niveau de contrôle
complet pour `Golferoo.Sim` (lots 2 à 4 : physique et état de partie), léger pour le rendu et les outils (lots 5 à 7).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
