import bpy, math, sys
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.open_mainfile(filepath="/home/user/blender-all-car-model-3D/ArtPipeline/VehicleLibrary/CAR-009/Working/CAR-009_Working_v001.blend")
sc = bpy.context.scene
# hide everything that is not the car (collision proxies, studio floor, other cameras / lights)
car_objs = []
for o in sc.objects:
    name = o.name
    keep = name.startswith("CAR-009") and "Collision" not in name and "COL" not in name.upper().split("_")
    if o.type in ("MESH", "EMPTY", "CURVE") and keep:
        car_objs.append(o)
    elif o.type in ("MESH", "CURVE", "LIGHT", "CAMERA"):
        o.hide_render = True
for coll in bpy.data.collections:
    if "ollision" in coll.name or "COL" in coll.name:
        coll.hide_render = True
root = bpy.data.objects.get("CAR-009_Root")
# drift pose: yaw the car, slight body roll
if root:
    root.rotation_euler = (0.0, math.radians(-3.0), math.radians(-6))
# bounds of car meshes
pts = []
bpy.context.view_layer.update()
for o in car_objs:
    if o.type == "MESH" and not o.hide_render:
        for c in o.bound_box:
            pts.append(o.matrix_world @ Vector(c))
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr = (mn + mx) / 2
print("bounds", mn, mx)
# camera: low front three-quarter, slightly dutch
cam_data = bpy.data.cameras.new("LogoCam"); cam_data.lens = 50
cam = bpy.data.objects.new("LogoCam", cam_data); sc.collection.objects.link(cam)
cam.location = ctr + Vector((5.2, 8.6, 1.35))
d = (ctr + Vector((0, 0, 0.05))) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam.rotation_euler.rotate_axis('Z', math.radians(-6))
sc.camera = cam
# lights: warm key sun, pink rim, cool fill
def light(name, typ, energy, color, loc, rot=None, size=None):
    ld = bpy.data.lights.new(name, typ); ld.energy = energy; ld.color = color
    if size and typ == "AREA": ld.size = size
    lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo); lo.location = loc
    if rot: lo.rotation_euler = rot
    return lo
light("Key", "SUN", 4.2, (1.0, 0.86, 0.72), (0,0,10), (math.radians(48), math.radians(10), math.radians(150)))
light("Rim", "AREA", 1400, (1.0, 0.45, 0.65), ctr + Vector((-5, -6, 3.5)), (math.radians(60), 0, math.radians(-40)), 6)
light("Fill", "AREA", 500, (0.6, 0.75, 1.0), ctr + Vector((7, 2, 2.5)), (math.radians(70), 0, math.radians(110)), 5)
sc.world = bpy.data.worlds.new("LogoWorld"); sc.world.use_nodes = True
bg = sc.world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.95, 0.8, 0.85, 1); bg.inputs[1].default_value = 0.6
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48
try: sc.cycles.use_denoising = True
except Exception: pass
sc.render.film_transparent = True
sc.render.resolution_x = 1600; sc.render.resolution_y = 1100; sc.render.resolution_percentage = 100
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
sc.view_settings.view_transform = "Standard" if "Standard" in [v for v in ("Standard",)] else sc.view_settings.view_transform
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("written", out)
