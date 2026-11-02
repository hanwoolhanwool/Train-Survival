import bpy,json,math
from mathutils import Vector,Matrix,Quaternion
rig=bpy.data.objects['RIG_GripContact_v19'];weapon=bpy.data.objects['Shotgun_Contact_v19']
for name,matrix in json.loads(rig['baseline_pose']).items():rig.pose.bones[name].matrix_basis=Matrix(matrix)
weapon.matrix_world=Matrix(json.loads(weapon['baseline_matrix']));bpy.context.view_layer.update()
delta=Vector(rig.get('contact_delta',(-.06,-.035,-.012)))
targets={side:rig.pose.bones[side+'Hand'].head.copy() for side in ['Left','Right']};targets['Left']+=delta
offset=Vector()
for iteration in range(8):
 for side in ['Right','Left']:
  a,b,h=[rig.pose.bones[side+n].head for n in ['UpperArm','LowerArm','Hand']];la=(b-a).length;lb=(h-b).length
  direction=targets[side]+offset-a;maximum=math.sqrt(la*la+lb*lb+2*la*lb*math.cos(math.radians(rig.get('minimum_bend',35) if side=='Left' else 0)))-.002
  if direction.length>maximum:offset+=direction.normalized()*(maximum-direction.length)
for side in ['Right','Left']:
 upper,lower,hand=[rig.pose.bones[side+n] for n in ['UpperArm','LowerArm','Hand']]
 target=targets[side]+offset;a=upper.head.copy();b=lower.head.copy();h=hand.head.copy();handMatrix=hand.matrix.copy()
 la=(b-a).length;lb=(h-b).length;direction=(target-a).normalized();distance=min((target-a).length,la+lb-.0001)
 bend=(b-a)-direction*(b-a).dot(direction);bend.normalize();along=(la*la-lb*lb+distance*distance)/(2*distance);elbow=a+direction*along+bend*math.sqrt(max(0,la*la-along*along))
 q=(b-a).rotation_difference(elbow-a);m=(q.to_matrix()@upper.matrix.to_3x3()).to_4x4();m.translation=a;upper.matrix=m;bpy.context.view_layer.update()
 q=(hand.head-lower.head).rotation_difference(target-lower.head);m=(q.to_matrix()@lower.matrix.to_3x3()).to_4x4();m.translation=lower.head;lower.matrix=m;bpy.context.view_layer.update()
 handMatrix.translation=hand.head;hand.matrix=handMatrix;bpy.context.view_layer.update()
weapon.matrix_world.translation+=rig.matrix_world.to_3x3()@offset
arm=rig.pose.bones['LeftLowerArm'];hand=rig.pose.bones['LeftHand'];a=arm.bone.head_local;h=hand.bone.head_local
qa=arm.matrix.to_quaternion()@arm.bone.matrix_local.to_quaternion().inverted();qh=hand.matrix.to_quaternion()@hand.bone.matrix_local.to_quaternion().inverted()
relative=qh@qa.inverted();axis=(hand.head-arm.head).normalized();projected=axis*Vector((relative.x,relative.y,relative.z)).dot(axis);twist=Quaternion((relative.w,*projected)).normalized();swing=relative@twist.inverted()
for i in range(1,4):
 u=i/4;q=max(0,min(1,(u-.5)/.5));q=q*q*(3-2*q)
 rotation=Quaternion().slerp(swing,q)@Quaternion().slerp(twist,u)@qa
 p=rig.pose.bones['LeftWristBlend'+str(i)];m=rotation.to_matrix().to_4x4()@p.bone.matrix_local;m.translation=arm.head.lerp(hand.head,u)+rotation@(h-a.lerp(h,u));p.matrix=m
bpy.context.view_layer.update()
print('Relative wrist shift',list(delta),'shared weapon offset',list(offset))
