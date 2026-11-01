import bpy,json,math,os
from mathutils import Matrix,Vector,Quaternion
ROOT='C:/Users/iam12/Train Survival/output/raccoon-run-v8';os.makedirs(ROOT,exist_ok=True)
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
if bpy.context.screen.is_animation_playing:bpy.ops.screen.animation_play()
bpy.context.window.scene=bpy.data.scenes['Scene'];scene=bpy.context.scene
src=bpy.data.objects['raccoon_RunAuthor_v7'];sr=bpy.data.objects['RIG_Raccoon_RunAuthor_v7']
col=bpy.data.collections.new('Raccoon_HybridAuthor_v8');scene.collection.children.link(col)
rig=sr.copy();rig.data=sr.data.copy();rig.name='RIG_Raccoon_HybridAuthor_v8';rig.animation_data_clear();col.objects.link(rig)
mesh=src.copy();mesh.data=src.data.copy();mesh.name='raccoon_HybridAuthor_v8';col.objects.link(mesh)
mesh.animation_data_clear();mesh.data.shape_keys.animation_data_clear();mesh.parent=rig;mesh.matrix_parent_inverse=Matrix.Identity(4);mesh.matrix_basis=Matrix.Identity(4)
for mod in mesh.modifiers:
 if mod.type=='ARMATURE':mod.object=rig
for p in rig.pose.bones:
 for c in list(p.constraints):p.constraints.remove(c)
 p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
ids={g.index:g.name for g in mesh.vertex_groups};tail_ids=[v.index for v in mesh.data.vertices if any(ids[g.group]=='SEL_Tail' and g.weight>.5 for g in v.groups)]
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
knots=[-.15,-.32,-.51,-.70,-.84]
deform=[g for g in mesh.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform]
for i in tail_ids:
 v=mesh.data.vertices[i].co
 for g in deform:g.remove([i])
 # A common length coordinate on both sides of each section avoids diagonal crushing.
 if v.z>=knots[0]:weights={'Tail01':1.0}
 elif v.z<=knots[-1]:weights={'Tail05':1.0}
 else:
  j=next(j for j in range(4) if knots[j]>=v.z>=knots[j+1]);t=smooth((knots[j]-v.z)/(knots[j]-knots[j+1]));weights={'Tail%02d'%(j+1):1-t,'Tail%02d'%(j+2):t}
 neck=1-smooth((-v.z-.08+.4*(v.y-.05))/.20)
 if neck>0:
  weights={k:w*(1-neck) for k,w in weights.items()};weights['Hips']=neck
 for name,w in weights.items():
  if w>1e-7:mesh.vertex_groups[name].add([i],w,'REPLACE')
# Preserve all geometry, UVs, and non-tail skinning; record the intended skinning edit.
json.dump({'tail_vertices':len(tail_ids),'weights':[{'index':i,'position':list(mesh.data.vertices[i].co),'weights':{ids[g.group]:g.weight for g in mesh.data.vertices[i].groups if ids[g.group] in rig.data.bones and rig.data.bones[ids[g.group]].use_deform}} for i in tail_ids]},open(ROOT+'/tail_weights.json','w'),indent=2)
old=json.load(open('C:/Users/iam12/Train Survival/output/raccoon-run-comparison/existing.json'))
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)));Ci=C.inverted();ground=.9504841566085815
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def oldmat(b):
 bone=rig.data.bones[b['name']];m=(C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@Ci@bone.matrix_local.to_3x3()).to_4x4();m.translation=C@vec(b['position'])-Vector((0,0,ground));return m
