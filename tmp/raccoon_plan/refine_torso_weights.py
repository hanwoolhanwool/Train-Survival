import bpy, math
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon']
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
tail={i for p in me.polygons if me.attributes['edit_region'].data[p.index].value==3 for i in p.vertices}
cuffs={i for p in me.polygons if me.attributes['edit_region'].data[p.index].value==4 for i in p.vertices}
weights=[]
for v in me.vertices:
    w={deform[g.group]:g.weight for g in v.groups if g.group in deform}
    if me.attributes['anatomy_part'].data[v.index].value==0 and v.index not in tail|cuffs:
        if -.04<v.co.z<.4:
            factor=smooth((abs(v.co.x)-.10)/.18)
            for n in list(w):
                if 'UpperArm' in n or 'Shoulder' in n:
                    x=w[n]*(1-factor);w[n]-=x;target='UpperChest' if v.co.z>.15 else 'Chest';w[target]=w.get(target,0)+x
        if -.74<v.co.z<-.48:
            factor=smooth((-.48-v.co.z)/.12)*smooth((v.co.z+.74)/.03);target=('Left' if v.co.x>0 else 'Right')+'LowerLeg'
            w={n:x*(1-factor) for n,x in w.items()};w[target]=w.get(target,0)+factor
    weights.append(w)
adj={v.index:[] for v in me.vertices}
for e in me.edges:
    a,b=e.vertices;adj[a].append(b);adj[b].append(a)
active={v.index for v in me.vertices if me.attributes['anatomy_part'].data[v.index].value==0 and v.index not in tail|cuffs and -.035<v.co.z<.32 and abs(v.co.x)>.20}
for _ in range(6):
    nxt=[w.copy() for w in weights]
    for i in active:
        neighbors=[j for j in adj[i] if me.attributes['anatomy_part'].data[j].value==0]
        if not neighbors:continue
        w={n:x*.55 for n,x in weights[i].items()}
        for j in neighbors:
            for n,x in weights[j].items():w[n]=w.get(n,0)+x*.45/len(neighbors)
        nxt[i]=w
    weights=nxt
for v,w in zip(me.vertices,weights):
    w=dict(sorted(((n,x) for n,x in w.items() if x>1e-6),key=lambda x:x[1],reverse=True)[:4]);total=sum(w.values())
    for g in list(v.groups):
        if g.group in deform:ob.vertex_groups[g.group].remove([v.index])
    for n,x in w.items():ob.vertex_groups[n].add([v.index],x/total,'REPLACE')
bpy.context.view_layer.update();print('Torso, shoulder and trouser hem weights refined.')
