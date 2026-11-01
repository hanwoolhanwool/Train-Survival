import bpy, math, json, numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.kdtree import KDTree

OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon']; me=ob.data
assert not any(m.type=='ARMATURE' for m in ob.modifiers), 'Rig already exists'
src=bpy.data.objects['raccoon_SOURCE_before_stage12']; ref=bpy.data.objects['Mesh_0.001']; ref_rig=bpy.data.objects['target_character']
a=np.array([v.co[:] for v in src.data.vertices]); b=np.array([v.co[:] for v in ref.data.vertices])
fit=np.linalg.lstsq(np.c_[a,np.ones(len(a))],b,rcond=None)[0]
inv=np.linalg.inv(fit[:3]); offset=fit[3]
def unfit(p): return Vector((np.array(p)-offset)@inv)
def smooth(t):
    t=max(0,min(1,t)); return t*t*(3-2*t)
def blend(a,b,t):
    out={k:v*(1-t) for k,v in a.items()}
    for k,v in b.items(): out[k]=out.get(k,0)+v*t
    return out

col=bpy.data.collections.new('Raccoon_Production_Rig'); bpy.context.scene.collection.children.link(col)
rig=bpy.data.objects.new('RIG_Raccoon',bpy.data.armatures.new('Raccoon_Humanoid_Skeleton')); col.objects.link(rig)
rig.matrix_world=ob.matrix_world.copy(); rig.show_in_front=True
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
mapping={}
for bone in ref_rig.data.bones:
    n=bone.name
    if n.startswith('mixamorig:') or n=='headfront': continue
    mapping[n]=n
mapping.update({'Bone_035':'Ear01.L','Bone_034':'Ear02.L','Bone_033':'Ear02.L','Bone_038':'Ear01.R','Bone_037':'Ear02.R','Bone_036':'Ear02.R','Bone_040':'Scarf01','Bone_039':'Scarf02'})
mapping.update({f'Bone_{n:03d}':f'Tail{i+1:02d}' for i,n in enumerate([19,18,17,16,15])})
def bone(name,head,tail,parent=None,deform=True,axis=None):
    eb=rig.data.edit_bones.new(name);eb.head=head;eb.tail=tail;eb.use_deform=deform
    if parent:eb.parent=rig.data.edit_bones[parent]
    if axis is not None:eb.align_roll(Vector(axis).cross((eb.tail-eb.head).normalized()))
    return eb
floor=min(v.co.z for v in me.vertices)
bone('Root',(0,0,floor),(0,0,floor+.15),deform=False)
for r in ref_rig.data.bones:
    if r.name not in mapping: continue
    n=mapping[r.name]
    if n in rig.data.edit_bones:continue
    parent=mapping.get(r.parent.name,'Root') if r.parent else 'Root'
    if n=='Scarf01':parent='UpperChest'
    eb=bone(n,unfit(r.head_local),unfit(r.tail_local),parent)
    eb.align_roll(r.matrix_local.to_3x3()@Vector((0,0,1)))
for side in ['L','R']:
    n='Ear02.'+side;old='Bone_033' if side=='L' else 'Bone_036'
    rig.data.edit_bones[n].tail=unfit(ref_rig.data.bones[old].tail_local)
layouts=json.loads(ob['elbow_layout_json']); fingers=json.loads(ob['finger_layout_json'])
finger_centers={}
for rec in layouts:
    side='Left' if rec['side']==1 else 'Right'; E=Vector(rec['elbow']);W=Vector(rec['wrist'])
    up=rig.data.edit_bones[side+'UpperArm'];lo=rig.data.edit_bones[side+'LowerArm'];hand=rig.data.edit_bones[side+'Hand']
    up.tail=E;lo.head=E;lo.tail=W
    axis=(W-E).normalized().cross(Vector((0,-1,0))).normalized()
    lo.align_roll(axis.cross((W-E).normalized()))
    hand.head=W;mid=next(f for f in fingers if f['side']==rec['side'] and f['name']=='middle');hand.tail=Vector(mid['base'])
    for f in [f for f in fingers if f['side']==rec['side']]:
        B=Vector(f['base']);T=Vector(f['tip']);sgn=rec['side'];D=(W-Vector((sgn*.509,-.256,.035))).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*sgn
        ids=[v for v in me.vertices if me.attributes['anatomy_part'].data[v.index].value==f['part']]
        # The construction retains seven eight-vertex rings and one tip.
        centers=[sum((v.co for v in ids[i:i+8]),Vector())/8 for i in range(0,56,8)]
        points=[B,centers[1].lerp(centers[2],.5),centers[3].lerp(centers[4],.5),T]
        names=[side+f['name'].capitalize()+s for s in ['Proximal','Intermediate','Distal']]
        for i,n in enumerate(names):
            eb=rig.data.edit_bones[n];eb.head=points[i];eb.tail=points[i+1]
            ax=(eb.tail-eb.head).normalized().cross(N).normalized()
            eb.align_roll(ax.cross((eb.tail-eb.head).normalized()))
        finger_centers[f['part']]={'names':names,'points':points,'base':B,'tip':T,'hand':side+'Hand'}
