const assert=require('node:assert/strict'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.env.TANKTOWN_PLAYWRIGHT_MODULE||'playwright');
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:process.env.TANKTOWN_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  try{
    const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname,'../title-screen-review.html')).href);
    const loaded=()=>page.waitForFunction(()=>{const image=document.querySelector('#capture');return image.complete&&image.naturalWidth>0});await loaded();
    for(const [scene,size] of Object.entries({town:[1920,1080],big:[1920,1080],candy:[1920,1080],amusement:[1920,1080],'720':[1280,720],'900':[1440,900],'5x4':[1280,1024],help:[1280,720]})){
      await page.selectOption('#state',scene);await loaded();assert.deepEqual(await page.locator('#capture').evaluate(i=>[i.naturalWidth,i.naturalHeight]),size);
    }
    await page.locator('#previous').click();await loaded();assert(await page.locator('#state').isDisabled());assert((await page.locator('#capture').getAttribute('src')).endsWith('tank-town-amusement-01-title.png'));
    await page.locator('#current').click();await loaded();assert(await page.locator('#state').isEnabled());
    for(const width of [1920,1280,720,390]){await page.setViewportSize({width,height:1080});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));}
    await page.selectOption('#state','candy');await loaded();await page.locator('#full-size').click();await page.waitForLoadState();assert(page.url().endsWith('tank-town-title-clean-2.png'));assert.deepEqual(errors,[]);
    console.log('TITLE BROWSER PASS: eight real Unity captures; previous/current; four page widths; full-size link; no errors');
  }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
