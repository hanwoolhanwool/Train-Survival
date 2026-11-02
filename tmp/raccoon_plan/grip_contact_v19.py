import bpy,json,math,pathlib
from mathutils import Vector,Matrix,Quaternion
out=pathlib.Path('C:/Users/iam12/Train Survival/output/raccoon-grip-v19');out.mkdir(exist_ok=True)
source=bpy.data.scenes['31 v18 받침팔 · 정지'];bpy.context.window.scene=source;source.frame_set(1)
scene=bpy.data.scenes.new('37 v19 손바닥 접촉 제작');scene.world=source.world;copies={}
for ob in source.objects:
 if ob.type not in {'ARMATURE','MESH','EMPTY'} or (ob.type=='MESH' and not ob.name.startswith('v18_idle_')):continue
 cp=ob.copy();cp.name='v19_'+ob.name
 if ob.data:cp.data=ob.data.copy()
 cp.animation_data_clear();scene.collection.objects.link(cp);copies[ob]=cp
 if cp.type=='MESH' and cp.data.shape_keys:cp.data.shape_keys.animation_data_clear()
for ob,cp in copies.items():
 cp.parent=copies.get(ob.parent);cp.matrix_world=ob.matrix_world.copy()
 for mod in cp.modifiers:
  if mod.type=='ARMATURE':mod.object=copies.get(mod.object,mod.object)
rig=copies[bpy.data.objects['RIG_v18_idle']];rig.name='RIG_GripContact_v19'
body=copies[bpy.data.objects['v18_idle_Raccoon_ArmBody_v18']];body.name='Raccoon_ContactBody_v19'
weapon=copies[bpy.data.objects['v18_idle_Weapon_Shotgun']];weapon.name='Shotgun_Contact_v19'
rig['baseline_pose']=json.dumps({p.name:list(map(list,p.matrix_basis)) for p in rig.pose.bones})
weapon['baseline_matrix']=json.dumps(list(map(list,weapon.matrix_world)))
bpy.context.window.scene=scene;scene.frame_set(1)
camera=bpy.data.objects.new('v19 接촉 확대',bpy.data.cameras.new('v19 camera'));scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=.75
target=Vector((-.03,-.55,1.11));camera.location=target+Vector((1,-1.5,.7));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1000;scene.render.resolution_y=900;scene.frame_start=scene.frame_end=1
for o in scene.objects:
 if o.type in {'ARMATURE','EMPTY'}:o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_GripContact_v19.blend',compress=True)