# Five tail joints follow the actual volume, independently of the reference skin.
tailpath=[Vector(p) for p in [(0,-.045,-.175),(0,.15,-.255),(0,.31,-.425),(0,.43,-.625),(0,.49,-.79),(0,.48,-.895)]]
for i in range(5):
    eb=rig.data.edit_bones[f'Tail{i+1:02d}'];eb.head=tailpath[i];eb.tail=tailpath[i+1];eb.align_roll(Vector((1,0,0)))
# Independent IK controls, switched off by default to preserve the approved rest pose.
ik_specs=[]
for side in ['Left','Right']:
    tag='L' if side=='Left' else 'R'
    for limb,upper,lower,end in [('Arm','UpperArm','LowerArm','Hand'),('Leg','UpperLeg','LowerLeg','Foot')]:
        ub=rig.data.edit_bones[side+upper];lb=rig.data.edit_bones[side+lower];eb=rig.data.edit_bones[side+end]
        A=ub.head.copy();B=lb.head.copy();C=lb.tail.copy();axis=(C-A).normalized();bend=B-(A+axis*(B-A).dot(axis))
        if bend.length<.0001:bend=Vector((0,-1,0))
        pole=B+bend.normalized()*.45
        target='CTRL_'+end+'.'+tag;pole_name='CTRL_'+limb+'Pole.'+tag
        cb=bone(target,eb.head.copy(),eb.tail.copy(),'Root',False);cb.roll=eb.roll
        bone(pole_name,pole,pole+Vector((0,0,.08)),'Root',False)
        ik_specs.append((side+lower,side+end,target,pole_name))
bpy.ops.object.mode_set(mode='OBJECT')
rig.data.display_type='OCTAHEDRAL'
deform=rig.data.collections.new('Deform - game export');ctrl=rig.data.collections.new('Controls - IK targets');extra=rig.data.collections.new('Tail ears scarf')
for b in rig.data.bones:
    (deform if b.use_deform else ctrl).assign(b)
    if b.name.startswith(('Tail','Ear','Scarf')):extra.assign(b)
for n,end,target,pole in ik_specs:
    pb=rig.pose.bones[n];control=rig.pose.bones[target];control['IK']=0.0;control.id_properties_ui('IK').update(min=0,max=1,description='0 FK, 1 IK. Move target and pole in Pose Mode.')
    c=pb.constraints.new('IK');c.name='IK_'+n;c.target=rig;c.subtarget=target;c.pole_target=rig;c.pole_subtarget=pole;c.chain_count=2;c.use_stretch=False;c.influence=0
    # Find the pole offset which reproduces the original joint plane.
    eb=rig.data.bones[n];rest=eb.head_local.copy();best=(1e9,0)
    c.influence=1
    for angle in np.linspace(-math.pi,math.pi,73):
        c.pole_angle=float(angle);bpy.context.view_layer.update();error=(pb.head-rest).length
        if error<best[0]:best=(error,float(angle))
    center=best[1]
    for angle in np.linspace(center-.05,center+.05,101):
        c.pole_angle=float(angle);bpy.context.view_layer.update();error=(pb.head-rest).length
        if error<best[0]:best=(error,float(angle))
    c.pole_angle=best[1];c.influence=0
    fc=c.driver_add('influence');d=fc.driver;v=d.variables.new();v.name='ik';v.type='SINGLE_PROP';v.targets[0].id=rig;v.targets[0].data_path=f'pose.bones["{target}"]["IK"]';d.expression='ik'
    rot=rig.pose.bones[end].constraints.new('COPY_ROTATION');rot.name='IK end orientation';rot.target=rig;rot.subtarget=target
    fc=rot.driver_add('influence');d=fc.driver;v=d.variables.new();v.name='ik';v.type='SINGLE_PROP';v.targets[0].id=rig;v.targets[0].data_path=f'pose.bones["{target}"]["IK"]';d.expression='ik'
    control['rest_IK_joint_error_m']=best[0]
