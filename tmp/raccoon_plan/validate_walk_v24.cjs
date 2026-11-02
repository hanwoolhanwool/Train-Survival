const fs=require('fs');
const base='output/raccoon-walk-polish-v24';
const report={};
for(const name of ['contact_walk','transitions']){
 const d=JSON.parse(fs.readFileSync(`${base}/after/${name}/motion_exchange.json`,'utf8'));
 const planted=[],stops=[],heights=[],errors=[];
 for(let i=1;i<d.frames.length;i++){
  const f=d.frames[i],p=d.frames[i-1];
  heights.push(f.bones.find(b=>b.name==='Hips').position.y-f.root.y);
  errors.push(...f.footError);
  for(let side=0;side<2;side++){
   if(f.planted?.[side]&&p.planted?.[side])planted.push(f.soleSpeed[side]);
   if(f.label==='Stop'&&p.label==='Stop'&&f.frame%60>20)stops.push(f.soleSpeed[side]);
  }
 }
 const mean=a=>a.reduce((s,v)=>s+v,0)/Math.max(1,a.length);
 report[name]={frames:d.frames.length,phaseRange:[Math.min(...d.frames.map(f=>f.phase)),Math.max(...d.frames.map(f=>f.phase))],
  minimumSoleClearance:Math.min(...d.frames.flatMap(f=>f.soleClearance)),
  hipHeightRange:[Math.min(...heights),Math.max(...heights)],maximumFootTargetError:Math.max(...errors),
  fullContactSamples:planted.length,meanFullContactSpeed:mean(planted),maxFullContactSpeed:Math.max(...planted),
  maxStopSpeed:stops.length?Math.max(...stops):null};
}
const walk=report.contact_walk,transition=report.transitions;
const before=JSON.parse(fs.readFileSync('output/raccoon-walk-candidates-v23/after/armed_calm/report.json','utf8')).phases[0];
const after=JSON.parse(fs.readFileSync(`${base}/after/contact_walk/report.json`,'utf8')).phases[0];
report.comparison={beforeMeanNearFloorSpeed:before.meanContactSlide,afterMeanNearFloorSpeed:after.meanContactSlide,
 reduction:1-after.meanContactSlide/before.meanContactSlide,maxGripSeparationError:after.maxGripError};
report.checks={
 activeAnimation:walk.phaseRange[1]-walk.phaseRange[0]>.95,
 adequateContacts:walk.fullContactSamples>400,
 stableFullContact:walk.meanFullContactSpeed<.01,
 groundClearance:walk.minimumSoleClearance>=0&&transition.minimumSoleClearance>=0,
 preservedWalkBob:walk.hipHeightRange[1]-walk.hipHeightRange[0]<.035,
 walkingReach:walk.maximumFootTargetError<.005,
 transitionReach:transition.maximumFootTargetError<.025,
 stoppedFeetStable:transition.maxStopSpeed<.003,
 gripPreserved:after.maxGripError<.001,
 contactImproved:report.comparison.reduction>.35
};
fs.writeFileSync(`${base}/quality_summary.json`,JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
if(Object.values(report.checks).some(v=>!v))process.exitCode=1;
