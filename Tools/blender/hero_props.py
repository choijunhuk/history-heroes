# 영웅 손·등 소품 (잔 다르크 검·깃발, 부디카 검, 토르 망치). 실행: Blender -b --factory-startup -P Tools/blender/hero_props.py 기본 몸 Superhero_Male_FullBody 바인드 자세 기준 (블렌더 좌표).
# 손 hand_r 머리 (0.706, -0.065, 1.456), 날은 +Y (Unity -Z, 관우 언월도와 같은 방향), 등 = +Y.
import bpy, bmesh, math
from mathutils import Vector, Matrix

OUT = "/Users/choi/Documents/history-heroes/Game/Assets/Art/3D/Props"
HX, HY, HZ = 0.80, -0.065, 1.42  # 손 쥔 자리 (언월도 자루 중심과 같은 X·Z)

def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m

def new_obj(name, bm, material):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(material))
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o

def box(name, center, size, material):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_obj(name, bm, material)

def cyl_y(name, center, radius, length, material, seg=10):
    # Y축 원기둥
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=radius, radius2=radius, depth=length)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90), 3, 'X'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_obj(name, bm, material)

def cyl_z(name, center, radius, length, material, seg=10):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=radius, radius2=radius, depth=length)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_obj(name, bm, material)

def sphere(name, center, radius, material):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=radius)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_obj(name, bm, material)

def blade(name, y0, profile, thick, material):
    """profile = [(y, 반폭)...] 마지막은 끝 (반폭 0). 단면은 마름모 (가운데 두께 thick)"""
    bm = bmesh.new()
    rings = []
    for y, w in profile:
        if w <= 0:
            rings.append([bm.verts.new((HX, y0 + y, HZ))])
        else:
            rings.append([bm.verts.new((HX + w, y0 + y, HZ)), bm.verts.new((HX, y0 + y, HZ + thick)),
                          bm.verts.new((HX - w, y0 + y, HZ)), bm.verts.new((HX, y0 + y, HZ - thick))])
    bm.faces.new(list(reversed(rings[0])))
    for a, b in zip(rings, rings[1:]):
        if len(b) == 1:
            for i in range(4):
                bm.faces.new((a[i], a[(i + 1) % 4], b[0]))
        else:
            for i in range(4):
                bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj(name, bm, material)

def export(name, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    bpy.ops.object.shade_flat()
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False)
    bpy.data.objects.remove(joined, do_unlink=True)

def reset():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

# 1) 잔 다르크 검: 십자 손잡이 (15세기 프랑스 장검 느낌), 날 0.86
objs = [
    cyl_y("grip", (HX, HY - 0.02, HZ), 0.021, 0.20, "Black"),
    sphere("pommel", (HX, HY - 0.14, HZ), 0.034, "Gold"),
    box("guard", (HX, HY + 0.095, HZ), (0.26, 0.035, 0.04), "Gold"),
    blade("blade", HY + 0.11, [(0.0, 0.034), (0.70, 0.028), (0.80, 0.018), (0.86, 0.0)], 0.009, "Metal"),
]
export("Jeanne_Sword", objs)
reset()

# 2) 부디카 검: 켈트 잎 모양 날 (가운데보다 끝 쪽이 넓음), 청동 손잡이, 날 0.74
objs = [
    cyl_y("grip", (HX, HY - 0.015, HZ), 0.022, 0.17, "Wood"),
    sphere("pommel", (HX, HY - 0.125, HZ), 0.038, "Bronze"),
    box("guard", (HX, HY + 0.085, HZ), (0.13, 0.03, 0.045), "Bronze"),
    blade("blade", HY + 0.10, [(0.0, 0.026), (0.22, 0.024), (0.48, 0.042), (0.62, 0.034), (0.74, 0.0)], 0.010, "Metal"),
]
export("Boudica_Sword", objs)
reset()

# 3) 잔 다르크 깃발: 등 뒤 깃대 + 가로대에 매달린 세로 깃발 (파랑 바탕 · 금 테두리 · 금 문장). spine_03 (0, 0.007, 1.311) 뒤 = +Y
#    처음 만든 옆으로 뻗은 흰 천은 화면에서 낫 날처럼 보였다 → 아래로 늘어진 깃발로 (2026-10-29 화면 확인)
PX, PY = 0.12, 0.17
L, R, TOP, BOT = PX + 0.03, PX + 0.33, 2.17, 1.72
flag = bmesh.new()
pts = [(L, TOP), (R, TOP), (R, BOT), ((L + R) / 2, BOT + 0.09), (L, BOT)]  # 아래 끝 제비꼬리
vs = [flag.verts.new((x, PY, z)) for x, z in pts]
face = flag.faces.new(vs)
ext = bmesh.ops.extrude_face_region(flag, geom=[face])  # 앞뒤 두 면 + 옆면 (한쪽 면만 있으면 뒤에서 안 보인다)
bmesh.ops.translate(flag, vec=Vector((0, 0.012, 0)), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
bmesh.ops.recalc_face_normals(flag, faces=flag.faces)
objs = [
    cyl_z("pole", (PX, PY, 1.64), 0.016, 1.34, "Wood"),
    sphere("tip", (PX, PY, 2.33), 0.032, "Gold"),
    box("crossbar", ((PX + R) / 2 + 0.01, PY + 0.006, TOP + 0.015), (R - PX + 0.04, 0.03, 0.03), "Gold"),
    new_obj("cloth", flag, "Blue"),
    box("emblem", ((L + R) / 2, PY + 0.006, 1.98), (0.09, 0.03, 0.11), "Gold"),
    box("trim", ((L + R) / 2, PY + 0.006, TOP - 0.03), (R - L, 0.026, 0.025), "Gold"),
]
export("Jeanne_Banner", objs)
reset()

# 4) 토르 망치 (신의 시련 1 · 플레이 토르, GodsVsHumanity 3-3): 짧은 자루 + 큰 쇠 머리, 금 띠
objs = [
    cyl_y("handle", (HX, HY + 0.08, HZ), 0.022, 0.42, "Wood"),
    cyl_y("wrap", (HX, HY - 0.04, HZ), 0.025, 0.14, "Black"),
    sphere("pommel", (HX, HY - 0.135, HZ), 0.03, "Gold"),
    box("head", (HX, HY + 0.34, HZ), (0.15, 0.15, 0.30), "Iron"),
    box("band_top", (HX, HY + 0.34, HZ + 0.115), (0.16, 0.16, 0.025), "Gold"),
    box("band_bottom", (HX, HY + 0.34, HZ - 0.115), (0.16, 0.16, 0.025), "Gold"),
]
export("Thor_Hammer", objs)
print("DONE")
