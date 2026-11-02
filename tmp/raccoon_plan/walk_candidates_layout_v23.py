import bpy,json,pathlib
from mathutils import Vector,Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival');C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
scene=bpy.data.scenes.new('54 걷기 후보 비교 · 현재 보존 / A / B');bpy.context.window.scene=scene
scene.world=bpy.data.scenes['51 걷기 비교 · 기존 게임 vs 현재 v21'].world
scene.render.resolution_x=1800;scene.render.resolution_y=1050;scene.render.fps=30;scene.frame_start=1;scene.frame_end=420;scene.sync_mode='FRAME_DROP'
cam=bpy.data.objects.new('v23 비교 카메라',bpy.data.cameras.new('v23 동일 시점'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=7.5
focus=Vector((0,0,1.05));cam.location=focus+Vector((5,-4,1.7));cam.rotation_euler=(focus-cam.location).to_track_quat('-Z','Y').to_euler()
q=cam.rotation_euler.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));towards=q@Vector((0,0,1));depth=Vector((-right.y,right.x,0))
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
def label(text,x,y,size,color):
 mat=bpy.data.materials.new('v23 글자 '+text);mat.use_nodes=True;s=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');s.inputs['Base Color'].default_value=(*color,1);s.inputs['Emission Color'].default_value=(*color,1);s.inputs['Emission Strength'].default_value=.8
 curve=bpy.data.curves.new(text,'FONT');curve.body=text;curve.font=font;curve.size=size;curve.align_x='CENTER';curve.align_y='CENTER';curve.materials.append(mat)
 ob=bpy.data.objects.new(text,curve);scene.collection.objects.link(ob);ob.location=focus+right*x+up*y+towards*1.5;ob.rotation_euler=cam.rotation_euler
for test,num,offset,title,subtitle,speed,color in [('current_walk',53,-2.35,'현재 동작 · 보존','v21 그대로 / 4.5m/s',4.5,(1,.74,.38)),('source_like',55,0,'후보 A · 원본형','원본 박자 / 검토 0.8m/s',.8,(.55,.8,1)),('armed_calm',56,2.35,'후보 B · 차분한 무장','A + 상체 흔들림 완화 / 0.8m/s',.8,(.56,1,.76))]:
 source=next(s for s in bpy.data.scenes if s.name.startswith(str(num)+' '));root=next(o for o in source.objects if o.type=='EMPTY' and '위치' in o.name);copies={}
 for ob in [root]+list(root.children_recursive):
  n=ob.copy();n.name='v23 비교_'+test+'_'+ob.name;scene.collection.objects.link(n);copies[ob]=n
 for ob,n in copies.items():
  if ob.parent in copies:n.parent=copies[ob.parent]
  for mod in n.modifiers:
   if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
 stage=copies[root];stage.animation_data_clear()
 folder='raccoon-walk-comparison-v22' if test=='current_walk' else 'raccoon-walk-candidates-v23'
 data=json.loads((project/'output'/folder/'after'/test/'motion_exchange.json').read_text())
 for f,record in enumerate(data['frames'][::2][:420],1):
  p=record['root'];stage.location=Vector((0,0,.9504841566085815))+right*offset-C@Vector((p['x'],p['y'],p['z']));stage.keyframe_insert('location',frame=f)
 for ob in copies.values():
  if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)
 mat=bpy.data.materials.new('v23 지면 '+test);mat.use_nodes=True;nodes=mat.node_tree.nodes;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.95
 pos=nodes.new('ShaderNodeNewGeometry');scroll=nodes.new('ShaderNodeVectorMath');scroll.operation='ADD';checker=nodes.new('ShaderNodeTexChecker');checker.inputs['Color1'].default_value=(.13,.17,.2,1);checker.inputs['Color2'].default_value=(.22,.27,.30,1);checker.inputs['Scale'].default_value=2
 mat.node_tree.links.new(pos.outputs['Position'],scroll.inputs[0]);mat.node_tree.links.new(scroll.outputs[0],checker.inputs['Vector']);mat.node_tree.links.new(checker.outputs['Color'],bs.inputs['Base Color'])
 for f,record in enumerate(data['frames'][::2][:420],1):scroll.inputs[1].default_value=(0,-record['root']['z'],0);scroll.inputs[1].keyframe_insert('default_value',frame=f)
 mesh=bpy.data.meshes.new('v23 비교 지면');mesh.from_pydata([right*(offset+x)+depth*y for x,y in [(-1.15,-18),(1.15,-18),(1.15,18),(-1.15,18)]],[],[(0,1,2,3)]);mesh.materials.append(mat);ob=bpy.data.objects.new('v23 '+test+' 지면 '+str(speed)+'mps',mesh);scene.collection.objects.link(ob)
 label(title,offset,1.40,.17,color);label(subtitle,offset,1.15,.085,(.94,.94,.94))
label('걷기 수정 후보  |  게임에는 아직 적용하지 않음',0,1.83,.13,(1,1,1))
label('왼쪽: 현재 그대로   /   가운데·오른쪽: 같은 느린 이동 속도   /   Space 재생·정지',0,-1.5,.095,(.95,.95,.95))
label('후보는 자세·리듬 검토용 · 발 미끄러짐과 이동 전환은 후속 조정',0,-1.72,.085,(.75,.82,.88))
scene['검토 안내']='현재 v21(4.5m/s)은 변경 없이 복사하여 비교. 후보 A/B는 원본 게임 걷기의 1배 박자를 유지하며 검토 이동 속도 0.8m/s. B는 척추·가슴·목·머리 곡선의 평균 자세를 보존하고 변화폭을 45%로 완화. 다리 곡선과 손 파지 프로필은 유지. 모든 후보는 게임에 미적용. 지면은 각 열의 실제 속도로 흐름. 캐릭터는 화면에서 제자리 표시. 수평 발 미끄러짐과 전환은 후속 검토.'
note=bpy.data.texts.new('v23 새 걷기 후보 안내');note.write(scene['검토 안내'])
scene.frame_set(1)
for w in bpy.context.window_manager.windows:
 w.scene=scene
 for area in w.screen.areas:
  if area.type=='VIEW_3D':
   v=area.spaces.active;v.camera=cam;v.use_local_camera=False;v.shading.type='MATERIAL';v.overlay.show_overlays=False;v.region_3d.view_perspective='CAMERA';v.region_3d.view_camera_zoom=7;v.region_3d.view_camera_offset=(0,0)
path='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_WalkCandidates_v23.blend'
# Write just the new review scenes and their dependencies; older files stay intact.
export={scene,note}|{s for s in bpy.data.scenes if s.name.startswith(('55 v23 ','56 v23 '))}
bpy.data.libraries.write(path,export,path_remap='ABSOLUTE',fake_user=True,compress=True)
(project/'output/raccoon-walk-candidates-v23/layout_status.txt').write_text(path)
print(path)
