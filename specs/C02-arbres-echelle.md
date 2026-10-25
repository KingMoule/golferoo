# Arbres et échelle du monde
Statut : livré (2026-10-25)
Créé : 2026-10-25

## But
1. **Échelle** : les arbres ont leur vraie taille et leur vrai espacement par rapport au golfeur (1,80 m) et au trou (par 4 de 348 m du tee au drapeau). Aujourd'hui, ils font 3,2 à 6 m et sont posés un par cellule de 4 m (`docs/prototype-c01-rapport.md`, section « Captures ») : environ 625 arbres à l'hectare, cinq à huit fois trop.
2. **Arbres vivants** (`docs/boucles-v1.md`, section « Arbres vivants ») : chaque arbre devient un objet de la simulation, avec son essence, son âge, sa hauteur et sa couronne. Le joueur garde ou rase les arbres du départ, plante de jeunes plants qui poussent, et un arbre peut tomber.

Ce qui prouve la réussite : sur les captures de la CI, un golfeur se lit à côté d'un arbre mature huit à dix fois plus haut que lui ; un arbre planté grandit quand on avance le temps ; une balle traverse plus souvent un feuillu au printemps qu'en été ; les budgets de C01 tiennent.

## Références (sources : `docs/guide-ia-assets-3d.md`, tableau « Tailles de référence »)
| Essence (Parkland) | Hauteur adulte | Couronne (diamètre) | Tronc | Adulte vers | Densité de la couronne |
|---|---|---|---|---|---|
| Feuillu (chêne, érable) | 12 à 20 m | 8 à 14 m | 0,4 à 0,8 m | 40 ans | forte l'été, faible au printemps |
| Conifère (pin) | 15 à 25 m | 4 à 8 m à la base | 0,3 à 0,6 m | 50 ans | forte toute l'année |
| Jeune plant | 1,5 à 3 m | 0,5 à 1,5 m | 0,05 m | | gêne peu la balle |

**Lot 3 (2026-10-25)** : les essences deviennent chêne, bouleau et sapin, avec les modèles de la PR #23 (`game/assets/trees/`). Les noms Feuillu et Pin sont rejetés dans le fichier du trou.

**Espacement** : dans un massif, deux arbres adultes sont espacés d'à peu près le diamètre de leur couronne, soit 7 à 12 m (environ 100 arbres à l'hectare). En bordure de fairway, ils sont isolés ou en rangée lâche. Le trou du prototype passe ainsi de 720 arbres à environ 120. Ces chiffres sont des hypothèses, validées sur les captures.

## Périmètre et hors périmètre
**Dans le périmètre**
- Modèle d'arbre dans la sim, index spatial, boisement généré de façon déterministe à partir du fichier du trou.
- Collision balle–arbre : tronc qui arrête ou renvoie, couronne traversée selon sa densité et la saison.
- Rendu à l'échelle réelle, un modèle par essence et un jeune plant par essence (scripts `bpy`), `MultiMesh` par bloc de terrain (la partie « arbres » de F6).
- Croissance en fonction de l'âge ; chute d'un arbre (mécanisme, animation, souche ou tronc couché).
- Outils : sélectionner, garder, raser (un arbre, une zone, « tout sauf les remarquables »), planter un jeune plant ou un arbre en motte.

**Hors périmètre** : calendrier et saisons réels (C03, F1), déclenchement des chutes par la météo (C04), économie (les coûts sont des constantes affichées, sans caisse), essences des autres thèmes (Tropical, Desert, Links), élagage, recalcul de la difficulté des trous (D5), immobilier, niveaux de détail et horizon (F6).

