import bpy, pathlib, shutil
project=pathlib.Path('C:/Users/iam12/Train Survival')
comparison=project/'output/raccoon-tail-v10/comparison'
shutil.copyfile(project/'output/raccoon-tail-v9/comparison/new.json',comparison/'existing.json')
code=(project/'tmp/raccoon_plan/compare_tail_v9.py').read_text(encoding='utf-8')
code=code.replace('raccoon-tail-v9','raccoon-tail-v10').replace('TailReview_v9','TailRebuild_v10').replace('COMPARE_v9_','COMPARE_v10_').replace('v9 꼬리','v10 꼬리')
code=code.replace("source=bpy.data.objects['raccoon_HybridAuthor_v8'] if key=='existing' else bpy.data.objects['Raccoon_TailBoundary_v9']", "source=bpy.data.objects['Raccoon_TailBoundary_v9'] if key=='existing' else bpy.data.objects['Raccoon_RoundTail_Body_v10']")
code=code.replace("weapons={};objects=[root,rig,mesh]", """weapons={};objects=[root,rig,mesh]
    if key=='new':
        extra=bpy.data.objects['Raccoon_RoundTail_v10'].copy();extra.name='COMPARE_v10_new_Tail';base.objects.link(extra)
        extra.parent=rig;extra.matrix_parent_inverse=Matrix.Identity(4);extra.matrix_basis=Matrix.Identity(4)
        for mod in extra.modifiers:
            if mod.type=='ARMATURE':mod.object=rig
        objects.append(extra)""")
code=code.replace('이전 꼬리 v8','이전 꼬리 v9').replace('꼬리 연결·움직임 수정','꼬리 형태 재제작 v10')
code=code.replace('기존 쪽은 기존 상체 레이어 및 고개 보정, 신규 쪽은 신규 달리기용 설정을 적용한 실제 게임 결과입니다.', '양쪽 모두 승인된 혼합 달리기 v8과 동일한 꼬리 움직임입니다. 오른쪽은 꼬리 표면과 연결부만 다시 만들었습니다.')
code=code.replace('오른쪽 혼합 달리기 v8','오른쪽 꼬리 재제작 v10')
exec(compile(code,str(project/'tmp/raccoon_plan/compare_tail_v10.generated.py'),'exec'))
# Keep a dedicated root close-up, driven by the same captured game animation.
side=scenes[1]
detail=bpy.data.scenes.new('04 v10 꼬리 연결부 확대');detail.render.fps=60;detail.frame_start=1;detail.frame_end=150;detail.world=side.world
for child in side.collection.children:detail.collection.children.link(child)
detail.render.resolution_x=1100;detail.render.resolution_y=900;detail.render.resolution_percentage=100
cam=bpy.data.objects.new('v10 확대 카메라',bpy.data.cameras.new('v10 확대 카메라'));detail.collection.objects.link(cam);detail.camera=cam
bpy.context.window.scene=detail;detail.frame_set(20)
rig=bpy.data.objects['COMPARE_v10_new_Rig_측면'];target=rig.matrix_world@rig.pose.bones['Tail01'].head
target+=Vector((-.10,0,-.015));cam.location=target+Vector((-1.2,-2,.65));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.15
for ob in detail.objects:
    if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.region_3d.view_camera_zoom=10
detail['비교 안내']='꼬리 v10 확대. 1~150프레임 60fps. Space 재생/정지. 01~03 v10 씬에서 이전 v9와 나란히 비교.'
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('V10_CLOSEUP_READY',OUT)
