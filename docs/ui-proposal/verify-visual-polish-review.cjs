const assert=require('node:assert/strict'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.env.TANKTOWN_PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.TANKTOWN_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(__dirname,'../visual-polish-review.html')).href);
  const loaded=()=>page.waitForFunction(()=>{const image=document.querySelector('#capture');return image.complete&&image.naturalWidth>0});await loaded();
  for(const scene of ['candy','big','park','tree','chocolate','flash','fire','smoke','warning','normal']){
   await page.selectOption('#scene',scene);await loaded();
   assert.deepEqual(await page.locator('#capture').evaluate(i=>[i.naturalWidth,i.naturalHeight]),[1920,1080]);
   assert.equal(await page.locator('#previous').isDisabled(),scene!=='candy');
   assert((await page.locator('#caption').textContent()).includes('原寸表示'));
  }
  await page.selectOption('#scene','candy');await loaded();await page.locator('#previous').click();await loaded();
  assert.deepEqual(await page.locator('#capture').evaluate(i=>[i.naturalWidth,i.naturalHeight]),[1280,720]);
  assert.equal(await page.locator('#previous').getAttribute('aria-pressed'),'true');
  await page.locator('#current').click();await loaded();assert((await page.locator('#capture').getAttribute('src')).endsWith('polish-candy.png'));
  for(const width of [1920,1280,720,390]){await page.setViewportSize({width,height:1080});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));}
  await page.locator('#full-size').click();await page.waitForLoadState();assert(page.url().endsWith('tank-town-polish-candy.png'));assert.deepEqual(errors,[]);
  console.log('VISUAL POLISH BROWSER PASS: ten native views, historical comparison, four page widths, original image link, no errors');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
