import bpy,numpy as np,math,os,json
from mathutils import Vector
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
src=bpy.data.objects['raccoon_SOURCE_before_cuff_v3'].data;im=bpy.data.images['Image_0'];pix=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(pix);pix=pix.reshape(im.size[1],im.size[0],4)
samples=[]
for f in src.polygons:
 if f.material_index!=0:continue
 co=f.center;side=1 if co.x>0 else -1;P=Vector((side*.422,-.242,.055));D=(Vector((side*.640,-.302,-.148))-P).normalized();s=(co-P).dot(D);rad=co-P-D*s
 if not (.30<abs(co.x)<.56 and -.39<co.y<-.11 and -.08<s<.025 and f.normal.dot(rad)>.025):continue
 u=sum((src.uv_layers.active.data[l].uv for l in f.loop_indices),Vector((0,0)))/len(f.loop_indices);col=pix[int(u.y*(im.size[1]-1)),int(u.x*(im.size[0]-1)),:3]
 r,g,b=col
 if r>.55 and .66<g/r<.90 and .35<b/r<.76:samples.append(col)
rgb=np.median(samples,axis=0);linear=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)
mat=bpy.data.materials['M_Raccoon_CuffFabric_v3'];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
tc=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(1,.35,1);links.new(tc.outputs['UV'],scale.inputs[0])
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=550;noise.inputs['Detail'].default_value=2;noise.inputs['Roughness'].default_value=.7;links.new(scale.outputs[0],noise.inputs['Vector'])
mapping=nodes.new('ShaderNodeMapRange');mapping.inputs['From Min'].default_value=0;mapping.inputs['From Max'].default_value=1;mapping.inputs['To Min'].default_value=.91;mapping.inputs['To Max'].default_value=1.05;links.new(noise.outputs['Fac'],mapping.inputs['Value'])
sep=nodes.new('ShaderNodeSeparateXYZ');links.new(tc.outputs['UV'],sep.inputs[0])
def mathnode(op,a,b=None):
 n=nodes.new('ShaderNodeMath');n.operation=op
 for i,x in enumerate([a,b]):
  if x is None:continue
  if isinstance(x,(float,int)):n.inputs[i].default_value=x
  else:links.new(x,n.inputs[i])
 return n.outputs[0]
weft=mathnode('SINE',mathnode('MULTIPLY',sep.outputs['X'],2*math.pi*192));warp=mathnode('SINE',mathnode('MULTIPLY',sep.outputs['Y'],2*math.pi*256))
weave=mathnode('ADD',mapping.outputs[0],mathnode('MULTIPLY',mathnode('ADD',weft,warp),.04))
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=(*[float(c) for c in linear],1);links.new(weave,mix.inputs[2])
emit=nodes.new('ShaderNodeEmission');links.new(mix.outputs[0],emit.inputs['Color']);out=nodes.new('ShaderNodeOutputMaterial');links.new(emit.outputs[0],out.inputs['Surface'])
img=bpy.data.images.get('T_Raccoon_CuffFabric_BaseColor_1K') or bpy.data.images.new('T_Raccoon_CuffFabric_BaseColor_1K',width=1024,height=1024,alpha=False);img.colorspace_settings.name='sRGB'
target=nodes.new('ShaderNodeTexImage');target.image=img;nodes.active=target
for n in nodes:n.select=False
target.select=True
scene=bpy.data.scenes.new('_CuffBake');scene.render.engine='CYCLES';scene.cycles.samples=1
mesh=bpy.data.meshes.new('_CuffBakePlane');mesh.from_pydata([(-1,-1,0),(1,-1,0),(1,1,0),(-1,1,0)],[],[(0,1,2,3)]);mesh.materials.append(mat);uv=mesh.uv_layers.new(name='UVMap')
for i,p in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=p
plane=bpy.data.objects.new('_CuffBakePlane',mesh);scene.collection.objects.link(plane);vl=scene.view_layers[0];vl.objects.active=plane;plane.select_set(True,view_layer=vl)
try:
 with bpy.context.temp_override(scene=scene,view_layer=vl,object=plane,active_object=plane,selected_objects=[plane],selected_editable_objects=[plane]):bpy.ops.object.bake(type='EMIT',margin=12,use_clear=True)
 os.makedirs(OUT+'/Textures',exist_ok=True);img.filepath_raw=OUT+'/Textures/T_Raccoon_CuffFabric_BaseColor_1K.png';img.file_format='PNG';img.save();img.pack()
finally:
 bpy.data.objects.remove(plane,do_unlink=True);bpy.data.meshes.remove(mesh);bpy.data.scenes.remove(scene)
nodes.clear();tex=nodes.new('ShaderNodeTexImage');tex.image=img;tex.location=(-300,0);bs=nodes.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=.78;out=nodes.new('ShaderNodeOutputMaterial');out.location=(300,0);links.new(tex.outputs['Color'],bs.inputs['Base Color']);links.new(bs.outputs[0],out.inputs['Surface'])
print(json.dumps({'cloth_samples':len(samples),'srgb_color':[float(c) for c in rgb],'texture':img.filepath_raw}))
