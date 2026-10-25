# Guide pour une IA qui écrit des assets 3D de Golferoo

Mis à jour : 2026-10-25. Ce fichier se donne tel quel à une autre IA (ChatGPT, Gemini, etc.) pour qu'elle écrive des scripts Blender qui fabriquent des objets 3D du jeu. Il est autonome : l'IA n'a pas besoin de voir le dépôt.

## Pour Guillaume : quoi faire, en cinq étapes

1. Ouvre une conversation avec l'autre IA et envoie-lui **ce fichier en entier** (pièce jointe ou copier-coller).
2. Envoie ensuite le prompt de la section « Exemple de prompt », en changeant l'objet demandé.
3. Elle te rend un fichier `.py`. Tu n'as rien à comprendre dedans.
4. Pour le faire tourner, deux façons :
   - **Sans rien installer (recommandé)** : dépose le `.py` dans le fil du projet Claude en disant « intègre cet asset ». Claude le lance dans le cloud, regarde la vignette, corrige au besoin, et ouvre une PR avec le `.glb`, le script et l'image.
   - **Pour voir l'objet toi-même** : installe [Blender](https://www.blender.org/download/) (gratuit), ouvre l'onglet *Scripting* en haut, clique *Open*, choisis le `.py`, puis *Run Script* (▶). L'objet apparaît dans la vue 3D (onglet *Layout*) et le script écrit `nom.glb` et `nom.png` dans le dossier `golferoo-assets` de ton dossier personnel.
5. Si le résultat ne te plaît pas, décris ce qui cloche à l'autre IA (« trop maigre », « le dossier est trop haut », « couleur trop vive ») avec la vignette PNG, et relance. Si le script plante, colle-lui le message d'erreur complet.

Une demande à la fois, un objet par script : c'est ce qui marche le mieux.

---

## Pour l'IA : le contexte

Golferoo est un simulateur de golf moderne en vue isométrique, fait sous Godot 4. Les objets 3D sont **fabriqués par des scripts Python Blender (`bpy`)**, jamais modélisés à la main ni téléchargés. Le script est la source de l'objet : il est relu, versionné et relancé à l'identique. Il tourne de deux façons, et il doit marcher dans les deux :

- sans écran : `python -I tools/blender/<nom>_lowpoly.py -- [graine] [sortie.glb] [vignette.png]`, avec le module `bpy==5.2.3` (Blender 5.2, Python 3.13), lancé depuis la racine du dépôt ;
- dans Blender ouvert, onglet *Scripting*, bouton *Run Script*, sans argument.

La personne qui lance le script ne connaît pas la 3D : elle ne retouchera rien à la main. Tout doit être dans le script.

## Règles obligatoires

### Échelle et repère
- **Échelle réelle, en mètres** (1 unité Blender = 1 m). Un golfeur fait 1,80 m : tout objet doit être juste à côté de lui.
- Repère Blender : Z vers le haut. **Le point le plus bas de l'objet est à z = 0**, centré sur l'origine en x et y. L'avant de l'objet (assise d'un banc, face d'un panneau) regarde vers **-Y**. L'export glTF convertit lui-même en Y vers le haut pour Godot.
- Appliquer toutes les transformations avant l'export (`transform_apply` sur position, rotation, échelle).

### Tailles de référence (réelles)
| Objet | Dimensions à viser |
|---|---|
| Golfeur | 1,80 m de haut |
| Feuillu mature (chêne, érable) | 12 à 20 m de haut, houppier de 8 à 14 m de large, tronc de 0,4 à 0,8 m de diamètre, premières branches vers 3 à 5 m |
| Conifère (pin, sapin) | 15 à 25 m de haut, 4 à 8 m de large à la base |
| Palmier | 8 à 15 m de haut, tronc de 0,3 à 0,4 m, couronne de 4 à 6 m |
| Buisson | 0,8 à 2 m de haut, 1 à 3 m de large |
| Rocher décoratif | 0,5 à 2 m |
| Banc | 1,80 m de long, assise à 0,45 m, dossier jusqu'à 0,85 m |
| Voiturette de golf | 2,4 m de long, 1,2 m de large, 1,8 m de haut |
| Drapeau de trou | mât de 2,1 à 2,4 m |
| Poubelle, lave-balles, marqueur de départ | 0,3 à 1,1 m |
| Club-house | un étage : 3,5 à 4 m par niveau |

