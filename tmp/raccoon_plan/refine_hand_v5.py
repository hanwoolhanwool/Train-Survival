import bpy,json,math
from mathutils import Vector
source=bpy.data.objects['raccoon']; review=bpy.data.objects['raccoon_GripReview_v4']
if not bpy.data.meshes.get('Raccoon_Hand_v4_Backup'):
    backup=source.data.copy();backup.name='Raccoon_Hand_v4_Backup';backup.use_fake_user=True
baseline=bpy.data.meshes['Raccoon_Hand_v4_Backup']
me=source.data; parts=me.attributes['anatomy_part']
adj={v.index:set() for v in me.vertices}
for e in me.edges:
    a,b=e.vertices;adj[a].add(b);adj[b].add(a)
coords=[v.co.copy() for v in baseline.vertices]
hand_ids={v.index for v in me.vertices if parts.data[v.index].value in [100,101,102,103,104,105,200,201,202,203,204,205]}
for f in json.loads(source['finger_layout_json']):
    side=f['side'];palm=100 if side==1 else 200
    ids={i for i in hand_ids if parts.data[i].value==f['part']}
    ring={i for i in ids if any(parts.data[j].value==palm for j in adj[i])};rings=[];seen=set()
    while ring:
        rings.append(sorted(ring));seen|=ring;ring={j for i in ring for j in adj[i] if j in ids and j not in seen}
    # Replace the rectangular sections by elliptical sections, keeping each centreline.
    for ri,ring in enumerate(rings[:-1]):
        center=sum((coords[i] for i in ring),Vector())/len(ring)
        before=Vector(f['base']) if ri==0 else sum((coords[i] for i in rings[ri-1]),Vector())/len(rings[ri-1])
        after=sum((coords[i] for i in rings[ri+1]),Vector())/len(rings[ri+1])
        tangent=(after-before).normalized()
        u=Vector((0,1,0));u=(u-tangent*u.dot(tangent)).normalized();n=tangent.cross(u).normalized()
        offsets=[coords[i]-center for i in ring]
        ru=max(abs(o.dot(u)) for o in offsets);rn=max(abs(o.dot(n)) for o in offsets)
        for i,o in zip(ring,offsets):
            a=o.dot(u)/max(ru,1e-6);b=o.dot(n)/max(rn,1e-6);length=math.sqrt(a*a+b*b)
            radial=(u*(a*ru)+n*(b*rn))/max(length,1e-6)
            coords[i]=center+radial+tangent*o.dot(tangent)
for side in [1,-1]:
    palm=100 if side==1 else 200;W=Vector((side*.640,-.302,-.148));C=Vector((side*.509,-.256,.035));D=(W-C).normalized()
    # Relax the palm edge and thumb saddle while anchoring the wrist seam.
    for iteration in range(6):
        result=[v.copy() for v in coords]
        for i in hand_ids:
            if parts.data[i].value!=palm:continue
            t=(baseline.vertices[i].co-W).dot(D)
            factor=min(1,max(0,t/.025))*.36
            average=sum((coords[j] for j in adj[i]),Vector())/len(adj[i])
            result[i]=coords[i].lerp(average,factor)
        coords=result
for ob in [source,review]:
    mesh=ob.data
    for i in hand_ids:
        delta=coords[i]-baseline.vertices[i].co
        for key,bkey in zip(mesh.shape_keys.key_blocks,baseline.shape_keys.key_blocks):key.data[i].co=bkey.data[i].co+delta
        mesh.vertices[i].co=coords[i]
    mesh.update()
    # Imported split normals must be recalculated on the edited hand surfaces.
    normals=[tuple(n.vector) for n in mesh.corner_normals]
    for loop in mesh.loops:
        if loop.vertex_index in hand_ids:normals[loop.index]=(0,0,0)
    mesh.normals_split_custom_set(normals)
    mesh.update()
bpy.context.view_layer.update()
print('Refined hand sections, palm contour and hand normals:',len(hand_ids),'vertices; topology/UV/shape keys retained')
