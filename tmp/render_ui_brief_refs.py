import bpy, math, os, json, sys
from mathutils import Vector
ROOT = r'C:\Users\iam12\Train Survival'
OUT = os.path.join(ROOT, 'docs', 'design', 'references', 'ingame-ui')
os.makedirs(OUT, exist_ok=True)
items = [(k, 'ResourceNodes/Res_'+k, 'T_Res_'+k+'_BaseColor') for k in ['Wood','Stone','Scrap','Niter','RawFood','Timber','OreVein','Rice','Salt','Ice','RareMetal','RelicPart']]
items += [('Harpoon','Weapon_Harpoon_Launcher','T_Weapon_Harpoon_BaseColor'),('Revolver','Weapon_Revolver','T_Weapon_Revolver_BaseColor'),('Shotgun','Weapon_Shotgun','T_Weapon_Shotgun_BaseColor'),('Hammer','Weapon_RepairHammer','T_Weapon_RepairHammer_BaseColor'),('Bundle','Prop_StorageBundle','T_Prop_StorageBundle_BaseColor')]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.resolution_x=384
scene.render.resolution_y=384
scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'
scene.world.color=(0.35,0.35,0.35)
scene.view_settings.view_transform='Standard'
bpy.ops.object.camera_add(location=(4,-6,3.5))
cam=bpy.context.object
cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'
cam.data.ortho_scale=3.2
scene.camera=cam
for pos,power,size in [((3,-4,6),450,5),((-4,-2,3),250,5),((0,4,5),250,4)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    light=bpy.context.object
    light.data.energy=power
    light.data.shape='DISK'
    light.data.size=size
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
base=set(bpy.data.objects)
records=[]
only_key = sys.argv[sys.argv.index('--only')+1] if '--only' in sys.argv else None
old_records = {}
if only_key:
    with open(os.path.join(OUT,'references.json'),encoding='utf-8') as f:
        old_records={r['key']:r for r in json.load(f)}
for key,model,texture in items:
    if only_key and key != only_key:
        records.append(old_records[key])
        continue
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'Assets','_Project','Art','Models',model+'.fbx'))
    if key == 'Harpoon':
        # Both exports retain the original assembly coordinates. Import together,
        # then normalize the assembly once; never center each component separately.
        bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'Assets','_Project','Art','Models','Weapon_Harpoon_Claw.fbx'))
    imported=set(bpy.data.objects)-base
    meshes=[o for o in imported if o.type=='MESH']
    bpy.context.view_layer.update()
    corners=[o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
    lo=Vector(tuple(min(v[a] for v in corners) for a in range(3)))
    hi=Vector(tuple(max(v[a] for v in corners) for a in range(3)))
    center=(lo+hi)/2
    scale=2.15/max(hi-lo)
    for obj in meshes:
        world=obj.matrix_world.copy()
        obj.parent=None
        obj.matrix_world=world
        obj.location=(obj.location-center)*scale
        obj.scale*=scale
    mat=bpy.data.materials.new('Ref_'+key)
    mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value=0.85
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(os.path.join(ROOT,'Assets','_Project','Art','Textures',texture+'.png'),check_existing=True)
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    # Match the revolver's screen direction: grip down, working end up-right.
    # A slightly more lateral view keeps the attached claw readable.
    cam.location=(6,-4,3.5) if key == 'Harpoon' else (4,-6,3.5)
    cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=os.path.join(OUT,'Ref_'+key+'.png')
    bpy.ops.render.render(write_still=True)
    records.append({'key':key,'model':'Assets/_Project/Art/Models/'+model+'.fbx','texture':'Assets/_Project/Art/Textures/'+texture+'.png','preview':'Ref_'+key+'.png'})
    if key == 'Harpoon':
        records[-1]['component_models'] = ['Assets/_Project/Art/Models/Weapon_Harpoon_Launcher.fbx','Assets/_Project/Art/Models/Weapon_Harpoon_Claw.fbx']
        records[-1]['assembly'] = 'Original shared FBX coordinates preserved; launcher and claw normalized as one assembled object.'
        records[-1]['presentation'] = 'Same screen direction as Revolver: grip down, attached claw up-right. Camera uses a slightly more lateral view for claw visibility; no mirroring.'
    for obj in imported:
        bpy.data.objects.remove(obj,do_unlink=True)
    print('REFERENCE_DONE '+key,flush=True)
with open(os.path.join(OUT,'references.json'),'w',encoding='utf-8') as f:
    json.dump(records,f,ensure_ascii=False,indent=2)
print('ALL_REFERENCES_DONE',flush=True)
