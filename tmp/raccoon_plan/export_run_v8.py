import bpy,json,os
from mathutils import Matrix
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
rig=bpy.data.objects['RIG_Raccoon_HybridAuthor_v8'];mesh=bpy.data.objects['raccoon_HybridAuthor_v8']
collection=bpy.data.collections.new('_RunExport_v8');bpy.context.scene.collection.children.link(collection)
er=rig.copy();er.data=rig.data.copy();er.name='Raccoon_Rig';collection.objects.link(er)
er.animation_data_clear();er.animation_data_create();er.animation_data.action=rig.animation_data.action.copy()
er.animation_data.action_slot=er.animation_data.action.slots[0]
er.matrix_world=Matrix.Identity(4);ground=min(v.co.z for v in mesh.data.vertices);er.data.transform(Matrix.Translation((0,0,-ground)))
em=mesh.copy();em.data=mesh.data.copy();em.name='CH_Raccoon_RunExport';collection.objects.link(em)
em.parent=er;em.matrix_parent_inverse=Matrix.Identity(4);em.matrix_basis=Matrix.Identity(4)
em.data.transform(Matrix.Translation((0,0,-ground)),shape_keys=True)
em.data.shape_keys.animation_data_clear()
for mod in em.modifiers:
    if mod.type=='ARMATURE':mod.object=er
for o in bpy.context.selected_objects:o.select_set(False)
er.hide_set(False);er.select_set(True);bpy.context.view_layer.objects.active=er
bpy.ops.object.mode_set(mode='EDIT')
for b in list(er.data.edit_bones):
    if b.name.startswith('CTRL_'):er.data.edit_bones.remove(b)
bpy.ops.object.mode_set(mode='OBJECT')
em.hide_set(False);em.select_set(True)
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=76;scene.render.fps=60
path=OUT+'/Raccoon_Shotgun_Run_Hybrid_v8.fbx'
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
bpy.data.objects.remove(em,do_unlink=True);bpy.data.objects.remove(er,do_unlink=True);bpy.data.collections.remove(collection)
scene.frame_end=75;scene.frame_set(4);mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Raccoon_HybridAuthor_v8.blend')
print('Exported newly authored run:',path)
