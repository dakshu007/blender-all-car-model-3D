"""vehicle_builder.py - reusable Blender geometry/material helpers for building
stylised vehicles from Python. Car-specific shapes live in cars/build_carXXX.py.

All helpers build into bmesh (fast, no operator context needed) and only touch
bpy.ops where unavoidable (object join).
"""

import math

import bpy
import bmesh
from mathutils import Matrix, Vector


# ---------------------------------------------------------------- colours ---
def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_lin(h, alpha=1.0):
    h = h.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    return (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), alpha)


# -------------------------------------------------------------- materials ---
def material(name, color="#808080", rough=0.6, metal=0.0, emission=None, emission_strength=0.0,
             alpha=1.0, image=None, specular=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hex_lin(color)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Specular IOR Level"].default_value = specular
    if emission:
        bsdf.inputs["Emission Color"].default_value = hex_lin(emission)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        if hasattr(m, "surface_render_method"):
            m.surface_render_method = "BLENDED"
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.location = (-400, 250)
        tex.interpolation = "Linear"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    m.diffuse_color = hex_lin(color, alpha)
    m.roughness = rough
    m.metallic = metal
    return m


# ------------------------------------------------------------ mesh builder ---
class MeshBuilder:
    """Accumulates closed parts into one bmesh with per-part materials."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _faces_of(self, verts):
        return list({f for v in verts for f in v.link_faces})

    def _setmat(self, faces, mat):
        idx = self.mi(mat)
        for f in faces:
            f.material_index = idx
        return faces

    def box(self, center, size, mat, rot=None, top_scale=None):
        """Box (optionally rotated). top_scale=(sx, sy) shrinks the local +Z face."""
        verts = bmesh.ops.create_cube(self.bm, size=1.0, matrix=Matrix.Diagonal(Vector(size)).to_4x4())["verts"]
        if top_scale:
            for v in verts:
                if v.co.z > 0:
                    v.co.x *= top_scale[0]
                    v.co.y *= top_scale[1]
        bmesh.ops.transform(self.bm, verts=verts, matrix=Matrix.LocRotScale(Vector(center), rot, None))
        return self._setmat(self._faces_of(verts), mat)

    def cyl(self, p1, p2, r, mat, seg=12, r2=None, cap=True):
        p1, p2 = Vector(p1), Vector(p2)
        d = p2 - p1
        L = d.length
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        M = Matrix.LocRotScale((p1 + p2) / 2, q, None)
        verts = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=seg,
                                      radius1=r, radius2=r if r2 is None else r2, depth=L, matrix=M)["verts"]
        return self._setmat(self._faces_of(verts), mat)

    def tube_path(self, pts, r, mat, seg=8):
        faces = []
        for a, b in zip(pts, pts[1:]):
            faces += self.cyl(a, b, r, mat, seg=seg)
        for p in pts[1:-1]:  # joint balls hide the gaps at bends
            faces += self.sphere(p, r * 1.02, mat, seg=seg)
        return faces

    def sphere(self, c, r, mat, seg=8):
        verts = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=max(4, seg // 2), radius=r,
                                          matrix=Matrix.Translation(Vector(c)))["verts"]
        return self._setmat(self._faces_of(verts), mat)

    def revolve(self, profile, seg, mat, center=(0, 0, 0), closed=False, edge_mats=None, phase=0.0):
        """Revolve (w, r) profile around the X axis. Returns grid[i][j] faces
        (i = segment, j = profile edge)."""
        c = Vector(center)
        rings = []
        for i in range(seg):
            a = 2 * math.pi * i / seg + phase
            ca, sa = math.cos(a), math.sin(a)
            rings.append([self.bm.verts.new(c + Vector((w, r * ca, r * sa))) for (w, r) in profile])
        n = len(profile)
        nedges = n if closed else n - 1
        grid = []
        for i in range(seg):
            a, b = rings[i], rings[(i + 1) % seg]
            row = []
            for j in range(nedges):
                j2 = (j + 1) % n
                f = self.bm.faces.new((a[j], a[j2], b[j2], b[j]))
                m = edge_mats[j] if edge_mats and edge_mats[j] is not None else mat
                f.material_index = self.mi(m)
                row.append(f)
            grid.append(row)
        return grid

    def loft(self, rings, mat, cap=True):
        vr = [[self.bm.verts.new(Vector(p)) for p in ring] for ring in rings]
        faces = []
        n = len(rings[0])
        for a, b in zip(vr, vr[1:]):
            for i in range(n):
                faces.append(self.bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])))
        if cap:
            faces.append(self.bm.faces.new(vr[0]))
            faces.append(self.bm.faces.new(list(reversed(vr[-1]))))
        return self._setmat(faces, mat)

    def prism(self, pts2d, plane, a0, a1, mat):
        """Extrude a 2D polygon. plane 'XY': pts=(x,y) extruded along Z from a0 to a1;
        'YZ': pts=(y,z) along X; 'XZ': pts=(x,z) along Y."""
        def P(p, a):
            if plane == "XY":
                return Vector((p[0], p[1], a))
            if plane == "YZ":
                return Vector((a, p[0], p[1]))
            return Vector((p[0], a, p[1]))
        bot = [self.bm.verts.new(P(p, a0)) for p in pts2d]
        top = [self.bm.verts.new(P(p, a1)) for p in pts2d]
        faces = [self.bm.faces.new(bot), self.bm.faces.new(top)]
        n = len(pts2d)
        for i in range(n):
            faces.append(self.bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])))
        return self._setmat(faces, mat)

    def finish(self, collection, recalc=True, location=None):
        if recalc:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        collection.objects.link(obj)
        if location is not None:
            obj.location = location
        return obj


def orient_faces(bm, faces, expected_fn):
    """Flip faces whose normal disagrees with expected_fn(face) (for open surfaces)."""
    flip = []
    for f in faces:
        f.normal_update()
        if f.normal.dot(expected_fn(f)) < 0:
            flip.append(f)
    if flip:
        bmesh.ops.reverse_faces(bm, faces=flip)


# ------------------------------------------------------------ object utils ---
def apply_modifiers(obj):
    deps = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(deps)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=deps)
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    me.name = obj.name
    return obj


def add_bevel(obj, width=0.012, segments=2, angle=30.0):
    m = obj.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle)
    m.use_clamp_overlap = True
    return m


def smooth_by_angle(obj, deg=32.0):
    me = obj.data
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(deg))


def join(objects, name):
    target = objects[0]
    with bpy.context.temp_override(active_object=target, object=target,
                                   selected_objects=objects, selected_editable_objects=objects):
        bpy.ops.object.join()
    target.name = name
    target.data.name = name
    return target


def project_uvs(obj, spec, plate_rects=None, force_side=False):
    """World-space per-face projection into the car's livery atlas.
    plate_rects: {material_name: (u0, v0, u1, v1)} maps those faces' bounds to a rect.
    force_side: map every face to the left/right side region by the sign of X
    (for flares/skirts so side stripes run across them without slivers)."""
    me = obj.data
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    mw = obj.matrix_world
    nm = mw.to_3x3().inverted().transposed()
    plate_rects = plate_rects or {}
    plate_faces = {}
    for poly in me.polygons:
        mat = me.materials[poly.material_index] if me.materials else None
        if mat is not None and mat.name in plate_rects:
            plate_faces.setdefault(mat.name, []).append(poly)
            continue
        n = (nm @ poly.normal).normalized()
        if force_side:
            region = "right" if (mw @ poly.center).x > 0 else "left"
        else:
            region = spec.region_for_normal(n.x, n.y, n.z)
        for li in poly.loop_indices:
            if region == "under":
                uv.data[li].uv = spec.UNDER_UV
            else:
                co = mw @ me.vertices[me.loops[li].vertex_index].co
                uv.data[li].uv = spec.UV_FUNCS[region](co.x, co.y, co.z)
    for mname, polys in plate_faces.items():
        u0, v0, u1, v1 = plate_rects[mname]
        cos = [mw @ me.vertices[me.loops[li].vertex_index].co for p in polys for li in p.loop_indices]
        xs = [c.x for c in cos]
        zs = [c.z for c in cos]
        x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
        for p in polys:
            n = (nm @ p.normal).normalized()
            for li in p.loop_indices:
                c = mw @ me.vertices[me.loops[li].vertex_index].co
                tx = (c.x - x0) / (x1 - x0 or 1)
                if n.y > 0:  # front-facing plate: viewer's right is -X
                    tx = 1 - tx
                tz = (c.z - z0) / (z1 - z0 or 1)
                uv.data[li].uv = (u0 + (u1 - u0) * tx, v0 + (v1 - v0) * tz)
    return uv


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons) if obj.type == "MESH" else 0
