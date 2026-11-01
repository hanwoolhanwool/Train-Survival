import bpy,json,math,os
from mathutils import Matrix,Vector,Quaternion
ROOT='C:/Users/iam12/Train Survival/output/raccoon-run-comparison'
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_RunComparison_v7.blend'
source=bpy.data.objects['raccoon'];source_rig=bpy.data.objects['RIG_Raccoon']
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)));Ci=C.inverted();ground=.9504841566085815
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def pos(v):return C@vec(v)-Vector((0,0,ground))
base=bpy.data.collections.new('달리기 비교 - 정면')
groups=[];validation={}
for key,label,x in [('existing','기존 게임',-1.2),('new','새 라쿤 v7',1.2)]:
    data=json.load(open(ROOT+'/'+key+'.json',encoding='utf-8'))
    root=bpy.data.objects.new(label+' 위치',None);base.objects.link(root);root.location=(x,0,ground)
    rig=source_rig.copy();rig.data=source_rig.data.copy();rig.name='COMPARE_'+key+'_Rig';base.objects.link(rig)
    rig.animation_data_clear();rig.parent=root;rig.matrix_parent_inverse=Matrix.Identity(4);rig.matrix_basis=Matrix.Identity(4)
    for p in rig.pose.bones:
        for c in list(p.constraints):p.constraints.remove(c)
        p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
    mesh=source.copy();mesh.data=source.data.copy();mesh.name='COMPARE_'+key+'_Raccoon';base.objects.link(mesh)
    mesh.animation_data_clear();mesh.parent=rig;mesh.matrix_parent_inverse=Matrix.Identity(4);mesh.matrix_basis=Matrix.Identity(4)
    mesh.data.shape_keys.animation_data_clear()
    for mod in mesh.modifiers:
        if mod.type=='ARMATURE':mod.object=rig
    atlas=bpy.data.images.load('C:/Users/iam12/Train Survival/Assets/_Project/Art/Characters/Raccoon/Textures/T_Raccoon_HandsTail_BaseColor.png',check_existing=True);atlas.pack()
    for n in mesh.data.materials[1].node_tree.nodes:
        if n.type=='TEX_IMAGE':n.image=atlas
    weapons={};objects=[root,rig,mesh]
    for g in data['geometry']:
        geo=bpy.data.meshes.new('Compare_'+g['source']);geo.from_pydata([C@vec(v) for v in g['vertices']],[],[g['triangles'][i:i+3][::-1] for i in range(0,len(g['triangles']),3)])
        uv=geo.uv_layers.new(name='UVMap')
        for lp in geo.loops:
            v=g['uv'][lp.vertex_index];uv.data[lp.index].uv=(v['x'],v['y'])
        mat_source=bpy.data.objects.get(g['source'])
        if mat_source and mat_source.type=='MESH' and mat_source.data.materials:geo.materials.append(mat_source.data.materials[0])
        for p in geo.polygons:p.use_smooth=True
        ob=bpy.data.objects.new('COMPARE_'+key+'_'+g['source'],geo);base.objects.link(ob);ob.parent=root;ob.rotation_mode='QUATERNION';weapons[g['name']]=ob;objects.append(ob)
    previous={};ordered=sorted(rig.pose.bones,key=lambda p:len(p.parent_recursive))
    for f in data['frames']:
        desired={}
        for b in f['bones']:
            bone=rig.data.bones.get(b['name'])
            if bone is None:continue
            delta=C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@Ci
            mat=(delta@bone.matrix_local.to_3x3()).to_4x4();mat.translation=pos(b['position']);desired[b['name']]=mat
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
        for i,k in enumerate(list(mesh.data.shape_keys.key_blocks)[1:]):k.value=f['shapes'][i]/100;k.keyframe_insert('value',frame=f['frame'])
        for w in f['weapons']:
            ob=weapons[w['name']];mat=(C@quat(w['rotation']).to_matrix()@Ci).to_4x4();mat.translation=pos(w['position']);mat=mat@Matrix.Diagonal((*vec(w['scale']),1));ob.matrix_basis=mat
            for prop in ['location','rotation_quaternion','scale']:ob.keyframe_insert(prop,frame=f['frame'])
    for owner in [rig,mesh.data.shape_keys,*weapons.values()]:
        if owner.animation_data and owner.animation_data.action:
            owner.animation_data.action.name='비교_'+label+'_'+owner.name
            for layer in owner.animation_data.action.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:
                        for curve in bag.fcurves:
                            for k in curve.keyframe_points:k.interpolation='LINEAR'
    groups.append((root,rig,mesh,objects,data))

font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
def material(name,color,emission=False):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=.85
    if emission:n.inputs['Emission Color'].default_value=(*color,1);n.inputs['Emission Strength'].default_value=1
    return m
