import bpy,json,math
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon'];part=me.attributes['anatomy_part']
adj={v.index:set() for v in me.vertices}
for e in me.edges:
    a,b=e.vertices;adj[a].add(b);adj[b].add(a)
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
def assign(i,w):
    for g in list(me.vertices[i].groups):
        if g.group in deform:ob.vertex_groups[g.group].remove([i])
    total=sum(w.values())
    for n,x in w.items():
        if x>0:ob.vertex_groups[n].add([i],x/total,'REPLACE')
for f in json.loads(ob['finger_layout_json']):
    ids={v.index for v in me.vertices if part.data[v.index].value==f['part']};palm=100 if f['side']==1 else 200
    ring={i for i in ids if any(part.data[j].value==palm for j in adj[i])};rings=[];seen=set()
    while ring:
        rings.append(sorted(ring));seen|=ring;ring={j for i in ring for j in adj[i] if j in ids and j not in seen}
    side='Left' if f['side']==1 else 'Right';names=[side+'Hand']+[side+f['name'].capitalize()+s for s in ['Proximal','Intermediate','Distal']]
    roots={j for i in rings[0] for j in adj[i] if part.data[j].value==palm}
    for i in roots:assign(i,{names[0]:.75,names[1]:.25})
    for q,ring in zip([.19,.30,.39,.59,.68,.86,.96,1],rings):
        w={n:math.exp(-.5*((q-c)/.20)**2) for n,c in zip(names,[-.13,.17,.50,.88])}
        w={n:x for n,x in w.items() if x>.012}
        for i in ring:assign(i,w)
bpy.context.view_layer.update();print('Finger weights smoothed along the actual eight ring sequences.')
