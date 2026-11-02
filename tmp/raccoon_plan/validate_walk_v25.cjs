const fs=require('fs');
const base='output/raccoon-walk-terrain-v25';
const result={};
for(const revision of ['before','after'])for(const test of ['turns','slopes','cross_slope','slope_turn']){
 const file=`${base}/${revision}/${test}/motion_exchange.json`;if(!fs.existsSync(file))continue;
 const data=JSON.parse(fs.readFileSync(file,'utf8')),report=JSON.parse(fs.readFileSync(`${base}/${revision}/${test}/report.json`,'utf8'));
 const phases={};let maxHipStep=0;const planted=[];
 for(let i=1;i<data.frames.length;i++){
  const f=data.frames[i],p=data.frames[i-1];
  const hip=f.bones.find(b=>b.name==='Hips'),old=p.bones.find(b=>b.name==='Hips');
  maxHipStep=Math.max(maxHipStep,Math.abs((hip.position.y-f.root.y)-(old.position.y-p.root.y)));
  if(!phases[f.label])phases[f.label]={frames:0,minClearance:999,maxError:0,fullContactSamples:0,sumContactSpeed:0};
  const phase=phases[f.label];phase.frames++;phase.minClearance=Math.min(phase.minClearance,...f.soleClearance);phase.maxError=Math.max(phase.maxError,...f.footError);
  for(let side=0;side<2;side++)if(f.planted[side]&&p.planted[side]){planted.push(f.soleSpeed[side]);phase.fullContactSamples++;phase.sumContactSpeed+=f.soleSpeed[side];}
 }
 for(const p of Object.values(phases)){p.meanContactSpeed=p.sumContactSpeed/Math.max(1,p.fullContactSamples);delete p.sumContactSpeed;}
 result[`${revision}/${test}`]={frames:data.frames.length,phases,lastRoot:data.frames.at(-1).root,maxHipHeightStep:maxHipStep,
  minFootClearance:Math.min(...data.frames.flatMap(f=>f.soleClearance)),maxFootError:Math.max(...data.frames.flatMap(f=>f.footError)),
  fullContactSamples:planted.length,meanFullContactSpeed:planted.reduce((s,v)=>s+v,0)/Math.max(1,planted.length),maxFullContactSpeed:Math.max(...planted),
  minTailClearance:Math.min(...report.phases.map(p=>p.minTailClearance)),maxGripError:Math.max(...report.phases.map(p=>p.maxGripError))};
}
const checks={};
for(const test of ['turns','slopes','cross_slope','slope_turn']){
 const r=result[`after/${test}`];
 checks[test]={frames:r.frames===1200,groundClearance:r.minFootClearance>=0,
  tailClearance:r.minTailClearance>=.02,reach:r.maxFootError<.015,
  contactCoverage:r.fullContactSamples>800,stableContacts:r.meanFullContactSpeed<.01,
  grip:r.maxGripError<.001,hipContinuity:r.maxHipHeightStep<.025};
}
checks.courseCoverage={uphill:result['after/slopes'].phases['Uphill 12']?.frames>=250,
 downhill:result['after/slopes'].phases['Downhill 12']?.frames>=250,
 turn180:result['after/turns'].phases['Turn left 180']?.frames>=150,
 completesSlope:result['after/slopes'].lastRoot.z>12};
result.checks=checks;
fs.writeFileSync(`${base}/quality_summary.json`,JSON.stringify(result,null,2));
for(const [k,v] of Object.entries(result))console.log(k,JSON.stringify({...v,phases:undefined}));
if(Object.values(checks).some(group=>Object.values(group).some(v=>!v)))process.exitCode=1;
