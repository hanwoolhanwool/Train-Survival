import bpy,math,json,pathlib
from mathutils import Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival')
if bpy.context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
source=bpy.data.scenes['01 v11 꼬리 비교 · 정면']
created=[];report={}
for number,title,angle in [(0,'후측면',135),(6,'정면',0),(7,'측면',90),(8,'후면',180)]:
    scene=bpy.data.scenes.new('%02d v11 기본 에셋 · %s'%(number,title));created.append(scene)
    scene.world=source.world;scene.render.fps=60;scene.frame_start=1;scene.frame_end=1
    scene.render.resolution_x=1600;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
    copies={}
    for original in source.objects:
        if 'Weapon_' in original.name:continue
        ob=original.copy();ob.name='REST_v11_'+title+'_'+original.name
        if original.data:ob.data=original.data.copy()
        scene.collection.objects.link(ob);copies[original]=ob;ob.animation_data_clear()
        for constraint in list(ob.constraints):ob.constraints.remove(constraint)
        if ob.type=='ARMATURE':
            ob.data.pose_position='REST';ob.data.animation_data_clear()
            for bone in ob.pose.bones:
                for constraint in list(bone.constraints):bone.constraints.remove(constraint)
                bone.matrix_basis=Matrix.Identity(4)
        if ob.type=='MESH' and ob.data.shape_keys:
            ob.data.shape_keys.animation_data_clear()
            for key in ob.data.shape_keys.key_blocks:key.value=0
        if ob.type=='EMPTY' and original.parent is None:ob.rotation_euler.z=math.radians(angle)
        if ob.type=='FONT':
            if '비교 조건' in original.name:ob.data.body='기본 에셋 / 리그 기본 자세 / 애니메이션·무기 없음'
            elif '조작 안내' in original.name:ob.data.body='Scene 00·06·07·08: 기본 형태   |   Scene 01~03: 달리기 비교'
    for original,ob in copies.items():
        if original.parent in copies:ob.parent=copies[original.parent]
        for mod in ob.modifiers:
            if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
    scene.camera=copies[source.camera]
    scene['비교 안내']='왼쪽 꼬리 보정 전 v7 / 오른쪽 현재 v11. 양쪽 모두 실제 리그 기본 자세, 모든 애니메이션과 보정 쉐이프 비활성. 달리기 비교는 01~03 v11 씬.'
    bpy.context.window.scene=scene;scene.frame_set(1)
    for ob in scene.objects:ob.hide_set(ob.type in {'ARMATURE','EMPTY'})
    animated=[ob.name for ob in scene.objects if ob.animation_data or (ob.type=='MESH' and ob.data.shape_keys and ob.data.shape_keys.animation_data)]
    report[scene.name]={'animated_objects':animated,'rest_rigs':sum(ob.type=='ARMATURE' and ob.data.pose_position=='REST' for ob in scene.objects)}
bpy.context.window.scene=created[0]
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        space=area.spaces.active;space.shading.type='MATERIAL';space.shading.use_scene_world=False;space.shading.use_scene_lights=False;space.overlay.show_overlays=False
        space.region_3d.view_perspective='CAMERA';space.region_3d.view_camera_zoom=15;space.region_3d.view_camera_offset=(0,0)
note=bpy.data.texts.new('v11 기본 에셋 비교 안내');note.write('비교 기준은 꼬리 보정 전 v7입니다. 최초 텍스처/리깅 이전 모델로 바꾸지 않았습니다.\n00/06/07/08 v11 기본 에셋: 애니메이션이 없는 리그 기본 자세의 후측면/정면/측면/후면 비교.\n01~03 v11 꼬리 비교: 기존 달리기 비교 유지.\n04 v11 꼬리 연결부 확대: 달리기 중 연결부 검토.\n')
(project/'output/raccoon-tail-v11/rest_asset_review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_TailRoot_v11.blend')
print(report)
