import bpy, numpy as np, json
from pathlib import Path
from mathutils import Vector

out=Path(__file__).parent
root=out.parents[2]
scene=bpy.context.scene
body=bpy.data.objects['Chibi_Body_v009']
edges=np.array([e.vertices[:] for e in body.data.edges])
base=np.array([v.co[:] for v in body.data.vertices])
rest=np.linalg.norm(base[edges[:,0]]-base[edges[:,1]],axis=1)
clips=json.loads((root/'Combat/clips.json').read_text())
report=[]
for c in clips:
    for on,an in c['actions'].items():
        o=bpy.data.objects[on];a=bpy.data.actions[an];o.animation_data.action=a;o.animation_data.action_slot=a.slots[0]
    rows=[]
    for f in range(c['start'],c['end']+1):
        scene.frame_set(f);e=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh()
        co=np.array([v.co[:] for v in m.vertices]);e.to_mesh_clear()
        lens=np.linalg.norm(co[edges[:,0]]-co[edges[:,1]],axis=1)
        for idx in np.argsort(lens/np.maximum(rest,1e-12))[:2]:
            vs=[int(v) for v in edges[idx]]
            rows.append({'frame':f,'edge':int(idx),'restLength':float(rest[idx]),'length':float(lens[idx]),'ratio':float(lens[idx]/rest[idx]),'restCenter':base[vs].mean(0).tolist(),'posedCenter':co[vs].mean(0).tolist(),
                         'vertexGroups':[{body.vertex_groups[g.group].name:round(g.weight,5) for g in body.data.vertices[v].groups}for v in vs]})
    report.append({'clip':c['name'],'worstEdges':sorted(rows,key=lambda r:r['ratio'])[:10]})
scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE';scene.display.shading.show_cavity=True
scene.render.image_settings.file_format='PNG'
camera=bpy.data.objects['Camera_ThreeQuarter'].copy();camera.data=camera.data.copy();scene.collection.objects.link(camera)
camera.data.ortho_scale=1.15;scene.camera=camera
target=Vector((0,-.10,.74))
for c,r in zip(clips,report):
    for on,an in c['actions'].items():
        o=bpy.data.objects[on];a=bpy.data.actions[an];o.animation_data.action=a;o.animation_data.action_slot=a.slots[0]
    for view,loc in [('Front',Vector((0,-4,1.1))),('Opposite',Vector((4,-.6,1.25))),('Quarter',Vector((-3,-3,1.5)))]:
        camera.location=loc;camera.rotation_euler=(target-loc).to_track_quat('-Z','Y').to_euler()
        for f in sorted(set([1,r['worstEdges'][0]['frame'],round(c['end']*.5),round(c['end']*.65)])):
            scene.frame_set(f);scene.render.filepath=str(out/f"Detail_{c['name'].split('_')[-1]}_{view}_{f:03}.png");bpy.ops.render.render(write_still=True)
(out/'deformation-diagnostics.json').write_text(json.dumps(report,indent=2))
