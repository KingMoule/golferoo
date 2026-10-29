"""Golfeur animé du jeu, à partir du modèle riggé de Guillaume (lot 1 de specs/C23-golfeurs-animes.md).

Usage (Python 3.13) :
    pip install bpy==5.2.3
    python -I tools/blender/golfer_anim.py [source.blend] [glb]

Par défaut : tools/blender/src/golfer_polo_v4.blend, game/assets/golfers/golfer.glb.
- Normalise : 1,75 m, pieds à l'origine, regard vers -Y de Blender (+Z dans Godot), cible du swing vers +X.
- Garde `golf_swing_2s` ; génère `walk` (boucle d'une seconde) et `idle` sauf si le fichier source les a déjà
  (la marche de Guillaume remplace alors la générée), puis `chip` et `putt`, le swing réduit vers sa pose d'adresse.
- Écrit dans les extras du squelette (métadonnée `extras` dans Godot) : l'instant d'impact de chaque geste
  (`impact_<clip>`, en secondes : le bas du club pendant la descente), la distance parcourue par cycle de marche
  (`walk_cycle_m`) et la balle à l'adresse (`ball_x`, `ball_z`, repère Godot, en mètres).
Échoue si le squelette n'a pas 21 os ou si le maillage dépasse TRIANGLE_BUDGET.
"""
import math
import sys
import time

import bpy
from mathutils import Euler, Quaternion

TRIANGLE_BUDGET = 2500
BONES = 21
HEIGHT = 1.75
SWING = "golf_swing_2s"
WALK_FRAMES = 30  # une seconde à 30 images/s
STRIDE = 0.72  # longueur d'un pas visée (m) : environ 1,3 m par cycle
REDUCED = {"chip": (0.6, 1.25), "putt": (0.4, 1.0)}  # amplitude, vitesse de lecture

src = sys.argv[1] if len(sys.argv) > 1 else "tools/blender/src/golfer_polo_v4.blend"
glb_path = sys.argv[2] if len(sys.argv) > 2 else "game/assets/golfers/golfer.glb"
t0 = time.time()

bpy.ops.wm.open_mainfile(filepath=src)
scene = bpy.context.scene
fps = scene.render.fps
rig = next(o for o in scene.objects if o.type == "ARMATURE")
mesh = next(o for o in scene.objects if o.type == "MESH" and o.parent == rig)
for o in list(scene.objects):
    if o not in (rig, mesh):
        bpy.data.objects.remove(o)

if len(rig.data.bones) != BONES:
    sys.exit(f"ERREUR : {len(rig.data.bones)} os, {BONES} attendus")
tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
if tris > TRIANGLE_BUDGET:
    sys.exit(f"ERREUR : {tris} triangles, budget {TRIANGLE_BUDGET}")

# Échelle et origine : hauteur de la pose de repos ramenée à HEIGHT, pieds (os root) à l'origine.
rest = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
k = HEIGHT / (max(v.z for v in rest) - min(v.z for v in rest))
root = rig.data.bones["root"].head_local
rig.location = (-root.x * k, -root.y * k, -min(v.z for v in rest) * k)
rig.scale = (k, k, k)
bpy.ops.object.select_all(action="SELECT")
bpy.context.view_layer.objects.active = rig
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for action in bpy.data.actions:
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    if fc.data_path.endswith(".location"):
                        for kp in fc.keyframe_points:
                            kp.co[1] *= k
                            kp.handle_left[1] *= k
                            kp.handle_right[1] *= k

pose = rig.pose.bones
rest_q = {b.name: b.matrix_local.to_quaternion() for b in rig.data.bones}


def local(name, q):
    """Rotation exprimée dans le repère du squelette au repos -> rotation locale de l'os."""
    r = rest_q[name]
    return r.inverted() @ q @ r


def rot(x=0.0, y=0.0, z=0.0):
    return Euler((math.radians(x), math.radians(y), math.radians(z))).to_quaternion()


def new_action(name):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    return action


def key(frame, rotations, hips_lift=0.0):
    """Pose clé : rotations (repère du squelette) des os nommés, les autres au repos."""
    for pb in pose:
        pb.rotation_quaternion = local(pb.name, rotations[pb.name]) if pb.name in rotations else Quaternion()
        pb.location = (0, hips_lift, 0) if pb.name == "hips" else (0, 0, 0)
        pb.scale = (1, 1, 1)
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def stand(arm_swing=0.0):
    """Bras le long du corps (le repos du modèle est en T), club porté vers l'avant."""
    return {
        "upper_arm.L": rot(x=-arm_swing) @ rot(y=38),
        "upper_arm.R": rot(x=arm_swing) @ rot(y=-38),
        "forearm.L": rot(x=-12 - max(0.0, arm_swing) * 0.5),
        "forearm.R": rot(x=-12 - max(0.0, -arm_swing) * 0.5),
        "club": rot(x=-40),
    }


