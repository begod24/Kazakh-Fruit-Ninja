"""Shared helpers for building the game's food models in Blender.

Every food is one closed mesh with one material, centred on its origin.
Blender front view (looking along +Y) is the in-game camera view: the FBX export maps
Blender X/Z/-Y to Unity X/Y/-Z, so the side facing Blender -Y faces the game camera.

Textures are baked from procedural materials:
  <id>_BaseColor.png  RGB albedo, A = smoothness (URP "Smoothness source: Albedo Alpha")
  <id>_Normal.png     tangent-space normal map
  <id>_Inside.png     texture of the cut surface (the slicer projects it planar, 0..1 over the cut)
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise

PROJECT = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Kazakh Fruit Ninja"
MODEL_DIR = PROJECT + "/Assets/_Game/Models/Foods"
TEX_DIR = MODEL_DIR + "/Textures"
RENDER_DIR = PROJECT + "/ArtSource/Foods/renders"
SKIN_RES = 1024
INSIDE_RES = 512


# ---------------------------------------------------------------- scene

def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type = 'METAL'
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = 'GPU'
    except Exception:
        scene.cycles.device = 'CPU'
    scene.cycles.samples = 8
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    bake = scene.render.bake
    bake.margin = 12
    bake.margin_type = 'EXTEND'
    return scene


def collection(name, parent=None):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(col)
    return col


def remove_object(name):
    obj = bpy.data.objects.get(name)
    if obj is None:
        return
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if data is not None and data.users == 0:
        bpy.data.meshes.remove(data)


def mesh_object(name, bm, col_name="Foods"):
    remove_object(name)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    collection(col_name).objects.link(obj)
    return obj


# ---------------------------------------------------------------- geometry

def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def fbm(p, octaves=4, seed=0.0):
    """Fractal noise in about [-1, 1]."""
    q = Vector(p) + Vector((seed * 17.3, seed * 5.1, seed * 11.7))
    total, amp, norm = 0.0, 1.0, 0.0
    for _ in range(octaves):
        total += amp * noise.noise(q)
        norm += amp
        amp *= 0.5
        q *= 2.03
    return total / norm


def cube_grid(cuts):
    """A cube [-1,1]^3 whose faces are regular grids (no poles), as a bmesh."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    return bm


def cube_to_sphere(p):
    x, y, z = p
    x2, y2, z2 = x * x, y * y, z * z
    return Vector((x * math.sqrt(max(0.0, 1 - y2 / 2 - z2 / 2 + y2 * z2 / 3)),
                   y * math.sqrt(max(0.0, 1 - z2 / 2 - x2 / 2 + z2 * x2 / 3)),
                   z * math.sqrt(max(0.0, 1 - x2 / 2 - y2 / 2 + x2 * y2 / 3))))


def blob(cuts, half_size, exponent=2.0):
    """Rounded blob: sphere for exponent 2, rounded box for larger exponents. Returns a bmesh."""
    bm = cube_grid(cuts)
    hx, hy, hz = half_size
    for v in bm.verts:
        d = cube_to_sphere(v.co)
        n = (abs(d.x) ** exponent + abs(d.y) ** exponent + abs(d.z) ** exponent) ** (1.0 / exponent)
        d /= n
        v.co = Vector((d.x * hx, d.y * hy, d.z * hz))
    return bm


