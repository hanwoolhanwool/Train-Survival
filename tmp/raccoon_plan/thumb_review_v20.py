import bpy, json, pathlib
from mathutils import Matrix, Vector, Quaternion

project = pathlib.Path('C:/Users/iam12/Train Survival')
out = project / 'output/raccoon-thumb-v20'
C = Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
Ci = C.inverted()
ground = .9504841566085815
names = ['LeftThumbProximal','LeftThumbIntermediate','LeftThumbDistal']
def vec(v): return Vector((v['x'],v['y'],v['z']))
def quat(v): return Quaternion((v['w'],v['x'],v['y'],v['z']))

datasets = {}
validation = {}
for test in ['idle','transitions','turn_stop','terrain']:
    old = json.loads((project / 'output/raccoon-grip-v19/after' / test / 'motion_exchange.json').read_text())
    new = json.loads((out / 'after' / test / 'motion_exchange.json').read_text())
    assert len(old['frames']) == len(new['frames'])
    errors = []
    for a,b in zip(old['frames'],new['frames']):
        for x,y in zip(a['bones'],b['bones']):
            assert x['name'] == y['name']
            if x['name'] not in names:
                errors.append((vec(x['position'])-vec(y['position'])).length)
                errors.append(1-abs(quat(x['rotation']).dot(quat(y['rotation']))))
        assert a['weapons'] == b['weapons'], 'Weapon changed outside thumb scope'
        assert a['shapes'] == b['shapes'], 'Shape animation changed outside thumb scope'
    assert max(errors) < .00001, (test,max(errors))
    validation[test] = {'max_non_thumb_difference':max(errors)}
    datasets[test] = new

for number,test in [(38,'idle'),(39,'transitions'),(40,'turn_stop'),(41,'terrain')]:
    scene = next(s for s in bpy.data.scenes if s.name.startswith(str(number)+' '))
    bpy.context.window.scene = scene
    rig = bpy.data.objects['RIG_v19_'+test]
    previous = {}
    for i,frame in enumerate(datasets[test]['frames'][::2],1):
        scene.frame_set(i)
        records = {b['name']:b for b in frame['bones']}
        desired = {}
        for name in ['LeftHand']+names:
            b = records[name];bone = rig.data.bones[name]
            delta = C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@Ci
            mat = (delta@bone.matrix_local.to_3x3()).to_4x4()
            mat.translation = C@vec(b['position'])-Vector((0,0,ground))
            desired[name] = mat
        for name in names:
            p = rig.pose.bones[name]
            basis = (p.parent.bone.matrix_local.inverted()@p.bone.matrix_local).inverted()@desired[p.parent.name].inverted()@desired[name]
            loc,rot,scale = basis.decompose()
            if name in previous: rot.make_compatible(previous[name])
            previous[name] = rot.copy()
            p.location=loc;p.rotation_quaternion=rot;p.scale=scale
            for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=i,group=name)
    scene.name = scene.name.replace('v19','v20')
    scene.frame_set(1)
for number in [42,43]:
    scene = next(s for s in bpy.data.scenes if s.name.startswith(str(number)+' '))
    scene.name = scene.name.replace('v19','v20')
scene = next(s for s in bpy.data.scenes if s.name.startswith('43 '))
bpy.context.window.scene=scene;scene.frame_set(1)
scene['검토 안내']='v20 받침손 엄지만 수정. 앞손잡이 옆면을 따라 앞으로 놓고 끝마디 굽힘을 완화했습니다. 팔, 손바닥, 나머지 손가락과 무기 위치는 v19 유지.'
note = bpy.data.texts.get('v20 엄지 수정 안내') or bpy.data.texts.new('v20 엄지 수정 안내')
note.clear();note.write('43 엄지 측면 확대 / 42 아래쪽 / 38 정지 / 39 이동 / 40 회전 / 41 지형. 실제 게임 기록에서 왼손 엄지 관절 3개만 갱신했습니다. 44는 제작용 고정 자세입니다.')
for window in bpy.context.window_manager.windows:
    window.scene=scene
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            v=area.spaces.active;v.camera=scene.camera;v.use_local_camera=False
            v.region_3d.view_perspective='CAMERA';v.region_3d.view_camera_zoom=4;v.region_3d.view_camera_offset=(0,0)
(out/'scope_validation.json').write_text(json.dumps(validation,indent=2))
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_Thumb_v20.blend',compress=True)
print(validation)
