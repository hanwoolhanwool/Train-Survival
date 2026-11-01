import pathlib,bpy
project=pathlib.Path('C:/Users/iam12/Train Survival')
code=(project/'tmp/raccoon_plan/compare_tail_v10.py').read_text(encoding='utf-8').replace('v10','v11').replace('V10','V11')
code=code.replace("project/'output/raccoon-tail-v9/comparison/new.json'", "project/'output/raccoon-run-comparison/new.json'")
code=code.replace("bpy.data.objects['Raccoon_TailBoundary_v9'] if key=='existing'", "bpy.data.objects['Raccoon_TailBaseline_v7'] if key=='existing'")
code=code.replace("objects.append(extra)","""objects.append(extra)
        opening=bpy.data.objects['Raccoon_TailOpening_v11'].copy();opening.name='COMPARE_v11_new_Opening';base.objects.link(opening)
        opening.parent=rig;opening.matrix_parent_inverse=Matrix.Identity(4);opening.matrix_basis=Matrix.Identity(4)
        for mod in opening.modifiers:
            if mod.type=='ARMATURE':mod.object=rig
        objects.append(opening)""")
code=code.replace("exec(compile(code,str(project/'tmp/raccoon_plan/compare_tail_v11.generated.py'),'exec'))", """code=code.replace('TailRebuild_v11','TailRoot_v11').replace('이전 꼬리 v9','꼬리 보정 전 v7').replace('꼬리 형태 재제작 v11','가는 뿌리 v11').replace('상체·하체 동일 / 꼬리만 비교','왼쪽: 보정 전 보관본 / 오른쪽: 현재 수정본')
code=code.replace('양쪽 모두 승인된 혼합 달리기 v8과 동일한 꼬리 움직임입니다. 오른쪽은 꼬리 표면과 연결부만 다시 만들었습니다.','왼쪽은 꼬리 달리기 보정 전 v7의 원래 모션입니다. 오른쪽은 현재 혼합 달리기와 가는 꼬리 뿌리 v11입니다. 각 버전의 원래 동작을 유지합니다.')
exec(compile(code,str(project/'tmp/raccoon_plan/compare_tail_v11.generated.py'),'exec'))""")
code=code.replace('이전 v9와 나란히 비교','꼬리 보정 전 v7과 나란히 비교')
exec(compile(code,'compare_tail_v11.wrapper.py','exec'))
for s in scenes:
 s['비교 안내']='왼쪽: 최초 달리기 꼬리 보정 전 v7 보관본. 오른쪽: 가는 뿌리 v11과 현재 혼합 달리기. 원래 모션을 각각 유지. Space 재생/정지.'
bpy.context.window.scene=scenes[1];scenes[1].frame_set(20)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_camera_zoom=15
bpy.ops.wm.save_as_mainfile(filepath=OUT)
