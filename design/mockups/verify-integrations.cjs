const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('fs');
const path = require('path');
const { pathToFileURL, fileURLToPath } = require('url');

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'], channel: process.env.PLAYWRIGHT_CHANNEL || undefined });
  const page = await browser.newPage({ viewport: { width: 1200, height: 1100 }, reducedMotion: 'reduce' });
  const results = [];
  const errors = [];
  const external = [];
  const output = path.join(__dirname, 'qa', 'integrations');
  fs.mkdirSync(output, { recursive: true });
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
  const open = async folder => {
    await page.goto(pathToFileURL(path.join(__dirname, folder, 'index.html')).href);
    await page.evaluate(() => document.fonts.ready);
  };

  for (const folder of ['settings-integrations', 'health-export']) {
    for (const name of fs.readdirSync(path.join(__dirname, folder)).filter(name => /\.(html|css|js)$/.test(name))) {
      const source = fs.readFileSync(path.join(__dirname, folder, name), 'utf8');
      check(`${folder}/${name} uses portable resources`, !/file:\/\/\/[A-Za-z]:|(?:^|["'\s(])[A-Za-z]:[\\/]|(?:Users|home)[\\/]/m.test(source));
    }
    await open(folder);
    check(`${folder} loads local font`, await page.evaluate(() => document.fonts.check('16px "Space Grotesk"')));
    check(`${folder} has unique IDs`, await page.evaluate(() => {
      const ids = [...document.querySelectorAll('[id]')].map(element => element.id);
      return ids.length === new Set(ids).size;
    }));
    const links = await page.locator('a[href]').evaluateAll(elements => elements.map(element => element.href));
    for (const link of links) {
      const url = new URL(link);
      if (url.protocol !== 'file:') continue;
      const file = fileURLToPath(url);
      check(`${folder} link target exists`, fs.existsSync(file));
      if (url.hash && file.endsWith('.html')) {
        check(`${folder} linked HTML anchor exists`, fs.readFileSync(file, 'utf8').includes(`id="${decodeURIComponent(url.hash.slice(1))}"`));
      }
    }
  }

  await open('settings-integrations');
  await page.locator('[data-action="zones"]').click();
  check('HRR has two labelled inputs', await page.locator('#zones-form input').count() === 2
    && await page.getByLabel('Resting heart rate (bpm)').count() === 1
    && await page.getByLabel('Maximum heart rate (bpm)').count() === 1);
  check('HRR preview is read-only', await page.locator('#zone-summary').evaluate(element => !element.querySelector('a,button,input,select,[tabindex]')));
  const zones = await page.locator('#zone-summary').innerText();
  check('HRR exact integer ranges and outside-zone labels', ['106–133', '134–147', '148–161', '162–175', '176–190', 'Below Zone 1', 'Above 190'].every(value => zones.includes(value)));
  check('HRR chart palette stays local and labelled', await page.locator('#zone-summary .hrr-segment').evaluateAll(elements => {
    const colors = ['rgb(80, 80, 80)', 'rgb(140, 184, 211)', 'rgb(143, 194, 180)', 'rgb(216, 206, 131)', 'rgb(220, 161, 111)', 'rgb(217, 130, 121)'];
    return elements.length === 6 && elements.every((element, index) => getComputedStyle(element).backgroundColor === colors[index] && element.textContent.trim().length > 0);
  }));
  await page.locator('.phone').screenshot({ path: path.join(output, 'zones.png') });
  await page.getByLabel('Maximum heart rate (bpm)').fill('50');
  await page.locator('#zones-form button[type="submit"]').click();
  check('Invalid maximum retains form and accessible error', await page.getByLabel('Maximum heart rate (bpm)').evaluate(element => element === document.activeElement && element.getAttribute('aria-invalid') === 'true')
    && (await page.locator('#zone-errors').innerText()).includes('Maximum must exceed'));
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  check('Cancel preserves committed synthetic inputs', JSON.stringify((await page.evaluate(() => window.prototypeState())).zones) === JSON.stringify({ resting: 50, maximum: 190 }));

  await open('health-export');
  check('Only the identified sixteen reference states', await page.locator('article.review-screen').count() === 16);
  check('Explicit approved state scope', await page.locator('.screen-status').evaluateAll(elements => elements.every(element => element.textContent.includes('Approved direction'))));
  const interruptedEntry = page.locator('#history a.row').filter({ hasText: 'Easy miles' });
  check('Interrupted history uses current local detail', await interruptedEntry.getAttribute('href') === '#interrupted-detail');
  const currentDocument = page.url().split('#')[0];
  await interruptedEntry.click();
  check('Interrupted history opens the truthful local detail', page.url() === `${currentDocument}#interrupted-detail`
    && (await page.locator('#interrupted-detail .phone').innerText()).includes('The actual stop time is unknown.'));
  check('Navigation rows retain separate decorative markers', await page.locator('.phone-content a.row').evaluateAll(elements => elements.length === 11
    && elements.every(element => element.lastElementChild.matches('svg.navigation-indicator[aria-hidden="true"][focusable="false"]'))));
  check('Static metric rows remain noninteractive', await page.locator('.phone-content div.row').evaluateAll(elements => elements.every(element => !element.querySelector('.navigation-indicator') && !element.hasAttribute('tabindex') && !element.hasAttribute('role'))));
  check('Recovery preserves last-saved and unknown-stop scope', (await page.locator('#interrupted .phone').innerText()).includes('actual stop time is unknown'));
  check('Failed save is not success', (await page.locator('#interrupted-save-error [role="alert"]').innerText()).includes('Save is not confirmed'));
  check('Live storage warning does not invent Continue mode', (await page.locator('#storage-error [role="alert"]').innerText()).includes('may be lost')
    && await page.locator('#storage-error .phone').getByRole('link', { name: 'Continue', exact: true }).count() === 0);
  for (const id of ['history', 'run-detail', 'interrupted-save-error', 'storage-error']) {
    const phone = page.locator(`#${id} .phone`);
    await phone.screenshot({ path: path.join(output, `${id}.png`) });
    check(`${id} actions retain 48px targets`, await phone.locator('a.action,a.row').evaluateAll(elements => elements.every(element => element.getBoundingClientRect().height >= 48)));
    await phone.evaluate(element => {
      const elements = [...element.querySelectorAll('h2,p,small,.row>span,a.action')];
      const sizes = elements.map(item => parseFloat(getComputedStyle(item).fontSize));
      elements.forEach((item, index) => item.style.fontSize = `${sizes[index] * 2}px`);
    });
    check(`${id} doubled text stays inside frame`, await phone.evaluate(element => element.scrollWidth <= element.clientWidth));
  }
  check('No page errors or network requests', errors.length === 0 && external.length === 0);
  fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify({ results, errors, external,
    limits: 'Browser design-reference checks only. No app persistence, real sensor/health/share operations, Android scaling, TalkBack or physical acceptance.' }, null, 2) + '\n', 'utf8');
  await browser.close();
  console.log(`PASS: ${results.length} integration-reference checks.`);
})().catch(error => { console.error(error); process.exit(1); });
