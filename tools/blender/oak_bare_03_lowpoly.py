"""Chêne mature nu — budget 800, raccord exact à oak_800_03.

Usage :
    python -I tools/blender/oak_bare_03_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'oak_bare_03'
TRIANGLE_BUDGET = 800
SEED = 1
FAMILY = "trees"
WINTER_MODE = 'bare800'
WINTER_DROP_RATIO = 0.20
SOURCE_NAME = 'oak_800_03'
KIND = 'oak'
VARIANT = 3
PALETTE = [('Bark', (0.32, 0.2, 0.1)), ('ReferenceCrownDark', (0.1, 0.32, 0.1)), ('ReferenceCrownLight', (0.2, 0.46, 0.14)), ('BarkLight', (0.4, 0.26, 0.14))]

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

def build_oak():
    global height
    bark,leaf,light,_=mats
    height=(18,20,15.5)[VARIANT-1]*rng.uniform(.85,1.15)
    if VARIANT==1:
        # Port de parc : trois étages de charpentières et couronne très large.
        branch([(0,0,0),(.10,0,3.6),(-.2,.1,7),(0,.1,11)], [.40,.32,.22,.09],bark,8)
        sites=[]
        for i in range(8):
            a=i*math.tau/8+.12
            r=(4.4,4.9,3.7,4.8,4.3,3.9,4.7,3.5)[i]
            sites.append((r*math.cos(a),r*math.sin(a),9.5+(i%3)*1.2))
        for i,(x,y,z) in enumerate(sites):
            branch([(0,0,4.1+i*.27),(x*.55,y*.55,z-2),(x,y,z)], [.20,.13,.055],bark,4)
            for j in range(3):
                ico((x+(j-1)*.8,y+(.6 if j==1 else -.3),z+(j%2)*1.3),
                    (2.2,2.0,2.1),leaf if (i+j)%3 else light,1,.14)
        ico((0,.1,13.5),(3.0,2.8,2.8),leaf,1,.13)
        root_flare(3,.35,1.1,bark)
    elif VARIANT==2:
        # Port de futaie : fût long et couronne haute en plusieurs flèches.
        branch([(0,0,0),(-.1,0,5.8),(.1,.1,10),(-.3,.1,15),(.1,0,18)], [.34,.28,.22,.13,.04],bark,8)
        for i in range(8):
            a=i*2.4;r=2.7+(i%2)*.7;z=9+i*.9
            x,y=r*math.cos(a),r*math.sin(a)
            branch([(.05,0,z-2),(x*.6,y*.6,z-.2),(x,y,z+1)], [.15,.09,.04],bark,4)
            for j in range(3):
                ico((x*.9+(.5 if j==1 else 0),y+(j-1)*.5,z+j*.85),(1.8,1.7,2.1),
                    light if i%4==0 and j==2 else leaf,1,.12)
        root_flare(3,.30,.95,bark)
    else:
        # Vétéran de lisière : tronc coudé, couronne latérale et quelques bois morts.
        branch([(0,0,0),(-.3,.1,3.5),(1.1,0,6.2),(2.7,.1,8),(4.1,.3,9)], [.40,.34,.25,.16,.06],bark,8)
        for i in range(8):
            a=i*math.tau/8
            x=2.1+4.5*math.cos(a);y=3.7*math.sin(a);z=8.5+(i%3)*1.0
            branch([(.4,0,4.7),(x*.58,y*.62,z-1.8),(x,y,z)], [.20,.12,.04],bark,4)
            for j in range(3):
                ico((x+(j-1)*.7,y+j*.22,z+j*.6),(2.0,1.85,1.65),leaf if (i+j)%3 else light,1,.15)
        for x,y,z in [(-2.5,-1,6.0),(1,-3.7,7.5)]:
            tube((.4,0,4.9),(x,y,z),.12,bark,3,.025)
        root_flare(3,.35,1.3,bark)

build_oak()

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

# --- Adaptation saisonnière, à partir du feuillu exact de même graine. ---
# Le calcul de la référence est interne : aucun script ou modèle externe à lire.
bpy.context.view_layer.update()
reference_points=[part.matrix_world @ v.co for part in parts for v in part.data.vertices]
reference_min=[min(v[k] for v in reference_points) for k in range(3)]
reference_max=[max(v[k] for v in reference_points) for k in range(3)]
reference_dimensions=[reference_max[k]-reference_min[k] for k in range(3)]
crown_parts=[]
crown_samples=[]
wood_parts=[]
wood_paths=[]
for part in parts:
    points=[part.matrix_world @ v.co for v in part.data.vertices]
    is_crown=any(m.name.startswith(('Foliage','ReferenceCrown')) for m in part.data.materials)
    if is_crown:
        crown_parts.append(part)
        center=tuple(sum(v[k] for v in points)/len(points) for k in range(3))
        extent=tuple((max(v[k] for v in points)-min(v[k] for v in points))/2 for k in range(3))
        crown_samples.append((center,extent))
    else:
        wood_parts.append(part)
        if len(part.data.polygons)>2:
            for poly in (part.data.polygons[0],part.data.polygons[-1]):
                center=tuple(sum(points[i][k] for i in poly.vertices)/len(poly.vertices) for k in range(3))
                wood_paths.append(center)
    # Conservation exacte des coordonnées finales du feuillu.
    for v,co in zip(part.data.vertices,points):v.co=co
    part.location=(0,0,0)
    part.rotation_euler=(0,0,0)
    part.scale=(1,1,1)
source_wood_points=[tuple(v.co) for part in wood_parts for v in part.data.vertices]


def winter_triangle_count():
    return sum(len(poly.vertices)-2 for part in parts for poly in part.data.polygons)


def distance_squared(a,b):
    return sum((a[k]-b[k])**2 for k in range(3))


def winter_shoot(points,radii,mat,pointed=False):
    # Branches à quatre pans, effilées ; les extrémités peuvent finir en pointe.
    vertices=[]
    rings_count=len(points)-int(pointed)
    for index,center in enumerate(points[:rings_count]):
        start=points[max(0,index-1)];end=points[min(len(points)-1,index+1)]
        axis=[end[k]-start[k] for k in range(3)]
        norm=math.sqrt(sum(v*v for v in axis))
        if norm<.00001:return None
        axis=[v/norm for v in axis]
        ref=(0,0,1) if abs(axis[2])<.9 else (0,1,0)
        u=[axis[1]*ref[2]-axis[2]*ref[1],axis[2]*ref[0]-axis[0]*ref[2],axis[0]*ref[1]-axis[1]*ref[0]]
        norm=math.sqrt(sum(v*v for v in u));u=[v/norm for v in u]
        v=[axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]]
        for side in range(4):
            angle=side*math.tau/4
            co=[center[k]+radii[index]*(u[k]*math.cos(angle)+v[k]*math.sin(angle)) for k in range(3)]
            vertices.append(tuple(max(reference_min[k],min(reference_max[k],co[k])) for k in range(3)))
    faces=[(3,2,1,0)]
    for ring in range(rings_count-1):
        for side in range(4):
            a=ring*4+side;b=ring*4+(side+1)%4
            faces.append((a,b,b+4,a+4))
    if pointed:
        index=len(vertices)
        vertices.append(tuple(max(reference_min[k],min(reference_max[k],points[-1][k])) for k in range(3)))
        for side in range(4):faces.append(((rings_count-1)*4+side,(rings_count-1)*4+(side+1)%4,index))
    else:faces.append(tuple(range(len(vertices)-4,len(vertices))))
    part=mesh_part('WinterBranch',vertices,faces,mat)
    return part


if WINTER_MODE=='light':
    # Environ un bouquet sur cinq tombe ; les volumes extrêmes sont conservés.
    protected=set()
    for axis in range(3):
        for choose in (min,max):
            protected.add(choose(range(len(crown_parts)),key=lambda i:choose(v.co[axis] for v in crown_parts[i].data.vertices)))
    removable=[i for i in range(len(crown_parts)) if i not in protected]
    rng.shuffle(removable)
    removed=set(removable[:int(len(crown_parts)*WINTER_DROP_RATIO)])
    for i,part in enumerate(crown_parts):
        if i in removed:
            parts.remove(part)
            bpy.data.objects.remove(part,do_unlink=True)
    for mat in bpy.data.materials:
        if mat.name.startswith('Foliage'):
            color=(.14,.24,.13) if 'Dark' in mat.name else (.24,.33,.17)
            mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
    winter_crowns_removed=len(removed)
    winter_crowns_original=len(crown_parts)
else:
    for part in crown_parts:
        parts.remove(part)
        bpy.data.objects.remove(part,do_unlink=True)
    bark=mats[3] if KIND=='birch' else mats[0]
    base_triangles=winter_triangle_count()
    crown_count=len(crown_samples)
    shoots_per_crown=3 if crown_count<=3 else 1
    planned_shoots=crown_count*shoots_per_crown
    # Trente triangles sont réservés aux cinq rameaux qui conservent les dimensions.
    reserved=30+planned_shoots*(12+6)
    curve_allowance=max(0,(TRIANGLE_BUDGET-base_triangles-reserved)//8)
    secondary=[]
    for index,(center,extent) in enumerate(crown_samples):
        parent=min(wood_paths,key=lambda p:distance_squared(p,center))
        for shoot in range(shoots_per_crown):
            angle=index*2.4+shoot*math.tau/shoots_per_crown+rng.uniform(-.18,.18)
            if KIND=='oak':
                end=(center[0]+extent[0]*.70*math.cos(angle),
                     center[1]+extent[1]*.70*math.sin(angle),
                     center[2]+extent[2]*(.30+.22*shoot))
            else:
                # Les rameaux du bouleau remontent en arche, puis retombent.
                end=(center[0]+extent[0]*.65*math.cos(angle),
                     center[1]+extent[1]*.65*math.sin(angle),center[2]-extent[2]*.40)
            start=parent
            if distance_squared(start,end)<.10:
                start=(center[0],center[1],center[2]-.6)
            curved=curve_allowance>0
            if curved:
                midpoint=tuple((start[k]+end[k])/2 for k in range(3))
                midpoint=(midpoint[0]-.12*math.sin(angle),midpoint[1]+.12*math.cos(angle),
                          midpoint[2]+(.6 if KIND=='birch' else .3))
                winter_shoot([start,midpoint,end],([.060,.035,.015] if KIND=='birch' else [.11,.065,.025]),bark)
                curve_allowance-=1
            else:winter_shoot([start,end],([.055,.015] if KIND=='birch' else [.10,.025]),bark)
            secondary.append((start,end,extent,angle))
    # Points extrêmes du feuillu : même emprise et même sommet, sans déplacer le tronc.
    anchors=list(wood_paths)+[end for start,end,extent,angle in secondary]
    for axis,choose in ((0,min),(0,max),(1,min),(1,max),(2,max)):
        target=tuple(choose(reference_points,key=lambda v:v[axis]))
        start=min(anchors,key=lambda p:distance_squared(p,target))
        if distance_squared(start,target)<.005:
            start=min(wood_paths,key=lambda p:distance_squared(p,target))
        winter_shoot([start,target],[.045 if KIND=='birch' else .075,0],bark,True)
    # Troisième ordre : fourches terminales réparties dans l'ancienne couronne.
    for pass_index in range(4):
        for index,(start,end,extent,angle) in enumerate(secondary):
            if winter_triangle_count()+6>TRIANGLE_BUDGET:break
            factor=.50+pass_index*.11
            fork=tuple(start[k]+(end[k]-start[k])*factor for k in range(3))
            turn=angle+(.8 if (index+pass_index)%2 else -.8)+pass_index*.65
            length=min(1.7,max(.5,sum(extent)/3*.65))
            target=(end[0]+length*.7*math.cos(turn),end[1]+length*.7*math.sin(turn),
                    end[2]+length*(.65 if KIND=='oak' else -.65))
            winter_shoot([fork,target],[.030 if KIND=='birch' else .05,0],bark,True)
    for mat in list(bpy.data.materials):
        if mat.name.startswith(('Foliage','ReferenceCrown')):
            bpy.data.materials.remove(mat,do_unlink=True)

# Vérifications de raccord, avant la jonction et l'export du gabarit.
bpy.context.view_layer.update()
winter_points=[p.matrix_world @ v.co for p in parts for v in p.data.vertices]
winter_dimensions=[max(v[k] for v in winter_points)-min(v[k] for v in winter_points) for k in range(3)]
dimension_errors=[abs(winter_dimensions[k]/reference_dimensions[k]-1) for k in range(3)]
if any(error>.10 for error in dimension_errors):
    sys.exit('ERREUR : hauteur ou largeur de la version hivernale à plus de 10 % du feuillu de même graine')
current_wood_points=[tuple(v.co) for part in wood_parts for v in part.data.vertices]
if current_wood_points!=source_wood_points:
    sys.exit('ERREUR : le tronc ou les branches d’origine ont été modifiés')
for part in parts:
    for layer in list(part.data.uv_layers):part.data.uv_layers.remove(layer)
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
