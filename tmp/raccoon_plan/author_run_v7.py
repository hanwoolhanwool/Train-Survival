import bpy,math,json,os
from mathutils import Vector,Matrix,Quaternion
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
QA='C:/Users/iam12/Train Survival/output/raccoon-run-v7'
os.makedirs(QA,exist_ok=True)
source=bpy.data.objects['raccoon'];source_rig=bpy.data.objects['RIG_Raccoon']
col=bpy.data.collections.get('Raccoon_RunAuthor_v7')
if col:
    for ob in list(col.objects):bpy.data.objects.remove(ob,do_unlink=True)
else:
    col=bpy.data.collections.new('Raccoon_RunAuthor_v7');bpy.context.scene.collection.children.link(col)
rig=source_rig.copy();rig.data=source_rig.data.copy();rig.name='RIG_Raccoon_RunAuthor_v7';col.objects.link(rig);rig.animation_data_clear()
mesh=source.copy();mesh.data=source.data.copy();mesh.name='raccoon_RunAuthor_v7';col.objects.link(mesh)
mesh.parent=rig;mesh.matrix_parent_inverse=Matrix.Identity(4);mesh.matrix_basis=Matrix.Identity(4)
mesh.data.shape_keys.animation_data_clear()
for k in mesh.data.shape_keys.key_blocks:
    if k.name!='Basis':k.value=0
for mod in mesh.modifiers:
    if mod.type=='ARMATURE':mod.object=rig
for p in rig.pose.bones:
    for c in list(p.constraints):p.constraints.remove(c)
    p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
for ob in bpy.context.scene.objects:ob.hide_set(ob not in [rig,mesh])
scene=bpy.context.scene;scene.render.fps=60;scene.frame_start=1;scene.frame_end=30
action=bpy.data.actions.new('Raccoon_Shotgun_Run_AUTHORED_v7');rig.animation_data_create();rig.animation_data.action=action
rest={b.name:b.matrix_local.copy() for b in rig.data.bones};ground=min(v.co.z for v in mesh.data.vertices)
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
grip=json.load(open('C:/Users/iam12/Train Survival/output/raccoon-grip-v5/blender_grip_exchange.json',encoding='utf-8'))['poses'][1]
grip_matrices={}
for b in grip['bones']:
    if b['name'] not in rest:continue
    delta=C@quat(b['rotation']).to_matrix()@quat(b['restRotation']).to_matrix().inverted()@C.inverted()
    mat=(delta@rest[b['name']].to_3x3()).to_4x4();mat.translation=C@vec(b['position'])+Vector((0,0,ground));grip_matrices[b['name']]=mat
finger_local={}
for p in rig.pose.bones:
    if any(s in p.name for s in ['Proximal','Intermediate','Distal']):
        local_rest=rest[p.parent.name].inverted()@rest[p.name]
        finger_local[p.name]=(local_rest.inverted()@grip_matrices[p.parent.name].inverted()@grip_matrices[p.name]).to_quaternion()
foot_ids={}
for side in ['Left','Right']:
    groups={mesh.vertex_groups[side+s].index for s in ['Foot','Toes']}
    foot_ids[side]=[v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups if g.group in groups)>.65]

def rotation(axis,degrees):return Matrix.Rotation(math.radians(degrees),4,axis)
def set_world(name,rot,position=None):
    p=rig.pose.bones[name];mat=rot.to_4x4();mat.translation=p.head.copy() if position is None else position;p.matrix=mat;bpy.context.view_layer.update()
def turn_world(name,axis,deg):
    p=rig.pose.bones[name];set_world(name,rotation(axis,deg).to_3x3()@p.matrix.to_3x3())
