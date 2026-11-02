"""Author a connected, simplified gripping hand. Coordinate values are native hand metres."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
parts=[]
def apply(obj,mod):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
def ellipsoid(name,centre,radii):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=centre)
    ob=bpy.context.object;ob.name=name;ob.scale=radii
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    parts.append(ob);return ob

# A cupped palm and one compact finger block suit the existing low-poly hands.
# The thumb remains a separately readable lobe, fused into the same skin volume.
ellipsoid('Palm',(-.006,.047,.003),(.033,.035,.047))
bpy.ops.mesh.primitive_cylinder_add(vertices=40,radius=.036,depth=.091,location=(.028,.068,-.006))
ring=bpy.context.object;ring.name='Curled_finger_block'
bevel=ring.modifiers.new('Rounded_knuckle_edges','BEVEL');bevel.width=.008;bevel.segments=3
apply(ring,bevel);parts.append(ring)
thumb=[((-.006,.037,.040),(.019,.023,.017)),((.013,.036,.046),(.018,.017,.016)),((.034,.041,.046),(.018,.016,.015)),((.052,.052,.039),(.016,.019,.016)),((.063,.068,.024),(.015,.020,.015))]
for i,(centre,radii) in enumerate(thumb):ellipsoid('Thumb_'+str(i),centre,radii)

# Size the smooth wrist from the inspected native opening. The original
# nonplanar/concave edge loop must not be projected into a loft: that produces
# folded ridges. A narrower continuous skin stem extends inside the sleeve.
boundary=json.loads((HERE/'left-wrist-boundary.json').read_text(encoding='utf-8-sig'))
points=[Vector((p['x'],p['y'],p['z'])) for p in boundary['points']]
ellipsoid('Wrist_skin',(-.012,.020,.008),(.035,.043,.046))

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
hand=bpy.context.object;hand.name='Chibi_LeftHand_StaffGrip'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Recalculate consistently before the volumetric union.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
remesh=hand.modifiers.new('Connected_skin_volume','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.0018;remesh.use_smooth_shade=True;apply(hand,remesh)
smooth=hand.modifiers.new('Skin_transition','SMOOTH');smooth.factor=.55;smooth.iterations=5;apply(hand,smooth)
decimate=hand.modifiers.new('Low_poly_surface','DECIMATE');decimate.ratio=.025;apply(hand,decimate)

# The shaft cavity is cut after smoothing so no skin surface crosses the shaft.
bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.0178,depth=.40,location=(.028,.068,0))
cutter=bpy.context.object;cutter.name='Exact_staff_clearance'
boolean=hand.modifiers.new('Shaft_cavity','BOOLEAN');boolean.operation='DIFFERENCE';boolean.solver='EXACT';boolean.object=cutter;apply(hand,boolean)
bpy.data.objects.remove(cutter,do_unlink=True)
bpy.context.view_layer.objects.active=hand
tri=hand.modifiers.new('Game_triangles','TRIANGULATE');apply(hand,tri)
for face in hand.data.polygons:face.use_smooth=True;face.material_index=0
hand.data.update()
bpy.ops.object.select_all(action='DESELECT');hand.select_set(True);bpy.context.view_layer.objects.active=hand
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.1519,island_margin=.035);bpy.ops.object.mode_set(mode='OBJECT')
# Bake subtle form shading to match the character's existing unlit atlas style.
material=bpy.data.materials.new('Closed_hand_baked_skin');material.use_nodes=True;material.diffuse_color=(.965,.792,.675,1)
hand.data.materials.clear();hand.data.materials.append(material)
nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
out=nodes.new('ShaderNodeOutputMaterial');em=nodes.new('ShaderNodeEmission');links.new(em.outputs[0],out.inputs['Surface'])
ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.035;ao.samples=32
shade=nodes.new('ShaderNodeMath');shade.operation='MULTIPLY_ADD';shade.inputs[1].default_value=.22;shade.inputs[2].default_value=.78;links.new(ao.outputs['AO'],shade.inputs[0])
geom=nodes.new('ShaderNodeNewGeometry');dot=nodes.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=Vector((-.4,.7,.6)).normalized();links.new(geom.outputs['Normal'],dot.inputs[0])
light=nodes.new('ShaderNodeMath');light.operation='MULTIPLY_ADD';light.inputs[1].default_value=.10;light.inputs[2].default_value=.90;links.new(dot.outputs['Value'],light.inputs[0])
factor=nodes.new('ShaderNodeMath');factor.operation='MULTIPLY';links.new(light.outputs[0],factor.inputs[0]);links.new(shade.outputs[0],factor.inputs[1])
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
def linear(s):return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
mix.inputs[1].default_value=tuple(linear(x/255) for x in (246,202,172))+(1,);links.new(factor.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],em.inputs['Color'])
image=bpy.data.images.new('Study_Hand_BaseColor',width=256,height=256,alpha=False)
texture=nodes.new('ShaderNodeTexImage');texture.image=image;nodes.active=texture
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32;scene.render.bake.margin=6
bpy.ops.object.bake(type='EMIT')
root=next(p for p in HERE.parents if (p/'Assets').is_dir())
texturepath=root/'Assets/Art/Characters/Protagonist01/AnimationStudies/SkeletonMageCast20261030/Study_Hand_BaseColor.png'
image.filepath_raw=str(texturepath);image.file_format='PNG';image.save();image.pack()
# Keep authored material readable when reopening the .blend.
links.new(texture.outputs['Color'],em.inputs['Color'])
hand.data.calc_loop_triangles();uv=hand.data.uv_layers.active.data
v=[];normals=[];coords=[];tris=[];lookup={}
for t in hand.data.loop_triangles:
    for loopindex in t.loops:
        vi=hand.data.loops[loopindex].vertex_index;p=hand.data.vertices[vi];tex=uv[loopindex].uv
        key=(vi,round(tex.x,7),round(tex.y,7))
        if key not in lookup:
            lookup[key]=len(v);v.append({'x':p.co.x,'y':p.co.y,'z':p.co.z});normals.append({'x':p.normal.x,'y':p.normal.y,'z':p.normal.z});coords.append({'x':tex.x,'y':tex.y})
        tris.append(lookup[key])
(HERE/'closed-left-hand.json').write_text(json.dumps({'vertices':v,'normals':normals,'uv':coords,'triangles':tris}),encoding='utf-8')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Chibi_LeftHand_StaffGrip.blend'))
print('CLOSED_HAND_EXPORTED',len(v),len(tris)//3)
