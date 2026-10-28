import bpy, json
from pathlib import Path

out = Path(__file__).parent
scene = bpy.context.scene
data = {
    'objects': [{'name': o.name, 'type': o.type, 'parent': o.parent.name if o.parent else None,
                 'location': list(o.location), 'rotationMode': o.rotation_mode,
                 'renderHidden': o.hide_render, 'dimensions': list(o.dimensions),
                 'modifiers': [(m.name, m.type) for m in o.modifiers],
                 'materials': [s.material.name if s.material else None for s in o.material_slots],
                 'vertices': len(o.data.vertices) if o.type == 'MESH' else None,
                 'cameraType': o.data.type if o.type == 'CAMERA' else None,
                 'orthoScale': o.data.ortho_scale if o.type == 'CAMERA' else None}
                for o in bpy.data.objects],
    'actions': [{'name': a.name, 'slots': [s.identifier for s in a.slots], 'range': list(a.frame_range)} for a in bpy.data.actions],
    'bones': [{'name': b.name, 'parent': b.parent.name if b.parent else None, 'length': b.length} for b in bpy.data.objects['Hero_KayKit_Rig'].data.bones],
    'scene': {'fps': scene.render.fps, 'engine': scene.render.engine, 'camera': scene.camera.name if scene.camera else None,
              'resolution': [scene.render.resolution_x,scene.render.resolution_y], 'keys': list(scene.keys())},
}
(out/'scene-inventory.json').write_text(json.dumps(data,indent=2),encoding='utf8')
print('COMBAT_SCENE_INVENTORY_COMPLETE')
