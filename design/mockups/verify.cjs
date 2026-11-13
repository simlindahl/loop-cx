const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1440,height:1000},deviceScaleFactor:1});
 const errors=[];const external=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/*',r=>{const u=r.request().url();if(!u.startsWith('file:')&&!u.startsWith('data:')){external.push(u);r.abort();}else r.continue();});
 await page.goto('file://'+__dirname+'/index.html');
 await page.evaluate(()=>document.fonts.ready);
 const structural=await page.evaluate(()=>{
  const ids=[...document.querySelectorAll('[id]')].map(x=>x.id);
  return {screens:document.querySelectorAll('.review-screen').length,duplicateIds:ids.filter((x,i)=>ids.indexOf(x)!==i),brokenLinks:[...document.querySelectorAll('a[href^="#"]')].map(a=>a.getAttribute('href')).filter(h=>!document.getElementById(h.slice(1))),fontsLoaded:document.fonts.check('16px "Space Grotesk"'),scripts:document.scripts.length};
 });
 const layout=[];
 for(const width of [1440,1024,700,390,360]){
  await page.setViewportSize({width,height:1000});
  const overflow=await page.evaluate(()=>({document:document.documentElement.scrollWidth>innerWidth+1,items:[...document.querySelectorAll('.phone,.body,.live,.statistics,.run-header,.workout-main,table,.row')].filter(e=>e.scrollWidth>e.clientWidth+2).map(e=>({screen:e.closest('.review-screen')?.id,tag:e.className,width:e.clientWidth,scroll:e.scrollWidth}))}));
  layout.push({width,...overflow});
 }
 await page.setViewportSize({width:1440,height:1000});
 fs.mkdirSync(__dirname+'/qa',{recursive:true});
 for(const id of ['training','work','edit-intervals','easy-run','settings','run-detail','zones','notification-work','sensor-loss','share','history','warm-up','loop-about','loop-start','loop-workouts','loop-strength','loop-learn','loop-sources']){
  await page.locator('#'+id+' .phone').screenshot({path:__dirname+'/qa/'+id+'.png'});
 }
 const knowledgeNavigation=[];
 for(const [selector,expected] of [
  ['#settings a[href="#loop-about"]','#loop-about'],
  ['#loop-about a[href="#loop-start"]','#loop-start'],
  ['#loop-start .action.primary','#loop-about'],
  ['#loop-about a[href="#loop-workouts"]','#loop-workouts'],
  ['#loop-workouts a[href="#loop-strength"]','#loop-strength'],
  ['#loop-strength .back','#loop-about'],
  ['#loop-about a[href="#loop-learn"]','#loop-learn'],
  ['#loop-learn a[href="#loop-sources"]','#loop-sources'],
  ['#loop-sources .back','#loop-about'],
  ['#loop-about .back','#settings']
 ]){
  await page.locator(selector).click();
  knowledgeNavigation.push({expected,actual:await page.evaluate(()=>location.hash)});
 }
 await page.locator('#training .action.round').first().click();
 const click=await page.evaluate(()=>location.hash);
 await page.setViewportSize({width:390,height:900});
 // Approximate 200% OS text scaling by doubling computed type sizes, preserving dp-like spacing.
 await page.evaluate(()=>{
  const elements=[...document.querySelectorAll('.phone *')].filter(e=>e.children.length===0||e.matches('a,button,label,h2,h3,p,span,strong,small,th,td,input'));
  const sizes=elements.map(e=>getComputedStyle(e).fontSize);
  elements.forEach((e,i)=>e.style.fontSize=(parseFloat(sizes[i])*2)+'px');
 });
 const scaled=await page.evaluate(()=>[...document.querySelectorAll('.phone')].filter(e=>e.scrollWidth>e.clientWidth+2).map(e=>({id:e.closest('.review-screen').id,w:e.clientWidth,scroll:e.scrollWidth})));
 await page.locator('#work .phone').screenshot({path:__dirname+'/qa/work-text-200.png'});
 const report={structural,layout,knowledgeNavigation,click,scaled,errors,external};
 fs.writeFileSync(__dirname+'/qa/report.json',JSON.stringify(report,null,2)+'\n',{encoding:'utf8'});
 if(structural.duplicateIds.length || structural.brokenLinks.length || !structural.fontsLoaded || structural.scripts || external.length || errors.length || click!=='#warm-up' || knowledgeNavigation.some(x=>x.actual!==x.expected) || layout.some(x=>x.document||x.items.length)) process.exitCode=1;
 console.log(JSON.stringify(report,null,2));
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
