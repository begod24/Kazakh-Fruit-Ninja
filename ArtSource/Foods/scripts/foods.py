"""Builds the game's foods in Blender: geometry, procedural material, baked textures, FBX.

Run inside Blender:  import foods; foods.build("kurt")   (or foods.build_all())
Sizes are in Unity units and roughly match the old placeholders, so FoodDefinition.scale becomes 1.
The side facing Blender -Y faces the game camera.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

import kfood_lib as K
from kfood_lib import fbm, smoothstep

TAU = 2 * math.pi


# ================================================================ shared helpers

def np_image(name, rgba, non_color=False):
    """Blender image from a (H, W, 4) float array; row 0 is the bottom."""
    h, w = rgba.shape[:2]
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, w, h, alpha=True, float_buffer=True)
    if non_color:
        img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    img.pack()
    img.use_fake_user = True
    return img


def gray_image(name, values):
    v = np.clip(values, 0, 1).astype(np.float32)
    return np_image(name, np.dstack([v, v, v, np.ones_like(v)]), non_color=True)


def planar_xz(nb, p, extent):
    """Object XZ in [-extent, extent] -> image UV 0..1 (for maps drawn with numpy)."""
    s = nb.sep(p)
    u = nb.math('MULTIPLY_ADD', s[0], 0.5 / extent, 0.5)
    v = nb.math('MULTIPLY_ADD', s[2], 0.5 / extent, 0.5)
    return nb.comb(u, v, 0.0)


def cylinder_uv(nb, p, z0, z1, front_at=0.5):
    """Wraps an image around Z: u = angle (front, -Y, at `front_at`), v = height between z0 and z1."""
    s = nb.sep(p)
    ang = nb.math('ARCTAN2', s[1], s[0])                   # -pi..pi, front (-Y) = -pi/2
    u = nb.math('MULTIPLY_ADD', ang, 1 / TAU, 0.5 + 0.25)   # front -> 0.5
    u = nb.math('FRACT', nb.math('ADD', u, front_at - 0.5))
    v = nb.math('MULTIPLY_ADD', s[2], 1 / (z1 - z0), -z0 / (z1 - z0))
    return nb.comb(u, v, 0.0)


def attr_fn(obj, name, fn):
    K.vertex_color_layer(obj, name, fn)


def drop_attributes(obj):
    me = obj.data
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)


def transform_mesh(obj, matrix):
    obj.data.transform(matrix)
    obj.data.update()


def render_text(text, font_path, res=(1400, 300), size=1.0, extrude=0.0):
    """Renders white text on transparent background with EEVEE and returns the alpha as a (H, W) array."""
    scene = bpy.data.scenes.get("_Text") or bpy.data.scenes.new("_Text")
    for o in list(scene.collection.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    font = bpy.data.fonts.load(font_path, check_existing=True)
    cu = bpy.data.curves.new("_txt", 'FONT')
    cu.body = text
    cu.font = font
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.size = size
    txt = bpy.data.objects.new("_txt", cu)
    scene.collection.objects.link(txt)
    mat = bpy.data.materials.get("_txt_mat") or bpy.data.materials.new("_txt_mat")
    nb = K.NB(mat)
    em = nb.node('ShaderNodeEmission', {'Color': (1, 1, 1, 1), 'Strength': 1.0})
    nb.nt.links.new(em.outputs[0], nb.out.inputs['Surface'])
    cu.materials.append(mat)
    cam = bpy.data.objects.new("_txtcam", bpy.data.cameras.new("_txtcam"))
    scene.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.location = (0, 0, 5)
    cam.data.ortho_scale = size * len(text) * 0.75
    scene.camera = cam
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'
    path = os.path.join(bpy.app.tempdir, "_text.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scene.name)
    img = bpy.data.images.load(path, check_existing=False)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(res[1], res[0], 4)
    bpy.data.images.remove(img)
    bpy.data.objects.remove(txt, do_unlink=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    return px[:, :, 3]


def fit_mask(mask, width, height):
    """Crops a mask to its content and resizes it (nearest-ish, via numpy) into width x height."""
    ys, xs = np.nonzero(mask > 0.02)
    m = mask[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    yi = (np.linspace(0, m.shape[0] - 1, height)).astype(int)
    xi = (np.linspace(0, m.shape[1] - 1, width)).astype(int)
    return m[np.ix_(yi, xi)]


# ================================================================ sausages (шұжық, қазы)

def sausage_mesh(length, R, sag, nu, n_body, nub_len, nub_r, shoulder, lump_amp, lump_freq, seed, fold_amp=0.12):
    L = length
    B = Vector((0, 1, 0))

    def center(s):
        return Vector(((s - 0.5) * L, 0.0, sag * (1 - (2 * s - 1) ** 2)))

    def tangent(s):
        return Vector((L, 0.0, -4 * sag * (2 * s - 1))).normalized()

    def radius(s, u):
        d = min(s, 1 - s) * L
        if d < nub_len:
            k = d / nub_len
            return nub_r * math.sqrt(min(1.0, k * 2.5)) * (1 + 0.12 * math.sin(TAU * u * 4 + k * 5))
        k = min(1.0, (d - nub_len) / shoulder)
        base = nub_r + (R - nub_r) * math.sin(k * math.pi / 2) ** 0.55
        folds = 1 + fold_amp * (1 - k) ** 2 * math.sin(TAU * u * 7 + d * 20)
        c = center(s)
        a = TAU * u
        lump = 1 + lump_amp * k * fbm(c * lump_freq + Vector((math.cos(a), math.sin(a), 0)) * 0.6, 3, seed)
        return base * folds * lump

    def f(u, v):
        s = v
        c = center(s)
        t = tangent(s)
        n = B.cross(t).normalized()
        r = radius(s, u)
        a = TAU * u
        return c + r * (math.cos(a) * n + math.sin(a) * B)

    end = nub_len + shoulder
    ds = [0.0, 0.004, 0.012, 0.024, nub_len * 0.6, nub_len, nub_len + shoulder * 0.12,
          nub_len + shoulder * 0.3, nub_len + shoulder * 0.55, nub_len + shoulder * 0.8, end]
    head = [d / L for d in ds]
    body = [(end + (L - 2 * end) * (i + 1) / (n_body + 1)) / L for i in range(n_body)]
    vs = head + body + [1 - x for x in reversed(head)]
    return K.grid_surface(f, nu, vs, poles=True)


def sausage_tie(obj, length, nub_len):
    half = length / 2
    attr_fn(obj, "tie", lambda co: 1 - smoothstep(nub_len - 0.01, nub_len + 0.025, half - abs(co.x)))


def twine(nb, p):
    """Cotton twine wrapped around the tied ends."""
    bands = nb.wave(p, 26, distortion=3.0, detail=2, direction='X')['Fac']
    fibre = nb.noise(p, 120, 3, 0.6)['Fac']
    col = nb.ramp(bands, [(0.2, (0.55, 0.45, 0.3)), (0.6, (0.86, 0.79, 0.62))])
    col = nb.mix(col, (0.5, 0.42, 0.3), nb.mapr(fibre, 0.4, 0.3))
    return col, bands


def shuzhuk():
    L = 1.5
    bm = sausage_mesh(L, 0.19, 0.17, 24, 34, 0.06, 0.042, 0.17, 0.08, 4.5, 3)
    obj = K.mesh_object("shuzhuk", bm)
    sausage_tie(obj, L, 0.06)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("shuzhuk")
    nb = K.NB(mat)
    p = nb.coord('Object')
    tie = nb.attr("tie")
    mott = nb.noise(p, 5, 4, 0.6)['Fac']
    fat = nb.mapr(nb.noise(nb.mapping(p, (0.6, 1, 1)), 7, 3, 0.55)['Fac'], 0.56, 0.7)
    specks = nb.mapr(nb.voronoi(p, 22)['Distance'], 0.08, 0.03)
    stretched = nb.mapping(p, (0.35, 1.0, 1.0))
    wrinkles = nb.voronoi(nb.vmath('ADD', stretched, nb.vmath('SCALE', nb.noise(p, 6)['Color'], scale=0.08)),
                          16, feature='DISTANCE_TO_EDGE')['Distance']
    wr = nb.mapr(wrinkles, 0.0, 0.05, 1.0, 0.0)
    fine = nb.noise(p, 70, 4, 0.6)['Fac']

    col = nb.ramp(mott, [(0.3, (0.12, 0.03, 0.025)), (0.55, (0.24, 0.065, 0.045)), (0.8, (0.36, 0.11, 0.07))])
    col = nb.mix(col, (0.6, 0.42, 0.22), nb.math('MULTIPLY', fat, 0.65))
    col = nb.mix(col, (0.45, 0.3, 0.2), nb.math('MULTIPLY', specks, 0.25))
    col = nb.mix(col, (0.07, 0.02, 0.015), nb.math('MULTIPLY', wr, 0.18))
    tw_col, tw_h = twine(nb, p)
    col = nb.mix(col, tw_col, tie)

    rough = nb.mapr(fine, 0.3, 0.7, 0.22, 0.4)
    rough = nb.math('MAXIMUM', rough, nb.math('MULTIPLY', tie, 0.9))
    h = nb.math('ADD', nb.math('MULTIPLY', fine, 0.25), nb.math('MULTIPLY', fat, 0.35))
    h = nb.math('SUBTRACT', h, nb.math('MULTIPLY', wr, 0.2))
    h = nb.mix(h, tw_h, tie)
    nb.principled(col, rough, nb.sep(h)[0], bump=0.35, bump_distance=0.012)
    K.finish(obj, "shuzhuk", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        base = nb.noise(q, 5, 4, 0.6)['Fac']
        chunks = nb.voronoi(nb.vmath('ADD', q, nb.vmath('SCALE', nb.noise(q, 5)['Color'], scale=0.08)), 9)
        fat = nb.mapr(nb.voronoi(q, 26, randomness=1)['Distance'], 0.16, 0.1)
        fat_sel = nb.math('GREATER_THAN', nb.sep(nb.voronoi(q, 26)['Color'])[0], 0.62)
        pepper = nb.math('MULTIPLY', nb.mapr(nb.voronoi(q, 40)['Distance'], 0.05, 0.03),
                         nb.math('GREATER_THAN', nb.sep(nb.voronoi(q, 40)['Color'])[1], 0.85))
        c = nb.ramp(base, [(0.3, (0.32, 0.08, 0.07)), (0.7, (0.52, 0.15, 0.12))])
        c = nb.mix(c, (0.62, 0.22, 0.18), nb.math('MULTIPLY', nb.sep(chunks['Color'])[2], 0.4))
        c = nb.mix(c, (0.2, 0.05, 0.04), nb.mapr(chunks['Distance'], 0.05, 0.0))
        c = nb.mix(c, (0.93, 0.86, 0.8), nb.math('MULTIPLY', fat, fat_sel))
        return nb.mix(c, (0.08, 0.05, 0.04), pepper)
    K.bake_inside("shuzhuk", inside)
    drop_attributes(obj)
    return obj


def kazy():
    L = 1.3
    bm = sausage_mesh(L, 0.28, 0.08, 26, 28, 0.06, 0.05, 0.2, 0.05, 3.2, 7)
    obj = K.mesh_object("kazy", bm)
    sausage_tie(obj, L, 0.06)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("kazy")
    nb = K.NB(mat)
    p = nb.coord('Object')
    tie = nb.attr("tie")
    mott = nb.noise(p, 3.2, 4, 0.6)['Fac']
    meat = nb.mapr(nb.noise(nb.mapping(p, (0.5, 1, 1)), 4.5, 4, 0.6, distortion=0.4)['Fac'], 0.4, 0.62)
    streak = nb.mapr(nb.noise(nb.mapping(p, (0.25, 1, 1)), 9, 3, 0.5)['Fac'], 0.6, 0.72)
    spots = nb.mapr(nb.voronoi(p, 18)['Distance'], 0.1, 0.03)
    fine = nb.noise(p, 60, 4, 0.6)['Fac']
    wrinkles = nb.voronoi(nb.vmath('ADD', nb.mapping(p, (0.3, 1, 1)), nb.vmath('SCALE', nb.noise(p, 5)['Color'], scale=0.1)),
                          10, feature='DISTANCE_TO_EDGE')['Distance']
    wr = nb.mapr(wrinkles, 0.0, 0.04, 1.0, 0.0)

    col = nb.ramp(mott, [(0.3, (0.62, 0.57, 0.52)), (0.7, (0.78, 0.74, 0.66))])
    col = nb.mix(col, (0.46, 0.32, 0.29), nb.math('MULTIPLY', meat, 0.85))
    col = nb.mix(col, (0.9, 0.85, 0.7), nb.math('MULTIPLY', streak, 0.7))
    col = nb.mix(col, (0.62, 0.5, 0.44), nb.math('MULTIPLY', spots, 0.3))
    col = nb.mix(col, (0.5, 0.43, 0.38), nb.math('MULTIPLY', wr, 0.12))
    tw_col, tw_h = twine(nb, p)
    col = nb.mix(col, tw_col, tie)

    rough = nb.mapr(fine, 0.3, 0.7, 0.25, 0.42)
    rough = nb.math('MAXIMUM', rough, nb.math('MULTIPLY', tie, 0.9))
    h = nb.math('ADD', nb.math('MULTIPLY', fine, 0.2), nb.math('MULTIPLY', meat, 0.3))
    h = nb.math('SUBTRACT', h, nb.math('MULTIPLY', wr, 0.15))
    h = nb.mix(h, tw_h, tie)
    nb.principled(col, rough, nb.sep(h)[0], bump=0.3, bump_distance=0.015)
    K.finish(obj, "kazy", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        warp = nb.vmath('ADD', q, nb.vmath('SCALE', nb.noise(q, 3, 3)['Color'], scale=0.25))
        pieces = nb.voronoi(warp, 6)
        fat_sel = nb.math('GREATER_THAN', nb.sep(pieces['Color'])[0], 0.72)
        base = nb.noise(q, 14, 5, 0.6)['Fac']
        meat = nb.ramp(base, [(0.3, (0.33, 0.2, 0.17)), (0.7, (0.5, 0.33, 0.28))])
        meat = nb.mix(meat, (0.58, 0.4, 0.36), nb.math('MULTIPLY', nb.sep(pieces['Color'])[1], 0.5))
        fat = nb.ramp(nb.noise(q, 9, 3)['Fac'], [(0.3, (0.86, 0.8, 0.66)), (0.7, (0.95, 0.91, 0.8))])
        c = nb.mix(meat, fat, fat_sel)
        seams = nb.voronoi(warp, 6, feature='DISTANCE_TO_EDGE')['Distance']
        return nb.mix(c, (0.3, 0.18, 0.15), nb.math('MULTIPLY', nb.mapr(seams, 0.04, 0.0), 0.5))
    K.bake_inside("kazy", inside)
    drop_attributes(obj)
    return obj


# ================================================================ Құрт

def kurt():
    bm = K.blob(9, (0.30, 0.29, 0.275))
    K.displace(bm, lambda co, n: 0.022 * fbm(co * 3.2, 3, seed=1) + 0.006 * fbm(co * 11, 2, seed=2))
    obj = K.mesh_object("kurt", bm)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("kurt")
    nb = K.NB(mat)
    p = nb.coord('Object')
    large = nb.noise(p, 3.5, 3, 0.55)['Fac']
    fine = nb.noise(p, 38, 6, 0.65)['Fac']
    powder = nb.noise(p, 140, 2, 0.5)['Fac']
    warped = nb.vmath('ADD', p, nb.vmath('SCALE', nb.noise(p, 4, 3, 0.5)['Color'], scale=0.12))
    cracks = nb.voronoi(warped, 5.0, feature='DISTANCE_TO_EDGE')['Distance']
    crack_mask = nb.mapr(nb.noise(p, 2.2, 2, 0.5)['Fac'], 0.56, 0.66)
    crack = nb.math('MULTIPLY', nb.mapr(cracks, 0.0, 0.012, 1.0, 0.0), crack_mask)

    col = nb.ramp(large, [(0.3, (0.86, 0.84, 0.77)), (0.7, (0.95, 0.94, 0.9))])
    col = nb.mix(col, (0.98, 0.98, 0.96), nb.mapr(powder, 0.55, 0.75), 'MIX')
    col = nb.mix(col, (0.80, 0.77, 0.69), nb.mapr(fine, 0.35, 0.2), 'MIX')
    col = nb.mix(col, (0.78, 0.75, 0.67), nb.math('MULTIPLY', crack, 0.6), 'MIX')

    height = nb.math('ADD', nb.math('MULTIPLY', fine, 0.6), nb.math('MULTIPLY', powder, 0.25))
    height = nb.math('SUBTRACT', height, nb.math('MULTIPLY', crack, 0.9))
    nb.principled(col, nb.mapr(powder, 0, 1, 0.82, 0.95), height, bump=0.35, bump_distance=0.01)
    K.finish(obj, "kurt", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        n1 = nb.noise(q, 6, 4, 0.6)['Fac']
        grain = nb.noise(q, 60, 3, 0.6)['Fac']
        pores = nb.voronoi(q, 28)['Distance']
        c = nb.ramp(n1, [(0.35, (0.93, 0.91, 0.84)), (0.7, (0.99, 0.98, 0.94))])
        c = nb.mix(c, (0.86, 0.83, 0.74), nb.mapr(grain, 0.3, 0.22))
        return nb.mix(c, (0.84, 0.81, 0.72), nb.mapr(pores, 0.06, 0.02))
    K.bake_inside("kurt", inside)
    return obj


# ================================================================ Бауырсақ

def baursak():
    bm = K.blob(10, (0.45, 0.40, 0.38), exponent=2.4)
    K.displace(bm, lambda co, n: 0.04 * fbm(co * 2.4, 3, seed=11) + 0.012 * fbm(co * 7, 2, seed=12))
    obj = K.mesh_object("baursak", bm)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("baursak")
    nb = K.NB(mat)
    p = nb.coord('Object')
    s = nb.sep(p)
    belt_noise = nb.math('MULTIPLY', nb.noise(p, 3, 2)['Fac'], 0.12)
    belt = nb.mapr(nb.math('ABSOLUTE', nb.math('ADD', s[2], nb.math('SUBTRACT', belt_noise, 0.06))), 0.02, 0.12, 1.0, 0.0,
                   interp='SMOOTHSTEP')
    tone = nb.noise(p, 3, 4, 0.6)['Fac']
    blisters = nb.voronoi(p, 34)['Distance']
    bl = nb.mapr(blisters, 0.18, 0.02)
    fine = nb.noise(p, 90, 3, 0.6)['Fac']

    col = nb.ramp(tone, [(0.25, (0.7, 0.4, 0.12)), (0.55, (0.84, 0.54, 0.2)), (0.8, (0.9, 0.64, 0.28))])
    col = nb.mix(col, (0.94, 0.74, 0.42), nb.math('MULTIPLY', belt, 0.45))
    col = nb.mix(col, (0.9, 0.62, 0.3), nb.math('MULTIPLY', bl, 0.35))
    col = nb.mix(col, (0.5, 0.26, 0.08), nb.mapr(fine, 0.3, 0.18))
    h = nb.math('ADD', nb.math('MULTIPLY', bl, 0.5), nb.math('MULTIPLY', fine, 0.3))
    nb.principled(col, nb.mapr(tone, 0, 1, 0.38, 0.55), h, bump=0.3, bump_distance=0.012)
    K.finish(obj, "baursak", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        warp = nb.vmath('ADD', q, nb.vmath('SCALE', nb.noise(q, 4)['Color'], scale=0.06))
        holes = nb.voronoi(warp, 22, feature='SMOOTH_F1')['Distance']
        big = nb.voronoi(warp, 7)['Distance']
        c = nb.ramp(nb.noise(q, 5, 3)['Fac'], [(0.3, (0.97, 0.88, 0.66)), (0.7, (1.0, 0.94, 0.78))])
        c = nb.mix(c, (0.82, 0.68, 0.45), nb.mapr(holes, 0.12, 0.05))
        return nb.mix(c, (0.76, 0.6, 0.38), nb.mapr(big, 0.08, 0.04))
    K.bake_inside("baursak", inside)
    return obj


# ================================================================ Апорт / Алтын апорт

APPLE_PROFILE = [
    (0.0, 0.64), (0.026, 0.635), (0.03, 0.58), (0.033, 0.5), (0.04, 0.455), (0.08, 0.44), (0.17, 0.46),
    (0.27, 0.5), (0.38, 0.48), (0.48, 0.4), (0.545, 0.25), (0.56, 0.04), (0.53, -0.12), (0.47, -0.25),
    (0.37, -0.35), (0.24, -0.4), (0.13, -0.395), (0.07, -0.37), (0.03, -0.375), (0.0, -0.38),
]


def apple_mesh():
    def radial(u, v, r, z):
        shoulder = smoothstep(0.1, 0.45, z) * (1 - smoothstep(0.46, 0.5, z))
        lobes = 1 + 0.035 * math.cos(TAU * u * 5 + 0.4) * shoulder
        lop = 1 + 0.045 * math.cos(TAU * u - 0.6) * smoothstep(-0.3, 0.3, z)
        wob = 1 + 0.02 * fbm(Vector((math.cos(TAU * u), math.sin(TAU * u), z * 2)) * 1.5, 2, seed=5)
        return lobes * lop * wob if r > 0.05 else 1.0

    bm = K.lathe(APPLE_PROFILE, 32, 42, radial)
    for v in bm.verts:  # bend the stem
        if v.co.z > 0.47 and v.co.xy.length < 0.05:
            k = (v.co.z - 0.47) / 0.17
            v.co.x += 0.06 * k * k
    return bm


def aport_like(name, palette):
    obj = K.mesh_object(name, apple_mesh())
    def stem(co):
        if co.z < 0.44:
            return 0.0
        off = 0.06 * max(0.0, (co.z - 0.47) / 0.17) ** 2
        return smoothstep(0.455, 0.49, co.z) * (1 - smoothstep(0.04, 0.06, Vector((co.x - off, co.y, 0)).length))
    attr_fn(obj, "stem", stem)
    attr_fn(obj, "cavity", lambda co: 1 - smoothstep(0.08, 0.26, co.xy.length) if abs(co.z) > 0.3 else 0.0)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material(name)
    nb = K.NB(mat)
    p = nb.coord('Object')
    stem = nb.attr("stem")
    cav = nb.attr("cavity")
    s = nb.sep(p)
    side = nb.math('ADD', nb.math('MULTIPLY', s[0], -0.9), nb.math('MULTIPLY', s[2], 0.6))
    blush = nb.mapr(nb.math('ADD', side, nb.math('MULTIPLY', nb.noise(p, 2.5, 3)['Fac'], 0.7)), palette['blush_lo'], palette['blush_hi'])
    streaks = nb.wave(nb.mapping(p, (1, 1, 0.25)), 7, distortion=6, detail=3, kind='BANDS', direction='Z')['Fac']
    streaks = nb.math('MULTIPLY', nb.mapr(streaks, 0.3, 0.8), nb.mapr(nb.noise(p, 12, 3)['Fac'], 0.3, 0.7))
    speck = nb.voronoi(p, 55)
    dots = nb.math('MULTIPLY', nb.mapr(speck['Distance'], 0.1, 0.05),
                   nb.math('GREATER_THAN', nb.sep(speck['Color'])[0], 0.35))
    fine = nb.noise(p, 45, 4, 0.6)['Fac']

    c = nb.ramp(nb.noise(p, 4, 3)['Fac'], palette['ground'])
    c = nb.mix(c, nb.ramp(fine, palette['blush']), blush)
    c = nb.mix(c, palette['streak'], nb.math('MULTIPLY', nb.math('MULTIPLY', streaks, blush), 0.55))
    c = nb.mix(c, palette['cavity'], nb.math('MULTIPLY', cav, 0.8))
    c = nb.mix(c, palette['dots'], nb.math('MULTIPLY', dots, 0.7))
    c = nb.mix(c, nb.ramp(fine, [(0.3, (0.16, 0.1, 0.05)), (0.7, (0.3, 0.2, 0.1))]), stem)
    rough = nb.math('ADD', nb.mapr(fine, 0, 1, palette['rough'] - 0.06, palette['rough'] + 0.06),
                    nb.math('MULTIPLY', nb.math('ADD', stem, dots), 0.4))
    h = nb.math('ADD', nb.math('MULTIPLY', fine, 0.15), nb.math('MULTIPLY', dots, 0.25))
    nb.principled(c, rough, h, bump=0.25, bump_distance=0.006)
    K.finish(obj, name, mat)
    drop_attributes(obj)
    return obj


def aport():
    obj = aport_like("aport", {
        'ground': [(0.3, (0.62, 0.66, 0.3)), (0.7, (0.78, 0.76, 0.4))],
        'blush': [(0.3, (0.7, 0.13, 0.2)), (0.7, (0.86, 0.3, 0.38))],
        'streak': (0.55, 0.05, 0.1),
        'blush_lo': -0.35, 'blush_hi': 0.25,
        'cavity': (0.6, 0.6, 0.3),
        'dots': (0.95, 0.85, 0.7),
        'rough': 0.33,
    })

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        fib = nb.noise(nb.mapping(q, (1, 3, 1)), 18, 4, 0.6)['Fac']
        c = nb.ramp(nb.noise(q, 4, 3)['Fac'], [(0.3, (0.95, 0.9, 0.7)), (0.7, (0.99, 0.96, 0.82))])
        c = nb.mix(c, (0.9, 0.85, 0.62), nb.mapr(fib, 0.4, 0.3))
        return c
    K.bake_inside("aport", inside)
    return obj


def golden_aport():
    return aport_like("golden_aport", {
        'ground': [(0.3, (0.9, 0.72, 0.22)), (0.7, (0.98, 0.84, 0.36))],
        'blush': [(0.3, (0.95, 0.66, 0.18)), (0.7, (1.0, 0.76, 0.28))],
        'streak': (0.9, 0.55, 0.14),
        'blush_lo': 0.1, 'blush_hi': 0.9,
        'cavity': (0.75, 0.52, 0.18),
        'dots': (0.72, 0.46, 0.18),
        'rough': 0.26,
    })


# ================================================================ flat things: шелпек, самса, той

def flat_surface(outline, front, back, nu, n_front, n_rim, n_back, rim_half):
    """
    A closed flat shape facing -Y.
    outline(u) -> rim radius; front(x, z, t) / back(x, z, t) -> surface offset (>0) from the mid plane,
    t = 0 at the centre and 1 at the rim; rim_half(u) -> half thickness at the rim.
    """
    def f(u, v):
        a = TAU * u
        R = outline(u)
        d = Vector((math.cos(a), 0, math.sin(a)))
        vf = n_front / (n_front + n_rim + n_back)
        vb = (n_front + n_rim) / (n_front + n_rim + n_back)
        if v <= vf:
            t = v / vf
            p = d * (R * t)
            return Vector((p.x, -(rim_half(u) + front(p.x, p.z, t) * (1 - t ** 4)), p.z))
        if v >= vb:
            t = (1 - v) / (1 - vb)
            p = d * (R * t)
            return Vector((p.x, rim_half(u) + back(p.x, p.z, t) * (1 - t ** 4), p.z))
        k = (v - vf) / (vb - vf)
        phi = -math.pi / 2 + math.pi * k
        e = rim_half(u)
        p = d * (R + e * 0.8 * math.cos(phi))
        return Vector((p.x, e * math.sin(phi), p.z))

    total = n_front + n_rim + n_back
    vs = [i / total for i in range(total + 1)]
    return K.grid_surface(f, nu, vs, poles=True)


def bubbles(seed, count, extent, rmin, rmax, hmin, hmax):
    rng = random.Random(seed)
    out = []
    for _ in range(count):
        a = rng.uniform(0, TAU)
        d = extent * math.sqrt(rng.uniform(0, 1))
        out.append((d * math.cos(a), d * math.sin(a), rng.uniform(rmin, rmax), rng.uniform(hmin, hmax)))

    def field(x, z):
        h = 0.0
        for bx, bz, r, hh in out:
            q = ((x - bx) ** 2 + (z - bz) ** 2) / (r * r)
            if q < 1:
                h = max(h, hh * (1 - q) ** 1.5)
        return h
    return field


def shelpek():
    R = 0.62
    bf = bubbles(21, 34, 0.5, 0.06, 0.16, 0.02, 0.065)
    bb = bubbles(22, 30, 0.52, 0.05, 0.13, 0.008, 0.03)

    def outline(u):
        a = TAU * u
        return R * (1 + 0.035 * fbm(Vector((math.cos(a), math.sin(a), 0)) * 1.6, 3, seed=4))

    def warp(x, z):
        return 0.025 * fbm(Vector((x, z, 0.3)) * 1.8, 2, seed=8)

    bm = flat_surface(outline,
                      lambda x, z, t: 0.02 + bf(x, z) + warp(x, z),
                      lambda x, z, t: 0.015 + bb(x, z) - warp(x, z),
                      48, 13, 5, 12, lambda u: 0.028)
    obj = K.mesh_object("shelpek", bm)
    attr_fn(obj, "bubble", lambda co: min(1.0, max(bf(co.x, co.z) / 0.045 if co.y < 0 else bb(co.x, co.z) / 0.025, 0)))
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("shelpek")
    nb = K.NB(mat)
    p = nb.coord('Object')
    bub = nb.attr("bubble")
    tone = nb.noise(p, 4, 4, 0.6)['Fac']
    spots = nb.mapr(nb.noise(p, 9, 3, 0.6)['Fac'], 0.58, 0.72)
    micro = nb.voronoi(p, 45)['Distance']
    fine = nb.noise(p, 80, 3, 0.6)['Fac']
    brown = nb.math('MULTIPLY', nb.mapr(bub, 0.35, 0.95, interp='SMOOTHSTEP'), nb.mapr(nb.noise(p, 14, 3)['Fac'], 0.3, 0.55, 0.6, 1.0))
    brown = nb.math('MAXIMUM', brown, nb.math('MULTIPLY', spots, 0.25))

    c = nb.ramp(tone, [(0.3, (0.92, 0.72, 0.36)), (0.7, (0.97, 0.82, 0.48))])
    c = nb.mix(c, nb.ramp(tone, [(0.3, (0.76, 0.44, 0.13)), (0.7, (0.86, 0.56, 0.2))]), brown)
    c = nb.mix(c, (0.99, 0.9, 0.62), nb.math('MULTIPLY', nb.mapr(micro, 0.1, 0.03), 0.2))
    c = nb.mix(c, (0.66, 0.4, 0.14), nb.math('MULTIPLY', nb.mapr(fine, 0.26, 0.18), 0.5))
    h = nb.math('ADD', nb.math('MULTIPLY', nb.mapr(micro, 0.15, 0.0), 0.35), nb.math('MULTIPLY', fine, 0.3))
    nb.principled(c, nb.mapr(tone, 0, 1, 0.3, 0.45), h, bump=0.15, bump_distance=0.008)
    K.finish(obj, "shelpek", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        layers = nb.wave(q, 9, distortion=4, detail=2, direction='Y')['Fac']
        holes = nb.voronoi(q, 30)['Distance']
        c = nb.ramp(nb.noise(q, 5, 3)['Fac'], [(0.3, (0.96, 0.86, 0.64)), (0.7, (1.0, 0.93, 0.76))])
        c = nb.mix(c, (0.93, 0.82, 0.58), nb.math('MULTIPLY', nb.mapr(layers, 0.2, 0.0), 0.5))
        return nb.mix(c, (0.86, 0.72, 0.48), nb.mapr(holes, 0.08, 0.03))
    K.bake_inside("shelpek", inside)
    drop_attributes(obj)
    return obj


def sd_triangle(x, y, r):
    """Signed distance to an equilateral triangle (pointing up) with inradius-ish size r (Inigo Quilez)."""
    k = math.sqrt(3.0)
    x = abs(x) - r
    y = y + r / k
    if x + k * y > 0.0:
        x, y = (x - k * y) / 2.0, (-k * x - y) / 2.0
    x -= min(max(x, -2.0 * r), 0.0)
    return -math.hypot(x, y) * (1.0 if y > 0 else -1.0)


def samsa_outline(size, corner):
    radii = []

    def sdf(x, z):
        return sd_triangle(x, z, size) - corner

    def outline(u):
        a = TAU * u
        lo, hi = 0.0, 2.0
        for _ in range(28):
            mid = (lo + hi) / 2
            if sdf(math.cos(a) * mid, math.sin(a) * mid) < 0:
                lo = mid
            else:
                hi = mid
        return lo
    return outline, sdf


def sesame_image(extent, sdf, res=1024, count=55, seed=5):
    rng = np.random.default_rng(seed)
    idx = (np.arange(res) + 0.5) / res * 2 * extent - extent
    X, Z = np.meshgrid(idx, idx)
    h = np.zeros((res, res), np.float32)
    placed = 0
    tries = 0
    while placed < count and tries < 5000:
        tries += 1
        cx, cz = rng.uniform(-extent, extent, 2)
        if sdf(cx, cz) > -0.09:
            continue
        ang = rng.uniform(0, math.pi)
        a, b = 0.027, 0.013
        ca, sa = math.cos(ang), math.sin(ang)
        lx = (X - cx) * ca + (Z - cz) * sa
        lz = -(X - cx) * sa + (Z - cz) * ca
        d = (lx / a) ** 2 + (lz / b) ** 2
        h = np.maximum(h, np.sqrt(np.clip(1 - d, 0, 1)))
        placed += 1
    return gray_image("samsa_sesame", h)


def samsa():
    outline, sdf = samsa_outline(0.42, 0.1)

    def seam(x, z):
        # three folded seams from the centre to the middle of each side
        a = math.atan2(z, x)
        d = math.hypot(x, z)
        best = 1.0
        for k in range(3):
            ang = -math.pi / 2 + k * TAU / 3
            diff = abs((a - ang + math.pi) % TAU - math.pi)
            best = min(best, diff * d)
        return max(0.0, 1 - best / 0.035) * smoothstep(0.08, 0.2, d)

    def front(x, z, t):
        return 0.03 + 0.12 * (1 - t ** 3.5) ** 0.6 + 0.012 * fbm(Vector((x, z, 0)) * 4, 2, seed=31) + 0.01 * seam(x, z)

    def back(x, z, t):
        return 0.02 + 0.07 * (1 - t ** 3) ** 0.6

    bm = flat_surface(outline, front, back, 60, 14, 5, 9, lambda u: 0.02)
    obj = K.mesh_object("samsa", bm)
    attr_fn(obj, "seam", lambda co: seam(co.x, co.z) if co.y < 0 else 0.0)
    attr_fn(obj, "front", lambda co: smoothstep(0.0, -0.05, co.y))
    K.centre(obj)
    K.unwrap(obj)
    sesame = sesame_image(0.7, sdf)

    mat = K.source_material("samsa")
    nb = K.NB(mat)
    p = nb.coord('Object')
    front_m = nb.attr("front")
    seam_m = nb.attr("seam")
    s = nb.sep(p)
    seeds = nb.math('MULTIPLY', nb.sep(nb.image(sesame, planar_xz(nb, p, 0.7)).outputs['Color'])[0], front_m)
    seed_m = nb.mapr(seeds, 0.05, 0.3)
    tone = nb.noise(p, 3.5, 4, 0.6)['Fac']
    char = nb.mapr(nb.noise(p, 7, 3, 0.6)['Fac'], 0.6, 0.74)
    fine = nb.noise(p, 70, 3, 0.6)['Fac']
    edge = nb.mapr(nb.math('ABSOLUTE', s[1]), 0.02, 0.09, 1.0, 0.0)

    c = nb.ramp(tone, [(0.25, (0.58, 0.27, 0.06)), (0.55, (0.72, 0.38, 0.1)), (0.85, (0.84, 0.5, 0.16))])
    c = nb.mix(c, (0.92, 0.66, 0.32), nb.math('MULTIPLY', edge, 0.6))
    c = nb.mix(c, (0.45, 0.2, 0.05), nb.math('MULTIPLY', char, front_m))
    c = nb.mix(c, (0.58, 0.3, 0.08), nb.math('MULTIPLY', seam_m, 0.6))
    c = nb.mix(c, (0.52, 0.28, 0.08), nb.mapr(fine, 0.3, 0.2))
    seed_col = nb.ramp(seeds, [(0.1, (0.8, 0.64, 0.38)), (0.6, (0.95, 0.88, 0.7))])
    c = nb.mix(c, seed_col, seed_m)
    rough = nb.math('ADD', nb.mapr(tone, 0, 1, 0.22, 0.34), nb.math('MULTIPLY', seed_m, 0.4))
    h = nb.math('ADD', nb.math('MULTIPLY', seeds, 1.0), nb.math('MULTIPLY', fine, 0.2))
    h = nb.math('SUBTRACT', h, nb.math('MULTIPLY', seam_m, 0.4))
    nb.principled(c, rough, h, bump=0.4, bump_distance=0.012)
    K.finish(obj, "samsa", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        mince = nb.voronoi(nb.vmath('ADD', q, nb.vmath('SCALE', nb.noise(q, 6)['Color'], scale=0.05)), 26)
        onion = nb.voronoi(q, 11)
        onion_m = nb.math('MULTIPLY', nb.mapr(onion['Distance'], 0.12, 0.07),
                          nb.math('GREATER_THAN', nb.sep(onion['Color'])[0], 0.55))
        c = nb.ramp(nb.sep(mince['Color'])[0], [(0.2, (0.3, 0.19, 0.13)), (0.8, (0.5, 0.34, 0.24))])
        c = nb.mix(c, (0.2, 0.12, 0.08), nb.mapr(mince['Distance'], 0.08, 0.0))
        c = nb.mix(c, (0.86, 0.8, 0.6), onion_m)
        pepper = nb.math('MULTIPLY', nb.mapr(nb.voronoi(q, 45)['Distance'], 0.05, 0.03),
                         nb.math('GREATER_THAN', nb.sep(nb.voronoi(q, 45)['Color'])[2], 0.8))
        return nb.mix(c, (0.08, 0.05, 0.03), pepper)
    K.bake_inside("samsa", inside)
    drop_attributes(obj)
    return obj


def coin_relief(res=1024):
    """Height map of the coin face: shanyrak with uyks, ram-horn ornament ring and a beaded border."""
    idx = (np.arange(res) + 0.5) / res * 2 - 1
    X, Y = np.meshgrid(idx, idx)
    R = np.hypot(X, Y)
    A = np.arctan2(Y, X)

    def band(d, w, soft=0.012):
        return np.clip((w - np.abs(d)) / soft, 0, 1)

    h = np.zeros_like(X)
    # shanyrak ring and crossed curved bars (күлдіреуіш)
    h = np.maximum(h, band(R - 0.3, 0.035))
    inner = R < 0.3
    for k in (-1, 0, 1):
        off = k * 0.1
        h = np.maximum(h, band(Y - off - 0.22 * (X ** 2) * np.sign(off if off else 1) * (1 if k else 0), 0.022) * inner)
        h = np.maximum(h, band(X - off - 0.22 * (Y ** 2) * np.sign(off if off else 1) * (1 if k else 0), 0.022) * inner)
    # uyks (roof poles)
    n = 24
    local = (A / (2 * np.pi) * n) % 1.0 - 0.5
    ray = band(local * R * 2 * np.pi / n, 0.012) * ((R > 0.34) & (R < 0.5))
    h = np.maximum(h, ray * np.clip((0.5 - R) / 0.05, 0.3, 1))
    # ram-horn ornament: repeating double curls
    m = 12
    cell = (A / (2 * np.pi) * m) % 1.0 - 0.5
    s = cell * 2 * np.pi / m * 0.7          # tangential distance
    t = R - 0.7                              # radial distance
    horn = band(np.hypot(s, t + 0.02) - 0.075, 0.016) * (t + 0.02 > -0.01)
    curl_l = band(np.hypot(s + 0.075, t + 0.04) - 0.03, 0.013)
    curl_r = band(np.hypot(s - 0.075, t + 0.04) - 0.03, 0.013)
    stem = band(s, 0.014) * ((t > -0.09) & (t < 0.0))
    orn = np.maximum.reduce([horn, curl_l * (t + 0.04 < 0.02), curl_r * (t + 0.04 < 0.02), stem])
    h = np.maximum(h, orn * ((R > 0.58) & (R < 0.82)))
    # beaded border and rim step
    beads = 64
    bc = (A / (2 * np.pi) * beads) % 1.0 - 0.5
    bead = np.clip(1 - np.hypot(bc * 2 * np.pi * 0.9 / beads, R - 0.88) / 0.022, 0, 1)
    h = np.maximum(h, np.sqrt(bead))
    return gray_image("toy_relief", h)


def toy():
    R = 0.42

    def front(x, z, t):
        rim = smoothstep(0.84, 0.9, t)
        return 0.03 + 0.012 * rim

    bm = flat_surface(lambda u: R, front, front, 48, 10, 5, 10, lambda u: 0.025)
    obj = K.mesh_object("toy", bm)
    K.unwrap(obj)
    relief = coin_relief()

    mat = K.source_material("toy")
    nb = K.NB(mat)
    p = nb.coord('Object')
    s = nb.sep(p)
    mirrored = nb.comb(nb.math('MULTIPLY', s[0], nb.math('SIGN', nb.math('MULTIPLY', s[1], -1))), 0, s[2])
    uvp = planar_xz(nb, mirrored, R * 0.98)
    face = nb.mapr(nb.math('ABSOLUTE', s[1]), 0.03, 0.04)
    rel = nb.math('MULTIPLY', nb.sep(nb.image(relief, uvp, extension='CLIP').outputs['Color'])[0], face)
    ang = nb.math('ARCTAN2', s[2], s[0])
    reeds = nb.math('MULTIPLY', nb.math('ABSOLUTE', nb.math('SINE', nb.math('MULTIPLY', ang, 140))),
                    nb.math('SUBTRACT', 1.0, face))
    wear = nb.noise(p, 9, 4, 0.6)['Fac']
    scratches = nb.mapr(nb.wave(nb.mapping(p, (1, 1, 1), rot=(0, 0.7, 0)), 60, distortion=8)['Fac'], 0.93, 1.0)

    c = nb.ramp(wear, [(0.3, (0.86, 0.6, 0.2)), (0.7, (1.0, 0.78, 0.33))])
    c = nb.mix(c, (0.62, 0.38, 0.1), nb.mapr(rel, 0.05, 0.5, 0.6, 0.0))
    c = nb.mix(c, (1.0, 0.86, 0.45), nb.mapr(rel, 0.6, 1.0, 0.0, 0.5))
    c = nb.mix(c, (0.7, 0.46, 0.14), nb.math('MULTIPLY', scratches, 0.3))
    rough = nb.math('ADD', nb.mapr(wear, 0, 1, 0.18, 0.3), nb.mapr(rel, 0.0, 0.3, 0.12, 0.0))
    h = nb.math('ADD', rel, nb.math('MULTIPLY', reeds, 0.6))
    nb.principled(c, rough, h, bump=0.6, bump_distance=0.012, metallic=1.0)
    K.finish(obj, "toy", mat, metallic=1.0)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        return nb.ramp(nb.noise(q, 8, 4)['Fac'], [(0.3, (0.92, 0.7, 0.28)), (0.7, (1.0, 0.82, 0.4))])
    K.bake_inside("toy", inside)
    return obj


# ================================================================ Ірімшік, Жент

def irimshik():
    bm = K.blob(9, (0.34, 0.29, 0.27), exponent=5.0)
    K.displace(bm, lambda co, n: 0.018 * fbm(co * 3, 3, seed=41) + 0.006 * fbm(co * 10, 2, seed=42))
    obj = K.mesh_object("irimshik", bm)
    for v in obj.data.vertices:  # slightly skewed, hand-cut block
        v.co.x += 0.06 * v.co.z
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("irimshik")
    nb = K.NB(mat)
    p = nb.coord('Object')
    tone = nb.noise(p, 4, 4, 0.6)['Fac']
    pores = nb.voronoi(p, 30)
    pore = nb.math('MULTIPLY', nb.mapr(pores['Distance'], 0.12, 0.04),
                   nb.math('GREATER_THAN', nb.sep(pores['Color'])[0], 0.3))
    big = nb.math('MULTIPLY', nb.mapr(nb.voronoi(p, 11)['Distance'], 0.1, 0.05),
                  nb.math('GREATER_THAN', nb.sep(nb.voronoi(p, 11)['Color'])[1], 0.75))
    fine = nb.noise(p, 90, 3, 0.6)['Fac']
    c = nb.ramp(tone, [(0.25, (0.74, 0.43, 0.17)), (0.55, (0.86, 0.56, 0.26)), (0.85, (0.93, 0.68, 0.38))])
    c = nb.mix(c, (0.58, 0.32, 0.11), nb.math('MULTIPLY', nb.math('MAXIMUM', pore, big), 0.85))
    c = nb.mix(c, (0.96, 0.76, 0.48), nb.mapr(fine, 0.7, 0.85))
    h = nb.math('SUBTRACT', nb.math('MULTIPLY', fine, 0.3), nb.math('MAXIMUM', pore, big))
    nb.principled(c, nb.mapr(tone, 0, 1, 0.7, 0.85), h, bump=0.45, bump_distance=0.01)
    K.finish(obj, "irimshik", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        pores = nb.voronoi(q, 24)
        pore = nb.math('MULTIPLY', nb.mapr(pores['Distance'], 0.14, 0.05),
                       nb.math('GREATER_THAN', nb.sep(pores['Color'])[0], 0.25))
        c = nb.ramp(nb.noise(q, 5, 4)['Fac'], [(0.3, (0.9, 0.62, 0.32)), (0.7, (0.97, 0.74, 0.45))])
        return nb.mix(c, (0.7, 0.42, 0.18), nb.math('MULTIPLY', pore, 0.8))
    K.bake_inside("irimshik", inside)
    return obj


def zhent():
    bm = K.blob(10, (0.47, 0.2, 0.17), exponent=5.0)
    K.displace(bm, lambda co, n: 0.014 * fbm(co * 3.5, 3, seed=51) + 0.005 * fbm(co * 14, 2, seed=52))
    obj = K.mesh_object("zhent", bm)
    K.centre(obj)
    K.unwrap(obj)

    def granules(nb, q, scale):
        g = nb.voronoi(q, scale)
        shade = nb.sep(g['Color'])[0]
        edge = nb.mapr(g['Distance'], 0.35, 0.05)
        return shade, edge

    mat = K.source_material("zhent")
    nb = K.NB(mat)
    p = nb.coord('Object')
    shade, grain = granules(nb, p, 110)
    tone = nb.noise(p, 3, 4, 0.6)['Fac']
    raisin = nb.voronoi(p, 9)
    rais = nb.math('MULTIPLY', nb.mapr(raisin['Distance'], 0.16, 0.1),
                   nb.math('GREATER_THAN', nb.sep(raisin['Color'])[2], 0.72))
    c = nb.ramp(tone, [(0.3, (0.6, 0.44, 0.28)), (0.7, (0.72, 0.56, 0.38))])
    c = nb.mix(c, nb.ramp(shade, [(0.0, (0.5, 0.35, 0.21)), (0.5, (0.7, 0.54, 0.36)), (1.0, (0.88, 0.75, 0.56))]), 0.7)
    c = nb.mix(c, (0.4, 0.28, 0.17), nb.math('MULTIPLY', nb.math('SUBTRACT', 1.0, grain), 0.3))
    c = nb.mix(c, (0.2, 0.08, 0.07), rais)
    h = nb.math('SUBTRACT', grain, nb.math('MULTIPLY', rais, 0.4))
    nb.principled(c, nb.math('SUBTRACT', 0.88, nb.math('MULTIPLY', rais, 0.5)), h, bump=0.35, bump_distance=0.006)
    K.finish(obj, "zhent", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        shade, grain = granules(nb, q, 70)
        raisin = nb.voronoi(q, 6)
        rais = nb.math('MULTIPLY', nb.mapr(raisin['Distance'], 0.18, 0.12),
                       nb.math('GREATER_THAN', nb.sep(raisin['Color'])[2], 0.6))
        c = nb.ramp(shade, [(0.0, (0.5, 0.36, 0.22)), (0.5, (0.7, 0.54, 0.36)), (1.0, (0.86, 0.72, 0.52))])
        c = nb.mix(c, (0.36, 0.25, 0.15), nb.math('MULTIPLY', nb.math('SUBTRACT', 1.0, grain), 0.4))
        return nb.mix(c, (0.24, 0.09, 0.08), rais)
    K.bake_inside("zhent", inside)
    return obj


# ================================================================ Қарта

def karta():
    Rc, w, d = 0.36, 0.17, 0.15

    def f(u, v):
        a = TAU * u
        rc = Rc * (1 + 0.05 * fbm(Vector((math.cos(a), math.sin(a), 0)) * 1.3, 2, seed=61))
        ww = w * (1 + 0.12 * fbm(Vector((math.cos(a), math.sin(a), 0.5)) * 1.8, 2, seed=62))
        b = TAU * v
        cb, sb = math.cos(b), math.sin(b)
        e = 4.0  # superellipse: flat front/back cut faces, rounded sides
        rx = ww * math.copysign(abs(cb) ** (2 / e), cb)
        ry = d * math.copysign(abs(sb) ** (2 / e), sb)
        rr = rc + rx
        return Vector((rr * math.cos(a) + 0.03 * math.sin(a * 2), ry, rr * math.sin(a)))

    rings = 20
    bm = K.grid_surface(f, 48, [j / rings for j in range(rings)], poles=False, periodic_v=True)
    K.displace(bm, lambda co, n: 0.008 * fbm(co * 6, 2, seed=63))
    obj = K.mesh_object("karta", bm)

    def layer(co):
        r = Vector((co.x, 0, co.z)).length
        return min(1.0, max(0.0, (r - (Rc - w)) / (2 * w)))
    attr_fn(obj, "layer", layer)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("karta")
    nb = K.NB(mat)
    p = nb.coord('Object')
    lay = nb.attr("layer")
    tone = nb.noise(p, 5, 4, 0.6)['Fac']
    streak = nb.noise(nb.mapping(p, (1, 3, 1)), 6, 4, 0.55)['Fac']
    fine = nb.noise(p, 70, 3, 0.6)['Fac']
    fat = nb.ramp(tone, [(0.3, (0.88, 0.8, 0.64)), (0.7, (0.96, 0.91, 0.8))])
    fat = nb.mix(fat, (0.99, 0.97, 0.9), nb.math('MULTIPLY', nb.mapr(streak, 0.55, 0.75), 0.45))
    fat = nb.mix(fat, (0.82, 0.7, 0.56), nb.math('MULTIPLY', nb.mapr(streak, 0.4, 0.3), 0.35))
    gut = nb.ramp(tone, [(0.3, (0.38, 0.28, 0.25)), (0.7, (0.55, 0.43, 0.38))])
    c = nb.mix(gut, fat, nb.mapr(nb.math('ADD', lay, nb.math('MULTIPLY', tone, 0.12)), 0.26, 0.36))
    c = nb.mix(c, (0.72, 0.58, 0.46), nb.math('MULTIPLY', nb.mapr(lay, 0.36, 0.3), nb.mapr(lay, 0.24, 0.3)))
    h = nb.math('ADD', nb.math('MULTIPLY', fine, 0.3), nb.math('MULTIPLY', streak, 0.15))
    nb.principled(c, nb.mapr(fine, 0, 1, 0.22, 0.34), h, bump=0.2, bump_distance=0.01)
    K.finish(obj, "karta", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        streak = nb.wave(q, 5, distortion=6, detail=3, direction='X')['Fac']
        c = nb.ramp(nb.noise(q, 6, 4)['Fac'], [(0.3, (0.88, 0.82, 0.68)), (0.7, (0.96, 0.92, 0.82))])
        c = nb.mix(c, (0.99, 0.97, 0.92), nb.math('MULTIPLY', nb.mapr(streak, 0.6, 0.9), 0.25))
        return nb.mix(c, (0.62, 0.5, 0.44), nb.math('MULTIPLY', nb.mapr(nb.noise(q, 3, 2)['Fac'], 0.66, 0.74), 0.6))
    K.bake_inside("karta", inside)
    drop_attributes(obj)
    return obj


# ================================================================ Қымыз

BOTTLE_PROFILE = [
    (0.0, 0.56), (0.085, 0.56), (0.1, 0.55), (0.103, 0.52), (0.103, 0.46), (0.11, 0.452), (0.11, 0.44),
    (0.085, 0.435), (0.082, 0.41), (0.098, 0.4), (0.098, 0.39), (0.088, 0.385), (0.1, 0.35),
    (0.17, 0.3), (0.235, 0.23), (0.28, 0.14), (0.297, 0.06), (0.3, 0.0), (0.3, -0.14), (0.3, -0.3),
    (0.288, -0.36), (0.298, -0.42), (0.298, -0.48), (0.27, -0.535), (0.2, -0.558), (0.1, -0.555),
    (0.05, -0.54), (0.0, -0.545),
]
LABEL_Z = (-0.33, 0.02)


def kymyz_label(res=(2048, 384)):
    w, h = res
    img = np.zeros((h, w, 4), np.float32)
    ys = (np.arange(h) + 0.5) / h
    xs = (np.arange(w) + 0.5) / w
    X, Y = np.meshgrid(xs, ys)
    gold_a, gold_b = np.array([0.97, 0.82, 0.38]), np.array([0.9, 0.66, 0.24])
    base = gold_a * (1 - Y[..., None]) * 0 + gold_b + (gold_a - gold_b) * (1 - np.abs(Y - 0.5)[..., None] * 2)
    red = np.array([0.66, 0.08, 0.06])
    blue = np.array([0.06, 0.45, 0.72])
    col = base
    band = (Y < 0.16) | (Y > 0.84)
    col = np.where(band[..., None], red, col)
    line = (np.abs(Y - 0.18) < 0.012) | (np.abs(Y - 0.82) < 0.012)
    col = np.where(line[..., None], blue, col)
    # small ram-horn-ish diamonds in the red bands
    n = 48
    cx = (X * n) % 1.0 - 0.5
    for yc in (0.08, 0.92):
        cy = (Y - yc) / 0.16 * n * (h / w) * 6
        diamond = (np.abs(cx) + np.abs(cy / 3.2) < 0.3) & (np.abs(Y - yc) < 0.06)
        col = np.where(diamond[..., None], gold_a, col)
    # text in the front third
    font = "/Library/Fonts/Arial Unicode.ttf"
    title = fit_mask(render_text("ҚЫМЫЗ", font), int(w * 0.3), int(h * 0.36))
    sub = fit_mask(render_text("ҰЛТТЫҚ СУСЫН", font), int(w * 0.2), int(h * 0.1))
    x0 = w // 2 - title.shape[1] // 2
    y0 = int(h * 0.44)
    col[y0:y0 + title.shape[0], x0:x0 + title.shape[1]] = (
        col[y0:y0 + title.shape[0], x0:x0 + title.shape[1]] * (1 - title[..., None]) + red * title[..., None])
    x1 = w // 2 - sub.shape[1] // 2
    y1 = int(h * 0.27)
    col[y1:y1 + sub.shape[0], x1:x1 + sub.shape[1]] = (
        col[y1:y1 + sub.shape[0], x1:x1 + sub.shape[1]] * (1 - sub[..., None]) + blue * sub[..., None])
    # horse-free emblem: a small shanyrak circle on each side of the title
    for cxp in (0.5 - 0.2, 0.5 + 0.2):
        d = np.hypot((X - cxp) * w / h, Y - 0.55)
        ring = np.abs(d - 0.13) < 0.018
        cross = (d < 0.13) & ((np.abs((X - cxp) * w / h) < 0.012) | (np.abs(Y - 0.55) < 0.012))
        col = np.where((ring | cross)[..., None], blue, col)
    # linear -> the image is float (linear), convert from sRGB-ish design values
    lin = np.where(col <= 0.04045, col / 12.92, ((col + 0.055) / 1.055) ** 2.4)
    img[..., :3] = lin
    img[..., 3] = 1.0
    return np_image("kymyz_label", img)


def kymyz():
    def radial(u, v, r, z):
        if -0.52 > z:  # petaloid base feet
            return 1 + 0.03 * max(0.0, math.cos(TAU * u * 5))
        return 1.0

    bm = K.lathe(BOTTLE_PROFILE, 32, 46, radial)

    def quilt(co):
        z = co.z
        if not (0.1 < z < 0.37):
            return 0.0
        a = math.atan2(co.y, co.x) / TAU
        su, sz = a * 10, (z - 0.1) / 0.09
        fa = abs((su + sz) % 1.0 - 0.5)
        fb = abs((su - sz) % 1.0 - 0.5)
        return 0.012 * min(fa, fb) * 2 * smoothstep(0.1, 0.14, z) * (1 - smoothstep(0.33, 0.37, z))
    obj = K.mesh_object("kymyz", bm)
    K.unwrap(obj)
    label = kymyz_label()

    mat = K.source_material("kymyz")
    nb = K.NB(mat)
    p = nb.coord('Object')
    s = nb.sep(p)
    z = s[2]
    lab_m = nb.math('MULTIPLY', nb.mapr(z, LABEL_Z[0], LABEL_Z[0] + 0.004), nb.mapr(z, LABEL_Z[1] - 0.004, LABEL_Z[1], 1, 0))
    cap_m = nb.mapr(z, 0.43, 0.437)
    lab = nb.image(label, cylinder_uv(nb, p, *LABEL_Z), extension='EXTEND').outputs['Color']
    ang = nb.math('ARCTAN2', s[1], s[0])
    ribs = nb.math('ABSOLUTE', nb.math('SINE', nb.math('MULTIPLY', ang, 30)))
    su = nb.math('MULTIPLY', ang, 10 / TAU)
    sz = nb.math('DIVIDE', nb.math('SUBTRACT', z, 0.1), 0.09)
    fa = nb.math('ABSOLUTE', nb.math('SUBTRACT', nb.math('FRACT', nb.math('ADD', su, sz)), 0.5))
    fb = nb.math('ABSOLUTE', nb.math('SUBTRACT', nb.math('FRACT', nb.math('SUBTRACT', su, sz)), 0.5))
    quilt_h = nb.math('MULTIPLY', nb.math('MINIMUM', fa, fb), nb.math('MULTIPLY', nb.mapr(z, 0.1, 0.14), nb.mapr(z, 0.33, 0.37, 1, 0)))
    milk = nb.ramp(nb.noise(p, 3, 3)['Fac'], [(0.3, (0.9, 0.89, 0.84)), (0.7, (0.96, 0.95, 0.91))])
    milk = nb.mix(milk, (0.82, 0.8, 0.74), nb.math('MULTIPLY', nb.mapr(quilt_h, 0.3, 0.0), nb.mapr(z, 0.1, 0.14)))
    cap = nb.ramp(ribs, [(0.0, (0.62, 0.46, 0.16)), (1.0, (0.92, 0.76, 0.36))])
    c = nb.mix(milk, lab, lab_m)
    c = nb.mix(c, cap, cap_m)
    rough = nb.math('ADD', 0.16, nb.math('MULTIPLY', lab_m, 0.22))
    h = nb.math('ADD', nb.math('MULTIPLY', quilt_h, 1.0), nb.math('MULTIPLY', nb.math('MULTIPLY', ribs, cap_m), 0.5))
    nb.principled(c, rough, h, bump=0.35, bump_distance=0.02)
    K.finish(obj, "kymyz", mat)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        bub = nb.voronoi(q, 20)
        b = nb.math('MULTIPLY', nb.mapr(bub['Distance'], 0.1, 0.06), nb.math('GREATER_THAN', nb.sep(bub['Color'])[0], 0.7))
        c = nb.ramp(nb.noise(q, 4, 3)['Fac'], [(0.3, (0.93, 0.92, 0.86)), (0.7, (0.98, 0.97, 0.93))])
        return nb.mix(c, (1.0, 1.0, 0.98), b)
    K.bake_inside("kymyz", inside)
    return obj


# ================================================================ Наурыз көже

BOWL_PROFILE = [
    (0.0, 0.16), (0.2, 0.16), (0.39, 0.16), (0.405, 0.163), (0.415, 0.19), (0.43, 0.225), (0.445, 0.24),
    (0.465, 0.242), (0.478, 0.23), (0.475, 0.16), (0.455, 0.07), (0.41, -0.04), (0.34, -0.13), (0.26, -0.19),
    (0.21, -0.2), (0.205, -0.24), (0.19, -0.255), (0.1, -0.25), (0.0, -0.245),
]
BOWL_BAND_Z = (-0.06, 0.16)


def bowl_band(res=(2048, 320)):
    w, h = res
    xs = (np.arange(w) + 0.5) / w
    ys = (np.arange(h) + 0.5) / h
    X, Y = np.meshgrid(xs, ys)
    n = 14
    cx = (X * n) % 1.0
    px = cx * w / n / h  # cell-local x in units of the band height
    ink = np.zeros_like(X)

    def line(d, wid):
        return np.clip((wid - np.abs(d)) / 0.012, 0, 1)
    wave = 0.5 + 0.22 * np.sin(2 * np.pi * cx)
    ink = np.maximum(ink, line(Y - wave, 0.035))
    for sx, sy, sgn in ((0.25, 0.72, 1), (0.75, 0.28, -1)):
        cxp = sx * w / n / h
        d = np.hypot(px - cxp, Y - sy)
        ang = np.arctan2(Y - sy, px - cxp)
        spiral = line(d - (0.05 + 0.03 * ((ang * sgn) % (2 * np.pi)) / (2 * np.pi)), 0.02) * (d < 0.12)
        ink = np.maximum(ink, spiral)
        leaf = np.clip(1 - np.hypot((px - cxp - 0.1 * sgn) / 0.05, (Y - sy + 0.08 * sgn) / 0.025), 0, 1)
        ink = np.maximum(ink, np.clip(leaf * 3, 0, 1))
    ink = np.maximum(ink, line(Y - 0.06, 0.018))
    ink = np.maximum(ink, line(Y - 0.94, 0.018))
    return gray_image("bowl_band", ink)


def nauryz():
    bm = K.lathe(BOWL_PROFILE, 36, 42)
    obj = K.mesh_object("nauryz", bm)
    attr_fn(obj, "soup", lambda co: 1.0 if (co.z > 0.155 and co.xy.length < 0.4) else 0.0)
    attr_fn(obj, "rim", lambda co: smoothstep(0.2, 0.225, co.z) if co.xy.length > 0.44 or co.z > 0.2 else 0.0)
    K.unwrap(obj)
    band = bowl_band()

    mat = K.source_material("nauryz")
    nb = K.NB(mat)
    p = nb.coord('Object')
    s = nb.sep(p)
    soup = nb.attr("soup")
    rim = nb.attr("rim")
    outer = nb.math('GREATER_THAN', nb.vmath('LENGTH', nb.comb(s[0], s[1], 0)), 0.4)
    band_m = nb.math('MULTIPLY', nb.sep(nb.image(band, cylinder_uv(nb, p, *BOWL_BAND_Z), extension='EXTEND').outputs['Color'])[0],
                     nb.math('MULTIPLY', outer, nb.math('MULTIPLY', nb.mapr(s[2], BOWL_BAND_Z[0], BOWL_BAND_Z[0] + 0.005),
                                                       nb.mapr(s[2], BOWL_BAND_Z[1] - 0.005, BOWL_BAND_Z[1], 1, 0))))
    glaze = nb.ramp(nb.noise(p, 5, 3)['Fac'], [(0.3, (0.9, 0.86, 0.76)), (0.7, (0.95, 0.92, 0.84))])
    brown = (0.42, 0.22, 0.1)
    bowl = nb.mix(glaze, brown, nb.math('MAXIMUM', rim, band_m))

    # soup: milky broth with meat, chickpeas, grains and herbs
    q = nb.mapping(p, (1, 1, 1))
    broth = nb.ramp(nb.noise(q, 6, 4)['Fac'], [(0.3, (0.74, 0.6, 0.4)), (0.7, (0.84, 0.72, 0.52))])
    chunks = nb.voronoi(nb.vmath('ADD', q, nb.vmath('SCALE', nb.noise(q, 7, 3)['Color'], scale=0.16)), 6.5)
    meat = nb.math('MULTIPLY', nb.mapr(chunks['Distance'], 0.42, 0.32, interp='SMOOTHSTEP'),
                   nb.math('GREATER_THAN', nb.sep(chunks['Color'])[0], 0.5))
    peas_v = nb.voronoi(q, 13)
    pea_d = peas_v['Distance']
    peas = nb.math('MULTIPLY', nb.mapr(pea_d, 0.33, 0.29), nb.math('GREATER_THAN', nb.sep(peas_v['Color'])[1], 0.3))
    peas = nb.math('MULTIPLY', peas, nb.math('SUBTRACT', 1.0, meat))
    grains_v = nb.voronoi(nb.mapping(q, (1, 2.0, 1)), 34)
    grains = nb.math('MULTIPLY', nb.mapr(grains_v['Distance'], 0.3, 0.22), nb.math('GREATER_THAN', nb.sep(grains_v['Color'])[2], 0.3))
    grains = nb.math('MULTIPLY', grains, nb.math('SUBTRACT', 1.0, nb.math('MAXIMUM', meat, peas)))
    herbs_v = nb.voronoi(q, 45)
    herbs = nb.math('MULTIPLY', nb.mapr(herbs_v['Distance'], 0.2, 0.12), nb.math('GREATER_THAN', nb.sep(herbs_v['Color'])[0], 0.88))
    sc = nb.mix(broth, (0.96, 0.93, 0.84), grains)
    pea_col = nb.ramp(pea_d, [(0.0, (0.97, 0.78, 0.38)), (0.3, (0.8, 0.55, 0.2))])
    sc = nb.mix(sc, pea_col, peas)
    meat_col = nb.ramp(nb.noise(q, 22, 4)['Fac'], [(0.3, (0.38, 0.23, 0.16)), (0.7, (0.56, 0.38, 0.28))])
    sc = nb.mix(sc, meat_col, meat)
    sc = nb.mix(sc, (0.2, 0.3, 0.1), herbs)
    c = nb.mix(bowl, sc, soup)

    soup_h = nb.math('ADD', nb.math('ADD', nb.math('MULTIPLY', meat, 0.8),
                                    nb.math('MULTIPLY', nb.math('MULTIPLY', peas, nb.mapr(pea_d, 0.33, 0.0)), 0.9)),
                     nb.math('MULTIPLY', grains, 0.3))
    h = nb.math('MULTIPLY', soup_h, soup)
    h = nb.math('ADD', h, nb.math('MULTIPLY', band_m, 0.15))
    rough = nb.mix(nb.math('ADD', 0.14, nb.math('MULTIPLY', rim, 0.1)), nb.math('ADD', 0.38, nb.math('MULTIPLY', meat, 0.25)), soup)
    nb.principled(c, nb.sep(rough)[0], h, bump=0.3, bump_distance=0.02)
    K.finish(obj, "nauryz", mat)

    # tilt the bowl toward the camera so the soup is visible, then recentre
    transform_mesh(obj, Matrix.Rotation(math.radians(30), 4, 'X'))
    K.centre(obj)

    def inside(nb, uv):
        q = nb.mapping(uv, (1, 1, 1))
        broth = nb.ramp(nb.noise(q, 5, 4)['Fac'], [(0.3, (0.82, 0.72, 0.52)), (0.7, (0.92, 0.84, 0.64))])
        grains_v = nb.voronoi(q, 40)
        grains = nb.math('MULTIPLY', nb.mapr(grains_v['Distance'], 0.3, 0.2), nb.math('GREATER_THAN', nb.sep(grains_v['Color'])[2], 0.4))
        peas_v = nb.voronoi(q, 14)
        peas = nb.math('MULTIPLY', nb.mapr(peas_v['Distance'], 0.32, 0.26), nb.math('GREATER_THAN', nb.sep(peas_v['Color'])[1], 0.7))
        c = nb.mix(broth, (0.97, 0.94, 0.86), grains)
        return nb.mix(c, (0.92, 0.7, 0.3), peas)
    K.bake_inside("nauryz", inside)
    drop_attributes(obj)
    return obj


# ================================================================ Ащы бұрыш (bomb)

PEPPER_PROFILE = [
    (0.0, 0.66), (0.03, 0.655), (0.036, 0.6), (0.04, 0.52), (0.055, 0.47), (0.13, 0.45), (0.2, 0.43),
    (0.25, 0.38), (0.275, 0.3), (0.28, 0.16), (0.265, 0.0), (0.235, -0.16), (0.19, -0.32), (0.14, -0.44),
    (0.09, -0.54), (0.045, -0.61), (0.015, -0.645), (0.0, -0.65),
]


def bomb_pepper():
    def calyx_z(u):
        return 0.36 - 0.07 * max(0.0, math.cos(TAU * u * 5)) ** 3

    def radial(u, v, r, z):
        body = smoothstep(0.42, 0.3, z)
        return 1 + 0.06 * math.cos(TAU * u * 3 + 0.5) * body + 0.02 * fbm(Vector((math.cos(TAU * u), math.sin(TAU * u), z * 3)), 2, seed=71) * body

    bm = K.lathe(PEPPER_PROFILE, 32, 50, radial)
    for v in bm.verts:
        z = v.co.z
        if z < 0.3:
            k = (0.3 - z) / 0.95
            v.co.x += 0.2 * k * k
        if z > 0.5:
            v.co.x -= 0.05 * ((z - 0.5) / 0.16) ** 2
    obj = K.mesh_object("bomb_pepper", bm)

    def green(co):
        a = math.atan2(co.y, co.x) / TAU
        return smoothstep(calyx_z(a) - 0.01, calyx_z(a) + 0.01, co.z)
    attr_fn(obj, "green", green)
    K.centre(obj)
    K.unwrap(obj)

    mat = K.source_material("bomb_pepper")
    nb = K.NB(mat)
    p = nb.coord('Object')
    g = nb.attr("green")
    tone = nb.noise(p, 4, 4, 0.6)['Fac']
    creases = nb.noise(nb.mapping(p, (1, 1, 0.12)), 5, 3, 0.5)['Fac']
    fine = nb.noise(p, 60, 3, 0.6)['Fac']
    red = nb.ramp(tone, [(0.3, (0.52, 0.02, 0.02)), (0.6, (0.74, 0.05, 0.03)), (0.9, (0.86, 0.12, 0.05))])
    red = nb.mix(red, (0.42, 0.015, 0.01), nb.math('MULTIPLY', nb.mapr(creases, 0.45, 0.3), 0.5))
    grn = nb.ramp(fine, [(0.3, (0.12, 0.26, 0.05)), (0.7, (0.24, 0.44, 0.1))])
    c = nb.mix(red, grn, g)
    rough = nb.mix(nb.mapr(fine, 0, 1, 0.12, 0.22), 0.5, g)
    h = nb.math('ADD', nb.math('MULTIPLY', creases, 0.5), nb.math('MULTIPLY', fine, 0.08))
    nb.principled(c, nb.sep(rough)[0], h, bump=0.2, bump_distance=0.015)
    K.finish(obj, "bomb_pepper", mat)
    drop_attributes(obj)
    return obj


BUILDERS = {
    "shuzhuk": shuzhuk,
    "kazy": kazy,
    "kurt": kurt,
    "baursak": baursak,
    "aport": aport,
    "shelpek": shelpek,
    "samsa": samsa,
    "irimshik": irimshik,
    "zhent": zhent,
    "karta": karta,
    "golden_aport": golden_aport,
    "kymyz": kymyz,
    "toy": toy,
    "nauryz": nauryz,
    "bomb_pepper": bomb_pepper,
}


def build(name):
    K.setup_scene()
    obj = BUILDERS[name]()
    K.export_fbx(obj)
    return {"name": name, "tris": K.tri_count(obj), "size": [round(x, 3) for x in obj.dimensions]}


def build_all():
    return [build(n) for n in BUILDERS]
