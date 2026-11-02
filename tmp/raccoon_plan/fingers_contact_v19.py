import bpy,json,math
from mathutils import Vector
r=bpy.data.objects['RIG_GripContact_v19'];w=bpy.data.objects['Shotgun_Contact_v19'];ids=json.loads(w['foregrip_faces']);vi={i for f in ids for i in w.data.polygons[f].vertices};points=[w.matrix_world@w.data.vertices[i].co for i in vi]
def aim(p,target):
 start=r.matrix_world@p.head;end=r.matrix_world@p.tail;q=(end-start).rotation_difference(target-start)
 world=r.matrix_world@p.matrix;mat=(q.to_matrix()@world.to_3x3()).to_4x4();mat.translation=start;p.matrix=r.matrix_world.inverted()@mat;bpy.context.view_layer.update()
for name in ['Index','Middle','Ring','Little']:
 bones=[r.pose.bones['Left'+name+s] for s in ['Proximal','Intermediate','Distal']]
 base=r.matrix_world@bones[0].head;near=[p for p in points if abs(p.y-base.y)<.014]
 cx=(min(p.x for p in near)+max(p.x for p in near))/2;cz=(min(p.z for p in near)+max(p.z for p in near))/2
 radius={'Index':.054,'Middle':.054,'Ring':.054,'Little':.047}[name]
 theta=math.atan2(base.z-cz,base.x-cx)
 for bone in bones:
  length=(bone.tail-bone.head).length;head=r.matrix_world@bone.head;currentRadius=math.hypot(head.x-cx,head.z-cz);theta=math.atan2(head.z-cz,head.x-cx)-math.acos(max(-1,min(1,(currentRadius*currentRadius+radius*radius-length*length)/(2*currentRadius*radius))))
  target=Vector((cx+radius*math.cos(theta),base.y,cz+radius*math.sin(theta)))
  aim(bone,target)
 print(name,'radius',radius,'center',cx,cz)
# Keep the thumb outside the right side of the grip, opposite the four fingers.
thumb=[r.pose.bones['LeftThumb'+s] for s in ['Proximal','Intermediate','Distal']]
for bone in thumb:
 target=r.matrix_world@bone.tail+Vector((.010,0,0));aim(bone,target)
