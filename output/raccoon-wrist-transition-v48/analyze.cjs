const fs=require('fs');const d=JSON.parse(fs.readFileSync('output/raccoon-wrist-transition-v48/mesh-before.json'));
const add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]),mul=(a,k)=>a.map(v=>v*k),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),len=a=>Math.sqrt(dot(a,a)),unit=a=>mul(a,1/len(a));
const bp=n=>{const b=d.bind[d.bones.indexOf(n)];return [0,1,2].map(i=>-(b[i]*b[3]+b[4+i]*b[7]+b[8+i]*b[11])*100);};
const wrist=bp('RightHand'),elbow=bp('RightLowerArm'),axis=unit(sub(wrist,elbow));
const verts=d.vertices.map(v=>mul(v,100)),groups=new Map();
verts.forEach((p,i)=>{let key=p.map(x=>x.toFixed(6)).join(',');if(!groups.has(key))groups.set(key,[]);groups.get(key).push(i)});
console.log({wrist,elbow,axis});
let rows=[];for(let ids of groups.values()){let i=ids[0],p=verts[i],delta=sub(p,wrist),t=dot(delta,axis),radius=len(sub(delta,mul(axis,t)));if(t>-.09&&t<.07&&radius<.10){rows.push({i,ids,t:+t.toFixed(5),r:+radius.toFixed(5),p:p.map(x=>+x.toFixed(5)),b:d.baked[i],w:d.weights[i].indices.map((j,k)=>[d.bones[j],d.weights[i].weights[k]]).filter(x=>x[1]>0)});}}
rows.sort((a,b)=>a.t-b.t);fs.writeFileSync('output/raccoon-wrist-transition-v48/wrist-rows.json',JSON.stringify(rows,null,2));
for(let t=-.09;t<.07;t+=.01){let r=rows.filter(r=>r.t>=t&&r.t<t+.01);console.log(t.toFixed(2),r.length,r.length?[Math.min(...r.map(x=>x.r)),Math.max(...r.map(x=>x.r))]:[]);}
console.log(rows.filter(r=>Math.abs(r.t)<.015));
