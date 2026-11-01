import bpy,json,math
from mathutils import Vector
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon'];parts=me.attributes['anatomy_part']
adj={v.index:set() for v in me.vertices}
for e in me.edges:
    a,b=e.vertices;adj[a].add(b);adj[b].add(a)
fingers=json.loads(ob['finger_layout_json']);layouts=json.loads(ob['elbow_layout_json'])
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for f in fingers:
    ids={v.index for v in me.vertices if parts.data[v.index].value==f['part']};palm=100 if f['side']==1 else 200
    ring={i for i in ids if any(parts.data[j].value==palm for j in adj[i])};rings=[];seen=set()
    while ring:
        rings.append(sorted(ring));seen|=ring;ring={j for i in ring for j in adj[i] if j in ids and j not in seen}
    assert [len(r) for r in rings]==[8,8,8,8,8,8,8,1],(f['name'],[len(r) for r in rings])
    centers=[sum((me.vertices[i].co for i in r),Vector())/len(r) for r in rings]
    points=[Vector(f['base']),centers[1].lerp(centers[2],.5),centers[3].lerp(centers[4],.5),Vector(f['tip'])]
    side='Left' if f['side']==1 else 'Right';W=Vector(next(r for r in layouts if r['side']==f['side'])['wrist']);D=(W-Vector((f['side']*.509,-.256,.035))).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*f['side']
    for i,seg in enumerate(['Proximal','Intermediate','Distal']):
        eb=rig.data.edit_bones[side+f['name'].capitalize()+seg];eb.head=points[i];eb.tail=points[i+1];axis=(eb.tail-eb.head).normalized().cross(N).normalized();eb.align_roll(axis.cross((eb.tail-eb.head).normalized()))
bpy.ops.object.mode_set(mode='OBJECT')
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
for v in me.vertices:
    if parts.data[v.index].value!=0:continue
    w={deform[g.group]:g.weight for g in v.groups if g.group in deform}
    for side in ['Left','Right']:
        x=w.pop(side+'LowerArm',0)+w.pop(side+'Hand',0)
        for n in list(w):
            if n.startswith(side) and any(s in n for s in ['Proximal','Intermediate','Distal']):x+=w.pop(n)
        w[side+'UpperArm']=w.get(side+'UpperArm',0)+x
    # The entire cuff and adjacent outer sleeve move together.
    side='Left' if v.co.x>0 else 'Right'
    if abs(v.co.x)>.30 and -.035<v.co.z<.26 and w.get(side+'UpperArm',0)>.70:w={side+'UpperArm':1}
    w={n:x for n,x in w.items() if x>1e-6};tot=sum(w.values())
    for idx in deform:ob.vertex_groups[idx].remove([v.index])
    for n,x in w.items():ob.vertex_groups[n].add([v.index],x/tot,'REPLACE')
rig.update_tag();bpy.context.view_layer.update();print('Finger joint rings corrected; sleeve/upper-arm weights cleaned.')