blue=material('비교 - 기존 표시',(.15,.65,1),True);orange=material('비교 - 신규 표시',(1,.55,.16),True);white=material('비교 - 안내',(.75,.8,.85),True);floor_mat=material('비교 - 바닥',(.065,.08,.10))
scenes=[]
for index,title,angle in [(1,'정면',0),(2,'측면',90),(3,'후면',180)]:
    scene=bpy.data.scenes.new('%02d 달리기 비교 · %s'%(index,title));scenes.append(scene)
    scene.render.fps=60;scene.frame_start=1;scene.frame_end=150;scene.render.resolution_x=1600;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
    scene.sync_mode='FRAME_DROP';scene.world=bpy.data.worlds.new('비교 배경 '+title);scene.world.color=(.12,.12,.12)
    col=base if index==1 else bpy.data.collections.new('달리기 비교 - '+title);scene.collection.children.link(col)
    if index!=1:
        for root,rig,mesh,objects,data in groups:
            copies={}
            for original in objects:
                copy=original.copy();copy.name=original.name+'_'+title;col.objects.link(copy);copies[original]=copy
            for original,copy in copies.items():
                if original.parent in copies:copy.parent=copies[original.parent]
                for mod in copy.modifiers:
                    if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
            copies[root].rotation_euler.z=math.radians(angle)
    cam_data=bpy.data.cameras.new('비교 카메라 '+title);cam=bpy.data.objects.new('비교 카메라 '+title,cam_data);col.objects.link(cam)
    cam.location=(0,-9,2.2);target=Vector((0,0,.95));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam_data.type='ORTHO';cam_data.ortho_scale=5.3;scene.camera=cam
    def text(name,body,location,size,mat):
        curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.font=font;curve.size=size;curve.align_x='CENTER'
        ob=bpy.data.objects.new(name,curve);col.objects.link(ob);ob.location=location;ob.rotation_euler=cam.rotation_euler;curve.materials.append(mat);return ob
    text('기존 제목 '+title,'기존 게임 달리기',(-1.2,0,2.05),.175,blue)
    text('신규 제목 '+title,'새 라쿤 달리기 v7',(1.2,0,2.05),.175,orange)
    text('비교 조건 '+title,'동일 라쿤 · 샷건 · 7m/s · 원래 재생 속도',(0,-.1,-.28),.095,white)
    text('조작 안내 '+title,'SPACE 재생/정지   |   상단 Scene에서 정면 · 측면 · 후면 전환',(0,-.1,-.46),.08,white)
    geo=bpy.data.meshes.new('비교 바닥');geo.from_pydata([(-3,-2,0),(3,-2,0),(3,3,0),(-3,3,0)],[],[(0,1,2,3)])
    ob=bpy.data.objects.new('비교 바닥 '+title,geo);col.objects.link(ob);geo.materials.append(floor_mat)
    for frame in [1,61,121]:scene.timeline_markers.new('%ds'%((frame-1)//60),frame=frame)
    scene['비교 안내']='왼쪽 기존 게임 달리기 / 오른쪽 새 라쿤 v7. 같은 모델과 샷건, 7m/s. 각 클립의 원래 길이와 게임 보정을 유지. Space 재생. Scene 목록에서 정면/측면/후면 선택.'
    scene.frame_set(1)
bpy.context.window.scene=scenes[0]
for scene in scenes:
    for ob in scene.objects:
        if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True,view_layer=scene.view_layers[0])
        else:ob.hide_set(False,view_layer=scene.view_layers[0])
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        s=area.spaces.active;s.shading.type='MATERIAL';s.shading.use_scene_world=False;s.shading.use_scene_lights=False;s.overlay.show_overlays=False
        s.region_3d.view_perspective='CAMERA';s.region_3d.view_camera_zoom=5;s.region_3d.view_camera_offset=(0,0);s.lock_camera=False
for root,rig,mesh,objects,data in groups:
    errors=[]
    for f in [1,16,31,61,121,240]:
        scenes[0].frame_set(f);bpy.context.view_layer.update()
        errors.extend((rig.pose.bones[b['name']].head-pos(b['position'])).length for b in data['frames'][f-1]['bones'] if b['name'] in rig.pose.bones)
    validation[rig.name]={'frames':len(data['frames']),'max_bone_error_m':max(errors)}
scenes[0].frame_set(1)
note=bpy.data.texts.new('달리기 비교 안내');note.write('왼쪽: 기존 게임 달리기. 오른쪽: 새 라쿤 달리기 v7.\n동일한 라쿤, 샷건, Speed 7. 재생 속도 조절 없이 각 클립 원래 리듬을 유지합니다.\n기존 쪽은 기존 상체 레이어 및 고개 보정, 신규 쪽은 신규 달리기용 설정을 적용한 실제 게임 결과입니다.\nSpace: 재생/정지. 좌우 방향키: 한 프레임씩. 상단 Scene 목록: 01 정면 / 02 측면 / 03 후면.\n1~150프레임, 60fps, 2.5초. 제작 원본과 게임 에셋은 변경하지 않습니다.\n')
json.dump(validation,open(ROOT+'/blender_comparison_validation.json','w'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=OUT);print('COMPARISON_SAVED',OUT,validation)
