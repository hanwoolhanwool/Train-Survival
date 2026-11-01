import bpy, json, math, os, bmesh
from mathutils import Vector,Matrix,Quaternion
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/qa_rig_v1.py',encoding='utf-8').read().split('results=[]')[0],'qa_helpers','exec'))
neutral()
for p in rig.pose.bones:
    if not any(s in p.name for s in ['Proximal','Intermediate','Distal']):p.rotation_mode='QUATERNION'
action=bpy.data.actions.new('QA_Raccoon_Rig_v1_Poses');action.use_fake_user=True
rig.animation_data_create().action=action
sequence=[(1,'neutral'),(21,'elbow90'),(41,'grip'),(61,'crouch'),(81,'reach'),(101,'stride'),(121,'tail_turn'),(141,'neutral')]
for frame,name in sequence:
    pose(name);rig.animation_data.action=action
    for p in rig.pose.bones:
        if not any(s in p.name for s in ['Proximal','Intermediate','Distal']):
            p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            p.keyframe_insert('location',frame=frame,group=p.name)
        for prop in ['curl','spread','IK']:
            if prop in p:p.keyframe_insert(data_path='["'+prop+'"]',frame=frame,group=p.name)
curves=[]
for layer in action.layers:
    for strip in layer.strips:
        if hasattr(strip,'channelbags'):
            for bag in strip.channelbags:curves.extend(bag.fcurves)
for curve in curves:
    for point in curve.keyframe_points:point.interpolation='CONSTANT'
qa=bpy.data.scenes.new('Raccoon_Rig_v1_PoseQA');qa.collection.objects.link(rig);qa.collection.objects.link(ob)
qa.frame_start=1;qa.frame_end=141;qa.render.fps=30
for f,name in sequence:qa.timeline_markers.new(name,frame=f)
qa['instructions']='Pose QA, not a gameplay animation. Frames 1 neutral; 21 elbows; 41 grip; 61 crouch; 81 reach; 101 stride; 121 tail and head; 141 reset.'
qa.frame_set(1);qa.view_layers[0].objects.active=rig
bpy.data.libraries.write(OUT+'/Raccoon_Rig_v1_PoseQA.blend',{qa,action},path_remap='ABSOLUTE',fake_user=False,compress=True)
bpy.data.scenes.remove(qa);neutral()
# Export clean, grounded copies. Authoring controls and Blender drivers stay in the blend.
export_col=bpy.data.collections.new('_RaccoonExportTemporary');bpy.context.scene.collection.children.link(export_col)
er=rig.copy();er.data=rig.data.copy();er.name='Raccoon_Rig';er.animation_data_clear();export_col.objects.link(er)
em=ob.copy();em.data=ob.data.copy();em.name='CH_Raccoon_TP';export_col.objects.link(em)
em.data.shape_keys.animation_data_clear()
for key in em.data.shape_keys.key_blocks:
    if key.name!='Basis':key.value=0
er.matrix_world=Matrix.Identity(4);em.parent=er;em.matrix_parent_inverse=Matrix.Identity(4);em.matrix_basis=Matrix.Identity(4)
floor=min(v.co.z for v in em.data.vertices);shift=Matrix.Translation((0,0,-floor));em.data.transform(shift,shape_keys=True);er.data.transform(shift)
for p in er.pose.bones:
    for c in list(p.constraints):p.constraints.remove(c)
    p.location=(0,0,0);p.rotation_quaternion=(1,0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
for m in em.modifiers:
    if m.type=='ARMATURE':m.object=er
for g in list(em.vertex_groups):
    if g.name.startswith('SEL_'):em.vertex_groups.remove(g)
for o in bpy.context.selected_objects:o.select_set(False)
er.select_set(True);bpy.context.view_layer.objects.active=er;bpy.ops.object.mode_set(mode='EDIT')
for b in list(er.data.edit_bones):
    if b.name.startswith('CTRL_'):er.data.edit_bones.remove(b)
bpy.ops.object.mode_set(mode='OBJECT');em.select_set(True)
assert '-Z' in [i.identifier for i in bpy.ops.export_scene.fbx.get_rna_type().properties['axis_forward'].enum_items]
assert 'Y' in [i.identifier for i in bpy.ops.export_scene.fbx.get_rna_type().properties['axis_up'].enum_items]
path=OUT+'/CH_Raccoon_TP_Rig_v1.fbx'
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
human_names=[b.name for b in er.data.bones if not b.name.startswith(('Tail','Ear','Scarf')) and b.name!='Root']
mapping={'human_bones':{n:n for n in human_names},'extra_deform_bones':[b.name for b in er.data.bones if b.name.startswith(('Tail','Ear','Scarf'))],'unit':'metres','rest_pose':'A pose; Unity Humanoid Configure and enforce T pose still required','forward':'Blender -Y -> FBX -Z','corrective_shapes':{'COR_Elbow90.L':{'bone':'LeftLowerArm','start_degrees':45,'full_degrees':90},'COR_Elbow90.R':{'bone':'RightLowerArm','start_degrees':45,'full_degrees':90}},'important':'Blender drivers do not run in Unity. Recreate corrective shape weights at runtime or bake to clips. Materials require URP conversion. No gameplay prefab replaced.'}
with open(OUT+'/Raccoon_Humanoid_Mapping_v1.json','w') as f:json.dump(mapping,f,indent=2)
# Round trip verifies actual FBX skin, bones, shape keys and dimensions.
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=path,use_anim=False);imported=set(bpy.data.objects)-before
im=next(o for o in imported if o.type=='MESH');ir=next(o for o in imported if o.type=='ARMATURE');im.data.calc_loop_triangles()
coords=[im.matrix_world@v.co for v in im.data.vertices]
report={'fbx':path,'bones':len(ir.data.bones),'human_mapping_count':len(human_names),'missing_bones':[n for n in er.data.bones.keys() if n not in ir.data.bones],'vertices':len(im.data.vertices),'triangles':len(im.data.loop_triangles),'shape_keys':[k.name for k in im.data.shape_keys.key_blocks] if im.data.shape_keys else [],'materials':len(im.data.materials),'height_metres':max(c.z for c in coords)-min(c.z for c in coords),'floor_z':min(c.z for c in coords),'unweighted_vertices':sum(not any(g.weight>1e-6 for g in v.groups) for v in im.data.vertices),'max_influences':max(sum(g.weight>1e-6 for g in v.groups) for v in im.data.vertices),'max_weight_sum_error':max(abs(1-sum(g.weight for g in v.groups)) for v in im.data.vertices)}
with open(OUT+'/rig_v1_fbx_roundtrip.json','w') as f:json.dump(report,f,indent=2)
for o in imported:bpy.data.objects.remove(o,do_unlink=True)
ed=er.data;md=em.data;bpy.data.objects.remove(em,do_unlink=True);bpy.data.objects.remove(er,do_unlink=True);bpy.data.meshes.remove(md);bpy.data.armatures.remove(ed);bpy.data.collections.remove(export_col)
neutral();ob['rigging_status']='Rig v1: 63 deform bones; four-influence normalized skin; elbow drivers; IK/FK; basic grip and pose QA. Unity Avatar and weapon-specific grip pending.'
rig['qa_file']='Raccoon_Rig_v1_PoseQA.blend';rig['gameplay_animation_status']='Not authored; QA action is a static pose sheet.'
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Train_Servival_raccoon_rig_v1.blend')
print('PACKAGED',json.dumps(report))
