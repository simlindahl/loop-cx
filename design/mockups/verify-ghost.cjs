const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('fs');
const path = require('path');
const { pathToFileURL, fileURLToPath } = require('url');

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'], channel: process.env.PLAYWRIGHT_CHANNEL || undefined });
  const page = await browser.newPage({ viewport: { width: 1200, height: 1100 }, reducedMotion: 'reduce' });
  const output = path.join(__dirname, 'qa', 'ghost');
  fs.mkdirSync(output, { recursive: true });
  const results = [];
  const errors = [];
  const external = [];
  const check = (name, passed) => {
    results.push({ name, passed });
    if (!passed) throw new Error(name);
  };
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/*', route => {
    const url = route.request().url();
    if (url.startsWith('file:') || url.startsWith('data:')) return route.continue();
    external.push(url);
    return route.abort();
  });
  for (const name of ['index.html', 'style.css']) {
    const source = fs.readFileSync(path.join(__dirname, 'ghost-run', name), 'utf8');
    check(`${name} uses portable resources`, !/file:\/\/\/[A-Za-z]:|(?:^|["'\s(])[A-Za-z]:[\\/]|(?:Users|home)[\\/]/m.test(source));
  }
  const currentDocument = pathToFileURL(path.join(__dirname, 'ghost-run', 'index.html')).href;
  await page.goto(currentDocument);
  await page.evaluate(() => document.fonts.ready);
  check('Twenty-one identified approved states', await page.locator('article.review-screen').count() === 21
    && await page.locator('.screen-status').evaluateAll(elements => elements.length === 21 && elements.every(element => element.textContent.includes('Approved direction'))));
  check('Unique IDs and loaded local font', await page.evaluate(() => {
    const ids = [...document.querySelectorAll('[id]')].map(element => element.id);
    return ids.length === new Set(ids).size && document.fonts.check('16px "Space Grotesk"');
  }));
  const links = await page.locator('a[href]').evaluateAll(elements => elements.map(element => element.href));
  const targets = new Set(links);
  for (const link of targets) {
    const url = new URL(link);
    if (url.protocol !== 'file:') continue;
    const file = fileURLToPath(url);
    check('Linked local document exists', fs.existsSync(file));
    if (url.hash && file.endsWith('.html')) {
      check('Linked HTML anchor exists', fs.readFileSync(file, 'utf8').includes(`id="${decodeURIComponent(url.hash.slice(1))}"`));
    }
  }
  check('Static illustrations do not run app operations', await page.locator('script').count() === 0);
  check('Ghost phone copy has no Core/RPE/training targets', await page.locator('article .phone').evaluateAll(elements => elements.every(element => !/\bC[12](?:\.\d)?\b|\bRPE\b|Easy run|target pace|Target Zone/i.test(element.innerText))));
  check('Capture and race have distinct identities', (await page.locator('#ghost-save .phone .eyebrow').textContent()).trim() === 'Ghost Run · Route capture'
    && (await page.locator('#ghost-live .phone .eyebrow').textContent()).trim() === 'Ghost Run · Route race');
  check('Measured pace and HR remain measurements', (await page.locator('#ghost-live .phone').innerText()).includes('5:20/km / 153 bpm'));
  for (const id of ['ghost-ready', 'ghost-ready-pb', 'ghost-ready-first']) {
    check(`${id} explains that pauses count`, (await page.locator(`#${id} .phone`).innerText()).includes('stops and pauses'));
  }
  for (const id of ['ghost-uncertain', 'ghost-off-route']) {
    check(`${id} retains pause and end`, await page.locator(`#${id} .phone a[href="#ghost-paused"]`).count() === 1
      && await page.locator(`#${id} .phone a[href="#ghost-abandon"]`).count() === 1);
  }
  check('Insufficient GPS blocks route saving', await page.locator('#ghost-save-blocked button').isDisabled());
  await page.locator('#ghost-choose a[href="#ghost-ready"]').click();
  check('Reference choice opens the reviewed ready state', page.url() === `${currentDocument}#ghost-ready`);
  const start = page.locator('#ghost-ready .phone a.action');
  await start.focus();
  await page.keyboard.press('Tab');
  await page.keyboard.press('Shift+Tab');
  check('Start has keyboard focus and full target', await start.evaluate(element => element === document.activeElement && getComputedStyle(element).outlineStyle === 'solid' && element.getBoundingClientRect().height >= 48));
  await page.keyboard.press('Enter');
  check('Start opens the race illustration', page.url() === `${currentDocument}#ghost-live`);
  for (const id of ['ghost-ready', 'ghost-live', 'ghost-result']) {
    await page.locator(`#${id} .phone`).screenshot({ path: path.join(output, `${id}.png`) });
  }
  await page.setViewportSize({ width: 390, height: 1000 });
  for (const id of ['ghost-live', 'ghost-choose']) {
    const phone = page.locator(`#${id} .phone`);
    await phone.evaluate(element => {
      const elements = [...element.querySelectorAll('h2,h3,p,span,a')];
      const sizes = elements.map(item => parseFloat(getComputedStyle(item).fontSize));
      elements.forEach((item, index) => item.style.fontSize = `${sizes[index] * 2}px`);
    });
    check(`${id} doubled text stays inside frame`, await phone.evaluate(element => element.scrollWidth <= element.clientWidth));
  }
  check('No page errors or network requests', errors.length === 0 && external.length === 0);
  fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify({ results, errors, external,
    limits: 'Approved static design reference only. No race engine, GPS/maps/provider operation, actual Verified/PB result, Android scaling, TalkBack or physical acceptance.' }, null, 2) + '\n', 'utf8');
  await browser.close();
  console.log(`PASS: ${results.length} Ghost-reference checks.`);
})().catch(error => { console.error(error); process.exit(1); });