# Source coordinates match exactly; average coincident split vertices before transfer.
kd=KDTree(len(src.data.vertices))
for v in src.data.vertices:kd.insert(v.co,v.index)
kd.balance()
source_weights=[]
for v in ref.data.vertices:
    w={}
    for g in v.groups:
        name=mapping.get(ref.vertex_groups[g.group].name)
        if name:w[name]=w.get(name,0)+g.weight
    source_weights.append(w)
tail_ids={vi for p in me.polygons if me.attributes['edit_region'].data[p.index].value==3 for vi in p.vertices}
cuff_ids={vi for p in me.polygons if me.attributes['edit_region'].data[p.index].value==4 for vi in p.vertices}
groups={n:ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n) for n in [b.name for b in rig.data.bones if b.use_deform]}
weights=[];errors=[]
for v in me.vertices:
    co=v.co;_,nearest,dist=kd.find(co);near=kd.find_range(co,.00002) if dist<.00002 else [(None,nearest,dist)]
    w={}
    for _,idx,_ in near:
        for n,val in source_weights[idx].items():w[n]=w.get(n,0)+val/len(near)
    errors.append(dist)
    part=me.attributes['anatomy_part'].data[v.index].value;t=me.attributes['elbow_t'].data[v.index].value
    side='Left' if co.x>0 else 'Right';sgn=1 if co.x>0 else -1
    if v.index in tail_ids:
        best=(1e9,0,0)
        for i,(A,B) in enumerate(zip(tailpath,tailpath[1:])):
            q=max(0,min(1,(co-A).dot(B-A)/(B-A).length_squared));d=(co-A.lerp(B,q)).length
            if d<best[0]:best=(d,i,q)
        _,i,q=best;s=i+q
        # Smooth transitions are centred at each joint.
        if s<.5:w=blend({'Hips':1},{'Tail01':1},smooth((s+.15)/.65))
        else:
            k=min(3,int(s-.5));f=smooth(s-.5-k);w={f'Tail{k+1:02d}':1-f,f'Tail{k+2:02d}':f}
    else:
        # Tail cannot pull the trousers; unmodified torso retains reference weights.
        tw=sum(w.pop(n,0) for n in list(w) if n.startswith('Tail'));w['Hips']=w.get('Hips',0)+tw
    if part in [110,210]:
        lower=smooth((t-.04)/.70);w={side+'UpperArm':1-lower,side+'LowerArm':lower}
        wrist=smooth((t-.86)/.25)*.5
        w=blend(w,{side+'Hand':1},wrist)
    elif part in [100,200]:
        rec=next(r for r in layouts if r['side']==sgn);W=Vector(rec['wrist']);D=(W-Vector((sgn*.509,-.256,.035))).normalized();s=(co-W).dot(D)
        w=blend({side+'LowerArm':1},{side+'Hand':1},.5+.5*smooth(s/.035))
    elif part in finger_centers:
        f=finger_centers[part];B=f['base'];T=f['tip'];q=(co-B).dot(T-B)/(T-B).length_squared
        if q<.18:w=blend({f['hand']:1},{f['names'][0]:1},smooth((q+.10)/.28))
        elif q<.48:w=blend({f['names'][0]:1},{f['names'][1]:1},smooth((q-.24)/.23))
        else:w=blend({f['names'][1]:1},{f['names'][2]:1},smooth((q-.54)/.22))
    if v.index in cuff_ids or (t>=0 and t<.10 and part==0):w={side+'UpperArm':1}
    # Keep chest and belt coherent instead of allowing thigh motion into the torso.
    if part==0 and v.index not in tail_ids and co.z>-.17:
        leg=sum(w.pop(n,0) for n in list(w) if 'Leg' in n or 'Foot' in n or 'Toes' in n)
        w['Hips']=w.get('Hips',0)+leg
    w={n:val for n,val in w.items() if val>1e-6 and n in groups}
    w=dict(sorted(w.items(),key=lambda kv:kv[1],reverse=True)[:4]);total=sum(w.values())
    if total<=0:w={'Hips':1};total=1
    w={n:val/total for n,val in w.items()};weights.append(w)
    for n,val in w.items():groups[n].add([v.index],val,'REPLACE')
