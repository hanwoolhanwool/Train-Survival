const fs=require('fs');let s=fs.readFileSync('output/raccoon-wrist-transition-v48/patch.cjs','utf8');let cut=s.indexOf('let pts=groups.map');s=s.slice(0,cut)+`
const vaxis=unit(sub([0,1,0],mul(axis,axis[1]))),uaxis=unit(cross(axis,vaxis));
for(let t=-.13;t<.06;t+=.01){let a=groups.filter(g=>Math.abs(g.t-t)<.005&&len(sub(g.p,wrist))<.18);console.log(t.toFixed(2),a.length,a.length?[uaxis,vaxis].map(ax=>{let vals=a.map(g=>dot(sub(g.p,wrist),ax));return [Math.min(...vals),Math.max(...vals)].map(x=>+x.toFixed(4))}):[])}
`;
fs.writeFileSync('output/raccoon-wrist-transition-v48/profile.cjs',s);
