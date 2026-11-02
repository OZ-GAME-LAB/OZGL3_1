"""Sculpt in the native right-hand frame, with fingertips toward +Y and palm toward +X."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / 'Assets').exists())
ASSETS = ROOT / 'Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts = []
def apply(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)

def lobe(name, center, radii, power=1, direction=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=18)
    part = bpy.context.object
    part.name = name
    for vert in part.data.vertices:
        vert.co = [math.copysign(abs(v) ** power, v) for v in vert.co]
    part.scale = radii
    if direction:
        part.rotation_mode = 'QUATERNION'
        part.rotation_quaternion = Vector((0,1,0)).rotation_difference(Vector(direction).normalized())
    part.location = center
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    parts.append(part)

# The stem overlaps the original sleeve. Its centre and span come from the
# nineteen-edge right wrist opening in right-hand-skin-selection.json.
lobe('Wrist', (.004,.013,.006), (.033,.036,.042))
lobe('Dorsum', (-.007,.049,.000), (.029,.040,.046), .82)
# The distal line falls gradually from the middle knuckle to the little finger.
for i,(z, y, width) in enumerate([(-.036,.067,.013),(-.014,.077,.0145),(.009,.081,.0145),(.032,.078,.014)]):
    lobe('MCP_'+str(i), (-.008,y,z), (.0225,.018,width), .84)
    lobe('Curled_finger_'+str(i), (.013,y-.004,z), (.0215,.018,width), .79)
    lobe('Folded_pad_'+str(i), (.022,y-.016,z), (.015,.019,width*.98), .84, (-.006,.026,0))
# The thumb opposes the fingers. The rounded distal tip is on the index/middle
# pads and the segment behind it fuses with the palm under the thumb web.
lobe('Thenar', (.010,.040,.034), (.020,.026,.018), .90)
lobe('Thumb_base', (.023,.043,.041), (.016,.021,.014), .88, (.020,.020,.008))
lobe('Thumb_knuckle', (.036,.052,.036), (.013,.016,.014))
lobe('Thumb_pad', (.038,.058,.021), (.013,.022,.0125), .84, (.001,.009,-.035))

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
hand=bpy.context.object;hand.name='Chibi_RightHand_Relaxed_Fist'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Recalculate consistently before the volumetric union.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
remesh=hand.modifiers.new('Connected_skin_volume','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.00145;remesh.use_smooth_shade=True;apply(hand,remesh)
smooth=hand.modifiers.new('Skin_transition','SMOOTH');smooth.factor=.55;smooth.iterations=7;apply(hand,smooth)
decimate=hand.modifiers.new('Low_poly_surface','DECIMATE');decimate.ratio=.034;apply(hand,decimate)

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
ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.024;ao.samples=32
shade=nodes.new('ShaderNodeMath');shade.operation='MULTIPLY_ADD';shade.inputs[1].default_value=.36;shade.inputs[2].default_value=.64;links.new(ao.outputs['AO'],shade.inputs[0])
geom=nodes.new('ShaderNodeNewGeometry');dot=nodes.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=Vector((-.7,-.35,.35)).normalized();links.new(geom.outputs['Normal'],dot.inputs[0])
light=nodes.new('ShaderNodeMath');light.operation='MULTIPLY_ADD';light.inputs[1].default_value=.12;light.inputs[2].default_value=.89;links.new(dot.outputs['Value'],light.inputs[0])
factor=nodes.new('ShaderNodeMath');factor.operation='MULTIPLY';links.new(light.outputs[0],factor.inputs[0]);links.new(shade.outputs[0],factor.inputs[1])
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
def linear(s):return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
mix.inputs[1].default_value=tuple(linear(x/255) for x in (246,202,172))+(1,);links.new(factor.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],em.inputs['Color'])
image=bpy.data.images.new('Study_Hand_BaseColor',width=256,height=256,alpha=False)
texture=nodes.new('ShaderNodeTexImage');texture.image=image;nodes.active=texture
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32;scene.render.bake.margin=6
bpy.ops.object.bake(type='EMIT')
texturepath=ASSETS/'Study_RightFist_BaseColor.png'
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
(HERE/'closed-right-fist.json').write_text(json.dumps({'vertices':v,'normals':normals,'uv':coords,'triangles':tris}),encoding='utf-8')
print('RIGHT_FIST_EXPORTED',len(v),len(tris)//3)
