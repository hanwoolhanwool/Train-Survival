import bpy,json,math,pathlib
from mathutils import Vector,Matrix,Quaternion
project=pathlib.Path('C:/Users/iam12/Train Survival');folder=project/'output/raccoon-locomotion-v14/after'
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)));Ci=C.inverted();ground=.9504841566085815
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def pos(v):return C@vec(v)-Vector((0,0,ground))
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
floor=bpy.data.materials.new('v14 지면 간격 표시');floor.use_nodes=True;nodes=floor.node_tree.nodes;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');checker=nodes.new('ShaderNodeTexChecker');checker.inputs['Color1'].default_value=(.13,.17,.2,1);checker.inputs['Color2'].default_value=(.22,.27,.30,1);checker.inputs['Scale'].default_value=2;geometry=nodes.new('ShaderNodeNewGeometry');floor.node_tree.links.new(geometry.outputs['Position'],checker.inputs['Vector']);floor.node_tree.links.new(checker.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.95
results={};scenes=[]
for number,test,title in [(11,'transitions','이동 전환'),(12,'turn_stop','회전·급정지'),(13,'terrain','경사·계단')]:
 data=json.loads((folder/test/'motion_exchange.json').read_text());scene=bpy.data.scenes.new('%02d v14 %s'%(number,title));scenes.append(scene);scene.world=bpy.context.scene.world;scene.render.fps=30;scene.frame_start=1;scene.frame_end=480;scene.sync_mode='FRAME_DROP';scene.render.resolution_x=1280;scene.render.resolution_y=960
 root=bpy.data.objects.new('v14 위치 '+title,None);scene.collection.objects.link(root);root.location=(0,0,ground)
 src=bpy.data.objects['RIG_Raccoon_HybridAuthor_v8'];rig=src.copy();rig.data=src.data.copy();rig.name='RIG_v14_'+test;scene.collection.objects.link(rig);rig.animation_data_clear();rig.parent=root;rig.matrix_parent_inverse=Matrix.Identity(4);rig.matrix_basis=Matrix.Identity(4);rig.data.pose_position='POSE'
 for p in rig.pose.bones:
  for c in list(p.constraints):p.constraints.remove(c)
  p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
 pieces=[]
 for sourceName in ['Raccoon_RoundTail_Body_v11','Raccoon_RoundTail_v11','Raccoon_TailOpening_v11']:
  source=bpy.data.objects[sourceName];ob=source.copy();ob.data=source.data.copy();ob.name='v14_'+test+'_'+sourceName;scene.collection.objects.link(ob);ob.animation_data_clear();ob.parent=rig;ob.matrix_parent_inverse=Matrix.Identity(4);ob.matrix_basis=Matrix.Identity(4)
  for mod in ob.modifiers:
   if mod.type=='ARMATURE':mod.object=rig
  if ob.data.shape_keys:ob.data.shape_keys.animation_data_clear()
  pieces.append(ob)
 body=pieces[0];weapons={}
 for g in data['geometry']:
  geo=bpy.data.meshes.new('v14 '+g['source']);geo.from_pydata([C@vec(v) for v in g['vertices']],[],[g['triangles'][i:i+3][::-1] for i in range(0,len(g['triangles']),3)]);uv=geo.uv_layers.new(name='UVMap')
  for lp in geo.loops:v=g['uv'][lp.vertex_index];uv.data[lp.index].uv=(v['x'],v['y'])
  source=bpy.data.objects.get(g['source'])
  if source and source.data.materials:geo.materials.append(source.data.materials[0])
  for p in geo.polygons:p.use_smooth=True
  ob=bpy.data.objects.new('v14_'+test+'_'+g['source'],geo);scene.collection.objects.link(ob);ob.parent=root;ob.rotation_mode='QUATERNION';weapons[g['name']]=ob
 for surface in data['surfaces']:
  geo=bpy.data.meshes.new('v14 course');geo.from_pydata([(x,y,z) for z in [-.5,.5] for y in [-.5,.5] for x in [-.5,.5]],[],[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]);geo.materials.append(floor);ob=bpy.data.objects.new('v14 '+surface['name'],geo);scene.collection.objects.link(ob)
  mat=(C@quat(surface['rotation']).to_matrix()@Ci).to_4x4();mat.translation=C@vec(surface['position']);sc=vec(surface['scale']);ob.matrix_world=mat@Matrix.Diagonal((sc.x,sc.z,sc.y,1))
 cam=bpy.data.objects.new('v14 추적 카메라 '+title,bpy.data.cameras.new('v14 camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=4.1;cam.data.clip_end=300
 previous={};ordered=sorted(rig.pose.bones,key=lambda p:len(p.parent_recursive));lastLabel=None
 for index,f in enumerate(data['frames'][::2]):
  frame=index+1;desired={}
  for b in f['bones']:
   bone=rig.data.bones.get(b['name'])
   if bone is None:continue
   delta=C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@Ci;mat=(delta@bone.matrix_local.to_3x3()).to_4x4();mat.translation=pos(b['position']);desired[b['name']]=mat
  for p in ordered:
   if p.name not in desired:continue
   if p.parent and p.parent.name in desired:basis=(p.parent.bone.matrix_local.inverted()@p.bone.matrix_local).inverted()@desired[p.parent.name].inverted()@desired[p.name]
   else:basis=p.bone.matrix_local.inverted()@desired[p.name]
   loc,rot,scale=basis.decompose()
   if p.name in previous:rot.make_compatible(previous[p.name])
   previous[p.name]=rot.copy();p.location=loc;p.rotation_quaternion=rot;p.scale=scale
   for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=frame,group=p.name)
  for i,k in enumerate(list(body.data.shape_keys.key_blocks)[1:]):k.value=f['shapes'][i]/100;k.keyframe_insert('value',frame=frame)
  for w in f['weapons']:
   ob=weapons[w['name']];mat=(C@quat(w['rotation']).to_matrix()@Ci).to_4x4();mat.translation=pos(w['position']);ob.matrix_basis=mat@Matrix.Diagonal((*vec(w['scale']),1))
   for prop in ['location','rotation_quaternion','scale']:ob.keyframe_insert(prop,frame=frame)
  target=C@vec(f['root'])+Vector((0,0,.95));cam.location=target+Vector((3.8,4,1.5));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.keyframe_insert('location',frame=frame);cam.keyframe_insert('rotation_euler',frame=frame)
  if f['label']!=lastLabel:scene.timeline_markers.new(f['label'],frame=frame);lastLabel=f['label']
 for owner in [rig,body.data.shape_keys,cam,*weapons.values()]:
  if owner.animation_data and owner.animation_data.action:
   owner.animation_data.action.name='v14 '+test+' '+owner.name
   for layer in owner.animation_data.action.layers:
    for strip in layer.strips:
     for bag in strip.channelbags:
      for curve in bag.fcurves:
       for key in curve.keyframe_points:key.interpolation='LINEAR'
 bpy.context.window.scene=scene;scene.frame_set(1)
 for ob in scene.objects:
  if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)
 errors=[]
 for frame in [1,90,180,240,360,480]:
  scene.frame_set(frame);f=data['frames'][(frame-1)*2];errors.extend((rig.pose.bones[b['name']].head-pos(b['position'])).length for b in f['bones'] if b['name'] in rig.pose.bones)
 results[test]={'frames':480,'fps':30,'max_bone_position_error_m':max(errors)};scene['검토 안내']='Space 재생. 실제 CharacterController 이동과 지형 충돌을 사용한 결과. 발 높이는 보정했으며 이동 속도 대비 보폭에 의한 수평 미끄러짐은 남아 있습니다.'
