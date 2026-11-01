import bpy,math,heapq,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/qa_rig_v1.py',encoding='utf-8').read().split('results=[]')[0],'qa_helpers','exec'))
def pairs():
    ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());ev.data.calc_loop_triangles();ts=[tuple(t.vertices) for t in ev.data.loop_triangles]
    tree=BVHTree.FromPolygons([v.co for v in ev.data.vertices],ts,all_triangles=True)
    return {(i,j) for i,j in tree.overlap(tree) if i<j and not set(ts[i]).intersection(ts[j])},ts
neutral();baseline,ts=pairs();seeds=set();names=['crouch','reach','stride','tail_turn']
for name in names:
    pose(name);pp,ts=pairs()
    for i,j in pp-baseline:seeds.update(ts[i]+ts[j])
neutral()
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
weights=[{deform[g.group]:g.weight for g in v.groups if g.group in deform} for v in me.vertices];original=[w.copy() for w in weights]
adj={v.index:[] for v in me.vertices};dist={i:0 for i in seeds};queue=[(0,i) for i in seeds];heapq.heapify(queue)
for e in me.edges:
    a,b=e.vertices;l=(me.vertices[a].co-me.vertices[b].co).length;adj[a].append((b,l));adj[b].append((a,l))
while queue:
    d,i=heapq.heappop(queue)
    if d>dist[i] or d>.07:continue
    for j,l in adj[i]:
        if d+l<dist.get(j,1e9):dist[j]=d+l;heapq.heappush(queue,(d+l,j))
active={i:math.exp(-(d/.030)**2) for i,d in dist.items() if d<.07 and me.attributes['anatomy_part'].data[i].value==0}
def apply(weights):
    for i in active:
        v=me.vertices[i];w=dict(sorted(weights[i].items(),key=lambda x:x[1],reverse=True)[:4]);total=sum(w.values())
        for g in list(v.groups):
            if g.group in deform:ob.vertex_groups[g.group].remove([i])
        for n,x in w.items():
            if x>1e-8:ob.vertex_groups[n].add([i],x/total,'REPLACE')
    ob.update_tag();bpy.context.view_layer.update()
def score():
    counts=[]
    for n in names:pose(n);p,_=pairs();counts.append(len(p-baseline))
    neutral();return counts
best_score=score();best=[w.copy() for w in weights];print('BEFORE',best_score,'active',len(active))
for iteration in range(16):
    nxt=[w.copy() for w in weights]
    for i,strength in active.items():
        neighbors=[(j,1/max(.003,l)) for j,l in adj[i] if me.attributes['anatomy_part'].data[j].value==0];tot=sum(x for j,x in neighbors)
        if not tot:continue
        alpha=.38*strength;w={n:x*(1-alpha) for n,x in weights[i].items()}
        for j,f in neighbors:
            for n,x in weights[j].items():w[n]=w.get(n,0)+x*alpha*f/tot
        nxt[i]=w
    weights=nxt
    if iteration in [1,3,7,15]:
        apply(weights);counts=score();print('PASS',iteration+1,counts)
        if sum(counts)<sum(best_score):best_score=counts;best=[w.copy() for w in weights]
apply(best);print('ACCEPTED',best_score)
