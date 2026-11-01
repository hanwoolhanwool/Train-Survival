import bpy, json, math, os
from mathutils import Matrix, Vector, Quaternion

OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
EXCHANGE='C:/Users/iam12/Train Survival/output/raccoon-grip-v4/blender_grip_exchange.json'
data=json.load(open(EXCHANGE,encoding='utf-8'))
source_rig=bpy.data.objects['RIG_Raccoon']
source_mesh=bpy.data.objects['raccoon']
if not os.path.exists(OUT+'/Before_GripReview_v4.blend'):
    bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Before_GripReview_v4.blend',copy=True)
collection=bpy.data.collections.get('Raccoon_GripReview_v4')
if not collection:
    collection=bpy.data.collections.new('Raccoon_GripReview_v4');bpy.context.scene.collection.children.link(collection)
rig=bpy.data.objects.get('RIG_Raccoon_GripReview_v4')
if not rig:
    rig=source_rig.copy();rig.data=source_rig.data.copy();rig.name='RIG_Raccoon_GripReview_v4';collection.objects.link(rig)
rig.animation_data_clear()
for p in rig.pose.bones:
    for c in p.constraints:c.mute=True
    p.rotation_mode='QUATERNION';p.matrix_basis=Matrix.Identity(4)
mesh=bpy.data.objects.get('raccoon_GripReview_v4')
if not mesh:
    mesh=source_mesh.copy();mesh.data=source_mesh.data.copy();mesh.name='raccoon_GripReview_v4';collection.objects.link(mesh)
for o in list(collection.objects):
    if o.name.startswith('REVIEW_'):bpy.data.objects.remove(o,do_unlink=True)
world=source_mesh.matrix_world.copy();mesh.parent=rig;mesh.matrix_world=world
for mod in mesh.modifiers:
    if mod.type=='ARMATURE':mod.object=rig
if mesh.data.shape_keys:mesh.data.shape_keys.animation_data_clear()
for o in bpy.context.scene.objects:
    if o not in [rig,mesh]:o.hide_set(True)
rig.hide_set(False);mesh.hide_set(False)
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
ground=.9504841566085815
def vec(v):return Vector((v['x'],v['y'],v['z']))
def quat(q):return Quaternion((q['w'],q['x'],q['y'],q['z']))
def position(v):
    p=C@vec(v);p.z-=ground;return p
all_weapons=[]
frames=[1,21,41,61]
for case,frame in zip(data['poses'],frames):
    bpy.context.scene.frame_set(frame)
    records={b['name']:b for b in case['bones']}
    for p in sorted(rig.pose.bones,key=lambda b:len(b.parent_recursive)):
        if p.name not in records:continue
        record=records[p.name]
        delta=C@quat(record['rotation']).to_matrix()@quat(record['restRotation']).to_matrix().inverted()@C.inverted()
        rot=delta@p.bone.matrix_local.to_3x3()
        matrix=rot.to_4x4();matrix.translation=position(record['position'])
        p.matrix=matrix;bpy.context.view_layer.update()
        p.keyframe_insert(data_path='location',frame=frame)
        p.keyframe_insert(data_path='rotation_quaternion',frame=frame)
        p.keyframe_insert(data_path='scale',frame=frame)
    for i,key in enumerate(list(mesh.data.shape_keys.key_blocks)[1:]):
        if i<len(case['shapes']):key.value=case['shapes'][i]/100
        key.keyframe_insert(data_path='value',frame=frame)
    for w in case['weapons']:
        geometry=bpy.data.meshes.new('GripReview_'+w['source'])
        vertices=[position(v) for v in w['vertices']]
        faces=[w['triangles'][i:i+3][::-1] for i in range(0,len(w['triangles']),3)]
        geometry.from_pydata(vertices,[],faces);geometry.update()
        uv=geometry.uv_layers.new(name='UVMap')
        for loop in geometry.loops:
            v=w['uv'][loop.vertex_index];uv.data[loop.index].uv=(v['x'],v['y'])
        obj=bpy.data.objects.new('REVIEW_'+case['name']+'_'+w['source'],geometry)
        collection.objects.link(obj);obj.matrix_world=rig.matrix_world.copy()
        source_name='tongs_LV_1' if w['source']=='Weapon_Harpoon_Launcher' else w['source']
        source=bpy.data.objects.get(source_name)
        if source and len(source.data.materials):geometry.materials.append(source.data.materials[0])
        for polygon in geometry.polygons:polygon.use_smooth=True
        for f in frames:
            obj.hide_viewport=(f!=frame);obj.hide_render=(f!=frame)
            obj.keyframe_insert(data_path='hide_viewport',frame=f);obj.keyframe_insert(data_path='hide_render',frame=f)
        all_weapons.append(obj)
    if not bpy.context.scene.timeline_markers.get(case['name']):bpy.context.scene.timeline_markers.new(case['name'],frame=frame)
for action in bpy.data.actions:
    if action.name.startswith((rig.name,mesh.data.shape_keys.name,'REVIEW_')):
        for slot in action.slots:
            for layer in action.layers:
                for strip in layer.strips:
                    bag=strip.channelbag(slot)
                    if bag:
                        for curve in bag.fcurves:
                            for k in curve.keyframe_points:k.interpolation='CONSTANT'
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=61;scene.frame_set(61)
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
rig.show_in_front=False;rig.hide_set(True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        space=area.spaces.active;space.shading.type='SOLID';space.shading.color_type='TEXTURE';space.overlay.show_overlays=False
        space.region_3d.view_perspective='ORTHO';space.region_3d.view_distance=.70
        space.region_3d.view_location=rig.matrix_world@Vector((-.30,-.65,.10))
        space.region_3d.view_rotation=Vector((-1,-1,.25)).to_track_quat('Z','Y')
bpy.context.view_layer.update()
note=bpy.data.texts.new('GripReview_v4_README')
note.write('1: Revolver / 21: Shotgun / 41: Revolver + left Harpoon / 61: Both hands, weapons hidden.\nReview copy only. Original rig v2 remains unchanged.\nWeapon scale: revolver +55.6%, shotgun +54.5%, harpoon +13.3% vs v3.\n')
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Raccoon_GripReview_v4.blend')
print('SAVED',bpy.data.filepath,'frames',frames,'weapons',len(all_weapons))
