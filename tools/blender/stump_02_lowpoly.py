"""Souche — Chandelle de tempête. Asset original, budget de 800 triangles.

Usage :
    python -I tools/blender/stump_02_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'stump_02'
TRIANGLE_BUDGET = 800
SEED = 1
KIND = 'stump'
VARIANT = 2
PALETTE = [('Bark', (0.32, 0.2, 0.1)), ('CutWood', (0.61, 0.44, 0.25)), ('BarkDark', (0.16, 0.12, 0.08)), ('FoliageMoss', (0.14, 0.27, 0.1))]

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
seed = int(args[0]) if len(args) > 0 else SEED
default_dir = "game/assets/trees" if bpy.app.background else os.path.expanduser("~/golferoo-assets")
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

def spruce_fan(z, radius, angle, mat):
    # Un rameau en éventail fermé : contour dentelé et dessous retombant.
    outline=[(.02,0,.65),(.45,-.75,-.1),(.9,-.45,-.6),
             (1.10,0,-.95),(.9,.45,-.6),(.45,.75,-.1)]
    local=[(.25,0,1.25),(.47,0,-.48)]+outline
    verts=[]
    width=1.1+radius*.4
    for x,y,dz in local:
        x*=radius*rng.uniform(.97,1.03);y*=width
        verts.append((x*math.cos(angle)-y*math.sin(angle),x*math.sin(angle)+y*math.cos(angle),z+dz))
    faces=[]
    for i in range(6):
        a=i+2;b=(i+1)%6+2
        faces.extend([(0,a,b),(1,b,a)])
    return mesh_part('SpruceBoughFan',verts,faces,mat)

def branch(points, radii, mat, sides=4):
    # Anneaux orientés suivant la tangente : vraie courbure de la branche.
    vertices = []
    for index, center in enumerate(points):
        a, b = points[max(0, index-1)], points[min(len(points)-1, index+1)]
        axis = [b[k]-a[k] for k in range(3)]
        norm = math.sqrt(sum(v*v for v in axis))
        axis = [v/norm for v in axis]
        ref = (0, 0, 1) if abs(axis[2]) < .9 else (0, 1, 0)
        u = [axis[1]*ref[2]-axis[2]*ref[1], axis[2]*ref[0]-axis[0]*ref[2], axis[0]*ref[1]-axis[1]*ref[0]]
        norm = math.sqrt(sum(v*v for v in u))
        u = [v/norm for v in u]
        v = [axis[1]*u[2]-axis[2]*u[1], axis[2]*u[0]-axis[0]*u[2], axis[0]*u[1]-axis[1]*u[0]]
        for side in range(sides):
            angle = math.tau*side/sides
            vertices.append(tuple(center[k]+radii[index]*(u[k]*math.cos(angle)+v[k]*math.sin(angle)) for k in range(3)))
    faces = [tuple(reversed(range(sides)))]
    for ring in range(len(points)-1):
        for side in range(sides):
            a = ring*sides+side
            b = ring*sides+(side+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(range(len(vertices)-sides,len(vertices))))
    return mesh_part('CurvedBranch',vertices,faces,mat)

def root_flare(count, radius, reach, mat):
    for index in range(count):
        angle = index*math.tau/count+rng.uniform(-.15,.15)
        c,s = math.cos(angle),math.sin(angle)
        branch([(0,0,radius*1.8),(reach*.5*c,reach*.5*s,radius*.6),(reach*c,reach*s,.08)],
               [radius*.65,radius*.5,.035],mat,4)

def leaf_blade(center, size, angle, mat, lobed=False):
    # Feuille épaisse en losange ou à lobes : six à seize triangles, sans carte alpha.
    if lobed:
        outline=[(0,-1),(.45,-.55),(.28,-.3),(.7,-.08),(.42,.12),(.62,.4),(.25,.55),(0,1),
                 (-.25,.55),(-.62,.4),(-.42,.12),(-.7,-.08),(-.28,-.3),(-.45,-.55)]
    else:
        outline=[(0,-1),(.60,-.25),(.45,.45),(0,1),(-.45,.45),(-.60,-.25)]
    verts=[center]
    for x,y in outline:
        verts.append((center[0]+size*(x*math.cos(angle)-y*math.sin(angle)),
                      center[1]+size*(x*math.sin(angle)+y*math.cos(angle)),
                      center[2]+size*(.24*abs(x)-.24*y)))
    faces=[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))]
    part=mesh_part('FoliageBlade',verts,faces,mat)
    # Les feuilles forment un pli et sont doubles faces à l'export glTF.
    mat.use_backface_culling=False
    return part

def bark_marks(x,y,z,radius,count,mat):
    for index in range(count):
        height=z+.45+index*.65
        angle=index*2.4
        c,s=math.cos(angle),math.sin(angle)
        width=radius*.8
        center=(x+c*(radius+.004),y+s*(radius+.004),height)
        verts=[(center[0]+a*s*width,center[1]-a*c*width,center[2]+b*.065)
               for a,b in [(-1,-1),(1,-.4),(.7,1),(-.7,.6)]]
        mesh_part('BarkScar',verts,[(0,1,2,3)],mat)

def hollow_shell(label, centers, radii, thickness, mat, inner_mat, cut_mat, sides=12, jagged=False):
    # Coque annulaire et fond véritable : la cavité n'est pas un disque peint.
    verts=[]
    for inner in (False,True):
        for ring,(x,y,z) in enumerate(centers):
            for i in range(sides):
                angle=math.tau*i/sides
                r=radii[ring]*(1+.065*math.sin(i*4.1))-(thickness if inner else 0)
                dz=(.14*math.sin(i*2.1)+(.35 if i%4==0 else 0)) if jagged and ring==len(centers)-1 else 0
                verts.append((x+r*math.cos(angle),y+r*math.sin(angle),z+dz))
    count=len(centers)*sides
    faces=[]
    ids=[]
    for ring in range(len(centers)-1):
        for i in range(sides):
            a=ring*sides+i;b=ring*sides+(i+1)%sides
            faces.extend([(a,b,b+sides,a+sides),(count+a+sides,count+b+sides,count+b,count+a)])
            ids.extend([0,1])
    for ring in (0,len(centers)-1):
        for i in range(sides):
            a=ring*sides+i;b=ring*sides+(i+1)%sides
            faces.append((a,count+a,count+b,b) if ring==0 else (a,b,count+b,count+a))
            ids.append(2)
    part=mesh_part(label,verts,faces,mat)
    part.data.materials.append(inner_mat);part.data.materials.append(cut_mat)
    for poly,idx in zip(part.data.polygons,ids):poly.material_index=idx
    return part
mats = []
for index, (label, rgb) in enumerate(PALETTE):
    mat = (wood, metal)[index] if index < 2 else material(label, rgb)
    mat.name = label
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    mats.append(mat)

def build_deadwood():
    global height
    bark,cut,dark,moss=mats
    height=None
    if KIND=='fallen_log':
        if VARIANT==1:
            # Gros fût fourchu et cassure éclatée ; sections ouvertes en bois clair.
            branch([(-3.8,0,.6),(-1.5,.1,.64),(1.2,0,.56),(3.4,.2,.45)], [.47,.40,.30,.16],bark,12)
            tube((-3.81,0,.6),(-3.83,0,.6),.445,cut,12)
            for i in range(7):
                a=i*math.tau/7
                start=(-3.6,.4*math.cos(a),.6+.4*math.sin(a))
                tube(start,(-4.15-rng.uniform(0,.4),start[1]*.9,start[2]+rng.uniform(-.08,.08)),.095,cut,4,.01)
            for i in range(8):
                x=-1.8+i*.63;sign=1 if i%2 else -1
                branch([(x,0,.62),(x+.35,sign*.65,.8),(x+.7,sign*(1.1+(i%3)*.2),.60)], [.13,.075,.025],bark,5)
            for i in range(6):
                x=-2.8+i*.85
                branch([(x,-.32,.5),(x+.2,-.30,.75),(x+.7,-.22,.77)],[.06,.06,.025],dark,4)
        elif VARIANT==2:
            # Déraciné : éventail vertical de racines et fût encore attaché.
            branch([(-2.7,0,.7),(-1,0,.63),(1.2,.1,.48),(3.8,.5,.33)],[.48,.40,.25,.08],bark,12)
            ico((-2.7,0,.85),(.32,1.05,.95),dark,2,.2)
            for i in range(10):
                a=i*math.tau/10
                end=(-2.9-rng.uniform(.1,.55),1.6*math.cos(a),1.1+1.0*math.sin(a))
                mid=(-2.85,end[1]*.55,.9+(end[2]-.9)*.5)
                branch([(-2.6,0,.9),mid,end],[.19,.12,.025],bark,5)
            for i in range(5):
                x=-.9+i*.9;s=(-1)**i
                branch([(x,0,.58),(x+.2,s*.5,.9),(x+.6,s*1.0,.78)],[.12,.08,.025],bark,4)
        else:
            # Tronc creux : coque traversante, ouverture brisée et champignons de mousse.
            part=hollow_shell('HollowLog',[(0,0,0),(0,0,1.4),(0,0,3.3),(0,0,5.8)], [.65,.61,.52,.44],.16,bark,dark,cut,16,True)
            part.rotation_euler[1]=math.pi/2
            part.location=(-2.9,0,.67)
            for i in range(6):
                x=-2+i*.8
                ico((x,.13,.67+(.58-i*.025)),(.4,.28,.12),moss,1,.2)
            for i in range(4):
                x=-1.8+i*1.1
                branch([(x,0,.7),(x+.25,(-1)**i*.7,.85),(x+.4,(-1)**i*1.2,.65)],[.12,.07,.02],bark,4)
    else:
        if VARIANT==1:
            # Coupe de scie : évasement racinaire, anneau d'écorce et cœur clair.
            rings('StumpFlare',[(0,0,0,.74),(.03,0,.25,.60),(.04,0,.7,.48),(.04,0,.85,.47)],bark,16,cap_mat=cut)
            root_flare(9,.31,1.4,bark)
            for i in range(10):
                a=i*math.tau/10
                branch([(.52*math.cos(a),.52*math.sin(a),.12),(.49*math.cos(a),.49*math.sin(a),.52),(.45*math.cos(a),.45*math.sin(a),.8)], [.065,.055,.022],dark,4)
            for radius in (.15,.29,.39):
                # Cernes polygonaux épais, géométriques et sans texture.
                verts=[]
                for r in (radius,radius+.05):
                    for i in range(16):
                        a=i*math.tau/16
                        verts.append((.04+r*math.cos(a),r*math.sin(a),.851))
                mesh_part('GrowthRing',verts,[(i,(i+1)%16,16+(i+1)%16,16+i) for i in range(16)],dark)
        elif VARIANT==2:
            hollow_shell('StormSplit',[(0,0,0),(.05,0,.6),(.14,0,1.3)],[.66,.49,.43],.19,bark,dark,cut,16,True)
            for i in range(7):
                a=i*math.tau/7
                start=(.12+.37*math.cos(a),.37*math.sin(a),1.1)
                branch([start,(start[0]+.1,start[1],1.5),(.15+.36*math.cos(a),.36*math.sin(a),1.8+rng.uniform(-.2,.4))],[.12,.085,.012],cut,4)
            root_flare(8,.29,1.3,bark)
            rings('RottenFloor',[(0,0,.1,.42),(0,0,.16,.42)],dark,12)
        else:
            hollow_shell('OldHollow',[(0,0,0),(.06,0,.25),(.1,0,.6)],[.80,.61,.54],.19,bark,dark,cut,16,True)
            root_flare(8,.28,1.5,bark)
            rings('RottenFloor',[(0,0,.08,.55),(0,0,.14,.54)],dark,12)
            for i in range(8):
                a=i*math.tau/8
                ico((.54*math.cos(a)+.1,.54*math.sin(a),.55+.05*(i%3)),(.26,.22,.12),moss,1,.2)

build_deadwood()

# Pose, inclinaison légère et hauteur réelle ; aucun ajustement d'échelle côté jeu.
bpy.context.view_layer.update()
points = [p.matrix_world @ v.co for p in parts for v in p.data.vertices]
bottom = min(v.z for v in points)
top = max(v.z for v in points)
z_scale = height / (top-bottom) if height is not None else rng.uniform(.94,1.06)
lean_x = rng.uniform(-.024,.024) + (.075 if KIND == 'conifer' and VARIANT == 3 else 0)
lean_y = rng.uniform(-.018,.018)
world_points = []
for part in parts:
    for vertex in part.data.vertices:
        co = part.matrix_world @ vertex.co
        co.z = (co.z-bottom)*z_scale
        co.x += lean_x*co.z
        co.y += lean_y*co.z
        vertex.co = co
        world_points.append(vertex.co.copy())
    part.location = (0,0,0)
    part.rotation_euler = (0,0,0)
    part.scale = (1,1,1)
    while part.data.uv_layers:
        part.data.uv_layers.remove(part.data.uv_layers[0])
center_x = (min(p.x for p in world_points)+max(p.x for p in world_points))/2
center_y = (min(p.y for p in world_points)+max(p.y for p in world_points))/2
for part in parts:
    for vertex in part.data.vertices:
        vertex.co.x -= center_x
        vertex.co.y -= center_y
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
