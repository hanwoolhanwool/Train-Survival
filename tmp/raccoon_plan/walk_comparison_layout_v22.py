import bpy,json,pathlib
from mathutils import Vector,Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival')
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
scene=bpy.data.scenes.new('51 걷기 비교 · 기존 게임 vs 현재 v21')
scene.world=bpy.context.scene.world;scene.render.resolution_x=1600;scene.render.resolution_y=1000
bpy.context.window.scene=scene
scene.render.fps=30;scene.frame_start=1;scene.frame_end=420;scene.sync_mode='FRAME_DROP'
cam=bpy.data.objects.new('걷기 비교 카메라',bpy.data.cameras.new('걷기 비교 동일 시점'))
scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=5.5
target=Vector((0,0,1.05));cam.location=target+Vector((5,-4,1.7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
q=cam.rotation_euler.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));towards=q@Vector((0,0,1))
for test,number,offset in [('legacy_walk',52,-1.35),('current_walk',53,1.35)]:
    source=next(s for s in bpy.data.scenes if s.name.startswith(str(number)+' v22 '))
    root=next(o for o in source.objects if o.type=='EMPTY' and o.name.startswith('v22 위치'))
    group=[root]+list(root.children_recursive);copies={}
    for ob in group:
        n=ob.copy();n.name='비교_'+test+'_'+ob.name;scene.collection.objects.link(n);copies[ob]=n
    for ob,n in copies.items():
        if ob.parent in copies:n.parent=copies[ob.parent]
        for mod in n.modifiers:
            if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
    stage=copies[root];stage.animation_data_clear()
    data=json.loads((project/'output/raccoon-walk-comparison-v22/after'/test/'motion_exchange.json').read_text())
    for f,record in enumerate(data['frames'][::2],1):
        p=record['root'];stage.location=Vector((0,0,.9504841566085815))+right*offset-C@Vector((p['x'],p['y'],p['z']))
        stage.keyframe_insert('location',frame=f)
    for ob in copies.values():
        if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)

floor=bpy.data.materials.new('비교 공통 지면');floor.use_nodes=True;nodes=floor.node_tree.nodes
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.95
position=nodes.new('ShaderNodeNewGeometry');offset=nodes.new('ShaderNodeVectorMath');offset.operation='ADD'
checker=nodes.new('ShaderNodeTexChecker');checker.inputs['Color1'].default_value=(.13,.17,.20,1);checker.inputs['Color2'].default_value=(.23,.28,.31,1);checker.inputs['Scale'].default_value=2
floor.node_tree.links.new(position.outputs['Position'],offset.inputs[0]);floor.node_tree.links.new(offset.outputs[0],checker.inputs['Vector']);floor.node_tree.links.new(checker.outputs['Color'],bs.inputs['Base Color'])
for f,record in enumerate(data['frames'][::2],1):
    offset.inputs[1].default_value=(0,-record['root']['z'],0);offset.inputs[1].keyframe_insert('default_value',frame=f)
mesh=bpy.data.meshes.new('비교 지면');mesh.from_pydata([(-20,-20,0),(20,-20,0),(20,20,0),(-20,20,0)],[],[(0,1,2,3)]);mesh.materials.append(floor)
ground=bpy.data.objects.new('동일 속도로 흐르는 비교 지면',mesh);scene.collection.objects.link(ground)
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
def label(text,x,y,size,color):
    mat=bpy.data.materials.new('비교 글자 '+text);mat.use_nodes=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=(*color,1);shader.inputs['Emission Color'].default_value=(*color,1);shader.inputs['Emission Strength'].default_value=.8
    curve=bpy.data.curves.new(text,'FONT');curve.body=text;curve.font=font;curve.size=size;curve.align_x='CENTER';curve.align_y='CENTER';curve.materials.append(mat)
    ob=bpy.data.objects.new(text,curve);scene.collection.objects.link(ob);ob.location=target+right*x+up*y+towards*1.5;ob.rotation_euler=cam.rotation_euler
label('기존 게임 걷기',-1.35,1.28,.19,(.63,.83,1))
label('현재 라쿤 v21',1.35,1.28,.19,(1,.76,.37))
label('원본 1배 · 라쿤 접지 보정 없음',-1.35,1.03,.09,(.90,.94,1))
label('빠른 박자 · 이동 거리 기반 접지',1.35,1.03,.09,(1,.93,.83))
label('동일 라쿤 모델  /  이동 속도 4.5m/s  /  Space 재생·정지',0,-1.35,.10,(.96,.96,.96))
scene['비교 안내']='왼쪽: AC_Player.controller 원본 걷기 1배를 현재 라쿤에 적용, RaccoonGrounding 제외. 오른쪽: 현재 v21 게임 적용 상태. 모델·무기 파지·실제 이동 속도 4.5m/s·화면 시점 동일. 양쪽을 같은 실시간으로 재생하며 박자를 억지로 맞추지 않습니다. 이동을 화면에서 상쇄하고 공통 지면을 같은 속도로 스크롤합니다.'
note=bpy.data.texts.get('걷기 비교 안내') or bpy.data.texts.new('걷기 비교 안내');note.clear();note.write(scene['비교 안내'])
bpy.context.window.scene=scene;scene.frame_set(1)
for window in bpy.context.window_manager.windows:
    window.scene=scene
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            v=area.spaces.active;v.camera=cam;v.use_local_camera=False;v.shading.type='MATERIAL';v.overlay.show_overlays=False
            v.region_3d.view_perspective='CAMERA';v.region_3d.view_camera_zoom=7;v.region_3d.view_camera_offset=(0,0)
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_WalkComparison_v22.blend',compress=True)
print('Side-by-side original game vs current walk comparison ready')
