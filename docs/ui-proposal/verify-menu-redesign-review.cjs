const assert=require('node:assert/strict'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.env.TANKTOWN_PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.TANKTOWN_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(__dirname,'../menu-redesign-review.html')).href);
  const loaded=()=>page.waitForFunction(()=>{const image=document.querySelector('#capture');return image.complete&&image.naturalWidth>0});await loaded();
  for(const state of ['howtoconcept','resultsconcept','howto','results','maximum','howto720','results720','maximum720','howto5x4','results5x4','title','title5x4']){
   await page.selectOption('#state',state);await loaded();const size=await page.locator('#capture').evaluate(i=>[i.naturalWidth,i.naturalHeight]);
   if(state.includes('concept')){assert(size[0]>=1200&&size[1]>=600)}else assert.deepEqual(size,state.includes('720')?[1280,720]:state.includes('5x4')?[1280,1024]:[1920,1080]);
  }
  for(const [state,before] of [['howto','tank-town-title-clean-help.png'],['results','tank-town-results-before-redesign.png'],['title','tank-town-title-clean-2.png']]){
   await page.selectOption('#state',state);await loaded();await page.locator('#previous').click();await loaded();assert(await page.locator('#state').isDisabled());assert((await page.locator('#capture').getAttribute('src')).endsWith(before));await page.locator('#current').click();await loaded();assert(await page.locator('#state').isEnabled());
  }
  for(const width of [1920,1280,720,390]){await page.setViewportSize({width,height:1080});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));}
  await page.selectOption('#state','results');await loaded();await page.locator('#full-size').click();await page.waitForLoadState();assert(page.url().endsWith('tank-town-menu-results.png'));assert.deepEqual(errors,[]);
  console.log('MENU BROWSER PASS: two generated concepts and ten Unity captures; three before/after comparisons; four page widths; original image link; no errors');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
