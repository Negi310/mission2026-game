// Optional browser verification. The preview itself needs only a browser.
const { chromium } = require(process.env.TANKTOWN_PLAYWRIGHT_MODULE || 'playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const fs = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({ executablePath: process.env.TANKTOWN_CHROME || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1984, height: 1360 }, deviceScaleFactor: 1 });
    const errors = []; page.on('pageerror', error => errors.push(String(error)));
    await page.goto(pathToFileURL(path.join(__dirname, 'preview.html')).href);
    await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
    assert.equal(await page.locator('#empty-slot').isVisible(), false);
    assert.equal(await page.locator('#held-icon').isVisible(), true);
    const output = path.resolve(__dirname, '..', 'images'); fs.mkdirSync(output, { recursive: true });
    assert.equal(await page.locator('.controls-hint').count(), 0);
    const checkGeometry = async () => {
      const valid = await page.evaluate(() => {
        const frame = document.querySelector('.viewport').getBoundingClientRect(), factor = frame.width / 1920;
        const rects = [...document.querySelectorAll('.upgrade,.special-area,.effect,.clock,.map,.score')].map(node => node.getBoundingClientRect());
        const tank = { left: frame.left + 802 * factor, right: frame.left + 1086 * factor, top: frame.top + 682 * factor, bottom: frame.top + 910 * factor };
        const overlaps = (a,b) => a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
        const widgets = [...document.querySelectorAll('.special-area,.effect')].map(node => node.getBoundingClientRect());
        return rects.every(r => r.left >= frame.left && r.top >= frame.top && r.right <= frame.right + 1 && r.bottom <= frame.bottom + 1)
          && widgets.every(r => !overlaps(r,tank))
          && widgets.every((r,i) => widgets.every((other,j) => i===j || !overlaps(r,other)));
      });
      assert(valid, 'Special widgets must stay within the screen and clear of the tank and one another');
    };
    await checkGeometry();
    await page.locator('.viewport').screenshot({ path: path.join(output, 'tank-town-ui-special-center.png') });
    await page.selectOption('#layout-select', 'near'); await checkGeometry();
    await page.locator('.viewport').screenshot({ path: path.join(output, 'tank-town-ui-special-near.png') });
    await page.selectOption('#layout-select', 'center');
    await page.click('#collect');
    await page.waitForSelector('.number.changing');
    await page.waitForFunction(() => window.uiDemo.snapshot.shown[0] === 3);
    await page.click('#burst');
    await page.waitForFunction(() => window.uiDemo.snapshot.shown.join(',') === '5,5,3,4');
    const strip = await page.locator('.picked').evaluateAll(nodes => nodes.map(node => { const r = node.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; }));
    assert(strip.length >= 8); assert(Math.max(...strip.map(r => r.y)) - Math.min(...strip.map(r => r.y)) < 20);
    await page.locator('.viewport').screenshot({ path: path.join(output, 'tank-town-ui-special-center-pickup.png') });
    await page.click('#activate');
    let state = await page.evaluate(() => window.uiDemo.snapshot);
    assert.equal(state.held, null); assert(state.effects.some(e => e.kind === 0));
    assert.equal(await page.locator('#empty-slot').isVisible(), true);
    await page.selectOption('#special-select', '4'); await page.click('#collect-special');
    assert.equal(await page.locator('#use').isDisabled(), true);
    assert.equal(await page.locator('#held-name').textContent(), 'ギガント爆弾');
    await page.click('#extend');
    state = await page.evaluate(() => window.uiDemo.snapshot);
    assert.equal(state.effects.find(e => e.kind === 2).remaining, 8);
    assert.equal(await page.locator('[data-kind="2"] .extension').textContent(), '+1.6秒');
    await page.evaluate(() => window.uiDemo.advance(7)); await page.waitForTimeout(240);
    state = await page.evaluate(() => window.uiDemo.snapshot);
    assert.deepEqual(state.effects.map(e => e.kind), [2]);
    assert.equal(await page.locator('#use').isDisabled(), false);
    assert.equal(await page.locator('.effect.low').count(), 1);
    await page.evaluate(() => window.uiDemo.advance(2)); await page.waitForTimeout(240);
    assert.equal(await page.locator('.effect').count(), 0);
    await page.selectOption('#special-select', '4'); await page.click('#collect-special'); await page.click('#activate');
    assert.equal(await page.locator('[data-kind="4"] .seconds').textContent(), '飛行中');
    await page.click('#impact'); assert.equal(await page.locator('.effect').count(), 0);
    await page.click('#reset'); await page.click('#tick'); await page.waitForTimeout(350); await page.click('#tick');
    state = await page.evaluate(() => window.uiDemo.snapshot); assert(state.effects.find(e => e.kind === 2).remaining < 6.4);
    await page.click('#reset');
    await page.setViewportSize({ width: 1344, height: 1050 });
    await page.waitForTimeout(150);
    await checkGeometry();
    await page.locator('.viewport').screenshot({ path: path.join(output, 'tank-town-ui-special-center-720.png') });
    await page.selectOption('#layout-select', 'near'); await checkGeometry();
    await page.locator('.viewport').screenshot({ path: path.join(output, 'tank-town-ui-special-near-720.png') });
    await page.selectOption('#layout-select', 'center');
    await page.evaluate(() => { window.uiDemo.collectSpecial(3); window.uiDemo.useHeld(); });
    await checkGeometry();
    const layout = await page.evaluate(() => { const view = document.querySelector('.viewport').getBoundingClientRect(); return [...document.querySelectorAll('.upgrade,.special-area,.effect,.clock,.map,.score')].map(node => { const r = node.getBoundingClientRect(); return r.left >= view.left && r.top >= view.top && r.right <= view.right + 1 && r.bottom <= view.bottom + 1; }); });
    assert(layout.every(Boolean)); assert.deepEqual(errors, []);
    console.log('UI PROPOSAL BROWSER PASS: no controls guide, center/near positions, clear tank silhouette, five active effects, assets, animated levels, one-row burst, held/active slots, duration reset, expiry, bomb state, timer, 1920/1280 layouts');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
