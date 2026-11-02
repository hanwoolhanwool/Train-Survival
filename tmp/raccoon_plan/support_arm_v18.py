import bpy,math,pathlib,json
from mathutils import Vector,Matrix,Quaternion
root=pathlib.Path('C:/Users/iam12/Train Survival');out=root/'output/raccoon-support-v18';out.mkdir(exist_ok=True)
source=bpy.data.scenes['24 v17 받침팔 · 정지'];bpy.context.window.scene=source;source.frame_set(1)
scene=bpy.data.scenes.new('29 v18 받침팔 형태 검토');scene.world=source.world
copies={}
for ob in source.objects:
 if ob.type not in {'ARMATURE','MESH','EMPTY'} or (ob.type=='MESH' and not ob.name.startswith('v17_idle_')):continue
 cp=ob.copy();cp.name='v18_'+ob.name
 if ob.data:cp.data=ob.data.copy()
 cp.animation_data_clear();scene.collection.objects.link(cp);copies[ob]=cp
 if cp.type=='MESH' and cp.data.shape_keys:cp.data.shape_keys.animation_data_clear()
for ob,cp in copies.items():
 cp.parent=copies.get(ob.parent);cp.matrix_world=ob.matrix_world.copy()
 for mod in cp.modifiers:
  if mod.type=='ARMATURE':mod.object=copies.get(mod.object,mod.object)
rig=copies[bpy.data.objects['RIG_v17_idle']];rig.name='RIG_SupportArm_v18'
body=copies[bpy.data.objects['v17_idle_Raccoon_WristBody_v16']];body.name='Raccoon_ArmBody_v18'
mesh=body.data;basis=mesh.shape_keys.key_blocks[0];original=[v.co.copy() for v in basis.data]
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
from collections import defaultdict
rings=defaultdict(list)
for v in mesh.vertices:
 k=mesh.attributes['elbow_ring'].data[v.index].value
 if v.co.x>.4 and k>0:rings[k].append(v.index)
scales={1:.97,2:.93,3:.88,4:.84,5:.84,6:.87,7:.92,8:.98,9:1.04,10:1.04}
for k,ids in rings.items():
 center=sum((original[i] for i in ids),Vector())/len(ids)
 for i in ids:
  p=center+(original[i]-center)*scales[k];delta=p-original[i]
  for key in mesh.shape_keys.key_blocks:key.data[i].co+=delta
  mesh.vertices[i].co=p
  mesh.shape_keys.key_blocks['COR_Elbow90.L'].data[i].co=p
deform={g.index:g.name for g in body.vertex_groups if g.name in rig.data.bones}
a=rig.data.bones['LeftLowerArm'].head_local;h=rig.data.bones['LeftHand'].head_local;axis=h-a
changed=[]
for v in mesh.vertices:
 influences={deform[g.group]:g.weight for g in v.groups if g.group in deform}
 allowed={'LeftUpperArm','LeftLowerArm','LeftHand','LeftWristBlend1','LeftWristBlend2','LeftWristBlend3'}
 if not influences or sum(w for n,w in influences.items() if n not in allowed)>.001 or sum(influences.values())<.95:continue
 if v.co.x<.44:continue
 t=mesh.attributes['elbow_t'].data[v.index].value
 u=(t-.39)/.61 if t>=0 else (v.co-a).dot(axis)/axis.length_squared
 u=max(0,min(1,u));upper=influences.get('LeftUpperArm',0)
 value=u*4;lo=min(3,int(value));q=value-lo
 names=['LeftLowerArm','LeftWristBlend1','LeftWristBlend2','LeftWristBlend3','LeftHand']
 weights={names[lo]:(1-upper)*(1-q),names[lo+1]:(1-upper)*q,'LeftUpperArm':upper}
 for g in list(v.groups):
  if g.group in deform:body.vertex_groups[g.group].remove([v.index])
 for n,w in weights.items():
  if w>1e-6:body.vertex_groups[n].add([v.index],w,'REPLACE')
 changed.append(v.index)
mesh.update();body['v18_source_positions']=json.dumps([list(p) for p in original]);body['v18_changed_indices']=json.dumps(sorted(set(changed)|{i for ids in rings.values() for i in ids}))
bpy.context.window.scene=scene;scene.frame_set(1);bpy.context.view_layer.update()
def apply_blends():
 arm=rig.pose.bones['LeftLowerArm'];hand=rig.pose.bones['LeftHand']
 qa=arm.matrix.to_quaternion()@arm.bone.matrix_local.to_quaternion().inverted();qh=hand.matrix.to_quaternion()@hand.bone.matrix_local.to_quaternion().inverted()
 rel=qh@qa.inverted();direction=(hand.head-arm.head).normalized();xyz=Vector((rel.x,rel.y,rel.z));tw=direction*xyz.dot(direction);twist=Quaternion((rel.w,*tw)).normalized();swing=rel@twist.inverted()
 for i in range(1,4):
  u=i/4;rotation=Quaternion().slerp(swing,smooth((u-.5)/.5))@Quaternion().slerp(twist,u)@qa
  center=arm.head.lerp(hand.head,u);anchor=a.lerp(h,u)
  position=center+rotation@(h-anchor)
  p=rig.pose.bones['LeftWristBlend'+str(i)];mat=rotation.to_matrix().to_4x4()@p.bone.matrix_local;mat.translation=position;p.matrix=mat
 bpy.context.view_layer.update()
apply_blends()
camera=bpy.data.objects.new('v18 받침팔 확대',bpy.data.cameras.new('v18 camera'));scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=.95
target=Vector((.15,-.39,1.10));camera.location=target+Vector((1.4,-1.6,1.0));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1000;scene.render.resolution_y=900;scene.frame_start=scene.frame_end=1
for ob in scene.objects:
 if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_SupportArm_v18.blend',compress=True)
print('v18 prototype, modified vertices',len(changed))
