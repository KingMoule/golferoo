"""Bouleau mature — fin d'automne : environ trois quarts des bouquets tombés, feuillage entre le roux et le brun d'hiver ; même bois que birch_03, feuillage emboîté avec birch_winter_light_03, budget 800.

Usage :
    python -I tools/blender/birch_autumn_late_03_lowpoly.py -- [graine] [glb] [png]
ou dans Blender (onglet Scripting, Run Script) : écrit dans ~/golferoo-assets/.
Un seul maillage joint, facettes plates, matériaux unis, échelle réelle en mètres, pied à l'origine.
"""
import math
import os
import random
import sys
import time

import bpy

NAME = 'birch_autumn_late_03'
TRIANGLE_BUDGET = 800
SEED = 1
FAMILY = "trees"
LEAF_DROP_RANGE = (0.72, 0.78)
AUTUMN_COLORS = ((0.395, 0.25, 0.06), (0.54, 0.42, 0.125))
SOURCE_NAME = 'birch_800_03'
KIND = 'birch'
VARIANT = 3
PALETTE = [('BarkWhite', (0.8, 0.79, 0.7)), ('FoliageDark', (0.1, 0.32, 0.1)), ('FoliageLight', (0.2, 0.46, 0.14)), ('BarkDark', (0.18, 0.19, 0.2))]

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

def build_birch():
    global height
    bark,leaf,light,dark=mats
    height=(19,17,18)[VARIANT-1]*rng.uniform(.85,1.15)
    if VARIANT==1:
        branch([(0,0,0),(.1,0,4),(-.05,.1,9),(.2,.15,15)], [.24,.20,.14,.035],bark,6)
        sites=[(3.1*math.cos(i*2.4),3.1*math.sin(i*2.4),8+i*.6) for i in range(10)]
        for i,(x,y,z) in enumerate(sites):
            branch([(0,0,z-2),(x*.65,y*.65,z+.9),(x,y,z)], [.095,.05,.015],bark,4)
            for j in range(2 if i<6 else 3):
                ico((x+(j-1)*.55,y+j*.28,z-j*.4),(1.4,1.15,1.85),leaf if (i+j)%3 else light,1,.16)
        bark_marks(0,0,0,.22,10,dark)
    elif VARIANT==2:
        # Cépée : trois fûts indépendants, houppiers décalés et trouées centrales.
        for stem in range(3):
            a=stem*math.tau/3+.3;x=2.0*math.cos(a);y=2.0*math.sin(a)
            branch([(.12*math.cos(a),.12*math.sin(a),0),(x*.4,y*.4,5),(x,y,13+stem*.5)], [.17,.13,.025],bark,5)
            for j in range(3):
                angle=a+j*1.8
                end=(x+2.1*math.cos(angle),y+2.1*math.sin(angle),9.4+j*1.25)
                branch([(x*.5,y*.5,6+j),((x+end[0])*.5,(y+end[1])*.5,end[2]+.7),end],[.085,.045,.012],bark,4)
                for k in range(3 if j<2 else 2):
                    ico((end[0]+(k-1)*.45,end[1]+k*.27,end[2]-.25*k),(1.3,1.1,1.7),light if (j+k)%3==0 else leaf,1,.15)
            bark_marks(x*.2,y*.2,0,.15,4,dark)
    else:
        # Port pleureur : couronne en fontaine et longs bouquets retombants.
        branch([(0,0,0),(-.15,0,4),(.1,0,9),(.6,.1,13.7)], [.25,.20,.14,.04],bark,6)
        for i in range(9):
            angle=i*math.tau/9;r=3.0+(i%2)*.7
            x,y=r*math.cos(angle),r*math.sin(angle)
            branch([(.1,0,8.3+i*.22),(x*.65,y*.65,12.3+(i%2)),(x,y,10.0+(i%3)*.6)], [.11,.06,.014],bark,4)
            for j in range(3):
                ico((x*(.64+j*.18),y*(.64+j*.18),12.4-j*1.45+(i%3)*.35),
                    (1.25,1.1,1.65),leaf if (i+j)%3 else light,1,.14)
        bark_marks(0,0,0,.22,8,dark)

