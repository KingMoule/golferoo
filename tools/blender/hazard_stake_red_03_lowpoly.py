"""Piquet rouge : Fût à huit facettes. Asset Golferoo original, échelle réelle.

Usage :
    python -I tools/blender/hazard_stake_red_03_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'hazard_stake_red_03'
FAMILY = 'furniture'
TRIANGLE_BUDGET = 300
SEED = 1
KIND = 'hazard_stake_red'
VARIANT = 3
PALETTE = [('MarkerColor', (0.9, 0.15, 0.12)), ('MetalDark', (0.18, 0.19, 0.2)), ('Wood', (0.45, 0.3, 0.16)), ('Stone', (0.5, 0.5, 0.48))]

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
seed = int(args[0]) if len(args) > 0 else SEED
default_dir = f"game/assets/{FAMILY}" if bpy.app.background else os.path.join(os.path.expanduser("~/golferoo-assets"), FAMILY)
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
deck_parts=[]
deck_tops=[]

def xz_prism(label,outline,depth,mat,center_y=0,deck=False):
    count=len(outline)
    vertices=[(x,y,z) for y in (center_y-depth/2,center_y+depth/2) for x,z in outline]
    faces=[tuple(range(count)),tuple(reversed(range(count,2*count)))]
    for index in range(count):
        other=(index+1)%count
        faces.append((index,index+count,other+count,other))
    part=mesh_part(label,vertices,faces,mat)
    if deck:
        deck_parts.append(part)
        ids=[index for index,(x,y,z) in enumerate(vertices) if abs(z-0.15*x)<1e-6]
        deck_tops.append((part,ids))
    return part

def ramp_piece(label,x0,x1,width,center_y,lower,upper,mat,deck=False):
    low0=max(0,0.15*x0+lower)
    low1=max(0,0.15*x1+lower)
    outline=[(x0,low0),(x1,low1),(x1,0.15*x1+upper),(x0,0.15*x0+upper)]
    outline=[point for index,point in enumerate(outline) if point!=outline[index-1]]
    return xz_prism(label,outline,width,mat,center_y,deck)

def deck_block(size,location,mat):
    part=block(size,location,mat)
    deck_parts.append(part)
    deck_tops.append((part,[v.index for v in part.data.vertices if v.co.z>0]))
    return part

def pyramidal_cap(x,y,z,width,height,mat):
    r=width/2
    return mesh_part('PyramidalCap',[(x-r,y-r,z),(x+r,y-r,z),(x+r,y+r,z),(x-r,y+r,z),(x,y,z+height)],
                     [(3,2,1,0),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],mat)

def picket(x,width,height,depth,mat,center_y=0,point_height=0.10):
    return xz_prism('Picket',[(x-width/2,0),(x+width/2,0),
                    (x+width/2,height-point_height),(x,height),
                    (x-width/2,height-point_height)],depth,mat,center_y)

def eyelet(center,radius,tube_radius,mat,segments=10):
    # Anneau dans le plan YZ : une corde peut le traverser suivant X.
    x,y,z=center
    vertices=[]
    for index in range(segments):
        theta=math.tau*index/segments
        for side in range(4):
            phi=math.tau*side/4
            r=radius+tube_radius*math.cos(phi)
            vertices.append((x+tube_radius*math.sin(phi),y+r*math.cos(theta),z+r*math.sin(theta)))
    faces=[]
    for index in range(segments):
        for side in range(4):
            other=(index+1)%segments
            faces.append((index*4+side,other*4+side,other*4+(side+1)%4,index*4+(side+1)%4))
    return mesh_part('Eyelet',vertices,faces,mat)

def sign_panel(width,height,location,body,face,angle=0):
    part=block((width,0.065,height),location,body,(-angle,0,0))
    part.name='BlankSignPanel'
    part.data.materials.append(face)
    for polygon in part.data.polygons:
        if polygon.normal.y < -0.9:
            polygon.material_index=1
    return part

def fence_post(x):
    primary,secondary,metal_mat,_=mats
    # Même premier tirage dans la clôture et son poteau terminal.
    height=1.14+rng.uniform(-0.02,0.02)
    cap_height=rng.uniform(0.07,0.11)
    if VARIANT==2:
        tube((x,0,0),(x,0,height-0.07),0.07,primary,6)
        rings('PostPoint',[(x,0,height-0.07,0.07)],primary,6,tip=(x,0,height))
    else:
        block((0.14,0.14,height-cap_height),(x,0,(height-cap_height)/2),primary)
        pyramidal_cap(x,0,height-cap_height,0.14,cap_height,primary)
    return height
mats = []
for index, (label, rgb) in enumerate(PALETTE):
    mat = (wood, metal)[index] if index < 2 else material(label, rgb)
    mat.name = label
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    mats.append(mat)

def build_stake():
    marker=mats[0]
    shoe=rng.uniform(0.07,0.14)
    if VARIANT==1:
        block((0.05,0.05,1.0-shoe),(0,0,(1.0+shoe)/2),marker)
        mesh_part('PointedFoot',[(-0.025,-0.025,shoe),(0.025,-0.025,shoe),
                  (0.025,0.025,shoe),(-0.025,0.025,shoe),(0,0,0)],
                  [(0,1,2,3),(0,4,1),(1,4,2),(2,4,3),(3,4,0)],marker)
    elif VARIANT==2:
        block((0.075,0.075,0.035),(0,0,0.0175),marker)
        block((0.05,0.05,0.965),(0,0,0.5175),marker)
        # Le socle est plus large ; le corps conserve sa section de 5 cm.
        cut=rng.uniform(0.025,0.065)
        rings('Collar',[(0,0,0.035,0.035),(0,0,0.035+cut,0.025)],marker,4)
    else:
        rings('FacetedStake',[(0,0,0,0.012),(0,0,shoe,0.025),
              (0,0,0.94,0.025),(0,0,1.0,0.019+rng.uniform(0,0.003))],marker,8)

build_stake()

# Les modules gardent leur repère contractuel ; seule la culée utilise X = 0..2.
if KIND not in ('fence_segment','fence_end_post','bridge_span','bridge_abutment'):
    bpy.context.view_layer.update()
    points = [part.matrix_world @ v.co for part in parts for v in part.data.vertices]
    offset = ((min(p.x for p in points)+max(p.x for p in points))/2,
              (min(p.y for p in points)+max(p.y for p in points))/2,
              min(p.z for p in points))
    for part in parts:
        for axis in range(3):
            part.location[axis] -= offset[axis]
for part in parts:
    for layer in list(part.data.uv_layers):
        part.data.uv_layers.remove(layer)

# Vérifications des raccords avant la jonction et l'export.
bpy.context.view_layer.update()
points = [part.matrix_world @ v.co for part in parts for v in part.data.vertices]
def require_close(value, expected, label):
    if abs(value-expected)>0.01:
        sys.exit(f"ERREUR : raccord {label} = {value:.4f}, attendu {expected:.4f} m")
if KIND=='fence_segment':
    require_close(min(p.x for p in points),-1.25,'clôture X min')
    require_close(max(p.x for p in points),1.25,'clôture X max')
    require_close(max(p.x for p in points)-min(p.x for p in points),2.50,'longueur clôture')
    if not 1.0<=max(p.z for p in points)<=1.2:
        sys.exit('ERREUR : hauteur de clôture hors de 1,0 à 1,2 m')
if KIND=='fence_end_post':
    require_close(min(p.x for p in points),-0.07,'poteau X min')
    require_close(max(p.x for p in points),0.07,'poteau X max')
if KIND in ('bridge_span','bridge_abutment'):
    x_min=-2.0 if KIND=='bridge_span' else 0.0
    require_close(min(p.x for p in points),x_min,'pont X min')
    require_close(max(p.x for p in points),2.0,'pont X max')
    require_close(min(p.z for p in points),0.0,'pont Z min')
    tops=[part.matrix_world @ part.data.vertices[index].co
          for part,indices in deck_tops for index in indices]
    require_close(min(p.x for p in tops),x_min,'tablier X min')
    require_close(max(p.x for p in tops),2.0,'tablier X max')
    require_close(min(p.y for p in tops),-1.30,'tablier Y min')
    require_close(max(p.y for p in tops),1.30,'tablier Y max')
    for point in tops:
        expected=0.30 if KIND=='bridge_span' else 0.15*point.x
        require_close(point.z,expected,'altitude du tablier')
    require_close(max(p.z for p in points),1.30,'garde-corps à la jonction')

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
    preview_x = 1.0 if KIND == "bridge_abutment" else 0.0
    bpy.ops.object.empty_add(location=(preview_x, 0, dims.z / 2))
    target = bpy.context.object
    d = max(dims) * 1.6
    bpy.ops.object.camera_add(location=(preview_x + d, -d, d * 1.05))
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
