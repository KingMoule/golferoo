"""Abri de frappe — Charpente bois · quatre postes. Modèle original Golferoo à échelle réelle.

Usage :
    python -I tools/blender/range_shelter_01_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'range_shelter_01'
FAMILY = 'buildings'
TRIANGLE_BUDGET = 3000
SEED = 1
KIND = 'range_shelter'
VARIANT = 1
PALETTE = [('TimberWall', (0.32, 0.2, 0.1)), ('Roof', (0.14, 0.18, 0.17)), ('Timber', (0.4, 0.26, 0.14)), ('GlazingOpaque', (0.1, 0.2, 0.23)), ('Stone', (0.5, 0.47, 0.39)), ('HittingGreen', (0.1, 0.32, 0.1))]

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


mats=[]
for index,(label,rgb) in enumerate(PALETTE):
    mat=(wood,metal)[index] if index<2 else material(label,rgb)
    mat.name=label
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(*rgb,1)
    mats.append(mat)

wall, roof_mat, trim, glass = mats[:4]

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
stone=mats[4] if len(mats)>4 else wall
green=mats[5] if len(mats)>5 else glass

def named_block(label,size,center,mat):
    o=block(size,center,mat);o.name=label
    return o

def plinth(w,d,h=.16,center=(0,0)):
    block((w,d,h),(*center,h/2),stone)

def door(x,y,z=.16,width=1.2,height=2.35):
    window_bank((x,y,z+height/2),width,height,1,transom=False)
    block((.08,.12,.36),(x+width*.30,y-.18,z+height*.52),trim)

def cornice(w,d,z,center=(0,0),mat=None):
    x,y=center
    for yy in (-d/2,d/2):block((w,.16,.18),(x,y+yy,z),mat or trim)
    for xx in (-w/2,w/2):block((.16,d,.18),(x+xx,y,z),mat or trim)

def timber_face(w,y,bottom,top,center=0,spacing=.85):
    count=round(w/spacing)
    for i in range(count+1):
        block((.09,.10,top-bottom),(center-w/2+w*i/count,y,(top+bottom)/2),trim)
    for z in (bottom,top):block((w,.14,.14),(center,y-.02,z),trim)

def column(x,y,base,height):
    block((.40,.40,.16),(x,y,base+.08),stone)
    tube((x,y,base+.16),(x,y,base+height-.15),.14,wall,6,.12)
    block((.39,.39,.15),(x,y,base+height-.075),wall)

def chimney(x,y,bottom,top):
    block((.65,.75,top-bottom),(x,y,(top+bottom)/2),stone)
    block((.85,.94,.18),(x,y,top-.09),roof_mat)
    block((.43,.08,.20),(x,y-.40,top-.36),glass)

def roof_truss(x,y,width,eave,rise):
    tube((x-width/2,y,eave),(x+width/2,y,eave),.08,trim,4)
    for sign in (-1,1):tube((x+sign*width/2,y,eave),(x,y,eave+rise),.08,trim,4)
    tube((x,y,eave),(x,y,eave+rise),.07,trim,4)

def parapet(w,d,z,center=(0,0),height=.26):
    x,y=center
    for yy in (-d/2+.08,d/2-.08):block((w,.16,height),(x,y+yy,z+height/2),wall)
    for xx in (-w/2+.08,w/2-.08):block((.16,d,height),(x+xx,y,z+height/2),wall)

def louver_wall(x,y,z,count,height,side=False):
    for i in range(count):
        if side:block((.18,.10,height),(x,y+i*.32,z),trim)
        else:block((.10,.18,height),(x+i*.32,y,z),trim)

def face_intersects_box(poly,lo,hi):
    # Découpe une face par les six plans d'un volume libre, en coordonnées réelles.
    polygon=[tuple(p) for p in poly]
    for axis in range(3):
        for boundary,sign in ((lo[axis],1),(hi[axis],-1)):
            source=polygon;polygon=[]
            if not source:return False
            for a,b in zip(source,source[1:]+source[:1]):
                da=(a[axis]-boundary)*sign;db=(b[axis]-boundary)*sign
                if da>=0:polygon.append(a)
                if (da>=0)!=(db>=0):
                    t=da/(da-db)
                    polygon.append(tuple(a[k]+t*(b[k]-a[k]) for k in range(3)))
    return bool(polygon)

def check_bays():
    bpy.context.view_layer.update()
    for index,bay in enumerate(bays,1):
        if bay['width']<2.5 or bay['height']<2.3:
            sys.exit(f"ERREUR : baie {index}, passage {bay['width']:.2f} × {bay['height']:.2f} m insuffisant")
        x=bay['center_x'];front=bay['front_y']
        lo=(x-bay['width']/2+.0001,front-.30,.0001)
        hi=(x+bay['width']/2-.0001,front+2.75,bay['height']-.0001)
        for part in parts:
            for face in part.data.polygons:
                points=[part.matrix_world @ part.data.vertices[v].co for v in face.vertices]
                if face_intersects_box(points,lo,hi):
                    sys.exit(f'ERREUR : baie {index} obstruée par {part.name}')
        bay['geometry_clear']=True
def build_range():
    global stall_count,stall_width,slab_height,shooting_direction
    stall_count=VARIANT+3;stall_width=3.0;slab_height=.12;shooting_direction=(0,-1,0)
    roof_variation=rng.uniform(-.08,.08)
    w=stall_count*stall_width;d=(4.4,4.7,4.9)[VARIANT-1]
    plinth(w,d,slab_height)
    for i in range(stall_count):
        x=-w/2+1.5+i*3
        named_block('HittingMat',(1.45,1.75,.025),(x,-.35,slab_height+.0125),green)
        # Les séparations s'arrêtent avant le bord de frappe, à 0,82 m du sol.
        if i<stall_count-1:
            block((.10,2.5,.65),(x+1.5,.65,slab_height+.325),trim if VARIANT==1 else wall)
            block((.17,2.6,.09),(x+1.5,.65,slab_height+.695),roof_mat)
        block((.55,.55,.06),(x+1.05,-.45,.17),stone)
    post_height=(2.60,2.62,2.72)[VARIANT-1]
    for i in range(stall_count+1):
        x=-w/2+.22 if i==0 else w/2-.22 if i==stall_count else -w/2+i*3
        # Poteaux derrière les tapis : aucune traverse ne barre la façade -Y.
        y=.95
        if VARIANT==2:column(x,y,.12,post_height-.12)
        else:block((.16,.16,post_height-.12),(x,y,(post_height+.12)/2),trim)
        block((.16,.16,post_height-.12),(x,d/2-.20,(post_height+.12)/2),trim)
    if VARIANT==1:
        open_gable(w,d,2.65,.60+roof_variation,closed_ends=False)
        for x in (-w/2+.2,0,w/2-.2):
            # Arbalétriers dans la profondeur, avec très large débord vers le tir.
            tube((x,-d/2+.1,2.65),(x,0,3.20),.08,trim,4)
        block((w,.18,.19),(0,.95,2.52),trim)
        block((w,.18,.19),(0,d/2-.20,2.52),trim)
    elif VARIANT==2:
        hipped_roof(w,d,2.65,.68+roof_variation,ridge_fraction=.78)
        for y in (.95,d/2-.20):block((w,.22,.22),(0,y,2.55),stone)
        for i in range(stall_count):
            x=-w/2+(i+.5)*3
            block((1.5,.09,.34),(x,d/2-.15,2.05),wall)
    else:
        # Trois toits mono-pente distincts couvrent deux postes chacun.
        for i in range(3):
            x=-w/2+3+i*6
            slope_roof(6.0,d,3.18+roof_variation,2.86,(x,0))
            block((6,.13,.18),(x,.95,2.69),trim)
            block((6,.13,.18),(x,d/2-.20,2.69),trim)
            block((.10,d,.16),(x+2.85,0,3.00),trim)
    # Bancs et fond ajouré au dos, jamais de mur devant les frappeurs.
    for i in range(stall_count):
        x=-w/2+1.5+i*3
        block((1.65,.32,.10),(x,d/2-.48,.55),trim)
        for xx in (x-.6,x+.6):block((.10,.25,.38),(xx,d/2-.48,.31),roof_mat)
    block((w-.20,.10,.42),(0,d/2-.10,.45),wall)

build_range()

# Coordonnées finales, UV retirés et origine centrée au niveau du sol.
bpy.context.view_layer.update()
for part in parts:
    for v in part.data.vertices:v.co=part.matrix_world @ v.co
    part.location=(0,0,0);part.rotation_euler=(0,0,0);part.scale=(1,1,1)
    for layer in list(part.data.uv_layers):part.data.uv_layers.remove(layer)
points=[v.co for p in parts for v in p.data.vertices]
shift=((min(v.x for v in points)+max(v.x for v in points))/2,
       (min(v.y for v in points)+max(v.y for v in points))/2,min(v.z for v in points))
for p in parts:
    for v in p.data.vertices:v.co.x-=shift[0];v.co.y-=shift[1];v.co.z-=shift[2]
if KIND=='cart_garage':
    for bay in bays:bay['center_x']-=shift[0];bay['front_y']-=shift[1]
    check_bays()
points=[v.co for p in parts for v in p.data.vertices]
preview_target=tuple((min(v[k] for v in points)+max(v[k] for v in points))/2 for k in range(3))

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
    bpy.ops.object.empty_add(location=preview_target)
    target = bpy.context.object
    d = max(dims) * 1.6
    bpy.ops.object.camera_add(location=(preview_target[0]+d, preview_target[1]-d, preview_target[2]+d*1.05))
    cam = bpy.context.object
    cam.constraints.new("TRACK_TO").target = target
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(dims) * 1.55
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
