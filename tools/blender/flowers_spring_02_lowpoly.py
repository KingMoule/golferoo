"""Petite touffe de printemps — Trois petites clochettes. Asset original Golferoo, échelle réelle.

Usage :
    python -I tools/blender/flowers_spring_02_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'flowers_spring_02'
FAMILY = 'bushes'
TRIANGLE_BUDGET = 150
SEED = 1
KIND = 'flowers_spring'
VARIANT = 2
ORIGIN_MODE = 'ground'
PALETTE = [('FoliageDark', (0.1, 0.32, 0.1)), ('Blossom', (0.93, 0.93, 0.93))]

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


def splinter(start,end,radius,mat):
    axis=[end[k]-start[k] for k in range(3)]
    norm=math.sqrt(sum(v*v for v in axis));axis=[v/norm for v in axis]
    ref=(0,0,1) if abs(axis[2])<.9 else (0,1,0)
    u=[axis[1]*ref[2]-axis[2]*ref[1],axis[2]*ref[0]-axis[0]*ref[2],axis[0]*ref[1]-axis[1]*ref[0]]
    norm=math.sqrt(sum(v*v for v in u));u=[v/norm for v in u]
    v=[axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]]
    vertices=[tuple(start[k]+radius*(math.cos(a)*u[k]+math.sin(a)*v[k]) for k in range(3)) for a in (0,math.pi/2,math.pi,3*math.pi/2)]
    vertices.append(end)
    return mesh_part('WoodSplinter',vertices,[(3,2,1,0)]+[(i,(i+1)%4,4) for i in range(4)],mat)

def hoop(x,radius,mat,sides=10):
    vertices=[]
    for dx,r in [(-.03,radius+.025),(-.03,radius-.025),(.03,radius-.025),(.03,radius+.025)]:
        for i in range(sides):
            a=i*math.tau/sides
            vertices.append((x+dx,r*math.cos(a),r*math.sin(a)))
    faces=[]
    for band in range(4):
        for i in range(sides):
            a=band*sides+i;b=band*sides+(i+1)%sides
            c=((band+1)%4)*sides+(i+1)%sides;d=((band+1)%4)*sides+i
            faces.append((a,b,c,d))
    return mesh_part('AttachmentHoop',vertices,faces,mat)

def leaf(center,angle,length,width,mat):
    c,s=math.cos(angle),math.sin(angle)
    x,y,z=center
    vertices=[(x,y,z),(x+length*.55*c-width*s,y+length*.55*s+width*c,z+.035),
              (x+length*c,y+length*s,z+.08),(x+length*.55*c+width*s,y+length*.55*s-width*c,z+.035)]
    return mesh_part('Leaf',vertices,[(0,1,2,3)],mat)

def corolla(center,radius,mat,shape='star'):
    x,y,z=center
    if shape=='bell':
        n=4;verts=[(x,y,z+.04)]
        for r,dz in ((radius*.38,.025),(radius,-.065)):
            for i in range(n):
                a=i*math.tau/n
                verts.append((x+r*math.cos(a),y+r*math.sin(a),z+dz))
        faces=[(0,1+i,1+(i+1)%n) for i in range(n)]
        faces += [(1+i,5+i,5+(i+1)%n,1+(i+1)%n) for i in range(n)]
    else:
        n=8 if shape=='star' else 10
        verts=[(x,y,z+.025)]
        for i in range(n):
            a=i*math.tau/n
            r=radius*(1 if i%2==0 else (.38 if shape=='star' else .72))
            verts.append((x+r*math.cos(a),y+r*math.sin(a),z))
        faces=[(0,1+i,1+(i+1)%n) for i in range(n)]
    return mesh_part('BlossomCorolla',verts,faces,mat)

def bench_at(x,y,length,wood_mat,frame_mat,turn=False):
    before=len(parts)
    for i in range(3):block((length,.11,.055),(0,-.13+i*.13,.46),wood_mat)
    for i in range(2):block((length,.05,.12),(0,.18,.67+i*.16),wood_mat)
    for sx in (-length*.36,length*.36):
        block((.08,.40,.08),(sx,0,.38),frame_mat)
        block((.08,.08,.39),(sx,-.13,.195),frame_mat)
        block((.08,.08,.84),(sx,.18,.42),frame_mat)
    for part in parts[before:]:
        if turn:
            part.location.x,part.location.y=-part.location.y,part.location.x
            part.rotation_euler[2]+=math.pi/2
        part.location.x+=x;part.location.y+=y
mats=[]
for index,(label,rgb) in enumerate(PALETTE):
    mat=(wood,metal)[index] if index<2 else material(label,rgb)
    mat.name=label
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(*rgb,1)
    mats.append(mat)

def build_flowers():
    foliage,blossom=mats
    light=KIND=='flowers_spring'
    count=(3,3,4)[VARIANT-1] if light else (5,5,6)[VARIANT-1]
    spread=(.23,.22,.27)[VARIANT-1] if light else (.31,.33,.37)[VARIANT-1]
    height=(.30,.33,.23)[VARIANT-1] if light else (.49,.51,.35)[VARIANT-1]
    shape=('star','bell','daisy')[VARIANT-1]
    for i in range(count):
        angle=i*math.tau/count+.20
        r=spread*(.7 if i==0 else 1)*rng.uniform(.90,1.05)
        x,y=r*math.cos(angle),r*math.sin(angle)
        z=height*rng.uniform(.82,1.0)
        tube((x*.80,y*.80,0),(x,y,z),.009,foliage,3,.006)
        corolla((x,y,z),(.082 if light else .095)*rng.uniform(.95,1.06),blossom,shape)
        leaf((x*.86,y*.86,z*.30),angle+1.0,.12,.026,foliage)
        if shape!='daisy':leaf((x*.92,y*.92,z*.53),angle-1.0,.11,.024,foliage)

build_flowers()


# Coordonnées finales et retrait des UV éventuellement créés par une primitive.
bpy.context.view_layer.update()
for part in parts:
    for v in part.data.vertices:v.co=part.matrix_world @ v.co
    part.location=(0,0,0);part.rotation_euler=(0,0,0);part.scale=(1,1,1)
    for layer in list(part.data.uv_layers):part.data.uv_layers.remove(layer)
points=[v.co for p in parts for v in p.data.vertices]
if ORIGIN_MODE=='ground':
    shift=((min(v.x for v in points)+max(v.x for v in points))/2,
           (min(v.y for v in points)+max(v.y for v in points))/2,min(v.z for v in points))
    for p in parts:
        for v in p.data.vertices:v.co.x-=shift[0];v.co.y-=shift[1];v.co.z-=shift[2]
points=[v.co for p in parts for v in p.data.vertices]
preview_target=tuple((min(v[k] for v in points)+max(v[k] for v in points))/2 for k in range(3))
asset_extra=f' attach_z={attachment_height:.6f}m' if KIND.startswith('windsock') else ''

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
if ORIGIN_MODE not in ("ring", "hand") and abs(low) > 0.01:
    sys.exit(f"ERREUR : le point le plus bas est à z = {low:.3f}, il doit être à 0")

bpy.ops.export_scene.gltf(filepath=glb_path, export_format="GLB", use_selection=True, export_apply=True)

if png_path:
    bpy.ops.object.empty_add(location=preview_target)
    target = bpy.context.object
    d = max(dims) * 1.6
    bpy.ops.object.camera_add(location=(preview_target[0]+d, preview_target[1]-d, preview_target[2]+d*1.05))
    cam = bpy.context.object
    cam.constraints.new("TRACK_TO").target = target
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(dims) * (1.65 if KIND == "rain_shelter" else 1.4)
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
      f"{dims.x:.2f} x {dims.y:.2f} x {dims.z:.2f} m {time.time() - t0:.2f} s{asset_extra}")
