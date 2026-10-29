"""Read-only render of saved original GLTF imports; never saves a .blend."""
import bpy, json, hashlib
from pathlib import Path
from mathutils import Vector

RECORDS = Path(__file__).resolve().parent
BASE = RECORDS.parents[1]
OUT = Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original/SourceReference')
OUT.mkdir(parents=True, exist_ok=True)
records = []

for role in ['Rogue', 'Warrior', 'Mage']:
    source = BASE / 'Editable' / ('Skeleton_' + role + '_Original_GLTF.blend')
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    rig.animation_data_create()
    action = bpy.data.actions['Idle_Combat']
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    scene.frame_set(0)
    # This accessory is not part of any skinned character in the original GLB.
    for obj in scene.objects:
        if obj.type == 'MESH' and obj.name.startswith('Icosphere') and obj.parent is None:
            obj.hide_render = True
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.cycles.use_denoising = True
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'CUDA'
    prefs.get_devices()
    for dev in prefs.devices:
        dev.use = dev.type == 'CUDA'
    scene.cycles.device = 'GPU'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = bpy.data.worlds.new('Reference_World')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.18, .18, .18, 1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .7
    for name, pos, energy, size in [('Reference_Key', (-3, -4, 5), 850, 5), ('Reference_Fill', (3, 2, 3), 550, 4)]:
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.shape = 'DISK'
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector((0, 0, 1)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    material = bpy.data.materials.new('Reference_Floor')
    material.diffuse_color = (.12, .14, .16, 1)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.08))
    bpy.context.object.data.materials.append(material)
    data = bpy.data.cameras.new('Reference_Camera')
    camera = bpy.data.objects.new('Reference_Camera', data)
    scene.collection.objects.link(camera)
    data.type = 'ORTHO'
    data.ortho_scale = 3.4
    scene.camera = camera
    for view, position in [('Front', (0, -8, 1.5)), ('Side', (-8, 0, 1.5))]:
        camera.location = position
        camera.rotation_euler = (Vector((0, 0, 1.3)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        path = OUT / (role + '_Idle_Combat_' + view + '.png')
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        records.append({'role': role, 'source': str(source), 'source_sha256': digest,
                        'action': 'Idle_Combat', 'frame': 0, 'view': view,
                        'render': str(path), 'source_saved': False,
                        'material_modified': False, 'rig_scale_modified': False,
                        'note': 'Native imported materials; only neutral review lighting. Floor is at Z=-0.08m, not a contact correction. No weapon is attached in this character-only reference.'})
    assert hashlib.sha256(source.read_bytes()).hexdigest() == digest

(RECORDS / 'manifest.json').write_text(json.dumps(records, indent=2))
print('SOURCE_REFERENCE_COMPLETE', len(records))