**À savoir sur les arbres** : depuis C02 (lot 1), le jeu met chaque arbre à la hauteur et à la largeur de couronne que la simulation lui donne (essence et âge), à partir de la taille native du modèle. Le script modélise donc toujours l'arbre à sa **taille réelle** (par exemple 14 m) ; un jeune plant est un modèle à part, pas un adulte réduit.

### Budget de triangles
Le script compte ses triangles et **échoue** (`sys.exit` avec un message `ERREUR`) s'il dépasse le budget. Budgets actuels :

| Famille | Budget |
|---|---|
| Arbre (toutes essences) | 800 |
| Buisson, touffe d'herbes hautes | 150 |
| Rocher | 120 |
| Petit mobilier (banc, poubelle, panneau, marqueur de départ, lave-balles) | 300 |
| Voiturette de golf (`vehicles`), tondeuse (`maintenance`) | 800 |
| Bâtiment (club-house, abri) | 1 500 |

Visez sous le budget : les arbres adultes font 744 à 796 triangles (choisi le 2026-10-25, 302 FPS avec 5 000 arbres sur RTX 2080), un jeune plant ou du bois mort nettement moins. Peu de faces, bien placées, valent mieux que beaucoup de petites.

### Style
- **Low-poly à facettes plates** : `poly.use_smooth = False` sur toutes les faces, pas de lissage, pas de modificateur Subdivision.
- **Couleurs unies** : un matériau Principled BSDF par couleur, seule la *Base Color* change, *Roughness* 0,9 à 0,95, rien d'autre. **Pas de texture, pas d'image, pas d'UV, pas de transparence, pas d'émission.** Au plus 4 matériaux par objet.
- Formes simples et lisibles de loin (la caméra est haute, en isométrique) : silhouette franche, proportions un peu affirmées, pas de petits détails de moins de 5 cm.
- Variété par une graine : toutes les variations aléatoires passent par `random.Random(seed)`. Même graine, même fichier, à l'octet près. Jamais `random.random()` seul, ni l'heure, ni `noise` non semé.

