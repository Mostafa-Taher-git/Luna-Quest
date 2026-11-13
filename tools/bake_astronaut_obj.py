"""Bake the frozen Sketchfab astronaut pose into a static OBJ for Unity.

The Sketchfab GLB has one long modelling action. The game holds the clean standing
idle at 0.4 s (see WorldBuilder.CreateAstronaut), so Unity gets the same pose as a
static mesh instead of a runtime glTF dependency. Materials are split so the visor
can be replaced with black glass.
"""
import sys, math, os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glb_math import skinned, js, acc, mat_vec, world, local_at, mat_inv, mat_mul, from_gltf, joints, ibms

POSE = 0.4
out_dir = os.path.join(ROOT, 'engines', 'unity', 'LunaQuest.Unity', 'Assets', 'Resources', 'Art', 'Astronaut')
os.makedirs(out_dir, exist_ok=True)

out, W = skinned(POSE)
G = W(9)

# glTF geometry is already Y-up/+Z-forward, matching Unity's convention.
verts = []
normals = []
faces = []
for mesh_name, material in (('Astronaut_Astronaut_0', 'Suit'), ('Astronaut_visier_0', 'Visor')):
    sp, idx = out[mesh_name]
    base = len(verts)
    start = len(faces) + 1
    # Positions are already skinned. The mesh node transform is included in G.
    for p in sp:
        wp = mat_vec(G, p)
        verts.append(wp)
    # Faces, preserving winding. Flip handedness? glTF and Unity are both right-handed
    # with +Z forward, so winding stays clockwise when viewed from outside.
    for i in range(0, len(idx), 3):
        faces.append((base + idx[i] + 1, base + idx[i + 1] + 1, base + idx[i + 2] + 1, material))
    # Use the GLB normals when present; fall back to a face normal pass below.
    prim = js['meshes'][0 if material == 'Suit' else 1]['primitives'][0]
    if 'NORMAL' in prim['attributes']:
        ns = acc(prim['attributes']['NORMAL'])
        L = local_at(POSE)
        Wf = lambda i: world(i, L)
        inv = mat_inv(Wf(9))
        skin = []
        for ji, jn in enumerate(joints):
            skin.append(mat_mul(mat_mul(inv, Wf(jn)), from_gltf(ibms[ji])))
        J = acc(prim['attributes']['JOINTS_0']); Wt = acc(prim['attributes']['WEIGHTS_0'])
        for vi, n in enumerate(ns):
            a = [0.0, 0.0, 0.0]
            for k in range(4):
                w = Wt[vi][k]
                if w <= 0: continue
                m = skin[J[vi][k]]
                # upper 3x3 pseudo-normal (ignore scale shears)
                nx = m[0]*n[0] + m[1]*n[1] + m[2]*n[2]
                ny = m[4]*n[0] + m[5]*n[1] + m[6]*n[2]
                nz = m[8]*n[0] + m[9]*n[1] + m[10]*n[2]
                a[0] += nx*w; a[1] += ny*w; a[2] += nz*w
            ln = math.sqrt(sum(v*v for v in a)) or 1.0
            normals.append([v/ln for v in a])
    else:
        normals.extend([[0,1,0]] * len(sp))

# Weld near-identical vertices in the OBJ to keep it lightweight? Keep explicit for
# material splits; 7.8k verts is small.
obj = ['# Luna-Quest astronaut — Sketchfab Cute Astronaut by haefu (CC-BY-4.0)',
       '# Frozen standing pose at 0.4 s; baked from cute_astronaut.glb for Unity.',
       'mtllib cute_astronaut_baked.mtl', 'o CuteAstronaut']
for v in verts:
    obj.append(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}")
for n in normals:
    obj.append(f"vn {n[0]:.6f} {n[1]:.6f} {n[2]:.6f}")
current = None
for a,b,c,m in faces:
    if m != current:
        obj.append(f"g {m}")
        obj.append(f"usemtl {m}")
        current = m
    # OBJ is 1-based, vertex//normal share the same index here.
    obj.append(f"f {a}//{a} {b}//{b} {c}//{c}")
open(os.path.join(out_dir, 'cute_astronaut_baked.obj'), 'w').write('\n'.join(obj) + '\n')

mtl = '''# Luna-Quest astronaut materials (Sketchfab Cute Astronaut, CC-BY-4.0, haefu)
newmtl Suit
Kd 0.78 0.80 0.83
Ka 0.10 0.10 0.11
Ks 0.08 0.08 0.09
Ns 24
illum 2

newmtl Visor
Kd 0.012 0.018 0.030
Ka 0.005 0.006 0.008
Ks 0.75 0.85 1.0
Ns 350
illum 2
'''
open(os.path.join(out_dir, 'cute_astronaut_baked.mtl'), 'w').write(mtl)
print('verts', len(verts), 'normals', len(normals), 'faces', len(faces))
print('bounds', [round(min(v[i] for v in verts),3) for i in range(3)], [round(max(v[i] for v in verts),3) for i in range(3)])
print('wrote', out_dir)
