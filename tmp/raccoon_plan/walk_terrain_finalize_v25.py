import bpy,json,pathlib
from mathutils import Vector
project=pathlib.Path('C:/Users/iam12/Train Survival')
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True);font.pack()
textmat=bpy.data.materials.new('v25 안내 글자');textmat.use_nodes=True
bs=next(n for n in textmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.94,.97,1,1);bs.inputs['Emission Color'].default_value=(.94,.97,1,1);bs.inputs['Emission Strength'].default_value=.8
panelmat=bpy.data.materials.new('v25 카메라 안내 배경');panelmat.use_nodes=True
bs=next(n for n in panelmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.018,.026,.038,1);bs.inputs['Roughness'].default_value=1
translations={'Idle':'정지','Start':'출발','Straight':'직진','Turn right 90':'우회전 90도','Turn left 180':'좌측으로 180도 전환','Decelerate':'감속','Stop':'정지','Flat':'평지','Uphill 12':'오르막 12도','Landing':'상단 평지','Downhill 12':'내리막 12도','Flat after slope':'평지 복귀','Cross slope idle':'옆경사 정지','Cross slope walk 10':'옆경사 10도 걷기','Cross slope decelerate':'옆경사 감속','Cross slope stop':'옆경사 정지','Slope turn right':'경사 위 우회전','Slope turn left':'경사 위 좌회전','Slope return forward':'경사 위 직진 복귀'}
for number,title in [(61,'방향 전환 · 좌우 회전과 180도 전환'),(62,'12도 오르막 · 내리막'),(63,'10도 옆경사 · 양발 접지 확인'),(65,'10도 경사 위 방향 전환')]:
 s=next(s for s in bpy.data.scenes if s.name.startswith(str(number)+' v25 '));cam=s.camera;cam.data.ortho_scale=3.7
 for marker in s.timeline_markers:marker.name=translations.get(marker.name,marker.name)
 s.timeline_markers.new('기록 끝 · 반복 연결용 아님',frame=s.frame_end)
 for txt,y,size in [(title,1.23,.085),('별도 v25 검토 · 최고 0.8m/s · 기존 동작 보존',1.11,.047),('Space 재생·정지  |  20초 경로 기록',-1.21,.05)]:
  curve=bpy.data.curves.new(txt,'FONT');curve.body=txt;curve.font=font;curve.size=size;curve.align_x='CENTER';curve.align_y='CENTER';curve.materials.append(textmat)
  ob=bpy.data.objects.new(txt,curve);s.collection.objects.link(ob);ob.parent=cam;ob.location=(0,y,-3);ob.rotation_euler=(0,0,0)
 for y,h in [(1.18,.3),(-1.21,.18)]:
  me=bpy.data.meshes.new('v25 안내 패널');me.from_pydata([(-1.77,-h/2,0),(1.77,-h/2,0),(1.77,h/2,0),(-1.77,h/2,0)],[],[(0,1,2,3)]);me.materials.append(panelmat);ob=bpy.data.objects.new('v25 안내 패널',me);s.collection.objects.link(ob);ob.parent=cam;ob.location=(0,y,-3.01)
 s['검토 범위']='20초 경로 기록. 게임 프리팹에 미적용. 기존 파일과 v24 보존. 실제 지형 충돌과 변형 발바닥 표본 검사. 반복 연결용 애니메이션이나 움직이는 열차 검수는 아님.'
 s.frame_set(1)
# Project the checker onto the horizontal plane to avoid bands caused by height changes.
for mat in bpy.data.materials:
 if not mat.name.startswith('v25 지면') or not mat.use_nodes:continue
 nodes=mat.node_tree.nodes;geo=next((n for n in nodes if n.type=='NEW_GEOMETRY'),None);checker=next((n for n in nodes if n.type=='TEX_CHECKER'),None)
 if geo and checker:
  xy=nodes.new('ShaderNodeVectorMath');xy.operation='MULTIPLY';xy.inputs[1].default_value=(1,1,0);mat.node_tree.links.new(geo.outputs['Position'],xy.inputs[0]);mat.node_tree.links.new(xy.outputs[0],checker.inputs['Vector'])
main=bpy.data.scenes['60 방향 전환 · v24와 v25 비교']
for ob in main.objects:
 if ob.type=='FONT' and ob.data.body.startswith('61 회전'):
  ob.data.body='61 회전  /  62 오르막·내리막  /  63 옆경사  /  65 경사 위 회전';ob.data.size=.08
main['비교 안내']+=' 65는 10도 옆경사에서 좌우 방향을 바꾸는 추가 검토다.'
note=next(t for t in bpy.data.texts if t.name.startswith('v25') and '안내' in t.name);note.write('\n'+main['비교 안내'])
path='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_TurnSlope_v25.blend'
export={main,note}|{s for s in bpy.data.scenes if s.name.startswith(('61 v25 ','62 v25 ','63 v25 ','65 v25 '))}
bpy.data.libraries.write(path,export,path_remap='ABSOLUTE',fake_user=True,compress=True)
print(path)
