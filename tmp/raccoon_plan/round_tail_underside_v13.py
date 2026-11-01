import bpy,math,json,pathlib
from mathutils import Vector
project=pathlib.Path('C:/Users/iam12/Train Survival');out=project/'output/raccoon-tail-v13';out.mkdir(exist_ok=True)
source=bpy.data.objects['Raccoon_RoundTail_v11'];geo=source.data.copy();geo.name='Raccoon_RoundedUnderside_v13'
# Remove the root radius dip and upward centre-line overshoot that created the lower notch.
controls=[(0,-.10,-.195,.072,.077),(0,.015,-.205,.083,.088),(0,.13,-.265,.145,.155),(0,.28,-.40,.215,.225),(0,.40,-.58,.235,.245),(0,.485,-.76,.18,.19),(0,.525,-.875,.085,.09),(0,.54,-.94,.004,.004)]
def sample(t):
 x=t*(len(controls)-1);i=min(len(controls)-2,int(x));u=x-i
 a=controls[max(0,i-1)];b=controls[i];c=controls[i+1];d=controls[min(len(controls)-1,i+2)]
 return [.5*(2*b[j]+(-a[j]+c[j])*u+(2*a[j]-5*b[j]+4*c[j]-d[j])*u*u+(-a[j]+3*b[j]-3*c[j]+d[j])*u*u*u) for j in range(5)]
for ring in range(33):
 t=ring/32;s=sample(t);center=Vector(s[:3]);tangent=(Vector(sample(min(1,t+.001))[:3])-Vector(sample(max(0,t-.001))[:3])).normalized();up=Vector((1,0,0)).cross(tangent).normalized()
 for k in range(24):
  angle=2*math.pi*k/24;geo.vertices[ring*24+k].co=center+Vector((math.cos(angle)*s[3],0,0))+up*(math.sin(angle)*s[4])
geo.vertices[792].co=Vector(sample(0)[:3]);geo.vertices[793].co=Vector(sample(1)[:3]);geo.update()
updated=[]
for ob in bpy.data.objects:
 if ob.type=='MESH' and (ob.name=='Raccoon_RoundTail_v11' or ('v11' in ob.name and '_new_Tail' in ob.name)):
  ob.data=geo;updated.append(ob.name)
geo.calc_loop_triangles();vertices=[]
for tri in geo.loop_triangles:
 for li in tri.loops:
  v=geo.vertices[geo.loops[li].vertex_index];vertices.append({'position':dict(zip(['x','y','z'],v.co)),'normal':dict(zip(['x','y','z'],geo.corner_normals[li].vector)),'uv':dict(zip(['x','y'],geo.uv_layers.active.data[li].uv)),'influences':[{'name':source.vertex_groups[g.group].name,'weight':g.weight} for g in v.groups]})
data=json.loads((project/'output/raccoon-tail-v12/tail_geometry.json').read_text())
for old,new in zip(data['vertices'],vertices):
 assert old['uv']==new['uv'], 'Per-corner topology changed'
 new['influences']=old['influences']
data['vertices']=vertices;(out/'tail_geometry.json').write_text(json.dumps(data))
report={'updated_objects':updated,'root_underside_z':[float(geo.vertices[r*24+18].co.z) for r in range(13)],'tail_vertices':len(geo.vertices),'tail_triangles':len(geo.loop_triangles)};(out/'rounding_report.json').write_text(json.dumps(report,indent=2));print(report)
