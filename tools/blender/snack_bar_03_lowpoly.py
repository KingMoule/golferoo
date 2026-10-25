"""Snack-bar et abri — Comptoir contemporain. Asset low-poly original de Golferoo.

Usage :
    python -I tools/blender/snack_bar_03_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'snack_bar_03'
TRIANGLE_BUDGET = 1500
SEED = 1
KIND = 'snack_bar'
VARIANT = 3
PALETTE = [('Wall', (0.69, 0.67, 0.57)), ('Roof', (0.19, 0.22, 0.2)), ('Trim', (0.45, 0.34, 0.22)), ('GlazingOpaque', (0.12, 0.19, 0.2))]

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
seed = int(args[0]) if len(args) > 0 else SEED
default_dir = "game/assets/buildings" if bpy.app.background else os.path.expanduser("~/golferoo-assets")
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
# Fonctions géométriques locales : aucune dépendance ni ressource externe.
def mesh_part(label, vertices, faces, mat):
    mesh = bpy.data.meshes.new(label)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    part = bpy.data.objects.new(label, mesh)
    scene.collection.objects.link(part)
    part.data.materials.append(mat)
    parts.append(part)
    return part

def block(size, location, mat, rotation=None):
    part = box(size, location, mat)
    if rotation:
        part.rotation_euler = rotation
    parts.append(part)
    return part

def tube(start, end, radius, mat, sides=6, top_radius=None, cap_mat=None):
    axis = [end[i] - start[i] for i in range(3)]
    length = math.sqrt(sum(value * value for value in axis))
    axis = [value / length for value in axis]
    ref = (0, 0, 1) if abs(axis[2]) < 0.9 else (0, 1, 0)
    u = [axis[1] * ref[2] - axis[2] * ref[1],
         axis[2] * ref[0] - axis[0] * ref[2],
         axis[0] * ref[1] - axis[1] * ref[0]]
    norm = math.sqrt(sum(value * value for value in u))
    u = [value / norm for value in u]
    v = [axis[1] * u[2] - axis[2] * u[1],
         axis[2] * u[0] - axis[0] * u[2],
         axis[0] * u[1] - axis[1] * u[0]]
    vertices = []
    for center, r in ((start, radius), (end, radius if top_radius is None else top_radius)):
        for side in range(sides):
            angle = math.tau * side / sides
            vertices.append(tuple(center[i] + r * (math.cos(angle) * u[i] + math.sin(angle) * v[i])
                                  for i in range(3)))
    faces = [tuple(reversed(range(sides)))]
    for side in range(sides):
        other = (side + 1) % sides
        faces.append((side, other, sides + other, sides + side))
    faces.append(tuple(range(sides, 2 * sides)))
    part = mesh_part('TaperedPrism', vertices, faces, mat)
    if cap_mat:
        part.data.materials.append(cap_mat)
        part.data.polygons[0].material_index = 1
        part.data.polygons[-1].material_index = 1
    return part

def ico(location, scale, mat, subdivisions=1, jitter=0.08):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    part = bpy.context.object
    for vertex in part.data.vertices:
        factor = rng.uniform(1 - jitter, 1 + jitter)
        vertex.co *= factor
    part.scale = scale
    part.data.materials.append(mat)
    parts.append(part)
    return part

def rings(label, levels, mat, sides=8, tip=None, cap_mat=None):
    vertices = []
    for x, y, z, radius in levels:
        for side in range(sides):
            angle = math.tau * side / sides
            vertices.append((x + radius * math.cos(angle), y + radius * math.sin(angle), z))
    faces = [tuple(reversed(range(sides)))]
    for ring_index in range(len(levels) - 1):
        for side in range(sides):
            other = (side + 1) % sides
            a = ring_index * sides + side
            b = ring_index * sides + other
            c = (ring_index + 1) * sides + other
            d = (ring_index + 1) * sides + side
            faces.append((a, b, c, d))
    if tip:
        tip_index = len(vertices)
        vertices.append(tip)
        last = (len(levels) - 1) * sides
        for side in range(sides):
            faces.append((last + side, last + (side + 1) % sides, tip_index))
    else:
        faces.append(tuple(range((len(levels) - 1) * sides, len(levels) * sides)))
    part = mesh_part(label, vertices, faces, mat)
    if cap_mat:
        part.data.materials.append(cap_mat)
        part.data.polygons[-1].material_index = 1
    return part

def gable(width, depth, eave, rise, mat, center=(0, 0)):
    x, y = center
    vertices = [(x - width / 2, y - depth / 2, eave),
                (x + width / 2, y - depth / 2, eave),
                (x, y - depth / 2, eave + rise),
                (x - width / 2, y + depth / 2, eave),
                (x + width / 2, y + depth / 2, eave),
                (x, y + depth / 2, eave + rise)]
    return mesh_part('PitchedRoof', vertices,
                     [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], mat)

def wheel(x, y, radius, width, dark, hub, sides=8):
    tube((x - width / 2, y, radius), (x + width / 2, y, radius), radius, dark, sides)
    outer = x + (width / 2 + 0.015) * (1 if x >= 0 else -1)
    tube((outer - 0.025, y, radius), (outer + 0.025, y, radius), radius * 0.48, hub, 6)

def steering(x, y, z, dark):
    # Couronne octogonale épaisse : pas de texture ni de transparence.
    vertices = []
    for radius in (0.20, 0.14):
        for side in range(8):
            angle = math.tau * side / 8
            vertices.append((x + radius * math.cos(angle), y + radius * math.sin(angle), z))
    faces = [(i, (i + 1) % 8, 8 + (i + 1) % 8, 8 + i) for i in range(8)]
    mesh_part('SteeringWheel', vertices, faces, dark)
    tube((x, y + 0.12, z - 0.45), (x, y, z), 0.035, dark, 4)
    block((0.33, 0.055, 0.055), (x, y, z), dark)

mats = []
for index, (label, rgb) in enumerate(PALETTE):
    mat = (wood, metal)[index] if index < 2 else material(label, rgb)
    mat.name = label
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    mats.append(mat)

def build_building():
    wall, roof, trim, glazing = mats
    shed = KIND == 'maintenance_shed'
    snack = KIND == 'snack_bar'
    if shed:
        width, depth, eave, rise = ((7.0, 5.0, 3.3, 1.2), (10.0, 7.0, 3.7, 1.0), (8.0, 6.0, 3.5, 0.9))[VARIANT - 1]
    elif snack:
        width, depth, eave, rise = ((4.2, 3.6, 2.7, 1.0), (5.2, 4.0, 2.9, 0.9), (5.0, 3.8, 2.8, 0.15))[VARIANT - 1]
    else:
        width, depth, eave, rise = ((13.0, 8.0, 3.5, 1.6), (11.0, 8.0, 3.5, 1.8), (15.0, 8.0, 3.7, 0.2))[VARIANT - 1]
    block((width + 0.25, depth + 0.25, 0.16), (0, 0, 0.08), trim)
    block((width, depth, eave - 0.16), (0, 0, (eave + 0.16) / 2), wall)
    if VARIANT == 3 and not shed:
        block((width + 0.8, depth + 0.8, 0.20), (0, 0, eave + 0.10), roof)
    else:
        gable(width + 0.65, depth + 0.65, eave, rise, roof)
    front = -depth / 2 - 0.045
    if shed:
        door_count = 2 if VARIANT == 2 else 1
        for index in range(door_count):
            x = (index - (door_count - 1) / 2) * 3.6 - (0.8 if door_count == 1 else 0)
            block((2.8, 0.08, 2.75), (x, front, 1.53), glazing)
            for z in (0.5, 1.1, 1.7, 2.3, 2.9):
                block((2.8, 0.07, 0.07), (x, front - 0.06, z), trim)
        block((0.90, 0.09, 2.10), (width / 2 - 1.0, front, 1.21), roof)
        for x in range(int(-width / 2) + 1, int(width / 2)):
            # Nervures larges, visibles de loin ; aucune texture.
            block((0.07, 0.09, eave - 0.2), (x, depth / 2 + 0.04, eave / 2), trim)
        for y in range(int(-depth / 2) + 1, int(depth / 2)):
            block((0.08, 0.07, eave - 0.2), (width / 2 + 0.04, y, eave / 2), trim)
    else:
        block((1.05, 0.10, 2.15), (width * 0.25, front, 1.24), glazing)
        window_positions = (-width * 0.24,) if snack else (-width * 0.32, -width * 0.10, width * 0.13)
        for x in window_positions:
            window_width = width * (0.38 if snack else 0.15)
            block((window_width + 0.16, 0.08, 1.30), (x, front, 1.75), trim)
            block((window_width, 0.07, 1.10), (x, front - 0.07, 1.75), glazing)
            if snack:
                block((window_width + 0.30, 0.42, 0.11), (x, front - 0.14, 1.16), trim)
        for y in (-depth * 0.20, depth * 0.20):
            block((0.08, 1.35, 1.20), (width / 2 + 0.045, y, 1.80), glazing)

    # Porche réellement dégagé : le toit protège un espace extérieur accessible.
    if not shed or VARIANT == 3:
        porch_depth = 2.6 if not snack else 2.2
        porch_width = width + 0.25 if not shed else width * 0.50
        porch_x = 0 if not shed else -width * 0.20
        porch_y = -depth / 2 - porch_depth / 2
        block((porch_width, porch_depth, 0.16), (porch_x, porch_y, 0.08), trim)
        porch_top = min(eave - 0.12, 3.15)
        block((porch_width + 0.4, porch_depth + 0.4, 0.16), (porch_x, porch_y, porch_top), roof)
        post_positions = (-porch_width * 0.46, 0, porch_width * 0.46) if not snack else (-porch_width * 0.44, porch_width * 0.44)
        for x in post_positions:
            block((0.16, 0.16, porch_top - 0.16), (porch_x + x, -depth / 2 - porch_depth + 0.15, porch_top / 2), trim)
        if snack and VARIANT == 2:
            for x in (-porch_width * 0.32, porch_width * 0.32):
                block((0.85, 0.36, 0.10), (x, porch_y, 0.46), trim)
                for offset in (-0.28, 0.28):
                    block((0.10, 0.26, 0.36), (x + offset, porch_y, 0.28), trim)
        if not snack and not shed and VARIANT == 1:
            for x in (-porch_width * 0.45, porch_width * 0.45):
                block((0.12, porch_depth - 0.2, 0.12), (x, porch_y, 0.95), trim)

build_building()

# Variations de proportions modestes pour les objets fabriqués.
if KIND in ('tee_marker', 'bin', 'ball_washer', 'green_mower',
            'fairway_mower', 'rough_mower', 'maintenance_shed',
            'golf_cart', 'clubhouse', 'snack_bar'):
    variation = (rng.uniform(0.94, 1.06), rng.uniform(0.95, 1.05), rng.uniform(0.97, 1.03))
    for part in parts:
        for axis in range(3):
            part.scale[axis] *= variation[axis]
            part.location[axis] *= variation[axis]

# Suppression explicite des UV éventuellement créés par les primitives.
for part in parts:
    for layer in list(part.data.uv_layers):
        part.data.uv_layers.remove(layer)

# Un seul pied à z = 0 et une emprise centrée ; aucune réduction d'échelle.
bpy.context.view_layer.update()
points = [part.matrix_world @ vertex.co for part in parts for vertex in part.data.vertices]
shift_x = (min(p.x for p in points) + max(p.x for p in points)) / 2
shift_y = (min(p.y for p in points) + max(p.y for p in points)) / 2
shift_z = min(p.z for p in points)
for part in parts:
    part.location.x -= shift_x
    part.location.y -= shift_y
    part.location.z -= shift_z
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
