import bpy,json,math
from mathutils import Vector
ob=bpy.data.objects['raccoon'];me=ob.data;parts=me.attributes['anatomy_part'];rig=bpy.data.objects['RIG_Raccoon']
adj={v.index:set() for v in me.vertices}
for e in me.edges:
    a,b=e.vertices;adj[a].add(b);adj[b].add(a)
fingers=json.loads(ob['finger_layout_json'])
for f in fingers:
    if f['name']!='thumb':continue
    ids={v.index for v in me.vertices if parts.data[v.index].value==f['part']};palm=100 if f['side']==1 else 200
    ring={i for i in ids if any(parts.data[j].value==palm for j in adj[i])};rings=[];seen=set()
    while ring:
        rings.append(sorted(ring));seen|=ring;ring={j for i in ring for j in adj[i] if j in ids and j not in seen}
    first=rings[0];base={i:next(j for j in adj[i] if parts.data[j].value==palm) for i in first}
    B=Vector(f['base']);T=Vector(f['tip']);D=(Vector((f['side']*.640,-.302,-.148))-Vector((f['side']*.509,-.256,.035))).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();axis=-U
    P=B+axis*.065;Q=T-(T-B).normalized()*.025
    def center(t):return B*(1-t)**3+P*(3*(1-t)**2*t)+Q*(3*(1-t)*t*t)+T*t**3
    def tangent(t):return ((P-B)*(3*(1-t)**2)+(Q-P)*(6*(1-t)*t)+(T-Q)*(3*t*t)).normalized()
    previous=base
    for ri,t in enumerate([.19,.30,.39,.59,.68,.86,.96]):
        current={}
        for i in rings[ri]:
            bi=base[i] if ri==0 else previous[next(j for j in adj[i] if j in previous)]
            current[i]=bi;offset=me.vertices[bi].co-B;offset-=axis*offset.dot(axis)
            radius=1-.32*t
            if t>.9:radius*=.68
            co=center(t)+axis.rotation_difference(tangent(t))@offset*radius
            delta=co-me.vertices[i].co
            for key in me.shape_keys.key_blocks:key.data[i].co+=delta
            me.vertices[i].co=co
        previous=current
me.update();bpy.context.view_layer.update();print('Thumb root tangent relaxed outward; topology and UV unchanged.')
