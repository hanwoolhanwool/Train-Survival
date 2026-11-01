import bpy, math, json, numpy as np
from mathutils import Vector, Quaternion, Matrix
from mathutils.bvhtree import BVHTree
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon'];rig=bpy.data.objects['RIG_Raccoon'];me=ob.data
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/blender_preview.py').read(),'preview.py','exec'))

def reset_pose():
    rig.animation_data_clear()
    # Restore the driver-only animation data if an action is merely detached.
    for p in rig.pose.bones:
        p.location=(0,0,0);p.rotation_quaternion=(1,0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
        for key in ['curl','spread','IK']:
            if key in p:p[key]=0.
    bpy.context.view_layer.update()

def neutral():
    if rig.animation_data:rig.animation_data.action=None
    for p in rig.pose.bones:
        p.location=(0,0,0);p.rotation_quaternion=(1,0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
        for key in ['curl','spread','IK']:
            if key in p:p[key]=0.
    rig.update_tag();bpy.context.view_layer.update()

def rotate(n,axis,degrees):
    p=rig.pose.bones[n];local=p.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(local,math.radians(degrees))

def pose(name):
    neutral()
    if name=='elbow90':
        for side in ['Left','Right']:
            p=rig.pose.bones[side+'LowerArm'];p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion((1,0,0),math.pi/2)
    elif name=='grip':
        for side in ['Left','Right']:rig.pose.bones[side+'Hand']['curl']=1.
    elif name=='crouch':
        for side in ['L','R']:rig.pose.bones['CTRL_Foot.'+side]['IK']=1.
        p=rig.pose.bones['Hips'];p.location=p.bone.matrix_local.to_3x3().inverted()@Vector((0,.045,-.14))
        rotate('Spine',(1,0,0),12)
    elif name=='reach':
        rotate('LeftUpperArm',(0,1,0),-45);rotate('RightUpperArm',(0,1,0),45)
    elif name=='stride':
        rotate('LeftUpperLeg',(1,0,0),-25);rotate('LeftLowerLeg',(1,0,0),40)
        rotate('RightUpperLeg',(1,0,0),20);rotate('RightLowerLeg',(1,0,0),15)
        for side in ['Left','Right']:
            p=rig.pose.bones[side+'LowerArm'];p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion((1,0,0),math.radians(35))
    elif name=='tail_turn':
        for i in range(1,6):rotate(f'Tail{i:02d}',(0,0,1),8)
        rotate('Head',(0,0,1),25);rotate('Ear01.L',(0,1,0),-12);rotate('Ear01.R',(0,1,0),12)
    rig.update_tag();bpy.context.view_layer.update()

def snapshot(name,render=True):
    pose(name);dg=bpy.context.evaluated_depsgraph_get();mesh=bpy.data.meshes.new_from_object(ob.evaluated_get(dg),depsgraph=dg)
    snap=bpy.data.objects.new('_RigQA_Snapshot',mesh);bpy.context.scene.collection.objects.link(snap)
    try:
        coords=np.array([v.co[:] for v in mesh.vertices]);orig=np.array([v.co[:] for v in me.vertices]);mesh.calc_loop_triangles()
        tris=[tuple(t.vertices) for t in mesh.loop_triangles];regions=[mesh.attributes['edit_region'].data[t.polygon_index].value for t in mesh.loop_triangles]
        tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],tris,all_triangles=True)
        pairs=[(i,j) for i,j in tree.overlap(tree) if i<j and not set(tris[i]).intersection(tris[j])]
        arm_pairs=[(i,j) for i,j in pairs if regions[i]==2 and regions[j]==2]
        cuff_pairs=[(i,j) for i,j in pairs if {regions[i],regions[j]}=={2,4}]
        finger_pairs=[(i,j) for i,j in pairs if regions[i]==1 and regions[j]==1]
        edges=np.array([e.vertices[:] for e in me.edges]);length=np.linalg.norm(coords[edges[:,0]]-coords[edges[:,1]],axis=1);base=np.linalg.norm(orig[edges[:,0]]-orig[edges[:,1]],axis=1)
        ratio=length/np.maximum(base,1e-10)
        report={'pose':name,'finite':bool(np.isfinite(coords).all()),'max_displacement':float(np.max(np.linalg.norm(coords-orig,axis=1))),'arm_intersections':len(arm_pairs),'arm_cuff_intersections':len(cuff_pairs),'hand_intersections':len(finger_pairs),'all_nonadjacent_intersections':len(pairs),'edge_stretch_p99':float(np.percentile(ratio,99)),'max_edge_stretch':float(ratio.max()),'correctives':{k.name:float(k.value) for k in me.shape_keys.key_blocks if k.name.startswith('COR_')}}
        if render:
            views=[('rig_v1_'+name,(.9,-1,.1),(0,-.03,0),2.15)]
            if name=='elbow90':views=[('rig_v1_elbow90',(1,-.1,.15),(.5,-.32,.01),.8)]
            if name=='grip':views=[('rig_v1_grip',(1,-.6,.3),(.67,-.32,-.24),.4),('rig_v1_grip_palm',(-1,-1,-.1),(.67,-.32,-.24),.4)]
            if name=='tail_turn':views=[('rig_v1_tail_turn',(1,1,.25),(0,.1,0),2.15)]
            preview(snap,OUT,views,800)
        return report
    finally:
        bpy.data.objects.remove(snap,do_unlink=True);bpy.data.meshes.remove(mesh)

results=[]
for name in globals().get('RIG_QA_POSES',['neutral','elbow90','grip','crouch','reach','stride','tail_turn']):
    report=snapshot(name,globals().get('RIG_QA_RENDER',True));results.append(report);print(json.dumps(report))
neutral()
with open(OUT+'/rig_v1_pose_report.json','w') as f:json.dump(results,f,indent=2)