## Conception
**Simulation** (`src/Golferoo.Sim/Trees/`, sans Godot)
- `TreeSpecies` : record immuable par essence (hauteur et couronne adultes, tronc, hauteur des premières branches, âge adulte, courbe de croissance, densité de la couronne par saison, type feuillu, conifère ou palmier). Table `TreeSpeciesTable` comme `SurfaceTable`.
- `Tree` : struct (identifiant, position x et z en mètres, essence, âge en mois, état debout, tombé ou souche, drapeau « remarquable »). Hauteur et couronne se **déduisent** de l'essence et de l'âge (fonction pure) : rien à recalculer à chaque tick.
- `TreeStand` : liste d'arbres et index spatial par case de 16 m, avec la hauteur maximale de la case. Ajouter, retirer, abattre ou vieillir un arbre met l'index à jour. Il est rangé dans `World`, copié dans `WorldSnapshot` seulement quand il change (compteur de version).
- `Season` : paramètre passé à la collision. C02 fixe « été » par défaut ; C03 le fournira.
- **Boisement** (`WoodlandGenerator`) : dans les tuiles `#` du fichier du trou, un tirage de Poisson-disk déterministe (graine du monde) place des arbres d'âges variés, à l'espacement de leur couronne. La surface `Trees` reste le **sous-bois** infranchissable, comme aujourd'hui. Le fichier du trou peut aussi lister des arbres isolés (`"trees": [{ "x", "z", "species", "age" }]`).
- **Règle de performance** (`docs/boucles-v1.md`) : un arbre isolé ne change jamais le coût de marche. Seuls le sous-bois, une plantation dans le sous-bois ou un défrichage touchent les champs de flux. Un golfeur peut donc frôler un tronc isolé de 0,6 m : c'est accepté.
- **Balle** (`BallSimulator`) : à chaque pas, si la balle est plus basse que la hauteur maximale de sa case de 16 m, elle teste les arbres de la case. Tronc : cylindre, rebond dur (restitution de l'écorce). Couronne : ellipsoïde ; la balle a une probabilité d'être arrêtée qui croît avec la longueur traversée et la densité (feuillu au printemps : faible), tirée du RNG déterministe du coup. Arrêtée, elle perd presque toute sa vitesse horizontale et tombe. Le test actuel « surface `Trees` sous 8 m » reste pour le sous-bois.

**Rendu** (`game/src/Rendering/TreeLayer.cs`)
- Lit les arbres du snapshot au lieu des cellules `Trees`. Échelle = hauteur de l'arbre / hauteur du modèle : `ExternalTreeHeight` disparaît et le modèle garde sa taille réelle.
- Un `MultiMesh` par bloc de terrain et par modèle : une plantation, un abattage ou une croissance ne reconstruit que les blocs touchés.
- Assets : `tools/blender/tree_lowpoly.py` paramétré par essence (feuillu, pin, jeune plant de chaque), budget de 400 triangles. `docs/guide-ia-assets-3d.md`, paragraphe « À savoir sur les arbres », est mis à jour.
- Chute : `Tween` de bascule d'environ 1,5 s, puis tronc couché et souche.

**Lots proposés (4 PR)**
1. **Échelle et arbres de la sim** : `TreeSpecies`, `Tree`, `TreeStand`, boisement généré, `TreeLayer` branché sur la sim à la vraie taille, couronnes en transparence tramée près du curseur et de la balle suivie, captures avant et après. La balle garde provisoirement le test du sous-bois.
2. **Balle et arbres** : tronc, couronne, densité par saison, tests de déterminisme, banc.
3. **Croissance et essences** : courbes de croissance, feuillu, pin et jeunes plants en `bpy`, commande de debug « avancer d'un an », `MultiMesh` par bloc.
4. **Outils et chute** : sélectionner, garder, raser, planter (jeune plant ou motte), coûts affichés ; abattage par commande (`World.FellTree`), animation, souche. Le déclencheur météo viendra de C04.

## Décisions de Guillaume (2026-10-25)
Les trois recommandations sont retenues.
1. **Le temps avant C03** : la hauteur dépend seulement de l'âge ; une commande de debug « avancer d'un an » fait vieillir les arbres. C03 la branchera sur le calendrier. Le lot 3 n'attend pas C03.
2. **Arbres qui cachent les golfeurs** : les couronnes sont rendues en transparence tramée autour du curseur et de la balle suivie (paramètre du shader des arbres), au lot 1. Un arbre de 18 m masque environ 25 m derrière lui en vue iso.
3. **Densité** : densité réelle, environ 100 arbres à l'hectare. On serrera un peu seulement si les captures montrent des massifs clairsemés.

## Critères d'acceptation
1. `dotnet test Golferoo.sln` passe ; des tests couvrent l'index spatial (ajout, retrait, hauteur maximale), le boisement (même graine, même forêt ; espacement minimal respecté), la croissance (hauteur croissante et bornée par l'adulte) et la balle (un tronc arrête toujours ; un feuillu au printemps laisse passer plus de balles qu'en été sur 10 000 tirages ; même graine, même trajectoire).
2. Au facteur 2, aucun arbre n'est plus proche d'un autre que 0,7 fois le rayon de leurs couronnes réunies, et un arbre mature mesure entre 12 et 25 m (test).
3. Banc (`dotnet run -c Release --project tests/Golferoo.Sim.Bench`) : le vol d'une balle sur une carte de 512² avec 20 000 arbres coûte au plus 1,2 fois le vol sans arbres ; le tick à 200 golfeurs reste sous 2 ms ; vieillir 20 000 arbres d'un mois coûte moins de 5 ms.
4. Un arbre isolé planté, grandi ou abattu ne déclenche aucun recalcul de champ de flux (test sur le compteur de reconstructions).
5. Captures de la CI (lignes `THUMB`) : un golfeur à côté d'un arbre mature, en vue proche ; le trou entier, en vue d'ensemble. Guillaume juge l'échelle et la densité.
6. Fumée Godot (`SMOKE OK`) ; `TreeLayer` compte les arbres de la sim et ne reconstruit que les blocs touchés par une plantation (compteur).
7. `Mesurer FPS.bat` sur la RTX 2080 de Guillaume, avec 5 000 arbres à la vraie taille et 200 golfeurs : au moins 240 FPS en 1080p.
8. Une capture montre un golfeur derrière un arbre, lisible à travers la couronne tramée.
9. `dotnet format Golferoo.sln --verify-no-changes` passe ; aucun `using Godot` dans `src/Golferoo.Sim/`.

## Niveau de contrôle
léger (le lot 2, balle et arbres, en contrôle complet : il touche la physique du coup)

## Clôture
- [x] Critères vérifiés (PR #20, #22, #24, #25, #26 ; le critère 7, 240 FPS sur la RTX 2080, reste à mesurer par Guillaume)
- [x] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
