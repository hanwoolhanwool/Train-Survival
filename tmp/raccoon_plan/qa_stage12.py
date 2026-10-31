import bpy,bmesh,json,math
from mathutils import Vector,Quaternion
from collections import Counter
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon'];me=ob.data
me.calc_loop_triangles();uv=me.uv_layers.active.data
bm=bmesh.new();bm.from_mesh(me)
report={'vertices':len(me.vertices),'faces':len(me.polygons),'triangles':len(me.loop_triangles),'quads':sum(len(f.vertices)==4 for f in me.polygons),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'loose_vertices':sum(not v.link_edges for v in bm.verts),'materials':len(me.materials),'digits_per_hand':5}
bm.free()
report['degenerate_uv_triangles']=0
report['degenerate_geometry_triangles']=sum(t.area<1e-12 for t in me.loop_triangles)
report['uv_outside_0_1']=sum(any(v<0 or v>1 for v in q.uv) for q in uv)
tail=[]
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def polyarea(p):return abs(sum(cross(p[0],p[i],p[i+1]) for i in range(1,len(p)-1)))/2 if len(p)>2 else 0
for t in me.loop_triangles:
 p=[tuple(uv[l].uv) for l in t.loops]
 if polyarea(p)<1e-10:report['degenerate_uv_triangles']+=1
 if me.attributes['edit_region'].data[t.polygon_index].value==3:
  if cross(*p)<0:p.reverse()
  tail.append(p)
def intersection(poly,clip):
 for i,a in enumerate(clip):
  b=clip[(i+1)%3];out=[]
  for j,p in enumerate(poly):
   q=poly[(j+1)%len(poly)];dp=cross(a,b,p);dq=cross(a,b,q)
   if dp>=-1e-12:out.append(p)
   if (dp>0 and dq<0) or (dp<0 and dq>0):out.append(tuple(p[k]+(q[k]-p[k])*(dp/(dp-dq)) for k in (0,1)))
  poly=out
  if not poly:return []
 return poly
overlaps=[]
for i,a in enumerate(tail):
 for j in range(i):
  b=tail[j]
  if any(max(p[k] for p in a)<=min(p[k] for p in b) or max(p[k] for p in b)<=min(p[k] for p in a) for k in (0,1)):continue
  area=polyarea(intersection(a,b))
  if area>1e-8:overlaps.append((i,j,area))
report['tail_uv_overlap_pairs']=len(overlaps)
report['tail_uv_overlap_area']=sum(t[2] for t in overlaps)
report['tail_triangles']=len(tail)
report['rigging_status']='Not rigged; temporary deformation review only.'
with open(OUT+'/stage12_geometry_report.json','w',encoding='utf-8') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/blender_preview.py').read(),'blender_preview.py','exec'))
test=ob.copy();test.data=me.copy();test.name='_RaccoonBendReview';bpy.context.scene.collection.objects.link(test)
try:
 for rec in json.loads(ob['finger_layout_json']):
  side=rec['side'];C=Vector((side*.509,-.256,.035));W=Vector((side*.640,-.302,-.148));D=(W-C).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*side
  base=Vector(rec['base']);tip=Vector(rec['tip']);axis=(tip-base).normalized();length=(tip-base).length
  bend=axis.cross(N).normalized()
  for v in test.data.vertices:
   if test.data.attributes['anatomy_part'].data[v.index].value!=rec['part']:continue
   original=v.co.copy();co=original.copy();s=(original-base).dot(axis)/length
   # Apply distal then proximal rotation; smooth each hinge over adjacent support loops.
   for t,deg in [(.65,35),(.32,40)]:
    weight=max(0,min(1,(s-t+.055)/.11));weight=weight*weight*(3-2*weight)
    pivot=base+axis*(length*t);co=pivot+Quaternion(bend,math.radians(deg)*weight)@(co-pivot)
   v.co=co
 test.data.normals_split_custom_set([(0,0,0)]*len(test.data.loops));test.data.update()
 preview(test,OUT,[('qa_finger_bend',(1,-.7,-.3),(.67,-.34,-.22),.43)],850,hand_only=True)
finally:
 mesh=test.data;bpy.data.objects.remove(test,do_unlink=True);bpy.data.meshes.remove(mesh)
