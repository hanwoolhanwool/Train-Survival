const fs=require('fs');const j=JSON.parse(fs.readFileSync('output/raccoon-skin-v59/source.json'));const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x)},weights=j.weights.map(w=>{const o={};w.ids.forEach((b,k)=>{if(w.weights[k])o[b]=(o[b]||0)+w.weights[k]});return o});const orig=weights.map(o=>({...o}));const body=new Set([...j.triangles[1],...j.triangles[4]]);
function mix(a,b,t){const o={};for(const k in a)o[k]=(o[k]||0)+a[k]*(1-t);for(const k in b)o[k]=(o[k]||0)+b[k]*t;return o}
function torso(y){const ids=[1,2,3,4,5],ys=ids.map(i=>j.bones[i].pos[1]);for(let k=0;k<4;k++)if(y<ys[k+1]){const t=clamp((y-ys[k])/(ys[k+1]-ys[k]));return {[ids[k]]:1-t,[ids[k+1]]:t}}return {5:1}}
for(const i of body){const [x,y,z]=j.vertices[i],ax=Math.abs(x);if(y<.81)continue;let w=torso(y);let a=smooth((ax-.19)/.17)*smooth((y-.86)/.17);const arm={};for(const k in orig[i])if((x<0&&Number(k)>=13&&Number(k)<=31)||(x>=0&&Number(k)>=32&&Number(k)<=50))arm[k]=orig[i][k];let sum=Object.values(arm).reduce((a,b)=>a+b,0);if(sum<.01)arm[x<0?14:33]=1;else for(const k in arm)arm[k]/=sum;
// Transition into the existing sleeve and preserve the accepted outer arm boundary.
w=mix(w,arm,a);w=mix(w,orig[i],smooth((ax-.36)/.08));
// Scarf articulation belongs only to the front knot/tails, never the back panel.
const scarf=smooth((z-.39)/.07)*smooth((y-1.10)/.07)*(1-smooth((y-1.32)/.035));let sw={};for(const k of [11,12])if(orig[i][k])sw[k]=orig[i][k];sum=Object.values(sw).reduce((a,b)=>a+b,0);if(sum>0){for(const k in sw)sw[k]/=sum;w=mix(w,sw,scarf)}
const face=smooth((y-1.315)/.055);let head={6:1};if(y>1.67){const e={};for(const k of [7,8,9,10])if(orig[i][k])e[k]=orig[i][k];sum=Object.values(e).reduce((a,b)=>a+b,0);if(sum>0){for(const k in e)e[k]/=sum;head=mix(head,e,smooth((y-1.67)/.09)*smooth((ax-.15)/.09));}}
w=mix(w,head,face);weights[i]=mix(orig[i],w,smooth((y-.81)/.065));}
// Weld only duplicate body positions so UV/normal splits share identical influences.
const groups=new Map();for(const i of body){const key=j.vertices[i].map(x=>Math.round(x*100000)).join(',');if(!groups.has(key))groups.set(key,[]);groups.get(key).push(i)}for(const ids of groups.values()){const w={};for(const i of ids)for(const k in weights[i])w[k]=(w[k]||0)+weights[i][k]/ids.length;for(const i of ids)weights[i]=w;}
// Match explicit shared material-boundary points to the repaired body, including cuffs.
const allGroups=new Map();j.vertices.forEach((p,i)=>{const key=p.map(x=>Math.round(x*100000)).join(',');if(!allGroups.has(key))allGroups.set(key,[]);allGroups.get(key).push(i)});for(const ids of allGroups.values()){const donor=ids.find(i=>body.has(i));if(donor!==undefined)for(const i of ids)weights[i]=weights[donor];}
// Floating point copies can straddle a quantization boundary. Match by distance
// across neighboring cells, without moving vertices or merging UV/normal data.
const cells=new Map(),cellSize=.00001;for(const i of body){const key=j.vertices[i].map(x=>Math.floor(x/cellSize)).join(',');if(!cells.has(key))cells.set(key,[]);cells.get(key).push(i)}
for(let i=0;i<weights.length;i++){if(body.has(i))continue;const p=j.vertices[i],c=p.map(x=>Math.floor(x/cellSize));let best=-1,dist=4e-12;for(let x=-1;x<=1;x++)for(let y=-1;y<=1;y++)for(let z=-1;z<=1;z++){const ids=cells.get([c[0]+x,c[1]+y,c[2]+z].join(','))||[];for(const k of ids){const q=j.vertices[k];let dd=0;for(let a=0;a<3;a++)dd+=(p[a]-q[a])**2;if(dd<=dist){dist=dd;best=k}}}if(best>=0)weights[i]=weights[best];}
let changed=0;const out=weights.map((w,i)=>{let e=Object.entries(w).filter(([k,w])=>w>1e-7).sort((a,b)=>b[1]-a[1]).slice(0,4);const sum=e.reduce((a,b)=>a+b[1],0);while(e.length<4)e.push(['0',0]);if(e.some(([k,v])=>Math.abs(v/sum-(orig[i][k]||0))>1e-5))changed++;return e.flatMap(([k,v])=>[Number(k),v/sum])});fs.writeFileSync('output/raccoon-skin-v59/weights.json',JSON.stringify(out));console.log('changed',changed);