mod=ob.modifiers.new('Raccoon Skin - Linear','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
world=ob.matrix_world.copy();ob.parent=rig;ob.matrix_world=world
for rec in layouts:
    side='Left' if rec['side']==1 else 'Right';tag='L' if rec['side']==1 else 'R'
    key=me.shape_keys.key_blocks['COR_Elbow90.'+tag]
    fc=key.driver_add('value');d=fc.driver;v=d.variables.new();v.name='bend';v.type='TRANSFORMS';tg=v.targets[0];tg.id=rig;tg.bone_target=side+'LowerArm';tg.transform_type='ROT_X';tg.transform_space='LOCAL_SPACE'
    d.expression='min(1,max(0,(bend-0.7853981634)/0.7853981634))'
for side in ['Left','Right']:
    hand=rig.pose.bones[side+'Hand'];hand['curl']=0.;hand['spread']=0.
    hand.id_properties_ui('curl').update(min=0,max=1,description='Master finger curl; each digit also has its own curl property.')
    hand.id_properties_ui('spread').update(min=-1,max=1)
    for finger in ['Thumb','Index','Middle','Ring','Little']:
        proximal=rig.pose.bones[side+finger+'Proximal'];proximal['curl']=0.;proximal.id_properties_ui('curl').update(min=-1,max=1)
        for segment,angle in zip(['Proximal','Intermediate','Distal'],[55,75,45] if finger!='Thumb' else [28,45,35]):
            pb=rig.pose.bones[side+finger+segment];pb.rotation_mode='XYZ'
            fc=pb.driver_add('rotation_euler',0);d=fc.driver
            for vn,bn,prop in [('master',hand.name,'curl'),('digit',proximal.name,'curl')]:
                v=d.variables.new();v.name=vn;v.type='SINGLE_PROP';v.targets[0].id=rig;v.targets[0].data_path=f'pose.bones["{bn}"]["{prop}"]'
            d.expression=f'(master+digit)*{math.radians(angle)}'
        fc=proximal.driver_add('rotation_euler',2);d=fc.driver;v=d.variables.new();v.name='spread';v.type='SINGLE_PROP';v.targets[0].id=rig;v.targets[0].data_path=f'pose.bones["{hand.name}"]["spread"]';d.expression=f'spread*{math.radians({"Thumb":-12,"Index":-8,"Middle":-2,"Ring":5,"Little":12}[finger])}'
for pb in rig.pose.bones:
    if pb.rotation_mode!='XYZ':pb.rotation_mode='QUATERNION'
    pb.lock_scale=(True,True,True)
    if pb.bone.use_deform and pb.name!='Hips':pb.lock_location=(True,True,True)
rig['rig_version']='v1 - humanoid deformation and posing rig'
rig['deform_bones']=sum(b.use_deform for b in rig.data.bones)
rig['instructions']='Pose Mode: FK on named bones; CTRL_Hand/Foot IK property 0=FK 1=IK. Hand curl/spread and proximal digit curl properties. Tail/ears/scarf FK. Export deform bones only; bake drivers.'
rig['reference_fit_max_error_m']=float(np.max(np.linalg.norm(np.c_[a,np.ones(len(a))]@fit-np.array([v.co[:] for v in ref.data.vertices]),axis=1)))
ob['rigging_status']='Rig v1 installed: 63 deform bones, normalized 4-influence skin, elbow drivers. Pose QA pending.'
ob['stage12']='Five digits, corrected tail UV and sleeve openings preserved; production rig v1 installed.'
bpy.context.view_layer.update()
print(json.dumps({'bones':len(rig.data.bones),'deform':rig['deform_bones'],'vertices':len(weights),'max_influences':max(map(len,weights)),'reference_exact_vertices':sum(x<.00002 for x in errors),'IK_errors':{t:rig.pose.bones[t]['rest_IK_joint_error_m'] for _,_,t,_ in ik_specs}}))