Palette du jeu (RGB linéaire de 0 à 1, celui qu'attend `Base Color`) :

| Usage | RGB |
|---|---|
| Écorce, bois brut | (0.32, 0.20, 0.10) ; plus clair (0.40, 0.26, 0.14) |
| Feuillage sombre | (0.10, 0.32, 0.10) |
| Feuillage clair | (0.20, 0.46, 0.14) |
| Bois verni (mobilier) | (0.45, 0.30, 0.16) |
| Métal sombre | (0.18, 0.19, 0.20) |
| Blanc cassé (mât, panneaux) | (0.93, 0.93, 0.93) |
| Rouge drapeau | (0.90, 0.15, 0.12) |
| Jaune accent | (0.90, 0.80, 0.20) |
| Sable | (0.78, 0.72, 0.55) |
| Pierre | (0.50, 0.50, 0.48) |

Une variante (automne, conifère bleuté) peut ajouter des teintes proches, de même saturation modérée. Pas de couleurs fluo.

### Structure du fichier rendu
- **Un seul objet maillé**, toutes les pièces jointes (`bpy.ops.object.join()`). Le jeu refuse un fichier qui contient plusieurs maillages.
- Export : `bpy.ops.export_scene.gltf(filepath=..., export_format="GLB", use_selection=True, export_apply=True)`. Un `.glb` binaire, pas de `.gltf` + `.bin`, pas de `.fbx` ni `.obj`.
- Pas d'armature, pas d'animation, pas de caméra ni de lumière dans le `.glb` (la vignette les ajoute **après** l'export).

### Structure du script
Suivez le gabarit ci-dessous, en ne changeant que la partie « construction » et les constantes du début. En particulier :
- Docstring en tête : ce que fait l'objet, la commande d'usage.
- Constantes `NAME`, `FAMILY` (dossier de `game/assets/`), `TRIANGLE_BUDGET`, `SEED`.
- Arguments lus **seulement après `--`**, pour que le script marche aussi dans Blender ouvert.
- Nettoyage de la scène en supprimant les objets, **sans** `bpy.ops.wm.read_factory_settings` (qui fait planter Blender ouvert).
- Vérifications avant l'export : budget de triangles, pied à z = 0.
- Vignette 256 px, caméra orthographique en vue isométrique, fond bleu ciel, rendu Cycles CPU 16 échantillons.
- Dernière ligne : `ASSET <fichier> graine=<n> <triangles> tri <largeur> x <profondeur> x <hauteur> m <durée> s`.
- Python standard + `bpy` seulement : pas de `bmesh` si ce n'est pas nécessaire, pas d'add-on, pas de fichier externe, pas de téléchargement.
- Noms de variables et d'objets en anglais, commentaires et messages en français.
- Nom du fichier : `<nom>_lowpoly.py` (par exemple `conifer_lowpoly.py`), nom de l'asset en anglais, minuscules.

### Interdits
- Copier ou imiter de près un modèle existant d'un autre jeu (EA Sports, PGA Tour 2K, OpenSGolf) ou d'un site de modèles.
- Toute marque, logo ou texte lisible (ni Titleist, ni Club Car, ni nom de parcours réel).

## Gabarit testé (banc de parc, 132 triangles)

Ce script a tourné avec `bpy` 5.2.3 : il sort un `.glb` de 11 Ko, identique d'un lancement à l'autre, et la vignette d'un banc à lattes. Repartez de lui.

```python
"""Banc de parc low-poly, exemple du gabarit de docs/guide-ia-assets-3d.md.

Usage :
    python -I tools/blender/bench_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = "bench"
FAMILY = "furniture"  # dossier de game/assets/ : trees, bushes, rocks, furniture, maintenance, vehicles, buildings
TRIANGLE_BUDGET = 300
SEED = 1

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
seed = int(args[0]) if len(args) > 0 else SEED
default_dir = f"game/assets/{FAMILY}" if bpy.app.background else os.path.expanduser("~/golferoo-assets")
glb_path = args[1] if len(args) > 1 else os.path.join(default_dir, f"{NAME}.glb")
png_path = args[2] if len(args) > 2 else (None if bpy.app.background else os.path.join(default_dir, f"{NAME}.png"))
os.makedirs(os.path.dirname(os.path.abspath(glb_path)), exist_ok=True)
rng = random.Random(seed)
t0 = time.time()

# Scène vide, sans read_factory_settings (qui ferait planter Blender ouvert).
for block in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for item in list(block):
        block.remove(item)
scene = bpy.context.scene


def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.95
    return m


def box(size, location, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    o = bpy.context.object
    o.scale = size
    o.data.materials.append(mat)
    return o


wood = material("Wood", (0.45, 0.30, 0.16))
metal = material("Metal", (0.18, 0.19, 0.20))
parts = []

# --- Construction : seule partie à réécrire pour un autre objet. ---
# Assise et dossier : lattes de bois, 1,80 m de long, assise à 0,45 m.
length = 1.8 + rng.uniform(-0.1, 0.1)
for i in range(3):
    parts.append(box((length, 0.12, 0.04), (0, -0.16 + i * 0.15, 0.45), wood))
for i in range(2):
    parts.append(box((length, 0.04, 0.11), (0, 0.24, 0.62 + i * 0.16), wood))

# Deux pieds métalliques en côté.
for x in (-length / 2 + 0.15, length / 2 - 0.15):
    parts.append(box((0.06, 0.5, 0.06), (x, 0, 0.40), metal))
    parts.append(box((0.06, 0.06, 0.42), (x, -0.2, 0.21), metal))
    parts.append(box((0.06, 0.06, 0.82), (x, 0.24, 0.41), metal))
# --- Fin de la construction. ---

bpy.ops.object.select_all(action="DESELECT")
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
obj = bpy.context.object
obj.name = NAME.capitalize()
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for poly in obj.data.polygons:
    poly.use_smooth = False

tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
if tris > TRIANGLE_BUDGET:
    sys.exit(f"ERREUR : {tris} triangles, budget {TRIANGLE_BUDGET}")
dims = obj.dimensions
low = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
if abs(low) > 0.01:
    sys.exit(f"ERREUR : le point le plus bas est à z = {low:.3f}, il doit être à 0")

bpy.ops.export_scene.gltf(filepath=glb_path, export_format="GLB", use_selection=True, export_apply=True)

if png_path:
    bpy.ops.object.empty_add(location=(0, 0, dims.z / 2))
    target = bpy.context.object
    d = max(dims) * 1.6
    bpy.ops.object.camera_add(location=(d, -d, d * 1.05))
    cam = bpy.context.object
    cam.constraints.new("TRACK_TO").target = target
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(dims) * 1.4
    scene.camera = cam
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(40), 0, math.radians(30)))
    bpy.context.object.data.energy = 3
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.85, 0.95, 1)
    scene.world = world
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.filepath = os.path.abspath(png_path)
    bpy.ops.render.render(write_still=True)

print(f"ASSET {os.path.basename(glb_path)} graine={seed} {tris} tri "
      f"{dims.x:.2f} x {dims.y:.2f} x {dims.z:.2f} m {time.time() - t0:.2f} s")
```

Techniques utiles pour des formes organiques à petit budget (c'est ainsi qu'est fait l'arbre actuel, 340 triangles) :
- Tronc : `primitive_cone_add(vertices=6, radius1=..., radius2=..., depth=...)`, un prisme effilé à six pans, légèrement penché par la graine.
- Masse de feuillage : `primitive_ico_sphere_add(subdivisions=1 ou 2)` (20 ou 80 triangles), aplatie (`scale.z` de 0,8 à 0,95), sommets bruités de ±12 % par la graine. Trois à cinq masses de tailles différentes, alternant deux verts.
- Conifère : trois à cinq cônes à 6 à 8 pans empilés, de plus en plus étroits.
- Rocher : icosphère de subdivision 1, écrasée et bruitée fort (±25 %).

## Comment le résultat est validé

L'IA ne peut pas toujours lancer le script ; voici ce qui sera vérifié dès qu'il tournera. Relisez votre script contre cette liste avant de le rendre.

1. Il tourne sans erreur avec `python -I script.py -- 3 out.glb out.png` **et** dans Blender ouvert.
2. La ligne `ASSET` donne un nombre de triangles sous le budget et des dimensions conformes au tableau des tailles (à ±20 %).
3. Deux lancements avec la même graine donnent un `.glb` identique ; trois graines différentes donnent trois variantes visiblement différentes mais de même style.
4. La vignette montre l'objet entier, posé, à facettes plates, aux couleurs de la palette.
5. Le `.glb` contient un seul maillage, sans texture.
6. Dans le jeu : import par Godot, puis capture du parcours à côté d'un golfeur pour juger l'échelle.

Les fichiers vont dans le dépôt ainsi :
- le script dans `tools/blender/<nom>_lowpoly.py` ;
- le `.glb` dans `game/assets/<famille>/<nom>.glb` (par exemple `game/assets/trees/conifer.glb`) ;
- la vignette dans la PR, pour la relecture.

Le test de fumée Godot de la CI ouvre chaque `.glb` de `game/assets/` et échoue si sa famille est inconnue, s'il contient plus d'un maillage, dépasse le budget de sa famille ou ne pose pas au sol. Le catalogue des modèles déjà livrés est `docs/assets-3d/catalogue.md`.

Seul l'arbre (`game/assets/trees/tree.glb`) est déjà lu par le jeu ; un nouvel objet demande un peu de code côté Godot pour être placé sur le parcours, que Claude ajoute au moment de l'intégration.

## Exemple de prompt

À copier après avoir envoyé ce fichier. Remplacez la partie entre crochets.

```text
Lis le guide joint en entier : c'est le cahier des charges du projet, toutes ses règles sont obligatoires.

Écris un script Blender autonome, conifer_lowpoly.py, qui fabrique [un pin parasol / sapin de bord de fairway, mature, d'environ 18 m de haut et 6 m de large à la base, tronc visible sur les 3 premiers mètres, trois à cinq étages de feuillage vert sombre légèrement bleuté].

Contraintes à respecter en plus du guide :
- repars du gabarit du guide et ne réécris que la partie « Construction » et les constantes ;
- budget : famille Arbre, 800 triangles ;
- la graine fait varier la hauteur de ±15 %, le nombre d'étages et une légère inclinaison ;
- l'échelle est réelle (le jeu adaptera, ne réduis pas l'arbre).

Rends-moi :
1. le script complet dans un seul bloc de code, prêt à lancer ;
2. en trois lignes, la forme que tu as visée et le nombre de triangles attendu ;
3. si tu peux exécuter du Python avec bpy, lance-le et montre la ligne ASSET et la vignette ; sinon dis-le, sans inventer de résultat.
```

Pour corriger après un premier essai, un message court suffit, avec la vignette jointe : « Le feuillage est trop régulier et le tronc trop épais ; garde le reste. Rends le script complet. »
