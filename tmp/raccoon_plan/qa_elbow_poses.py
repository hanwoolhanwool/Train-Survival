import bpy,json,math,numpy as np
from mathutils import Vector,Quaternion
from mathutils.bvhtree import BVHTree
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon'];layout=json.loads(ob['elbow_layout_json'])
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/blender_preview.py').read(),'preview.py','exec'))
selected=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active
rigdata=bpy.data.armatures.new('_ElbowTestRig');rig=bpy.data.objects.new('_ElbowTestRig',rigdata);bpy.context.scene.collection.objects.link(rig)
test=ob.copy();test.data=ob.data.copy();test.name='_ElbowTestSkin';test.location=(0,0,0);bpy.context.scene.collection.objects.link(test);test.hide_render=False
test.vertex_groups.clear()
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
results=[]
def weight(t):
 q=max(0,min(1,(t-.04)/.70));return q*q*(3-2*q)
def crossarea(mesh,ids):
 p=np.array([mesh.vertices[i].co[:] for i in ids],dtype=float);p-=p.mean(axis=0)
 _,_,axes=np.linalg.svd(p,full_matrices=False);xy=p@axes[:2].T
 return abs(sum(np.cross(xy[i],xy[(i+1)%len(xy)]) for i in range(len(xy))))*.5
