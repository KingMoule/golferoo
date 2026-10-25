"""Tondeuse de rough — Trois plateaux flottants. Asset low-poly original de Golferoo.

Usage :
    python -I tools/blender/rough_mower_02_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'rough_mower_02'
TRIANGLE_BUDGET = 800
SEED = 1
KIND = 'rough_mower'
VARIANT = 2
PALETTE = [('Paint', (0.17, 0.32, 0.2)), ('Rubber', (0.07, 0.08, 0.07)), ('Stone', (0.5, 0.5, 0.48)), ('Seat', (0.55, 0.45, 0.3))]

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
seed = int(args[0]) if len(args) > 0 else SEED
default_dir = "game/assets/maintenance" if bpy.app.background else os.path.expanduser("~/golferoo-assets")
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

def build_mower():
    paint, dark, steel, seat_mat = mats
    if KIND == 'green_mower' and VARIANT != 2:
        width = 0.72 if VARIANT == 1 else 0.90
        block((width, 0.62, 0.18), (0, 0, 0.36), paint)
        for x in (-width / 2, width / 2):
            wheel(x, 0.12, 0.20, 0.10, dark, steel)
        tube((-width / 2, -0.32, 0.16), (width / 2, -0.32, 0.16), 0.14, steel, 10)
        block((width * 0.95, 0.40, 0.24), (0, -0.59, 0.23), dark)
        for x in (-0.27, 0.27):
            tube((x, 0.15, 0.40), (x, 0.75, 1.05), 0.035, dark, 6)
        tube((-0.30, 0.75, 1.05), (0.30, 0.75, 1.05), 0.045, dark, 6)
        block((0.29, 0.24, 0.18), (0, 0.13, 0.54), paint if VARIANT == 1 else steel)
        if VARIANT == 3:
            block((0.33, 0.15, 0.16), (0, -0.03, 0.69), dark)
        return

    rough = KIND == 'rough_mower'
    fairway = KIND == 'fairway_mower'
    radius = 0.36 if rough else 0.30
    track = 0.68 if rough or fairway else 0.54
    for x in (-track, track):
        for y in (-0.50, 0.70):
            wheel(x, y, radius, 0.23, dark, steel)
    block((1.12, 1.85, 0.20), (0, 0.10, 0.45), dark)
    block((0.95, 0.70, 0.45), (0, 0.72, 0.76), paint)
    block((0.95, 0.55, 0.12), (0, -0.65, 0.60), paint)
    block((0.50, 0.43, 0.13), (0, 0.08, 0.91), seat_mat)
    block((0.50, 0.12, 0.43), (0, 0.32, 1.12), seat_mat)
    steering(0, -0.40, 1.18, dark)
    for x in (-0.48, 0.48):
        tube((x, 0.52, 0.65), (x, 0.52, 1.80), 0.045, dark, 4)
    tube((-0.48, 0.52, 1.80), (0.48, 0.52, 1.80), 0.045, dark, 4)
    if VARIANT == 3:
        block((1.25, 1.25, 0.10), (0, 0.13, 1.90), paint)

    if rough:
        if VARIANT == 1:
            block((1.95, 0.95, 0.20), (0, -1.15, 0.24), paint)
            positions = [(-0.65, -1.3), (0, -1.3), (0.65, -1.3)]
            for x, y in positions:
                tube((x, y, 0.15), (x, y, 0.32), 0.35, paint, 8)
        else:
            for index, (x, y) in enumerate(((-1.03, -0.95), (0, -1.33), (1.03, -0.95))):
                deck = tube((x, y, 0.13), (x, y, 0.32), 0.55, paint, 10)
                if VARIANT == 3 and index != 1:
                    # Plateaux latéraux relevés, silhouette de transport.
                    sign = 1 if x > 0 else -1
                    hinge = sign * 0.50
                    angle = math.radians(55)
                    for vertex in deck.data.vertices:
                        dx, dz = vertex.co.x - hinge, vertex.co.z - 0.24
                        vertex.co.x = hinge + dx * math.cos(angle) - sign * dz * math.sin(angle)
                        vertex.co.z = 0.24 + sign * dx * math.sin(angle) + dz * math.cos(angle)
                tube((0, -0.35, 0.47), (x, y, 0.35), 0.055, dark, 4)
    else:
        count = 3 if not fairway or VARIANT == 2 else 5
        positions = [(-0.72, -1.10), (0.72, -1.10), (0, -0.45)]
        if count == 5:
            positions = [(-1.05, -1.10), (0, -1.25), (1.05, -1.10), (-0.65, 0.25), (0.65, 0.25)]
        for x, y in positions:
            tube((x - 0.34, y, 0.19), (x + 0.34, y, 0.19), 0.15, dark, 6)
            # Deux arêtes métalliques rendent le cylindre de coupe lisible de loin.
            block((0.60, 0.065, 0.065), (x, y - 0.125, 0.22), steel)
            block((0.60, 0.065, 0.065), (x, y + 0.01, 0.33), steel)
            for offset in (-0.33, 0.33):
                block((0.06, 0.42, 0.25), (x + offset, y, 0.24), paint)
            tube((0, -0.15, 0.50), (x, y, 0.32), 0.05, dark, 4)

build_mower()

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