def grid_surface(f, nu, vs, poles=True, periodic_v=False):
    """
    Surface from f(u, v) -> Vector; u in [0,1) wraps around.
    vs: the v values of the rings. With poles, vs[0] and vs[-1] are single pole vertices.
    With periodic_v (torus), the last ring connects back to the first.
    """
    bm = bmesh.new()
    rings = []
    for j, v in enumerate(vs):
        if poles and (j == 0 or j == len(vs) - 1):
            rings.append([bm.verts.new(f(0.0, v))])
        else:
            rings.append([bm.verts.new(f(i / nu, v)) for i in range(nu)])
    count = len(rings) if periodic_v else len(rings) - 1
    for j in range(count):
        a, b = rings[j], rings[(j + 1) % len(rings)]
        if len(a) == 1:
            for i in range(nu):
                bm.faces.new((a[0], b[(i + 1) % nu], b[i]))
        elif len(b) == 1:
            for i in range(nu):
                bm.faces.new((a[i], a[(i + 1) % nu], b[0]))
        else:
            for i in range(nu):
                bm.faces.new((a[i], a[(i + 1) % nu], b[(i + 1) % nu], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def resample(points, count, closed=False):
    """Catmull-Rom through 2D/3D points, resampled to `count` points evenly spaced by arc length."""
    pts = [Vector(p) for p in points]
    dense = []
    n = len(pts)
    segs = n if closed else n - 1
    for i in range(segs):
        p0 = pts[(i - 1) % n] if (closed or i > 0) else pts[0]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if (closed or i + 2 < n) else pts[-1]
        for k in range(24):
            t = k / 24
            t2, t3 = t * t, t * t * t
            dense.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                                + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        dense.append(pts[-1])
    lengths = [0.0]
    for i in range(1, len(dense)):
        lengths.append(lengths[-1] + (dense[i] - dense[i - 1]).length)
    total = lengths[-1] + ((dense[0] - dense[-1]).length if closed else 0.0)
    out = []
    steps = count if closed else count - 1
    j = 0
    for k in range(count):
        target = total * k / steps
        while j < len(lengths) - 2 and lengths[j + 1] < target:
            j += 1
        seg = max(lengths[j + 1] - lengths[j], 1e-9) if j + 1 < len(lengths) else 1e-9
        t = min(max((target - lengths[j]) / seg, 0.0), 1.0)
        a = dense[j]
        b = dense[j + 1] if j + 1 < len(dense) else dense[0]
        out.append(a.lerp(b, t))
    return out


def lathe(profile, nu, rings, radial=None):
    """
    Revolve a (r, z) profile listed top to bottom with r = 0 at both ends around Z.
    radial(u, v, r, z) -> radius multiplier gives lobes / irregularity.
    """
    samples = resample(profile, rings)
    samples[0].x = 0.0
    samples[-1].x = 0.0
    vs = [k / (rings - 1) for k in range(rings)]

    def f(u, v):
        k = round(v * (rings - 1))
        r, z = samples[k].x, samples[k].y
        if radial is not None:
            r *= radial(u, v, r, z)
        a = 2 * math.pi * u
        return Vector((r * math.cos(a), r * math.sin(a), z))

    return grid_surface(f, nu, vs, poles=True)


def displace(bm, fn):
    """Moves each vertex along its normal by fn(co, normal)."""
    bm.normal_update()
    offsets = [(v, v.normal.copy() * fn(v.co.copy(), v.normal.copy())) for v in bm.verts]
    for v, off in offsets:
        v.co += off


def centre(obj):
    """Puts the origin at the centre of the bounds (the game spins food around it)."""
    me = obj.data
    lo = Vector((min(v.co[i] for v in me.vertices) for i in range(3)))
    hi = Vector((max(v.co[i] for v in me.vertices) for i in range(3)))
    c = (lo + hi) / 2
    for v in me.vertices:
        v.co -= c
    me.update()
    return hi - lo


def unwrap(obj, angle=66.0, margin=0.006):
    ctx = bpy.context
    for o in ctx.selected_objects:
        o.select_set(False)
    ctx.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=margin, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def vertex_color_layer(obj, name, fn):
    """Point colour attribute from fn(co) -> float, readable in shaders with an Attribute node."""
    me = obj.data
    if name in me.color_attributes:
        me.color_attributes.remove(me.color_attributes[name])
    attr = me.color_attributes.new(name, 'FLOAT_COLOR', 'POINT')
    for i, v in enumerate(me.vertices):
        x = fn(v.co.copy())
        attr.data[i].color = (x, x, x, 1.0)


# ---------------------------------------------------------------- node builder

def to_linear(c):
    """Colours in these scripts are written as sRGB (as picked from photos); shader nodes want linear."""
    def f(x):
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return tuple(f(x) for x in c[:3]) + tuple(c[3:])


class NB:
    """Tiny helper to write shader node trees in code."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.mat = mat
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.out = self.nt.nodes.new('ShaderNodeOutputMaterial')
        self.x = 0

    def node(self, kind, inputs=None, **props):
        n = self.nt.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 40
        for k, v in props.items():
            setattr(n, k, v)
        for k, v in (inputs or {}).items():
            self.set(n.inputs[k], v)
        return n

    def set(self, socket, value):
        if isinstance(value, bpy.types.NodeSocket):
            self.nt.links.new(value, socket)
        elif isinstance(value, bpy.types.Node):
            self.nt.links.new(value.outputs[0], socket)
        else:
            if socket.type == 'RGBA':
                if isinstance(value, (tuple, list)):
                    value = to_linear(value)
                    if len(value) == 3:
                        value = (*value, 1.0)
                else:
                    value = (value, value, value, 1.0)
            socket.default_value = value

    # --- inputs
    def coord(self, kind='Object'):
        return self.node('ShaderNodeTexCoord').outputs[kind]

    def attr(self, name):
        return self.node('ShaderNodeAttribute', attribute_name=name).outputs['Fac']

    def value(self, v):
        n = self.node('ShaderNodeValue')
        n.outputs[0].default_value = v
        return n.outputs[0]

    # --- vectors
    def mapping(self, vec, scale=(1, 1, 1), loc=(0, 0, 0), rot=(0, 0, 0)):
        return self.node('ShaderNodeMapping', {'Vector': vec, 'Scale': scale, 'Location': loc, 'Rotation': rot}).outputs[0]

    def sep(self, vec):
        return self.node('ShaderNodeSeparateXYZ', {'Vector': vec}).outputs

    def comb(self, x=0.0, y=0.0, z=0.0):
        return self.node('ShaderNodeCombineXYZ', {'X': x, 'Y': y, 'Z': z}).outputs[0]

    def vmath(self, op, a, b=None, scale=None):
        n = self.node('ShaderNodeVectorMath', operation=op)
        self.set(n.inputs[0], a)
        if b is not None:
            self.set(n.inputs[1], b)
        if scale is not None:
            self.set(n.inputs['Scale'], scale)
        return n.outputs['Value'] if op in ('LENGTH', 'DOT_PRODUCT', 'DISTANCE') else n.outputs['Vector']

    # --- textures
    def noise(self, vec, scale=5.0, detail=4.0, rough=0.5, distortion=0.0, lac=2.0):
        n = self.node('ShaderNodeTexNoise', {'Vector': vec, 'Scale': scale, 'Detail': detail,
                                             'Roughness': rough, 'Distortion': distortion, 'Lacunarity': lac})
        return n.outputs

    def voronoi(self, vec, scale=5.0, feature='F1', randomness=1.0, metric='EUCLIDEAN'):
        n = self.node('ShaderNodeTexVoronoi', {'Vector': vec, 'Scale': scale, 'Randomness': randomness},
                      feature=feature, distance=metric)
        return n.outputs

    def wave(self, vec, scale=5.0, distortion=0.0, detail=2.0, kind='BANDS', direction='X', profile='SIN'):
        n = self.node('ShaderNodeTexWave', {'Vector': vec, 'Scale': scale, 'Distortion': distortion, 'Detail': detail},
                      wave_type=kind, bands_direction=direction, wave_profile=profile)
        return n.outputs

    def image(self, img, vec=None, interpolation='Linear', extension='REPEAT'):
        n = self.node('ShaderNodeTexImage', interpolation=interpolation, extension=extension)
        n.image = img
        if vec is not None:
            self.set(n.inputs['Vector'], vec)
        return n

    # --- colour / math
    def ramp(self, fac, stops, interp='LINEAR'):
        n = self.node('ShaderNodeValToRGB')
        self.set(n.inputs['Fac'], fac)
        cr = n.color_ramp
        cr.interpolation = interp
        while len(cr.elements) > 1:
            cr.elements.remove(cr.elements[-1])
        for i, (pos, col) in enumerate(stops):
            e = cr.elements[0] if i == 0 else cr.elements.new(pos)
            e.position = pos
            e.color = to_linear((*col, 1.0) if len(col) == 3 else col)
        return n.outputs['Color']

    def framp(self, fac, stops, interp='LINEAR'):
        """Float ramp: stops are (pos, value)."""
        n = self.ramp(fac, [(p, (0, 0, 0)) for p, _ in stops], interp).node
        for e, (_, v) in zip(n.color_ramp.elements, stops):
            e.color = (v, v, v, 1.0)
        return self.sep(n.outputs['Color'])[0]

    def mix(self, a, b, fac, blend='MIX', clamp=True):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_result=clamp)
        self.set(n.inputs[0], fac)
        self.set(n.inputs[6], a)
        self.set(n.inputs[7], b)
        return n.outputs[2]

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.set(n.inputs[0], a)
        if b is not None:
            self.set(n.inputs[1], b)
        if c is not None:
            self.set(n.inputs[2], c)
        return n.outputs[0]

    def mapr(self, x, a, b, c=0.0, d=1.0, clamp=True, interp='LINEAR'):
        return self.node('ShaderNodeMapRange', {'Value': x, 'From Min': a, 'From Max': b, 'To Min': c, 'To Max': d},
                         clamp=clamp, interpolation_type=interp).outputs[0]

    def hsv(self, col, h=0.5, s=1.0, v=1.0, fac=1.0):
        return self.node('ShaderNodeHueSaturation', {'Color': col, 'Hue': h, 'Saturation': s, 'Value': v, 'Fac': fac}).outputs[0]

    # --- output
    def principled(self, color, rough=0.5, height=None, bump=0.3, bump_distance=0.02, metallic=0.0):
        bsdf = self.node('ShaderNodeBsdfPrincipled', {'Base Color': color, 'Roughness': rough, 'Metallic': metallic})
        if height is not None:
            b = self.node('ShaderNodeBump', {'Height': height, 'Strength': bump, 'Distance': bump_distance})
            self.set(bsdf.inputs['Normal'], b.outputs['Normal'])
        self.nt.links.new(bsdf.outputs[0], self.out.inputs['Surface'])
        self.bsdf = bsdf
        return bsdf


# ---------------------------------------------------------------- baking

def new_image(name, res, non_color=False, alpha=True):
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    img.alpha_mode = 'CHANNEL_PACKED'
    return img


def _bake(obj, mat, img, kind, **kw):
    nt = mat.node_tree
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    for n in nt.nodes:
        n.select = False
    tex.select = True
    nt.nodes.active = tex
    ctx = bpy.context
    for o in ctx.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    ctx.view_layer.objects.active = obj
    bpy.ops.object.bake(type=kind, use_clear=True, margin=12, **kw)
    nt.nodes.remove(tex)


def bake_skin(obj, mat, name, res=SKIN_RES, normals=True):
    """Bakes albedo(+smoothness in alpha) and normal textures of a procedural material. Returns (base, normal) images."""
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    out = next(n for n in mat.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
    nt = mat.node_tree
    obj.data.materials.clear()
    obj.data.materials.append(mat)

    # Albedo through an emission shader: exact colours, no lighting.
    emit = nt.nodes.new('ShaderNodeEmission')
    color_src = bsdf.inputs['Base Color'].links[0].from_socket if bsdf.inputs['Base Color'].is_linked else None
    if color_src is not None:
        nt.links.new(color_src, emit.inputs['Color'])
    else:
        emit.inputs['Color'].default_value = bsdf.inputs['Base Color'].default_value
    nt.links.new(emit.outputs[0], out.inputs['Surface'])
    base = new_image(name + "_BaseColor", res)
    _bake(obj, mat, base, 'EMIT')

    # Roughness through the same emission trick (grey = roughness).
    rough = new_image(name + "_Rough", res, non_color=True)
    rsrc = bsdf.inputs['Roughness'].links[0].from_socket if bsdf.inputs['Roughness'].is_linked else None
    if rsrc is not None:
        nt.links.new(rsrc, emit.inputs['Color'])
    else:
        r = bsdf.inputs['Roughness'].default_value
        emit.inputs['Color'].default_value = (r, r, r, 1)
    _bake(obj, mat, rough, 'EMIT')
    nt.nodes.remove(emit)
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])

    # Pack smoothness into albedo alpha.
    px = np.empty(res * res * 4, dtype=np.float32)
    base.pixels.foreach_get(px)
    rp = np.empty(res * res * 4, dtype=np.float32)
    rough.pixels.foreach_get(rp)
    px = px.reshape(-1, 4)
    px[:, 3] = 1.0 - rp.reshape(-1, 4)[:, 0]
    base.pixels.foreach_set(px.ravel())
    save_image(base, name + "_BaseColor.png")
    bpy.data.images.remove(rough)

    normal = None
    if normals:
        normal = new_image(name + "_Normal", res, non_color=True, alpha=False)
        _bake(obj, mat, normal, 'NORMAL', normal_space='TANGENT')
        save_image(normal, name + "_Normal.png")
    return base, normal


def save_image(img, filename):
    os.makedirs(TEX_DIR, exist_ok=True)
    img.filepath_raw = os.path.join(TEX_DIR, filename)
    img.file_format = 'PNG'
    img.save()


def bake_plane():
    """A unit plane with 0..1 UVs used to bake flat (cut surface) textures."""
    name = "_BakePlane"
    obj = bpy.data.objects.get(name)
    if obj is None:
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            for loop in f.loops:
                loop[uv].uv = (loop.vert.co.x + 0.5, loop.vert.co.y + 0.5)
        obj = mesh_object(name, bm, col_name="_Bake")
    obj.hide_render = False
    return obj


def bake_inside(name, build, res=INSIDE_RES):
    """build(nb, uv3) returns a colour socket; uv3 is (u, v, 0) over the cut in 0..1."""
    mat = bpy.data.materials.get(name + "_Inside_src") or bpy.data.materials.new(name + "_Inside_src")
    mat.use_fake_user = True
    nb = NB(mat)
    uv = nb.coord('UV')
    color = build(nb, uv)
    nb.principled(color, 0.8)
    plane = bake_plane()
    plane.data.materials.clear()
    plane.data.materials.append(mat)
    bsdf = nb.bsdf
    emit = nb.nt.nodes.new('ShaderNodeEmission')
    nb.nt.links.new(bsdf.inputs['Base Color'].links[0].from_socket, emit.inputs['Color'])
    nb.nt.links.new(emit.outputs[0], nb.out.inputs['Surface'])
    img = new_image(name + "_Inside", res, alpha=False)
    _bake(plane, mat, img, 'EMIT')
    nb.nt.nodes.remove(emit)
    nb.nt.links.new(bsdf.outputs[0], nb.out.inputs['Surface'])
    save_image(img, name + "_Inside.png")
    return img


def game_material(name, base, normal, metallic=0.0, normal_strength=1.0):
    """Preview material built from the baked textures, as Unity will show it."""
    mat = bpy.data.materials.get(name + "_Skin") or bpy.data.materials.new(name + "_Skin")
    nb = NB(mat)
    uv = nb.coord('UV')
    t = nb.image(base, uv)
    smooth = t.outputs['Alpha']
    rough = nb.math('SUBTRACT', 1.0, smooth)
    bsdf = nb.principled(t.outputs['Color'], rough, metallic=metallic)
    if normal is not None:
        tn = nb.image(normal, uv)
        nm = nb.node('ShaderNodeNormalMap', {'Color': tn.outputs['Color'], 'Strength': normal_strength})
        nb.set(bsdf.inputs['Normal'], nm.outputs['Normal'])
    return mat


def source_material(name):
    mat = bpy.data.materials.get(name + "_src") or bpy.data.materials.new(name + "_src")
    mat.use_fake_user = True
    return mat


def finish(obj, name, src_mat, metallic=0.0, normals=True):
    """Bake the procedural material and switch the object to the textured game material."""
    base, normal = bake_skin(obj, src_mat, name, normals=normals)
    mat = game_material(name, base, normal, metallic=metallic)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return mat


# ---------------------------------------------------------------- export

def export_fbx(obj, name=None):
    name = name or obj.name
    os.makedirs(MODEL_DIR, exist_ok=True)
    ctx = bpy.context
    loc, rot, scl = obj.location.copy(), obj.rotation_euler.copy(), obj.scale.copy()
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)
    hidden = obj.hide_get()
    obj.hide_set(False)
    for o in ctx.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    ctx.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODEL_DIR, name + ".fbx"),
        use_selection=True, object_types={'MESH'},
        axis_forward='Z', axis_up='Y', bake_space_transform=True,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        mesh_smooth_type='FACE', use_tspace=True, use_mesh_modifiers=True,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    obj.location, obj.rotation_euler, obj.scale = loc, rot, scl
    obj.hide_set(hidden)


# ---------------------------------------------------------------- preview

def _preview_rig():
    col = collection("_Preview")
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        col.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.rotation_euler = (math.radians(90), 0, 0)  # looks along +Y, like the game camera
    lights = [("Key", (2.5, -4, 3.5), 190, (1.0, 0.95, 0.88), 3.0),
              ("Fill", (-4, -3, 0.5), 90, (0.8, 0.88, 1.0), 4.0),
              ("Rim", (0.5, 4, 3), 220, (1.0, 0.9, 0.8), 2.0)]
    for name, loc, power, color, size in lights:
        lamp = bpy.data.objects.get(name)
        if lamp is None:
            lamp = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
            col.objects.link(lamp)
        lamp.location = loc
        lamp.data.energy = power
        lamp.data.color = color
        lamp.data.size = size
        direction = -Vector(loc)
        lamp.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    world = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.09, 0.055, 0.04, 1)
    bg.inputs[1].default_value = 0.35
    bpy.context.scene.world = world
    return cam


def preview(names, filename, spacing=1.4, res=(1600, 600), per_row=None, tilt=True):
    """Renders the given food objects in a row (EEVEE), leaving their mesh data untouched."""
    scene = bpy.context.scene
    cam = _preview_rig()
    scene.camera = cam
    objs = [bpy.data.objects[n] for n in names]
    for o in bpy.data.collections["Foods"].objects:
        o.hide_render = o not in objs
    bake_col = bpy.data.collections.get("_Bake")
    if bake_col:
        for o in bake_col.objects:
            o.hide_render = True
    per_row = per_row or len(objs)
    rows = math.ceil(len(objs) / per_row)
    for i, o in enumerate(objs):
        r, c = divmod(i, per_row)
        o.location = ((c - (per_row - 1) / 2) * spacing, 0, -(r - (rows - 1) / 2) * spacing)
        o.rotation_euler = (math.radians(-12), math.radians(8), math.radians(-10)) if tilt else (0, 0, 0)
    cam.location = (0, -10, 0)
    cam.data.ortho_scale = max(per_row * spacing, rows * spacing * res[0] / res[1]) * 1.02
    engine = scene.render.engine
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    path = os.path.join(RENDER_DIR, filename)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    scene.render.engine = engine
    return path
