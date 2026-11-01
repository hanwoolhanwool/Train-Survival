import bpy,json,pathlib,math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
project=pathlib.Path('C:/Users/iam12/Train Survival');out=project/'output/raccoon-tail-v12';out.mkdir(exist_ok=True)
body=bpy.data.objects['Raccoon_RoundTail_Body_v10'];mesh=body.data;mesh.calc_loop_triangles()
triangles=list(mesh.loop_triangles);tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[tuple(t.vertices) for t in triangles],all_triangles=True)
mat=mesh.materials[0];bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');source=bs.inputs['Base Color'].links[0].from_node.image
pixels=np.empty(len(source.pixels),dtype=np.float32);source.pixels.foreach_get(pixels);pixels=pixels.reshape(source.size[1],source.size[0],4)
uv=mesh.uv_layers.active
def color_at(x,z):
    hit,normal,index,distance=tree.ray_cast(Vector((x,.8,z)),Vector((0,-1,0)))
    if hit is None or hit.y<-.14:return None
    tri=triangles[index];co=[mesh.vertices[i].co for i in tri.vertices];uvco=[Vector((*uv.data[li].uv,0)) for li in tri.loops]
    t=barycentric_transform(hit,*co,*uvco);px=min(source.size[0]-1,max(0,int(t.x*source.size[0])));py=min(source.size[1]-1,max(0,int(t.y*source.size[1])))
    return pixels[py,px,:3].copy()
N=512;result=np.ones((N,N,4),dtype=np.float32);missing=0
# Planar UVs preserve two-dimensional texel area. Extend nearby belt and denim across the small opening.
for j in range(N):
    z=-.35+(j+.5)/N*.30
    for i in range(N):
        x=-.24+(i+.5)/N*.48
        c=color_at(x,z)
        original=None if c is None else c.copy()
        repair=max(0,min(1,(.17-abs(x))/.04))*max(0,min(1,(z+.205)/.025))*max(0,min(1,(-.055-z)/.025))
        if c is None or repair>0:
            tile=1-abs(2*(((x+.24)/.06)%1)-1)
            sx=(.13 if z>-.19 else .085)+.03*tile;left=color_at(-sx,z);right=color_at(sx,z)
            if left is None:left=right
            if right is None:right=left
            if left is None:
                missing+=1;c=np.array((.16,.20,.21))
            else:
                f=max(0,min(1,(x+.08)/.16));f=f*f*(3-2*f);c=left*(1-f)+right*f
            if original is not None:c=original*(1-repair)+c*repair
        result[j,i,:3]=c
image=bpy.data.images.new('T_Raccoon_TailSurround_v12',width=N,height=N,alpha=True);image.pixels.foreach_set(result.ravel());image.filepath_raw=str(out/'T_Raccoon_TailSurround_v12.png');image.file_format='PNG';image.save();image.pack()
material=bpy.data.materials.new('M_Raccoon_TailSurround_v12');material.use_nodes=True;n=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Roughness'].default_value=.88;tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;material.node_tree.links.new(tex.outputs['Color'],n.inputs['Base Color'])
# Update only the current opening pieces; the archived v7 and v11 files remain available.
objects=[ob for ob in bpy.data.objects if ob.type=='MESH' and ('Opening' in ob.name or ob.name=='Raccoon_TailOpening_v11') and 'v11' in ob.name]
for ob in objects:
    ob.data=ob.data.copy();ob.data.materials.clear();ob.data.materials.append(material)
    layer=ob.data.uv_layers.active
    for loop in ob.data.loops:
        p=ob.data.vertices[loop.vertex_index].co;layer.data[loop.index].uv=((p.x+.24)/.48,(p.z+.35)/.30)
    ob.data.update()
data=json.loads((project/'output/raccoon-tail-v11/tail_geometry.json').read_text())
for vertex in data['opening']['vertices']:
    p=vertex['position'];vertex['uv']={'x':(p['x']+.24)/.48,'y':(p['z']+.35)/.30}
(out/'tail_geometry.json').write_text(json.dumps(data))
print('SURROUND_UV_REPAIRED',len(objects),'objects; unsampled atlas pixels',missing)