bpy.context.window.scene=scenes[0];scenes[0].frame_set(90)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  s=area.spaces.active;s.shading.type='MATERIAL';s.shading.use_scene_world=False;s.shading.use_scene_lights=False;s.overlay.show_overlays=False;s.region_3d.view_perspective='CAMERA';s.region_3d.view_camera_zoom=10;s.region_3d.view_camera_offset=(0,0)
note=bpy.data.texts.new('v14 이동 검수 안내');note.write('11 이동 전환 / 12 방향 전환·급정지 / 13 경사·계단. 각 16초, 30fps. Space 재생.\n양손 파지 이탈과 발의 지형 관통을 보정했습니다. 꼬리 모델·상체 클립은 그대로입니다.\n걷기 4.5m/s, 달리기 7m/s와 현재 하체 보폭 사이의 차이로 수평 미끄러짐은 남아 있습니다. 해당 문제를 숨기기 위해 이동 속도를 바꾸거나 발을 지면에 강제로 고정하지 않았습니다.\n이 파일은 단일 캐릭터 검토용이며 네트워크 및 이동 열차 검수는 별도입니다.\n')
(project/'output/raccoon-locomotion-v14/blender_review_validation.json').write_text(json.dumps(results,indent=2));bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_LocomotionReview_v14.blend');print(results)
