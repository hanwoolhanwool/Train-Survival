const fs=require('fs'),j=JSON.parse(fs.readFileSync('output/raccoon-skin-v59/source.json')),old=JSON.parse(fs.readFileSync('output/raccoon-skin-v59/weights.json')),ids=JSON.parse(fs.readFileSync('output/raccoon-chin-v60/face-triangle-vertices.json'));const out=old.map(w=>w.slice()),seeds=ids.map(i=>j.vertices[i]);const changed=[];
// Bind the complete fur surface triangles, including the atlas boundary corners.
// Matching coincident copies preserves UV/normal seams without altering geometry.
for(let i=0;i<j.vertices.length;i++){const p=j.vertices[i];if(!seeds.some(q=>p.reduce((s,x,k)=>s+(x-q[k])**2,0)<4e-12))continue;if(old[i][0]===6&&old[i][1]===1)continue;out[i]=[6,1,0,0,0,0,0,0];changed.push(i)}
fs.writeFileSync('output/raccoon-chin-v60/weights.json',JSON.stringify(out));fs.writeFileSync('output/raccoon-chin-v60/changed-vertices.json',JSON.stringify(changed));console.log('Changed chin surface vertices',changed.length);
