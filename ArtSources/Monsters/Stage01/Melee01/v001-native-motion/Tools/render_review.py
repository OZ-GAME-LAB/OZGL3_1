import bpy, sys, json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
scene=bpy.context.scene;rig=bpy.data.objects['Melee01_Rig']
scene.render.engine='CYCLES';scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.use_file_extension=True
scene.render.threads_mode='FIXED';scene.render.threads=8
def choose(name):
    a=bpy.data.actions[name];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0]
    for t in rig.animation_data.nla_tracks:t.mute=True
    return a
mode=args[0] if args else 'keys'
if mode=='keys':
    out=ROOT/'Review/Keys';out.mkdir(exist_ok=True)
    scene.render.resolution_x=480;scene.render.resolution_y=480;scene.cycles.samples=8
    for clip in ['Idle','Move','Attack','Hit']:
        a=choose(clip);end=float(a.frame_range[1])
        for view in ['Side','Front','Opposite','Back']:
            scene.camera=bpy.data.objects['Review_'+view]
            scene.camera.data.ortho_scale=2.1
            scene.camera.rotation_euler=(__import__('mathutils').Vector((0,-0.1,0.65))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
            for i,t in enumerate([0,.25,.45,.6,.8,1]):
                f=end*t;scene.frame_set(math.floor(f),subframe=f%1)
                scene.render.filepath=str(out/f'{clip}_{view}_{i:02}.png');bpy.ops.render.render(write_still=True)
elif mode=='side':
    prefs=bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type='CUDA';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='CUDA'
    scene.cycles.device='GPU'
    scene.camera=bpy.data.objects['Review_Side']
    for clip in ['Idle','Move','Attack','Hit']:
        a=choose(clip);out=ROOT/'Review/Frames'/clip;out.mkdir(parents=True,exist_ok=True)
        end=int(round(a.frame_range[1]))
        for f in range(end+1):
            scene.frame_set(f);scene.render.filepath=str(out/f'{f:04}.png');bpy.ops.render.render(write_still=True)
print('RENDER_COMPLETE',mode)