build_birch()

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



# Feuillage clairsemé : la chute touche aussi les bouquets visibles du pourtour.
drop_ratio=rng.uniform(*LEAF_DROP_RANGE)
keep_count=round(len(crown_parts)*(1-drop_ratio))
indices=list(range(len(crown_parts)))
rng.shuffle(indices)
kept=set(indices[:keep_count])
removed=[i for i in range(len(crown_parts)) if i not in kept]
for index in removed:
    part=crown_parts[index]
    parts.remove(part)
    bpy.data.objects.remove(part,do_unlink=True)

# Les triangles libérés rendent visibles les rameaux sous les bouquets tombés.
# Cinq rameaux finissent en pointe, réservant 30 triangles au raccord de l'emprise.
twig_material=mats[3] if KIND=='birch' else mats[0]
for order,index in enumerate(removed):
    center,extent=crown_samples[index]
    start=min(wood_paths,key=lambda point:distance_squared(point,center))
    angle=index*2.4+rng.uniform(-.20,.20)
    end=(center[0]+extent[0]*.35*math.cos(angle),center[1]+extent[1]*.35*math.sin(angle),
         center[2]+extent[2]*(.32 if KIND=='oak' else -.32))
    if distance_squared(start,end)<.04:
        end=(end[0]+.3*math.cos(angle),end[1]+.3*math.sin(angle),end[2]+.25)
    middle=tuple((start[k]+end[k])/2 for k in range(3))
    middle=(middle[0]-.08*math.sin(angle),middle[1]+.08*math.cos(angle),middle[2]+(.40 if KIND=='birch' else .20))
    radii=[.06,.035,.014] if KIND=='birch' else [.095,.055,.020]
    winter_shoot([start,middle,end],radii,twig_material,pointed=order<5)

# De fins rameaux conservent le sommet et l'emprise, même si des bouquets extrêmes tombent.
for axis,choose in ((0,min),(0,max),(1,min),(1,max),(2,max)):
    target=tuple(choose(reference_points,key=lambda v:v[axis]))
    start=min(wood_paths,key=lambda point:distance_squared(point,target))
    winter_shoot([start,target],[.035 if KIND=='birch' else .055,0],twig_material,pointed=True)

# Seules les deux couleurs du feuillage changent ; l'écorce reste identique à l'été.
for mat in bpy.data.materials:
    if mat.name.startswith('Foliage'):
        color=AUTUMN_COLORS[0 if 'Dark' in mat.name else 1]
        mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
winter_crowns_original=len(crown_parts)
winter_crowns_removed=len(removed)
winter_crowns_retained=len(kept)
winter_added_twigs=len(removed)+5

# Raccord contrôlé avant l'export : pas de déplacement du tronc ou des branches d'été.
bpy.context.view_layer.update()
winter_points=[part.matrix_world @ v.co for part in parts for v in part.data.vertices]
winter_dimensions=[max(v[k] for v in winter_points)-min(v[k] for v in winter_points) for k in range(3)]
dimension_errors=[abs(winter_dimensions[k]/reference_dimensions[k]-1) for k in range(3)]
if any(error>.10 for error in dimension_errors):
    sys.exit('ERREUR : dimensions saisonnières à plus de 10 % de celles du feuillu de même graine')
if [tuple(v.co) for part in wood_parts for v in part.data.vertices]!=source_wood_points:
    sys.exit('ERREUR : le bois d’origine a été modifié')
if not 0.65<=winter_crowns_removed/winter_crowns_original<=0.85:
    sys.exit('ERREUR : la réduction du feuillage sort de la plage de cette étape')
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
