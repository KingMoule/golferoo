"""Essai de la filière « scripts bpy » : un arbre low-poly sans ouvrir Blender.

Usage (Python 3.13) :
    pip install bpy==5.2.3
    python -I tools/blender/arbre_lowpoly.py <graine> <sortie_sans_extension>

Écrit <sortie>.glb (un seul maillage, facettes plates, deux matériaux unis)
et <sortie>.png (vignette 256 px, caméra iso orthographique) pour la relecture.
Voir docs/assets-3d.md.
"""
import math
import random
import sys
import time

import bpy

seed = int(sys.argv[1]) if len(sys.argv) > 1 else 1
out = sys.argv[2] if len(sys.argv) > 2 else "tree"
rng = random.Random(seed)
t0 = time.time()

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    return m


bark = mat("Bark", (0.30, 0.18, 0.09))
leaf = mat("Leaf", (0.16, 0.45, 0.14))

bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.12, depth=1.2, location=(0, 0, 0.6))
trunk = bpy.context.object
trunk.data.materials.append(bark)

parts = [trunk]
z = 1.0
for i in range(3):
    r = 0.9 - i * 0.22 + rng.uniform(-0.08, 0.08)
    bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=r, depth=0.9, location=(0, 0, z + 0.45))
    c = bpy.context.object
    c.rotation_euler.z = rng.uniform(0, math.tau)
    c.data.materials.append(leaf)
    # Petit bruit sur les sommets pour casser la symétrie
    for v in c.data.vertices:
        v.co.x += rng.uniform(-0.06, 0.06)
        v.co.y += rng.uniform(-0.06, 0.06)
    parts.append(c)
    z += 0.55

for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = trunk
bpy.ops.object.join()
tree = bpy.context.object
tree.name = f"Tree_{seed}"
for poly in tree.data.polygons:
    poly.use_smooth = False  # facettes plates : style low-poly

bpy.ops.export_scene.gltf(filepath=f"{out}.glb", export_format="GLB", use_selection=True)
tris = sum(len(p.vertices) - 2 for p in tree.data.polygons)

# Vignette : caméra iso orthographique, comme le jeu
bpy.ops.object.empty_add(location=(0, 0, 1.4))
target = bpy.context.object
bpy.ops.object.camera_add(location=(5, -5, 5.5))
cam = bpy.context.object
cam.constraints.new("TRACK_TO").target = target
cam.data.type = "ORTHO"
cam.data.ortho_scale = 3.6
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
scene.render.filepath = f"{out}.png"
bpy.ops.render.render(write_still=True)
print(f"OK seed={seed} tris={tris} temps={time.time() - t0:.1f}s")