def solve_chain(upper_name,lower_name,end_name,target,hint):
    upper=rig.pose.bones[upper_name];lower=rig.pose.bones[lower_name];end=rig.pose.bones[end_name]
    start=upper.head.copy();a=(lower.head-start).length;b=(end.head-lower.head).length
    delta=target-start;distance=max(abs(a-b)+.002,min(delta.length,a+b-.003));axis=delta.normalized()
    pole=hint-start;pole=(pole-axis*pole.dot(axis)).normalized();along=(a*a-b*b+distance*distance)/(2*distance)
    elbow=start+axis*along+pole*math.sqrt(max(0,a*a-along*along))
    q=(lower.head-start).rotation_difference(elbow-start);set_world(upper_name,q.to_matrix()@upper.matrix.to_3x3())
    q=(end.head-lower.head).rotation_difference(start+axis*distance-lower.head);set_world(lower_name,q.to_matrix()@lower.matrix.to_3x3())
    return (end.head-target).length
def smooth(v):return v*v*(3-2*v)
def foot_motion(phase,side,bob):
    stance=.20;period=.5;speed=7.;lead=speed*period*stance/2
    sign=1 if side=='Left' else -1;foot=rig.data.bones[side+'Foot'];toes=rig.data.bones[side+'Toes']
    yaw=rotation('Z',-18*sign)
    if phase<=stance:
        forward=lead-speed*period*phase;lift=0;roll=18*smooth(max(0,(phase-stance*.70)/(stance*.30)))
        toe_roll=0
    else:
        u=(phase-stance)/(1-stance);swing=(1-stance)*period;m=-speed*swing
        forward=(2*u**3-3*u*u+1)*(-lead)+(u**3-2*u*u+u)*m+(-2*u**3+3*u*u)*lead+(u**3-u*u)*m
        # Tuck the recovering foot beneath the pelvis instead of making a wide airborne split.
        # This quintic term preserves the contact position and velocity at both ends.
        forward+=18*u*u*(1-u)**2*(1-2*u)
        lift=.26*math.sin(math.pi*u)**.80
        roll=18*(1-u)-40*math.sin(math.pi*u)**2
        toe_roll=roll
    centre_y=-.267
    ankle=Vector((sign*.185,centre_y-forward,foot.head_local.z+lift))
    foot_rotation=yaw.to_3x3()@rotation('X',roll).to_3x3()@rest[side+'Foot'].to_3x3()
    if phase<=stance:
        pivot=Vector((toes.head_local.x,toes.head_local.y,ground))-foot.head_local
        ankle+=yaw.to_3x3()@(pivot-rotation('X',roll).to_3x3()@pivot)
    error=solve_chain(side+'UpperLeg',side+'LowerLeg',side+'Foot',ankle,Vector((sign*.20,-1.0,-.55)))
    set_world(side+'Foot',foot_rotation)
    set_world(side+'Toes',yaw.to_3x3()@rotation('X',toe_roll).to_3x3()@rest[side+'Toes'].to_3x3())
    return error,phase<=stance,ankle

