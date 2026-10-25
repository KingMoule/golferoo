"""Pavillon contemporain en L. Club-house low-poly original à échelle réelle.

Usage :
    python -I tools/blender/clubhouse_modern_v2_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'clubhouse_modern_v2'
TRIANGLE_BUDGET = 1500
SEED = 1
PALETTE = [('Concrete', (0.72, 0.72, 0.66)), ('Roof', (0.15, 0.2, 0.19)), ('Timber', (0.4, 0.26, 0.14)), ('GlazingOpaque', (0.1, 0.2, 0.23))]

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

wall, roof_mat, trim, glass = mats

def window_bank(center, width, height, panes, side=False, transom=True):
    x,y,z=center
    if side:
        block((0.08,width,height),(x,y,z),glass)
        for index in range(panes+1):
            block((0.12,0.10,height+0.16),(x+0.08,y-width/2+width*index/panes,z),trim)
        for offset in (-height/2,height/2):
            block((0.12,width+0.10,0.10),(x+0.08,y,z+offset),trim)
        if transom:
            block((0.12,width,0.10),(x+0.08,y,z+height*0.22),trim)
    else:
        block((width,0.08,height),(x,y,z),glass)
        for index in range(panes+1):
            block((0.10,0.12,height+0.16),(x-width/2+width*index/panes,y-0.08,z),trim)
        for offset in (-height/2,height/2):
            block((width+0.10,0.12,0.10),(x,y-0.08,z+offset),trim)
        if transom:
            block((width,0.12,0.10),(x,y-0.08,z+height*0.22),trim)

def hipped_roof(width,depth,eave,rise,center=(0,0),ridge_fraction=0.50):
    x,y=center
    vertices=[(x-width/2,y-depth/2,eave),(x+width/2,y-depth/2,eave),
              (x+width/2,y+depth/2,eave),(x-width/2,y+depth/2,eave),
              (x-width*ridge_fraction/2,y,eave+rise),(x+width*ridge_fraction/2,y,eave+rise)]
    return mesh_part('HippedRoof',vertices,[(3,2,1,0),(0,1,5,4),(1,2,5),(2,3,4,5),(3,0,4)],roof_mat)

def slope_roof(width,depth,low,high,center):
    x,y=center
    vertices=[]
    for dz in (-0.13,0.13):
        vertices.extend(((x-width/2,y-depth/2,low+dz),(x+width/2,y-depth/2,low+dz),
                         (x+width/2,y+depth/2,high+dz),(x-width/2,y+depth/2,high+dz)))
    return mesh_part('SlopingCanopy',vertices,[(3,2,1,0),(4,5,6,7),(0,1,5,4),
                     (1,2,6,5),(2,3,7,6),(3,0,4,7)],roof_mat)

def open_gable(width,depth,eave,rise,center=(0,0),closed_ends=False):
    x,y=center
    for sign in (-1,1):
        vertices=[]
        for dz in (-0.10,0.10):
            vertices.extend(((x,y-depth/2,eave+rise+dz),(x+sign*width/2,y-depth/2,eave+dz),
                             (x+sign*width/2,y+depth/2,eave+dz),(x,y+depth/2,eave+rise+dz)))
        faces=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
        if sign<0:
            faces=[tuple(reversed(face)) for face in faces]
        mesh_part('RoofPanel',vertices,faces,roof_mat)
    if closed_ends:
        mesh_part('GableWalls',[(x-width/2,y-depth/2,eave),(x+width/2,y-depth/2,eave),
                  (x,y-depth/2,eave+rise),(x-width/2,y+depth/2,eave),
                  (x+width/2,y+depth/2,eave),(x,y+depth/2,eave+rise)],
                  [(0,1,2),(5,4,3)],wall)

def rail_section(start,end,balusters=4,height=1.0):
    x1,y1,z=start
    x2,y2,_=end
    for offset in (0.18,height):
        tube((x1,y1,z+offset),(x2,y2,z+offset),0.055,trim,4)
    for index in range(balusters):
        f=(index+0.5)/balusters
        block((0.075,0.075,height-0.18),(x1+(x2-x1)*f,y1+(y2-y1)*f,z+(height+0.18)/2),trim)

def steps(width,depth,rise,count,center):
    x,y=center
    for index in range(count):
        h=rise*(index+1)/count
        block((width,depth/count,h),(x,y-depth/2+depth*(index+0.5)/count,h/2),wall)

def table_set(x,y,z):
    tube((x,y,z+0.70),(x,y,z+0.78),0.64,trim,8)
    tube((x,y,z+0.08),(x,y,z+0.70),0.09,roof_mat,6)
    block((0.70,0.56,0.08),(x,y,z+0.04),roof_mat)
    for offset in (-0.85,0.85):
        block((1.1,0.31,0.08),(x,y+offset,z+0.43),trim)
        block((0.75,0.19,0.39),(x,y+offset,z+0.195),roof_mat)

def build_modern():
    # Deux ailes et un noyau de service décalé composent un plan en L.
    outline=[(-11.8,-12.5),(-5.6,-12.5),(-5.6,-8.3),(9.9,-8.3),
             (9.9,4.5),(-6.9,4.5),(-6.9,3.9),(-11.8,3.9)]
    vertices=[(x,y,z) for z in (0,0.42) for x,y in outline]
    faces=[tuple(reversed(range(8))),tuple(range(8,16))]
    for index in range(8):
        other=(index+1)%8
        faces.append((index,other,other+8,index+8))
    mesh_part('ContinuousTerrace',vertices,faces,wall)
    block((16.0,8.2,3.5),(1.5,0,2.17),wall)
    block((5.8,13.0,3.5),(-8.7,-3.0,2.17),wall)
    block((18.0,10.3,0.30),(1.8,-0.3,4.07),roof_mat)
    block((6.7,14.0,0.32),(-8.7,-3.0,4.12),roof_mat)
    block((3.0,4.0,4.8),(-7.8,1.4,2.82),trim)
    block((3.35,4.35,0.22),(-7.8,1.4,5.32),roof_mat)
    for x in (-4.4,1.0,6.4):
        block((0.08,10.3,0.15),(x,-0.3,4.30),roof_mat)
    block((17.4,0.16,0.24),(1.8,-5.40,3.90),wall)

    window_bank((1.5,-4.18,2.18),14.1,2.90,7)
    window_bank((9.56,0.1,2.18),6.8,2.90,4,side=True)
    window_bank((-8.7,-9.57,2.20),4.7,2.70,3)
    window_bank((-5.73,-3.0,2.20),11.0,2.70,5,side=True)
    # Les panneaux sont opaques, conformément au guide ; les montants donnent l'échelle.
    for index in range(14):
        y=-8.9+index*0.80
        block((0.24,0.10,3.3),(-11.72,y,2.15),trim)

    # Grand débord soutenu par deux paires de poteaux obliques.
    block((17.6,3.6,0.24),(1.5,-6.30,3.77),roof_mat)
    for x in (-5.2,8.2):
        tube((x,-7.55,0.42),(x+0.55,-7.55,3.65),0.10,trim,4)
        tube((x+1.1,-7.55,0.42),(x+0.55,-7.55,3.65),0.10,trim,4)

    # Pergola latérale et mobilier de terrasse : des volumes qui restent lisibles.
    for index in range(9):
        block((5.8,0.14,0.18),(-8.7,-12.2+index*0.30,3.35),trim)
    for x in (-11.3,-6.1):
        block((0.16,2.8,0.22),(x,-11.0,3.24),trim)
        block((0.16,0.16,2.85),(x,-12.2,1.845),trim)
    table_set(-2.7,-6.3,0.42)
    table_set(4.0,-6.3,0.42)
    steps(3.0,1.5,0.42,3,(0,-9.05))
    for x1,x2 in ((-5.8,-2.0),(2.0,9.5)):
        rail_section((x1,-8.15,0.42),(x2,-8.15,0.42),1,0.90)
    # Rampe latérale, indépendante de l'escalier central.
    vertices=[(9.9,-9.1,0),(11.9,-9.1,0),(11.9,-5.0,0),(9.9,-5.0,0),
              (9.9,-9.1,0.04),(11.9,-9.1,0.04),(11.9,-5.0,0.42),(9.9,-5.0,0.42)]
    mesh_part('AccessRamp',vertices,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],wall)
    for x in (9.95,11.85):
        tube((x,-9.0,0.95),(x,-5.1,1.33),0.045,trim,4)
        for y,z in ((-9.0,0.04),(-5.1,0.41)):
            block((0.07,0.07,0.88),(x,y,z+0.44),trim)

build_modern()

# La graine fait varier légèrement les proportions sans changer le langage architectural.
variation = (rng.uniform(0.97, 1.03), rng.uniform(0.97, 1.03), rng.uniform(0.98, 1.02))
for part in parts:
    for axis in range(3):
        part.scale[axis] *= variation[axis]
        part.location[axis] *= variation[axis]
    for layer in list(part.data.uv_layers):
        part.data.uv_layers.remove(layer)
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
