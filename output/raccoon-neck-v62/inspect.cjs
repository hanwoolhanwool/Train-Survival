const fs=require('fs'),ts=JSON.parse(fs.readFileSync('output/raccoon-neck-v62/collar-triangles.json'));console.log(ts.filter(t=>t.k>18000).map(t=>({k:t.k,ids:t.ids,p:t.p,rgb:t.rgb})));