reports=[];previous_quat={}
for frame in range(1,32):
    phase=((frame-1)/30)%1;scene.frame_set(frame)
    for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    half=(phase*2)%1
    bob=-.025*math.sin(math.pi*half/.4) if half<.4 else .035*math.sin(math.pi*(half-.4)/.6)
    sway=math.cos(2*math.pi*phase);twist=3*math.sin(2*math.pi*phase)
    hips_pos=rig.data.bones['Hips'].head_local+Vector((.015*sway,0,-.130+bob))
    hips_rot=rotation('Z',twist)@rotation('Y',2*sway)@rotation('X',3)@rest['Hips']
    set_world('Hips',hips_rot.to_3x3(),hips_pos)
    turn_world('Spine','X',3);turn_world('Chest','X',3);turn_world('Chest','Z',-twist*.65)
    set_world('Head',rotation('X',.8*math.cos(4*math.pi*phase)).to_3x3()@rest['Head'].to_3x3())
    contacts={};errors={}
    for side,offset in [('Left',0),('Right',.5)]:
        p=(phase+offset)%1;err,contact,target=foot_motion(p,side,bob);contacts[side]=contact;errors[side]=err
        # Keep the actual skinned sole on the floor during support, not merely the ankle marker.
        if True:
            for iteration in range(2):
                ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());geo=ev.to_mesh();minimum=min(geo.vertices[i].co.z for i in foot_ids[side]);ev.to_mesh_clear()
                correction=ground+(.0015 if contact else .012)-minimum
                if not contact:correction=max(0,correction)
                if abs(correction)<.0002:break
                frot=rig.pose.bones[side+'Foot'].matrix.to_3x3().copy();trot=rig.pose.bones[side+'Toes'].matrix.to_3x3().copy()
                target.z+=correction;solve_chain(side+'UpperLeg',side+'LowerLeg',side+'Foot',target,Vector(((1 if side=='Left' else -1)*.20,-1,-.55)))
                set_world(side+'Foot',frot);set_world(side+'Toes',trot)
    for side,sign in [('Left',1),('Right',-1)]:
        wrist=grip_matrices[side+'Hand'].translation+Vector((0,-.025,-.130+bob*.55))
        solve_chain(side+'UpperArm',side+'LowerArm',side+'Hand',wrist,Vector((sign*.48,-.30,-.2)))
        set_world(side+'Hand',grip_matrices[side+'Hand'].to_3x3())
    for name,q in finger_local.items():rig.pose.bones[name].rotation_quaternion=q
    for i in range(5):
        turn_world('Tail%02d'%(i+1),'X',22 if i==0 else 4)
        turn_world('Tail%02d'%(i+1),'Z',1.6*math.sin(2*math.pi*phase-i*.6))
    bpy.context.view_layer.update()
    for p in rig.pose.bones:
        if p.name.startswith('CTRL_'):continue
        q=p.rotation_quaternion.copy()
        if p.name in previous_quat:q.make_compatible(previous_quat[p.name])
        previous_quat[p.name]=q.copy();p.rotation_quaternion=q
        for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=frame,group=p.name)
    ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());geo=ev.to_mesh()
    minima={side:min(geo.vertices[i].co.z-ground for i in ids) for side,ids in foot_ids.items()};ev.to_mesh_clear()
    reports.append({'frame':frame,'phase':phase,'contact':contacts,'sole_height':minima,'ik_error':errors})
for layer in action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in bag.fcurves:
                for k in curve.keyframe_points:k.interpolation='LINEAR'
for marker in list(scene.timeline_markers):scene.timeline_markers.remove(marker)
for f,label in [(1,'왼발 착지'),(4,'압축'),(7,'도약'),(12,'발 회수'),(16,'오른발 착지'),(19,'압축'),(22,'도약'),(27,'발 회수')]:scene.timeline_markers.new(label,frame=f)
scene.frame_set(4);rig.hide_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        s=area.spaces.active;s.shading.type='MATERIAL';s.shading.use_scene_world=False;s.shading.use_scene_lights=False;s.overlay.show_overlays=False
        s.region_3d.view_perspective='ORTHO';s.region_3d.view_distance=2.8;s.region_3d.view_location=rig.matrix_world@Vector((0,-.2,-.05));s.region_3d.view_rotation=Vector((-1,-2,.25)).to_track_quat('Z','Y')
json.dump({'provenance':'Newly authored from raccoon bind skeleton, analytic foot paths and original body poses. No existing locomotion samples read. Approved v5 static hand grip reused.','fps':60,'duration':.5,'speed_mps':7,'reports':reports},open(QA+'/authoring_report.json','w'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Raccoon_RunAuthor_v7.blend')
print('Authored frames',len(reports),'max swing IK error',max(max(r['ik_error'].values()) for r in reports),'contact heights',[(s,min(r['sole_height'][s] for r in reports if r['contact'][s]),max(r['sole_height'][s] for r in reports if r['contact'][s])) for s in ['Left','Right']])
