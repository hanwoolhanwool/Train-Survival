import bpy,json,math
from mathutils import Vector,Matrix,Quaternion
ROOT='C:/Users/iam12/Train Survival/output/raccoon-motion-v6'
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
data=json.load(open(ROOT+'/motion_exchange.json',encoding='utf-8'))
source=bpy.data.objects['raccoon'];source_rig=bpy.data.objects['RIG_Raccoon']
collection=bpy.data.collections.get('Raccoon_MotionReview_v6')
if collection:
    for o in list(collection.objects):bpy.data.objects.remove(o,do_unlink=True)
else:
    collection=bpy.data.collections.new('Raccoon_MotionReview_v6');bpy.context.scene.collection.children.link(collection)
rig=source_rig.copy();rig.data=source_rig.data.copy();rig.name='RIG_Raccoon_MotionReview_v6';collection.objects.link(rig);rig.animation_data_clear()
for p in rig.pose.bones:
    for c in p.constraints:c.mute=True
    p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
mesh=source.copy();mesh.data=source.data.copy();mesh.name='raccoon_MotionReview_v6';collection.objects.link(mesh)
mesh.parent=rig;mesh.matrix_parent_inverse=Matrix.Identity(4);mesh.matrix_basis=Matrix.Identity(4)
for mod in mesh.modifiers:
    if mod.type=='ARMATURE':mod.object=rig
mesh.data.shape_keys.animation_data_clear()
# Use the final atlas matching this mesh's tail UVs, rather than the old packed strip image.
atlas=bpy.data.images.load('C:/Users/iam12/Train Survival/Assets/_Project/Art/Characters/Raccoon/Textures/T_Raccoon_HandsTail_BaseColor.png',check_existing=True)
atlas.pack()
for node in mesh.data.materials[1].node_tree.nodes:
    if node.type=='TEX_IMAGE':node.image=atlas;mesh.data.materials[1].node_tree.nodes.active=node
for o in bpy.context.scene.objects:o.hide_set(o not in [rig,mesh])
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)));Cinv=C.inverted();ground=.9504841566085815
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def position(v):
    p=C@vec(v);p.z-=ground;return p
weapons={}
for g in data['geometry']:
    geo=bpy.data.meshes.new('Motion_'+g['source']);geo.from_pydata([C@vec(v) for v in g['vertices']],[],[g['triangles'][i:i+3][::-1] for i in range(0,len(g['triangles']),3)])
    uv=geo.uv_layers.new(name='UVMap')
    for loop in geo.loops:
        v=g['uv'][loop.vertex_index];uv.data[loop.index].uv=(v['x'],v['y'])
    mat_source=bpy.data.objects.get(g['source'])
    if mat_source and mat_source.type=='MESH' and mat_source.data.materials:geo.materials.append(mat_source.data.materials[0])
    for p in geo.polygons:p.use_smooth=True
    ob=bpy.data.objects.new('MOTION_'+g['source'],geo);collection.objects.link(ob);weapons[g['name']]=ob
previous={}
ordered=sorted(rig.pose.bones,key=lambda p:len(p.parent_recursive))
for f in data['frames']:
    desired={}
    for b in f['bones']:
        bone=rig.data.bones.get(b['name'])
        if bone is None:continue
        delta=C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@Cinv
        mat=(delta@bone.matrix_local.to_3x3()).to_4x4();mat.translation=position(b['position']);desired[b['name']]=mat
    for p in ordered:
        if p.name not in desired:continue
        if p.parent and p.parent.name in desired:
            rest_local=p.parent.bone.matrix_local.inverted()@p.bone.matrix_local
            basis=rest_local.inverted()@desired[p.parent.name].inverted()@desired[p.name]
        else:basis=p.bone.matrix_local.inverted()@desired[p.name]
        loc,rot,scale=basis.decompose()
        if p.name in previous:rot.make_compatible(previous[p.name])
        previous[p.name]=rot.copy();p.location=loc;p.rotation_quaternion=rot;p.scale=scale
        for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=f['frame'],group=p.name)
    for i,key in enumerate(list(mesh.data.shape_keys.key_blocks)[1:]):
        key.value=f['shapes'][i]/100;key.keyframe_insert('value',frame=f['frame'])
    for w in f['weapons']:
        ob=weapons[w['name']];mat=(C@quat(w['rotation']).to_matrix()@Cinv).to_4x4()
        mat.translation=position(w['position']);mat=mat@Matrix.Diagonal((*vec(w['scale']),1))
        ob.matrix_world=rig.matrix_world@mat;ob.rotation_mode='QUATERNION'
        for prop in ['location','rotation_quaternion','scale']:ob.keyframe_insert(prop,frame=f['frame'])
        ob.hide_viewport=ob.hide_render=not w['visible']
        ob.keyframe_insert('hide_viewport',frame=f['frame']);ob.keyframe_insert('hide_render',frame=f['frame'])
for owner in [rig,mesh.data.shape_keys,*weapons.values()]:
    if not owner.animation_data or not owner.animation_data.action:continue
    action=owner.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:key.interpolation='CONSTANT' if curve.data_path.startswith('hide_') else 'LINEAR'
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=360
for m in list(scene.timeline_markers):scene.timeline_markers.remove(m)
for frame,label in [(1,'대기'),(61,'걷기'),(121,'달리기'),(181,'회전'),(241,'정지 · 조준'),(301,'무기 전환')]:scene.timeline_markers.new(label,frame=frame)
scene.frame_set(105);rig.hide_set(True)
for o in bpy.context.selected_objects:o.select_set(False)
mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        s=area.spaces.active;s.shading.type='MATERIAL';s.shading.use_scene_world=False;s.shading.use_scene_lights=False;s.overlay.show_overlays=False;s.clip_start=.001
        s.region_3d.view_perspective='ORTHO';s.region_3d.view_distance=3.1
        s.region_3d.view_location=rig.matrix_world@Vector((0,0,.0));s.region_3d.view_rotation=Vector((-1,-2,.3)).to_track_quat('Z','Y')
note=bpy.data.texts.get('MotionReview_v6_README') or bpy.data.texts.new('MotionReview_v6_README');note.clear()
note.write('12 seconds at 30 fps. Space plays the review. 1 idle, 61 walk, 121 run, 181 turn, 241 stop/aim, 301 weapon switch.\nUnity bone transforms, corrected locomotion root offsets, equipment IK and raccoon-specific procedural tail motion are baked for inspection. This is a review action, not the runtime animation source. Runtime remains Animator + RaccoonEquipmentPose + RaccoonTailMotion. Existing locomotion clips are integration assets, not newly authored raccoon locomotion.\n')
bpy.context.view_layer.update()
# Compare imported skeleton against the exchange at the current frame.
record=data['frames'][scene.frame_current-1]
errors=[(rig.pose.bones[b['name']].head-position(b['position'])).length for b in record['bones'] if b['name'] in rig.pose.bones]
report={'frames':len(data['frames']),'fps':30,'max_bone_position_error_m':max(errors),'file':OUT+'/Raccoon_MotionReview_v6.blend'}
json.dump(report,open(ROOT+'/blender_review_validation.json','w'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=report['file']);print(report)
