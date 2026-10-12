const assert = require('node:assert/strict');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.TANKTOWN_PLAYWRIGHT_MODULE || 'playwright');
(async()=>{
  const browser = await chromium.launch({headless:true,executablePath:process.env.TANKTOWN_CHROME || 'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  try {
    const page = await browser.newPage({viewport:{width:1920,height:1080}});
    const failures=[];page.on('pageerror',error=>failures.push(error.message));
    await page.goto(pathToFileURL(path.join(__dirname,'native-review.html')).href);
    await page.waitForFunction(()=>[...document.querySelectorAll('.shot img')].length===14&&[...document.querySelectorAll('.shot img')].every(image=>image.complete&&image.naturalWidth>0));
    assert.deepEqual(failures,[]);
    const sizes=await page.locator('.shot img').evaluateAll(images=>images.map(image=>[image.naturalWidth,image.naturalHeight]));
    assert(sizes.some(([w,h])=>w===1920&&h===1080));assert(sizes.some(([w,h])=>w===1280&&h===720));assert(sizes.some(([w,h])=>w===1440&&h===900));
    await page.locator('#filter').selectOption('Town');assert.equal(await page.locator('.shot:visible').count(),1);
    await page.locator('.shot:visible').click();await page.waitForFunction(()=>document.getElementById('large').complete&&document.getElementById('large').naturalWidth>0);
    assert.equal(await page.locator('#zoom').evaluate(dialog=>dialog.open),true);assert.match(await page.locator('#caption').textContent(),/いつもの街/);
    await page.locator('#close').click();assert.equal(await page.locator('#zoom').evaluate(dialog=>dialog.open),false);
    await page.locator('#filter').selectOption('BigCity');assert.equal(await page.locator('.shot:visible').count(),1);
    await page.locator('#filter').selectOption('CandyTown');assert.equal(await page.locator('.shot:visible').count(),12);
    await page.locator('#filter').selectOption('all');
    for(const width of [1920,1280,720]){await page.setViewportSize({width,height:1080});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));}
    assert.deepEqual(failures,[]);
    console.log('NATIVE HUD REVIEW BROWSER PASS: 14 actual Unity screenshots, 3 stages, 3 capture resolutions, filtering, enlarged view, responsive gallery, no errors');
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