lower={'Root','Hips','LeftUpperLeg','LeftLowerLeg','LeftFoot','LeftToes','RightUpperLeg','RightLowerLeg','RightFoot','RightToes'}
ordered=sorted(rig.pose.bones,key=lambda p:len(p.parent_recursive));previous={};leg_errors=[]
first_lower={b['name']:oldmat(b) for b in old['frames'][0]['bones'] if b['name'] in lower}
for frame in range(1,77):
 phase=(frame-1)/75;native=1+phase*30;scene.frame_set(int(native),subframe=native%1);bpy.context.view_layer.update()
 upper={p.name:p.matrix.copy() for p in sr.pose.bones};record=old['frames'][(frame-1)%75];oldbones={b['name']:oldmat(b) for b in record['bones'] if b['name'] in rig.data.bones};desired={}
 for p in ordered:
  if p.name.startswith('CTRL_'):continue
  if p.name in lower:
   if frame<=69:desired[p.name]=oldbones[p.name].copy()
   else:
    blend=smooth((frame-69)/7)
    a=oldbones[p.name] if not p.parent else oldbones[p.parent.name].inverted()@oldbones[p.name]
    b=first_lower[p.name] if not p.parent else first_lower[p.parent.name].inverted()@first_lower[p.name]
    loc=a.translation.lerp(b.translation,blend);rot=a.to_quaternion().slerp(b.to_quaternion(),blend)
    local=rot.to_matrix().to_4x4();local.translation=loc
    desired[p.name]=local if not p.parent else desired[p.parent.name]@local
   continue
  local=p.parent.bone.matrix_local.inverted()@p.bone.matrix_local if p.parent else p.bone.matrix_local
  parent=desired[p.parent.name] if p.parent else Matrix.Identity(4)
  if p.name.startswith('Tail'):
   i=int(p.name[-2:])-1;inv=p.bone.matrix_local.to_3x3().inverted()
   lift=(18 if i==0 else 2)+math.sin(4*math.pi*phase-i*.65)*(2 if i==0 else 1.5)
   sway=math.sin(2*math.pi*phase-i*.65)*(3+min(i,2)*.8)
   q=Quaternion(inv@Vector((0,0,1)),math.radians(sway))@Quaternion(inv@Vector((1,0,0)),math.radians(lift))
   desired[p.name]=parent@local@q.to_matrix().to_4x4()
  else:
   m=upper[p.name].to_3x3().to_4x4();m.translation=parent@local.translation;desired[p.name]=m
 for p in ordered:
  if p.name not in desired:continue
  local=p.parent.bone.matrix_local.inverted()@p.bone.matrix_local if p.parent else p.bone.matrix_local
  parent=desired[p.parent.name] if p.parent and p.parent.name in desired else Matrix.Identity(4)
  basis=local.inverted()@parent.inverted()@desired[p.name];loc,q,scale=basis.decompose()
  if p.name in previous:q.make_compatible(previous[p.name])
  previous[p.name]=q.copy();p.location=loc;p.rotation_quaternion=q;p.scale=scale
  for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=frame,group=p.name)
 bpy.context.view_layer.update();leg_errors.extend((rig.pose.bones[n].head-desired[n].translation).length for n in lower)
rig.animation_data.action.name='Raccoon_ShotgunRun_HYBRID_v8'
for layer in rig.animation_data.action.layers:
 for strip in layer.strips:
  for bag in strip.channelbags:
   for curve in bag.fcurves:
    for k in curve.keyframe_points:k.interpolation='LINEAR'
for o in scene.objects:o.hide_set(o not in [rig,mesh])
rig.hide_set(True);scene.render.fps=60;scene.frame_start=1;scene.frame_end=75;scene.frame_set(1)
for marker in list(scene.timeline_markers):scene.timeline_markers.remove(marker)
for f,name in [(1,'기존 하체 + 새 상체'),(20,'1/4'),(39,'1/2'),(57,'3/4')]:scene.timeline_markers.new(name,frame=f)
json.dump({'provenance':'Existing game run hips and legs + v7 newly authored upper body, retimed to the existing 1.25-second cycle. Tail weights corrected; tail preview reauthored.','max_lower_body_position_error_m':max(leg_errors),'frames':76,'fps':60,'duration':1.25},open(ROOT+'/authoring_report.json','w'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Raccoon_HybridAuthor_v8.blend');print('HYBRID',max(leg_errors),len(tail_ids))
