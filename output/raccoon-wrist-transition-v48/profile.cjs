const fs=require('fs');const d=JSON.parse(fs.readFileSync('output/raccoon-wrist-transition-v48/mesh-before.json'));
const add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]),mul=(a,k)=>a.map(v=>v*k),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),len=a=>Math.sqrt(dot(a,a)),unit=a=>mul(a,1/Math.max(1e-15,len(a))),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],smooth=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x)};
const bp=n=>{const b=d.bind[d.bones.indexOf(n)];return [0,1,2].map(i=>-(b[i]*b[3]+b[4+i]*b[7]+b[8+i]*b[11])*100)};
const wrist=bp('RightHand'),axis=unit(sub(wrist,bp('RightLowerArm'))),groups=[],lookup=new Map(),vi=[];
d.vertices.forEach((v,i)=>{let p=mul(v,100),key=p.map(x=>x.toFixed(6)).join(',');if(!lookup.has(key)){lookup.set(key,groups.length);groups.push({p,ids:[],adj:new Set()})}let g=lookup.get(key);groups[g].ids.push(i);vi[i]=g});
for(let k=0;k<d.triangles.length;k+=3){let face=d.triangles.slice(k,k+3).map(i=>vi[i]);for(let i of face)for(let j of face)if(i!=j)groups[i].adj.add(j)}
for(let g of groups){const delta=sub(g.p,wrist),t=dot(delta,axis),rad=len(sub(delta,mul(axis,t))),w=d.weights[g.ids[0]];let primary=w.indices.reduce((sum,j,k)=>sum+([34,35].includes(j)?w.weights[k]:0),0);g.t=t;g.mask=rad<.085&&primary>.999?smooth((t+.075)/.035)*(1-smooth((t-.005)/.035)):0;}

const vaxis=unit(sub([0,1,0],mul(axis,axis[1]))),uaxis=unit(cross(axis,vaxis));
for(let t=-.13;t<.06;t+=.01){let a=groups.filter(g=>Math.abs(g.t-t)<.005&&len(sub(g.p,wrist))<.18);console.log(t.toFixed(2),a.length,a.length?[uaxis,vaxis].map(ax=>{let vals=a.map(g=>dot(sub(g.p,wrist),ax));return [Math.min(...vals),Math.max(...vals)].map(x=>+x.toFixed(4))}):[])}
