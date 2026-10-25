"""Arbre low-poly du jeu, construit par script Blender sans écran (filière B de docs/assets-3d.md, lot 7 de C01).

Usage (Python 3.13) :
    pip install bpy==5.2.3
    python -I tools/blender/tree_lowpoly.py [graine] [glb] [png]

Par défaut : graine 7, game/assets/trees/tree.glb, pas de vignette.
Écrit un seul maillage joint (tronc + quatre masses de feuillage), facettes plates, matériaux unis,
pied à l'origine, environ 4,2 m de haut. Le jeu mesure sa hauteur native et le met à l'échelle de chaque arbre de la sim (TreeLayer : hauteur et couronne selon l'âge).
Échoue si le maillage dépasse TRIANGLE_BUDGET (même budget que l'arbre procédural du jeu).
"""
import math
import random
import sys
import time

import bpy

TRIANGLE_BUDGET = 400

seed = int(sys.argv[1]) if len(sys.argv) > 1 else 7
glb_path = sys.argv[2] if len(sys.argv) > 2 else "game/assets/trees/tree.glb"
png_path = sys.argv[3] if len(sys.argv) > 3 else None
rng = random.Random(seed)
t0 = time.time()

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.95
    return m


bark = material("Bark", (0.32, 0.20, 0.10))
leaves = [material("LeafDark", (0.10, 0.32, 0.10)), material("LeafLight", (0.20, 0.46, 0.14))]

# Tronc : prisme à six pans, effilé, légèrement penché.
bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.22, radius2=0.12, depth=1.9, location=(0, 0, 0.95))
trunk = bpy.context.object
trunk.rotation_euler = (rng.uniform(-0.06, 0.06), rng.uniform(-0.06, 0.06), rng.uniform(0, math.tau))
trunk.data.materials.append(bark)
parts = [trunk]

# Feuillage : une masse centrale haute et trois masses autour, icosphères bruitées (80 triangles chacune).
blobs = [((0.0, 0.0, 3.0), 1.15)]
for i in range(3):
    a = i * math.tau / 3 + rng.uniform(-0.4, 0.4)
    d = rng.uniform(0.55, 0.75)
    blobs.append(((math.cos(a) * d, math.sin(a) * d, rng.uniform(2.1, 2.5)), rng.uniform(0.75, 0.9)))

for i, (center, radius) in enumerate(blobs):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=center)
    blob = bpy.context.object
    blob.scale.z = rng.uniform(0.8, 0.95)
    blob.data.materials.append(leaves[i % 2])
    for v in blob.data.vertices:
        k = 1 + rng.uniform(-0.12, 0.12)
        v.co *= k
    parts.append(blob)

bpy.ops.object.select_all(action="DESELECT")
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = trunk
bpy.ops.object.join()
tree = bpy.context.object
tree.name = "Tree"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for poly in tree.data.polygons:
    poly.use_smooth = False

tris = sum(len(p.vertices) - 2 for p in tree.data.polygons)
if tris > TRIANGLE_BUDGET:
    sys.exit(f"ERREUR : {tris} triangles, budget {TRIANGLE_BUDGET}")

bpy.ops.export_scene.gltf(filepath=glb_path, export_format="GLB", use_selection=True, export_apply=True)

if png_path:
    bpy.ops.object.empty_add(location=(0, 0, 1.9))
    target = bpy.context.object
    bpy.ops.object.camera_add(location=(7, -7, 7.5))
    cam = bpy.context.object
    cam.constraints.new("TRACK_TO").target = target
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 5.2
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
    scene.render.filepath = png_path
    bpy.ops.render.render(write_still=True)

print(f"ASSET tree.glb graine={seed} {tris} tri {time.time() - t0:.2f} s")
