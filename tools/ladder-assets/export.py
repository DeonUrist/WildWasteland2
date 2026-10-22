# Mixamo ladder climb FBX -> WW pose animation (.wwpose v2), Unity axes, metres.
# The loop is made vertical (the source ladder's lean is rotated out) and in place along the ladder; phase 0 = the left sole on a rung
# at the root (both feet planted), so the runtime can put rung heights under the feet. One cycle = two rungs.
import bpy, struct, sys, math, mathutils
args = sys.argv[sys.argv.index("--")+1:]; src, dst = args[0], args[1]; ref = int(args[2]) if len(args) > 2 else 30
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = bpy.data.actions[0]; f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
bones = list(arm.data.bones); names = [b.name for b in bones]
parent = [names.index(b.parent.name) if b.parent else -1 for b in bones]
ix = {n: names.index("mixamorig:" + n) for n in ("Hips", "LeftToeBase", "RightToeBase", "LeftHand", "RightHand")}
W = arm.matrix_world
def world(m4): return W @ m4
rest = [world(b.matrix_local) for b in bones]
frames = []
for f in range(f0, f1 + 1):
    bpy.context.scene.frame_set(f); frames.append([world(arm.pose.bones[n].matrix) for n in names])
N = len(frames) - 1
climb = frames[N][ix["Hips"]].translation - frames[0][ix["Hips"]].translation   # Blender axes, one cycle
# un-lean: rotate the climb direction onto +Z (Blender up) about the horizontal axis perpendicular to it
up = mathutils.Vector((0, 0, 1)); axis = climb.normalized().cross(up)
U = mathutils.Matrix.Identity(3) if axis.length < 1e-6 else mathutils.Matrix.Rotation(climb.normalized().angle(up), 3, axis.normalized())
print("lean degrees", math.degrees(climb.normalized().angle(up)), "cycle", climb.length)
# Blender (x, y, z) -> Unity (-x, z, -y): the Mixamo T-pose (facing Blender -Y) faces Unity +Z, left hand -X (AvatarBuilder convention)
S = mathutils.Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))
def conv(m4, lean):
    R = (U if lean else mathutils.Matrix.Identity(3))
    p = S @ (R @ m4.translation); r = m4.to_3x3().normalized()
    return p, (S @ R @ r @ S.transposed()).to_quaternion()
restc = [conv(m, False) for m in rest]
fr = [[conv(m, True) for m in f] for f in frames]
rise = fr[N][ix["Hips"]][0].y - fr[0][ix["Hips"]][0].y
drift = fr[N][ix["Hips"]][0] - fr[0][ix["Hips"]][0]
print("rise", rise, "drift after un-lean", drift.x, drift.z)
restToe = min(restc[ix["LeftToeBase"]][0].y, restc[ix["RightToeBase"]][0].y)
r0 = ref - f0
loop = [fr[(r0 + i) % N] if r0 + i < N else None for i in range(N)]
# frames from the reference on, wrapping into the next cycle (shifted down one cycle so the climb stays continuous)
seq = []
for i in range(N):
    k = r0 + i
    if k < N: seq.append([(p, q) for p, q in fr[k]])
    else: seq.append([(p + mathutils.Vector((0, rise, 0)), q) for p, q in fr[k - N]])
base0 = seq[0][ix["LeftToeBase"]][0].y - restToe          # left sole on the rung at phase 0
hx = sum(s[ix["Hips"]][0].x for s in seq) / N; hz0 = seq[0][ix["Hips"]][0].z
handz = sum((s[ix["LeftHand"]][0].z + s[ix["RightHand"]][0].z) * .5 - s[ix["Hips"]][0].z for s in seq) / N
toez = sum((s[ix["LeftToeBase"]][0].z + s[ix["RightToeBase"]][0].z) * .5 - s[ix["Hips"]][0].z for s in seq) / N
print("hips->hands", handz, "hips->toes", toez, "hands x", seq[0][ix["LeftHand"]][0].x - hx, seq[0][ix["RightHand"]][0].x - hx)
for i in (0, N // 4, N // 2, 3 * N // 4):
    s = seq[i]; b = base0 + rise * i / N
    print(" i", i, "L sole", round(s[ix["LeftToeBase"]][0].y - restToe - b, 3), "R sole", round(s[ix["RightToeBase"]][0].y - restToe - b, 3))
out = bytearray(b"WWPA") + struct.pack("<iiiffff", 3, len(bones), N, 30.0, rise, abs(handz), abs(toez))
for n, p in zip(names, parent):
    e = n.encode(); out += struct.pack("<H", len(e)) + e + struct.pack("<i", p)
def put(p, q, b):
    global out
    out += struct.pack("<7f", p.x - b.x, p.y - b.y, p.z - b.z, q.x, q.y, q.z, q.w)
rr = restc[ix["Hips"]][0]
for p, q in restc: put(p, q, mathutils.Vector((rr.x, 0.0, rr.z)))
for i, s in enumerate(seq):
    b = mathutils.Vector((hx + drift.x * i / N, base0 + rise * i / N, hz0 + drift.z * i / N))
    for p, q in s: put(p, q, b)
# v3: per frame, how planted each foot is (0..255): its sole barely moves along the (vertical) ladder
def planted(j):
    raw = []
    for i in range(N):
        a = seq[i][j][0]; b = seq[(i + 1) % N][j][0] + (mathutils.Vector((0, rise, 0)) if i + 1 == N else mathutils.Vector())
        raw.append(1.0 if (b - a).length < .008 else 0.0)
    sm = [sum(raw[(i + k) % N] for k in (-2, -1, 0, 1, 2)) / 5.0 for i in range(N)]
    return [max(0, min(255, int(round(v * 255)))) for v in sm]
cl, cr = planted(ix["LeftToeBase"]), planted(ix["RightToeBase"])
print("left planted", "".join("#" if v > 200 else ("+" if v > 0 else ".") for v in cl))
print("right planted", "".join("#" if v > 200 else ("+" if v > 0 else ".") for v in cr))
out += bytes(cl) + bytes(cr)
open(dst, "wb").write(out); print("wrote", len(out), "frames", N)
