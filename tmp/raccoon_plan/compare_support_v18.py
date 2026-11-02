import bpy,math,json,pathlib
from mathutils import Vector
scene=bpy.data.scenes.new('35 v18 받침팔 수정 전후')
scene.world=bpy.data.scenes['31 v18 받침팔 · 정지'].world
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.frame_start=scene.frame_end=1
cam=bpy.data.objects.new('v18 비교 확대',bpy.data.cameras.new('v18 comparison'))
scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.45
target=Vector((-.10,0,1.25));cam.location=target+Vector((0,-5,1.5));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf',check_existing=True)
for version,source_name,x,label in [('v17','24 v17 받침팔 · 정지',-.62,'수정 전 v17'),('v18','31 v18 받침팔 · 정지',.62,'수정 후 v18')]:
    source=bpy.data.scenes[source_name];bpy.context.window.scene=source;source.frame_set(1)
    originals=[o for o in source.objects if o.type=='ARMATURE' or (o.type=='MESH' and o.name.startswith(version+'_idle_'))]
    parent=bpy.data.objects.new('v18 compare '+version,None);scene.collection.objects.link(parent);copies={}
    for ob in originals:
        cp=ob.copy();cp.data=ob.data.copy();cp.name='v18compare_'+version+'_'+ob.name;cp.animation_data_clear();scene.collection.objects.link(cp);copies[ob]=cp
        if cp.type=='MESH' and cp.data.shape_keys:cp.data.shape_keys.animation_data_clear()
    for ob,cp in copies.items():
        cp.parent=copies.get(ob.parent,parent);cp.matrix_world=ob.matrix_world.copy()
        for mod in cp.modifiers:
            if mod.type=='ARMATURE':mod.object=copies.get(mod.object,mod.object)
    parent.location.x=x;parent.rotation_euler.z=math.radians(-48)
    text=bpy.data.curves.new('v18 comparison label','FONT');text.body=label;text.font=font;text.size=.09;text.align_x='CENTER'
    ob=bpy.data.objects.new(label,text);scene.collection.objects.link(ob);ob.location=(x,-.7,1.72);ob.rotation_euler=cam.rotation_euler
    material=bpy.data.materials.new('v18 label '+version);material.use_nodes=True
    p=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED');color=(.95,.45,.16,1) if version=='v17' else (.2,.85,.55,1)
    p.inputs['Base Color'].default_value=color;p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=.7;text.materials.append(material)
bpy.context.window.scene=scene;scene.frame_set(1)
for o in scene.objects:
    if o.type in {'ARMATURE','EMPTY'}:o.hide_set(True)
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.region_3d.view_camera_zoom=8;a.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)
print(scene.name)
