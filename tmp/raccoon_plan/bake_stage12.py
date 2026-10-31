import bpy,os,json,math,bmesh
from mathutils import Vector
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
os.makedirs(OUT+'/Textures',exist_ok=True)
obj=bpy.data.objects['raccoon'];mat=obj.data.materials[1]
previous_source=bpy.data.materials.get('M_Raccoon_HandsTail_ProceduralSource')
if previous_source:bpy.data.materials.remove(previous_source)
procedural=mat.copy();procedural.name='M_Raccoon_HandsTail_ProceduralSource';procedural.use_fake_user=True
nodes=procedural.node_tree.nodes;links=procedural.node_tree.links
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
color=bs.inputs['Base Color'].links[0].from_socket
emit=nodes.new('ShaderNodeEmission');links.new(color,emit.inputs['Color']);links.new(emit.outputs[0],output.inputs['Surface'])
img=bpy.data.images.get('T_Raccoon_HandsTail_BaseColor_2K') or bpy.data.images.new('T_Raccoon_HandsTail_BaseColor_2K',width=2048,height=2048,alpha=False)
img.colorspace_settings.name='sRGB'
target=nodes.new('ShaderNodeTexImage');target.image=img;nodes.active=target
for n in nodes:n.select=False
target.select=True
scene=bpy.data.scenes.new('_RaccoonBake_Temporary');scene.render.engine='CYCLES';scene.cycles.samples=1
mesh=bpy.data.meshes.new('_RaccoonBakePlane');mesh.from_pydata([(-1,-1,0),(1,-1,0),(1,1,0),(-1,1,0)],[],[(0,1,2,3)]);mesh.materials.append(procedural)
uv=mesh.uv_layers.new(name='UVMap')
for i,p in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=p
plane=bpy.data.objects.new('_RaccoonBakePlane',mesh);scene.collection.objects.link(plane)
vl=scene.view_layers[0];vl.objects.active=plane;plane.select_set(True,view_layer=vl)
try:
    with bpy.context.temp_override(scene=scene,view_layer=vl,object=plane,active_object=plane,selected_objects=[plane],selected_editable_objects=[plane]):
        bpy.ops.object.bake(type='EMIT',margin=16,use_clear=True)
    img.filepath_raw=OUT+'/Textures/T_Raccoon_HandsTail_BaseColor_2K.png';img.file_format='PNG';img.save();img.pack()
finally:
    bpy.data.objects.remove(plane,do_unlink=True);bpy.data.meshes.remove(mesh);bpy.data.scenes.remove(scene)
# The production material is image-based, keeping the authoring shader as a reusable source.
nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
tex=nodes.new('ShaderNodeTexImage');tex.image=img;tex.location=(-330,100);tex.label='Baked 2K UV atlas - tail bands and hands'
bs=nodes.new('ShaderNodeBsdfPrincipled');bs.location=(0,100);bs.inputs['Roughness'].default_value=.76;bs.inputs['Metallic'].default_value=0
out=nodes.new('ShaderNodeOutputMaterial');out.location=(320,100);links.new(tex.outputs['Color'],bs.inputs['Base Color']);links.new(bs.outputs[0],out.inputs['Surface'])
# Mark actual UV seams for convenient subsequent editing.
me=obj.data
bm=bmesh.new();bm.from_mesh(me);uv=bm.loops.layers.uv.active
for e in bm.edges:
    if len(e.link_faces)!=2:continue
    a,b=e.link_faces
    la={l.vert:tuple(l[uv].uv) for l in a.loops if l.vert in e.verts}
    lb={l.vert:tuple(l[uv].uv) for l in b.loops if l.vert in e.verts}
    e.seam=any((Vector(la[v])-Vector(lb[v])).length>1e-5 for v in e.verts)
# Preserve split normals while updating seam flags only.
for e in bm.edges:
    pass
seams={(min(e.verts[0].index,e.verts[1].index),max(e.verts[0].index,e.verts[1].index)):e.seam for e in bm.edges}
bm.free()
for e in me.edges:e.use_seam=seams.get(tuple(sorted(e.vertices)),False)
# Selection groups are masks for future manual work, not skinning weights.
for vg in list(obj.vertex_groups):
    if vg.name.startswith('SEL_'):obj.vertex_groups.remove(vg)
parts=me.attributes['anatomy_part']
for side,base in [('L',100),('R',200)]:
    for j,name in enumerate(['Index','Middle','Ring','Little','Thumb'],1):
        ids=[i for i,x in enumerate(parts.data) if x.value==base+j]
        group=obj.vertex_groups.new(name='SEL_'+name+'.'+side);group.add(ids,1,'REPLACE')
    for suffix,ids in [('Hand',[i for i,x in enumerate(parts.data) if base<=x.value<=base+5]),('Forearm',[i for i,x in enumerate(parts.data) if x.value==base+10])]:
        group=obj.vertex_groups.new(name='SEL_'+suffix+'.'+side);group.add(ids,1,'REPLACE')
tailids=sorted({v for f in me.polygons if me.attributes['edit_region'].data[f.index].value==3 for v in f.vertices})
group=obj.vertex_groups.new(name='SEL_Tail');group.add(tailids,1,'REPLACE')
obj['rigging_status']='NOT RIGGED - selection masks only'
print('Baked',img.filepath_raw,'size',list(img.size),'packed',bool(img.packed_file),'tail faces',sum(me.attributes['edit_region'].data[f.index].value==3 for f in me.polygons))
