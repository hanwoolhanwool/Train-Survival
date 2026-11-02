import bpy,json,pathlib
from mathutils import Vector,Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival')
src=bpy.data.scenes['47 v21 발 접지 · 이동']
scene=bpy.data.scenes.get('50 v21 발 접지 확대') or bpy.data.scenes.new('50 v21 발 접지 확대')
scene.world=src.world;scene.render.resolution_x=1280;scene.render.resolution_y=960
scene.render.fps=30;scene.frame_start=1;scene.frame_end=480;scene.sync_mode='FRAME_DROP'
for ob in src.objects:
    if ob.type!='CAMERA' and ob.name not in scene.objects:scene.collection.objects.link(ob)
cam=bpy.data.objects.new('v21 발 접지 확대 카메라',bpy.data.cameras.new('v21 feet'))
scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.65
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
data=json.loads((project/'output/raccoon-footwork-v21/after/transitions/motion_exchange.json').read_text())
for i,f in enumerate(data['frames'][::2],1):
    p=f['root'];target=C@Vector((p['x'],p['y'],p['z']))+Vector((0,0,.45))
    cam.location=target+Vector((2,-3,1.2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.keyframe_insert('location',frame=i);cam.keyframe_insert('rotation_euler',frame=i)
for source in [scene,src]:
    source.timeline_markers.clear()
    for name,frame in [('정지',1),('걷기 시작',61),('달리기 전환',151),('걷기로 감속',271),('정지 전환',361)]:source.timeline_markers.new(name,frame=frame)
    source['검토 안내']='Space 재생: 실제 게임 이동 기록. 디딘 발은 지지면에 고정하고 이동 거리로 다음 착지점을 정합니다. 45 수정 전 / 47 수정 후 전신 / 50 발 확대. 손·무기 설정은 v20 유지.'
note=bpy.data.texts.get('v21 이동 검수 안내') or bpy.data.texts.new('v21 이동 검수 안내');note.clear()
note.write('45 v20 수정 전 발 / 46 v21 정지 / 47 v21 이동 전신 / 48 회전 / 49 경사·계단 / 50 발 확대. 각 동작은 실제 게임 실행을 기록한 16초, 30fps. 기존 클립은 골반·상체와 무릎 방향의 기준이며 발 궤적은 실제 이동 거리와 지면을 기준으로 보정합니다. 손·팔의 파지 프로필은 v20과 같습니다. 이동 열차 및 네트워크 실전 검수는 별도입니다.')
bpy.context.window.scene=src;src.frame_set(100)
for window in bpy.context.window_manager.windows:
    window.scene=src
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            v=area.spaces.active;v.camera=src.camera;v.use_local_camera=False
            v.shading.type='MATERIAL';v.overlay.show_overlays=False
            v.region_3d.view_perspective='CAMERA';v.region_3d.view_camera_zoom=4;v.region_3d.view_camera_offset=(0,0)
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_Footwork_v21.blend',compress=True)
print('Final footwork review ready')