try:
 bpy.ops.object.mode_set(mode='EDIT')
 for rec in layout:
  side='L' if rec['side']>0 else 'R';upper=rigdata.edit_bones.new('Upper.'+side);upper.head=rec['root'];upper.tail=rec['elbow'];lower=rigdata.edit_bones.new('Lower.'+side);lower.head=upper.tail;lower.tail=rec['wrist'];lower.parent=upper;lower.use_connect=True
 bpy.ops.object.mode_set(mode='OBJECT')
 for rec in layout:
  sign=rec['side'];side='L' if sign>0 else 'R';base=100 if sign>0 else 200
  upper=test.vertex_groups.new(name='Upper.'+side);lower=test.vertex_groups.new(name='Lower.'+side)
  for v in test.data.vertices:
   part=test.data.attributes['anatomy_part'].data[v.index].value;t=test.data.attributes['elbow_t'].data[v.index].value
   if v.co.x*sign<=0:continue
   if base<=part<=base+5:w=1
   elif part==base+10:w=weight(t)
   elif t==0:w=0
   else:continue
   if w<1:upper.add([v.index],1-w,'REPLACE')
   if w>0:lower.add([v.index],w,'REPLACE')
 mod=test.modifiers.new('_ElbowLinearSkinTest','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
 for angle in globals().get('QA_ANGLES',[45,90]):
  if test.data.shape_keys:
   for key in test.data.shape_keys.key_blocks:
    if key.name.startswith('COR_Elbow90.'):key.value=max(0,min(1,(angle-45)/45))
  for rec in layout:
   side='L' if rec['side']>0 else 'R';pb=rig.pose.bones['Lower.'+side]
   direction=(Vector(rec['wrist'])-Vector(rec['elbow'])).normalized();front=Vector((0,-1,0));bendaxis=direction.cross(front).normalized();localaxis=pb.bone.matrix_local.to_3x3().inverted()@bendaxis
   pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion(localaxis,math.radians(angle))
  bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();evaluated=test.evaluated_get(dg)
  posedmesh=bpy.data.meshes.new_from_object(evaluated,depsgraph=dg);posed=bpy.data.objects.new('_ElbowPoseSnapshot',posedmesh);bpy.context.scene.collection.objects.link(posed)
  try:
   ratios=[]
   for sign in [1,-1]:
    for rid in [3,4,5,6,7]:
     ids=[v.index for v in test.data.vertices if v.co.x*sign>0 and test.data.attributes['elbow_ring'].data[v.index].value==rid]
     # Ring vertices retain construction order through evaluation.
     if len(ids)!=16:continue
     ratio=crossarea(posedmesh,ids)/crossarea(test.data,ids);ratios.append({'side':sign,'ring':rid,'area_ratio':round(float(ratio),4)})
   posedmesh.calc_loop_triangles()
   tris=[tuple(t.vertices) for t in posedmesh.loop_triangles if posedmesh.attributes['edit_region'].data[t.polygon_index].value==2]
   bvh=BVHTree.FromPolygons([v.co for v in posedmesh.vertices],tris,all_triangles=True,epsilon=0)
   intersections=[(i,j) for i,j in bvh.overlap(bvh) if i<j and not set(tris[i]).intersection(tris[j])]
   locations=[round(sum(test.data.attributes['elbow_t'].data[k].value for k in tris[i]+tris[j])/6,3) for i,j in intersections]
   results.append({'angle_degrees':angle,'skinning':'linear blend, preserve volume disabled','correctives':bool(test.data.shape_keys and angle>45),'cross_sections':ratios,'arm_nonadjacent_triangle_intersections':len(intersections),'intersection_positions':locations})
   if globals().get('QA_RENDER',True):preview(posed,OUT,[(f'elbow_pose_{angle}_side',(1,0,.12),(.51,-.37,-.005),.80),(f'elbow_pose_{angle}_front',(0,-1,.05),(0,-.02,0),2.15),(f'elbow_pose_{angle}_right',(-1,0,.12),(-.51,-.37,-.005),.80)],850)
  finally:bpy.data.objects.remove(posed,do_unlink=True);bpy.data.meshes.remove(posedmesh)
 if globals().get('QA_SAVE',False):
  qa=bpy.data.scenes.new('Raccoon_Elbow_QA');qa.collection.objects.link(test);qa.collection.objects.link(rig);qa.frame_start=1;qa.frame_end=91
  test.name='QA_Raccoon_Elbow_Skin';rig.name='QA_Elbow_Test_Rig';test['purpose']='Arm deformation test only; not the final game rig.'
  for frame,angle in [(1,0),(46,45),(91,90)]:
   for rec in layout:
    side='L' if rec['side']>0 else 'R';pb=rig.pose.bones['Lower.'+side];direction=(Vector(rec['wrist'])-Vector(rec['elbow'])).normalized();axis=direction.cross(Vector((0,-1,0))).normalized();local=pb.bone.matrix_local.to_3x3().inverted()@axis
    pb.rotation_quaternion=Quaternion(local,math.radians(angle));pb.keyframe_insert(data_path='rotation_quaternion',frame=frame)
   if test.data.shape_keys:
    for key in test.data.shape_keys.key_blocks:
     if key.name.startswith('COR_Elbow90.'):key.value=max(0,min(1,(angle-45)/45));key.keyframe_insert(data_path='value',frame=frame)
  for datablock in [rig,test.data.shape_keys]:
   if not datablock or not datablock.animation_data:continue
   action=datablock.animation_data.action;curves=[]
   if hasattr(action,'fcurves'):curves=list(action.fcurves)
   elif hasattr(action,'layers'):
    for layer in action.layers:
     for strip in layer.strips:
      if hasattr(strip,'channelbags'):
       for bag in strip.channelbags:curves.extend(bag.fcurves)
   for curve in curves:
    for point in curve.keyframe_points:point.interpolation='LINEAR'
  qa.frame_set(1);rig.show_in_front=True;qa.view_layers[0].objects.active=test;test.select_set(True,view_layer=qa.view_layers[0])
  bpy.data.libraries.write(OUT+'/Raccoon_Elbow_PoseQA.blend',{qa},path_remap='ABSOLUTE',fake_user=False,compress=True)
  bpy.data.scenes.remove(qa)
 with open(OUT+'/elbow_pose_report.json','w') as f:json.dump(results,f,indent=2)
 print(json.dumps(results))
finally:
 if rig.mode!='OBJECT':bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='OBJECT')
 mesh=test.data;bpy.data.objects.remove(test,do_unlink=True);bpy.data.meshes.remove(mesh);bpy.data.objects.remove(rig,do_unlink=True);bpy.data.armatures.remove(rigdata)
 for o in selected:o.select_set(True)
 bpy.context.view_layer.objects.active=active
