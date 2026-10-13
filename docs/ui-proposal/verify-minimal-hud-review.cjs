const assert=require('node:assert/strict'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.env.TANKTOWN_PLAYWRIGHT_MODULE||'playwright');
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:process.env.TANKTOWN_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  try{
    const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.goto(pathToFileURL(path.join(__dirname,'../minimal-hud-review.html')).href);
    const loaded=()=>page.waitForFunction(()=>{const image=document.querySelector('#capture');return image.complete&&image.naturalWidth>0});
    await loaded();
    for(const scene of ['refined','refinedchain','refinedeffects','refined720','soft','softchain','empty','effects','720','beam','narrow','chain','amusement']){
      await page.selectOption('#state',scene);await loaded();
      const size=await page.locator('#capture').evaluate(image=>[image.naturalWidth,image.naturalHeight]);
      assert.deepEqual(size,scene==='720'||scene==='refined720'?[1280,720]:scene==='narrow'?[1280,1024]:[1920,1080]);
      assert.equal(await page.locator('#current').getAttribute('aria-pressed'),'true');
    }
    await page.locator('#previous').click();await loaded();
    assert(await page.locator('#state').isDisabled());
    assert((await page.locator('#capture').getAttribute('src')).includes('tank-town-hud-CandyTown-04-held-and-auto'));
    await page.locator('#current').click();await loaded();assert(await page.locator('#state').isEnabled());
    await page.selectOption('#state','softchain');await loaded();await page.locator('#previous').click();await loaded();
    assert((await page.locator('#capture').getAttribute('src')).endsWith('tank-town-minimal-hud-chain.png'));
    await page.locator('#current').click();await loaded();
    await page.selectOption('#state','refinedchain');await loaded();await page.locator('#previous').click();await loaded();
    assert((await page.locator('#capture').getAttribute('src')).endsWith('tank-town-minimal-hud-soft-chain.png'));
    await page.locator('#current').click();await loaded();
    for(const width of [1920,1280,720,390]){await page.setViewportSize({width,height:1080});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));}
    await page.selectOption('#state','effects');await loaded();await page.locator('#full-size').click();await page.waitForLoadState();
    assert(page.url().endsWith('tank-town-minimal-hud-effects.png'));assert.deepEqual(errors,[]);
    console.log('MINIMAL HUD BROWSER PASS: 13 Unity captures; previous/current and refined header comparison; 1920/1280/720/390 widths; full-size link; no errors');
  }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
