import bpy,json,pathlib
from mathutils import Vector,Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival')
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
scene=bpy.data.scenes.new('60 방향 전환 · v24와 v25 비교')
scene.world=bpy.context.scene.world;scene.render.resolution_x=1600;scene.render.resolution_y=1000
bpy.context.window.scene=scene
scene.render.fps=30;scene.frame_start=1;scene.frame_end=600;scene.sync_mode='FRAME_DROP'
cam=bpy.data.objects.new('걷기 비교 카메라',bpy.data.cameras.new('걷기 비교 동일 시점'))
scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=5.5
target=Vector((0,0,1.05));cam.location=target+Vector((5,-4,1.7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
q=cam.rotation_euler.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));towards=q@Vector((0,0,1))
for test,number,offset in [('before',64,-1.35),('after',61,1.35)]:
    source=next(s for s in bpy.data.scenes if s.name.startswith(str(number)+' '))
    root=next(o for o in source.objects if o.type=='EMPTY' and '위치' in o.name)
    group=[root]+list(root.children_recursive);copies={}
    for ob in group:
        n=ob.copy();n.name='비교_'+test+'_'+ob.name;scene.collection.objects.link(n);copies[ob]=n
    for ob,n in copies.items():
        if ob.parent in copies:n.parent=copies[ob.parent]
        for mod in n.modifiers:
            if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
    stage=copies[root];stage.animation_data_clear()
    folder='raccoon-walk-terrain-v25'
    data=json.loads((project/'output'/folder/test/'turns'/'motion_exchange.json').read_text())
    for f,record in enumerate(data['frames'][::2],1):
        p=record['root'];stage.location=Vector((0,0,.9504841566085815))+right*offset-C@Vector((p['x'],0,p['z']))
        stage.keyframe_insert('location',frame=f)
    for ob in copies.values():
        if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)

floor=bpy.data.materials.new('비교 공통 지면');floor.use_nodes=True;nodes=floor.node_tree.nodes
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.95
position=nodes.new('ShaderNodeNewGeometry');offset=nodes.new('ShaderNodeVectorMath');offset.operation='ADD'
checker=nodes.new('ShaderNodeTexChecker');checker.inputs['Color1'].default_value=(.13,.17,.20,1);checker.inputs['Color2'].default_value=(.23,.28,.31,1);checker.inputs['Scale'].default_value=2
floor.node_tree.links.new(position.outputs['Position'],offset.inputs[0]);floor.node_tree.links.new(offset.outputs[0],checker.inputs['Vector']);floor.node_tree.links.new(checker.outputs['Color'],bs.inputs['Base Color'])
for f,record in enumerate(data['frames'][::2],1):
    offset.inputs[1].default_value=(-record['root']['x'],-record['root']['z'],0);offset.inputs[1].keyframe_insert('default_value',frame=f)
mesh=bpy.data.meshes.new('비교 지면');mesh.from_pydata([(-20,-20,0),(20,-20,0),(20,20,0),(-20,20,0)],[],[(0,1,2,3)]);mesh.materials.append(floor)
ground=bpy.data.objects.new('동일 속도로 흐르는 비교 지면',mesh);scene.collection.objects.link(ground)
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
def label(text,x,y,size,color):
    mat=bpy.data.materials.new('비교 글자 '+text);mat.use_nodes=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=(*color,1);shader.inputs['Emission Color'].default_value=(*color,1);shader.inputs['Emission Strength'].default_value=.8
    curve=bpy.data.curves.new(text,'FONT');curve.body=text;curve.font=font;curve.size=size;curve.align_x='CENTER';curve.align_y='CENTER';curve.materials.append(mat)
    ob=bpy.data.objects.new(text,curve);scene.collection.objects.link(ob);ob.location=target+right*x+up*y+towards*5;ob.rotation_euler=cam.rotation_euler
label('v24 · 수정 전',-1.35,1.28,.19,(.63,.83,1))
label('v25 · 회전 보정',1.35,1.28,.19,(1,.76,.37))
label('디딘 발이 몸의 회전을 따라감',-1.35,1.03,.09,(.90,.94,1))
label('디딘 발 방향 유지 · 비틀림 완화',1.35,1.03,.09,(1,.93,.83))
label('동일 이동 속도 0.8m/s  /  게임에 미적용  /  Space 재생·정지',0,-1.35,.10,(.96,.96,.96))
scene['비교 안내']='왼쪽은 v24를 새 회전 경로에 적용한 기준 기록, 오른쪽은 별도 v25 수정본. 이동 경로와 최고 속도 0.8m/s 동일. 회전할 때 디딘 발 방향을 유지하고 35~55도 비틀림에서 접촉을 완만하게 해제. 61 회전, 62 오르막·내리막 12도, 63 옆경사 10도. 각 20초 시나리오이며 반복 연결용 클립은 아님. 기존 동작과 v24 파일 보존. 게임에는 미적용.'
label('61 회전  /  62 오르막·내리막  /  63 옆경사',0,-1.57,.095,(.9,.94,.98))
mat=bpy.data.materials.new('v25 안내 배경');mat.use_nodes=True;shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Base Color'].default_value=(.018,.026,.038,1);shader.inputs['Roughness'].default_value=1
for y,h in [(1.17,.65),(-1.46,.49)]:
    me=bpy.data.meshes.new('v25 안내 배경');center=target+up*y+towards*4.95;me.from_pydata([center+right*x+up*z for x,z in [(-2.65,-h/2),(2.65,-h/2),(2.65,h/2),(-2.65,h/2)]],[],[(0,1,2,3)]);me.materials.append(mat);ob=bpy.data.objects.new('v25 안내 패널',me);scene.collection.objects.link(ob)
note=bpy.data.texts.new('v25 회전·경사 안내');note.write(scene['비교 안내'])
scene.frame_set(1)
for window in bpy.context.window_manager.windows:
    window.scene=scene
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            v=area.spaces.active;v.camera=cam;v.use_local_camera=False;v.shading.type='MATERIAL';v.overlay.show_overlays=False
            v.region_3d.view_perspective='CAMERA';v.region_3d.view_camera_zoom=7;v.region_3d.view_camera_offset=(0,0)
path='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_TurnSlope_v25.blend'
export={scene,note}|{s for s in bpy.data.scenes if s.name.startswith(('61 v25 ','62 v25 ','63 v25 '))}
bpy.data.libraries.write(path,export,path_remap='ABSOLUTE',fake_user=True,compress=True)
print(path)