swing = bpy.data.actions[SWING]
start, end = (int(f) for f in swing.frame_range)
rig.animation_data.action = swing

# Impact : le bas du club pendant la descente (parmi les images où le club va vite), et la balle à l'adresse.
scene.frame_set(start)
ball = rig.matrix_world @ pose["club"].tail
tips, prev = [], None
for f in range(start, end + 1):
    scene.frame_set(f)
    tip = (rig.matrix_world @ pose["club"].tail).copy()
    tips.append((f, tip, 0.0 if prev is None else (tip - prev).length))
    prev = tip
fast = max(s for _, _, s in tips) / 2
impact_frame = min((t for t in tips if t[2] >= fast), key=lambda t: t[1].z)[0]
impact = (impact_frame - start) / fps

if "walk" not in bpy.data.actions:
    # Marche : cuisses en opposition, genou plié pendant le retour de la jambe, bras opposés aux jambes, bassin qui monte
    # quand les jambes se croisent. Le pas (cuisse * longueur de jambe) vaut environ STRIDE.
    new_action("walk")
    leg = rig.data.bones["thigh.L"].head_local.z
    thigh = math.degrees(math.asin(STRIDE / 2 / leg))
    for f in range(WALK_FRAMES + 1):
        a = math.tau * f / WALK_FRAMES
        s, c = math.sin(a), math.cos(a)
        r = stand(arm_swing=22 * s)
        r["thigh.L"], r["thigh.R"] = rot(x=-thigh * s), rot(x=thigh * s)
        r["shin.L"], r["shin.R"] = rot(x=45 * max(0.0, c) ** 2), rot(x=45 * max(0.0, -c) ** 2)
        r["foot.L"], r["foot.R"] = rot(x=-0.4 * thigh * s), rot(x=0.4 * thigh * s)
        r["hips"] = rot(z=6 * s)
        r["chest"] = rot(z=-9 * s)
        key(start + f, r, hips_lift=0.025 * math.cos(2 * a))

if "idle" not in bpy.data.actions:
    new_action("idle")  # repos debout, une lente respiration de deux secondes
    for f in (0, fps, 2 * fps):
        r = stand()
        r["chest"] = rot(x=-2 if f == fps else 0)
        key(start + f, r)

for name, (amplitude, speed) in REDUCED.items():
    if name in bpy.data.actions:
        continue
    # Le swing rapproché de sa pose d'adresse (image start) et joué à `speed`.
    rig.animation_data.action = swing
    scene.frame_set(start)
    address = {pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in pose}
    frames = []
    for f in range(start, end + 1):
        scene.frame_set(f)
        frames.append({pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in pose})
    new_action(name)
    count = round((end - start) / speed)
    for i in range(count + 1):
        u = min(i * speed, end - start)
        lo = int(u)
        hi = min(lo + 1, end - start)
        for pb in pose:
            q = frames[lo][pb.name][0].slerp(frames[hi][pb.name][0], u - lo)
            loc = frames[lo][pb.name][1].lerp(frames[hi][pb.name][1], u - lo)
            pb.rotation_quaternion = address[pb.name][0].slerp(q, amplitude)
            pb.location = address[pb.name][1].lerp(loc, amplitude)
            pb.keyframe_insert("rotation_quaternion", frame=start + i)
            pb.keyframe_insert("location", frame=start + i)
    rig[f"impact_{name}"] = round(impact / speed, 4)

# Distance par cycle de marche : le pied d'appui recule de la longueur d'un pas par demi-cycle.
walk = bpy.data.actions["walk"]
rig.animation_data.action = walk
ws, we = (int(f) for f in walk.frame_range)
ys = []
for f in range(ws, we + 1):
    scene.frame_set(f)
    ys.append((rig.matrix_world @ pose["foot.L"].head).y)
rig["walk_cycle_m"] = round(2 * (max(ys) - min(ys)), 4)
rig[f"impact_{SWING}"] = round(impact, 4)
rig["ball_x"], rig["ball_z"] = round(ball.x, 4), round(-ball.y, 4)
rig["height_m"] = HEIGHT
rig.animation_data.action = swing
scene.frame_set(start)

bpy.ops.export_scene.gltf(
    filepath=glb_path,
    export_format="GLB",
    export_animation_mode="ACTIONS",
    export_anim_slide_to_zero=True,
    export_extras=True,
    export_apply=False,
)
actions = sorted(a.name for a in bpy.data.actions)
print(
    f"ASSET golfer.glb {tris} tri {len(rig.data.bones)} os animations={','.join(actions)} impact={impact:.4f} s "
    f"(image {impact_frame}) cycle={rig['walk_cycle_m']} m {time.time() - t0:.2f} s"
)
