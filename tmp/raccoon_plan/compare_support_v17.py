import bpy,math,json,pathlib
from mathutils import Vector
scene=bpy.data.scenes.new('28 v17 받침팔 전후 비교')
scene.world=bpy.data.scenes['24 v17 받침팔 · 정지'].world
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.frame_start=scene.frame_end=1
camera=bpy.data.objects.new('v17 비교 카메라',bpy.data.cameras.new('v17 compare camera'))
scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=3.5
camera.location=(0,-5,2.1);target=Vector((0,0,1.23));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True)
measurements={}
for version,source_name,offset,label in [('v16','19 v16 받침손 · 정지',-.85,'수정 전'),('v17','24 v17 받침팔 · 정지',.85,'수정 후')]:
 source=bpy.data.scenes[source_name];bpy.context.window.scene=source;source.frame_set(1)
 originals=[o for o in source.objects if o.type=='ARMATURE' or (o.type=='MESH' and (o.name.startswith(version+'_idle_')))]
 parent=bpy.data.objects.new('비교 '+version,None);scene.collection.objects.link(parent);copies={}
 for ob in originals:
  cp=ob.copy();cp.data=ob.data.copy();cp.name='compare_'+version+'_'+ob.name;cp.animation_data_clear();scene.collection.objects.link(cp);copies[ob]=cp
  if cp.type=='MESH' and cp.data.shape_keys:cp.data.shape_keys.animation_data_clear()
 for ob,cp in copies.items():
  cp.parent=copies.get(ob.parent,parent);cp.matrix_world=ob.matrix_world.copy()
  for mod in cp.modifiers:
   if mod.type=='ARMATURE':mod.object=copies.get(mod.object,mod.object)
 parent.location.x=offset;parent.rotation_euler.z=math.radians(-48)
 rig=next(o for o in originals if o.type=='ARMATURE');a,b,c=[rig.pose.bones['Left'+n].head for n in ['UpperArm','LowerArm','Hand']]
 bend=math.degrees((b-a).angle(c-b));measurements[version]={'elbow_flex_degrees':bend}
 text=bpy.data.curves.new('v17 비교 설명','FONT');text.body=label+'  '+version+'\n팔꿈치 %.0f°'%bend;text.font=font;text.size=.115;text.align_x='CENTER'
 ob=bpy.data.objects.new(label,text);scene.collection.objects.link(ob);ob.location=(offset,-.2,2.12);ob.rotation_euler=camera.rotation_euler
 material=bpy.data.materials.new('v17 label '+version);material.use_nodes=True
 bs=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED');color=(.95,.45,.16,1) if version=='v16' else (.2,.85,.55,1)
 bs.inputs['Base Color'].default_value=color;bs.inputs['Emission Color'].default_value=color;bs.inputs['Emission Strength'].default_value=.7;text.materials.append(material)
bpy.context.window.scene=scene;scene.frame_set(1)
for ob in scene.objects:
 if ob.type in {'ARMATURE','EMPTY'}:ob.hide_set(True)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  space=area.spaces.active;space.shading.type='MATERIAL';space.shading.use_scene_lights=False;space.shading.use_scene_world=False;space.overlay.show_overlays=False;space.region_3d.view_perspective='CAMERA';space.region_3d.view_camera_zoom=8;space.region_3d.view_camera_offset=(0,0)
out=pathlib.Path('C:/Users/iam12/Train Survival/output/raccoon-support-v17');(out/'comparison.json').write_text(json.dumps(measurements,indent=2))
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Raccoon_SupportArm_v17.blend',compress=True)
print(measurements)
