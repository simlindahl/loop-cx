const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('fs');
const path = require('path');

(async () => {
    const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || undefined });
    try {
        const page = await browser.newPage({ viewport: { width: 390, height: 900 } });
        await page.setContent('<main data-motion-surface="training"><h2 id="start-heading" tabindex="-1" data-motion-group>Training</h2><section data-motion-group>Core</section></main>');
        const source = fs.readFileSync(path.join(__dirname, '../../src/LoopCx/wwwroot/js/core-dialog.js'), 'utf8');
        await page.evaluate(async source => {
            window.motion = await import('data:text/javascript;base64,' + source);
        }, Buffer.from(source, 'utf8').toString('base64'));
        const checks = await page.evaluate(async () => {
            const api = window.motion;
            const passed = [];
            const check = (name, condition) => {
                if (!condition) throw new Error(name);
                passed.push(name);
            };
            const active = () => document.getAnimations().filter(animation => !['idle', 'finished'].includes(animation.playState));
            const copies = () => [...document.querySelectorAll('[inert][aria-hidden="true"]')];
            const wait = () => new Promise(resolve => setTimeout(resolve, 320));
            document.documentElement.dataset.systemMotion = 'pending';
            api.syncMotion(false);
            check('Unresolved native motion preference stays static', active().length === 0);
            api.setSystemMotion(false);

            // The real History renderer can expose a new destination before its archive load finishes.
            const main = document.querySelector('main');
            main.dataset.motionSurface = 'history';
            main.innerHTML = '<section data-motion-owner="history-test" data-motion-destination="list" data-motion-ready="false"><button>Back</button><h2 id="history-heading" tabindex="-1" data-motion-group>History</h2><p role="status">Loading stored recording…</p><div data-motion-group></div></section>';
            const history = main.firstElementChild;
            const heading = document.getElementById('history-heading');
            const body = history.lastElementChild;
            const list = '<button style="min-height:48px">C1 · Intervals</button><button style="min-height:48px">Ghost Run</button>';
            api.focus('history-heading');
            api.syncMotion(false);
            check('Loading render does not consume or animate the new History destination', active().length === 0 && document.activeElement === heading);
            body.innerHTML = list;
            history.dataset.motionReady = 'true';
            history.querySelector('[role="status"]').remove();
            api.syncMotion(false);
            const entrance = active();
            check('Ready History animates two groups with the approved timing and stagger', entrance.length === 2
                && entrance[0].effect.getTiming().duration === 220 && entrance[0].effect.getTiming().delay === 0
                && entrance[1].effect.getTiming().duration === 220 && entrance[1].effect.getTiming().delay === 24);
            check('Rows have no individual effect and their hit height is preserved', [...body.children].every(row => row.getAnimations().length === 0 && row.getBoundingClientRect().height >= 48));
            body.firstElementChild.textContent = 'C1 · Pending';
            api.syncMotion(false);
            check('Same-destination status refresh retains effect identity and focus', active().every(animation => entrance.includes(animation)) && document.activeElement === heading);
            await wait();

            api.leaveSurface(history);
            const firstCopy = copies()[0];
            check('Exit is a 120ms inert hidden heading with no duplicate IDs or announcements', copies().length === 1
                && firstCopy.getAnimations()[0].effect.getTiming().duration === 120 && !firstCopy.id
                && !firstCopy.querySelector('[role="status"], [role="alert"]') && document.activeElement === heading);
            history.dataset.motionDestination = 'run:one';
            history.dataset.motionReady = 'false';
            body.replaceChildren();
            api.syncMotion(false);
            check('Selected-run loading does not animate an empty body', body.getAnimations().length === 0);
            heading.textContent = 'C1 · Intervals';
            body.innerHTML = '<dl><dt>Distance</dt><dd>5.00 km</dd></dl><button style="min-height:48px">Share workout data</button>';
            history.dataset.motionReady = 'true';
            api.syncMotion(false);
            const detailEntrance = body.getAnimations()[0];
            check('Loaded selected run enters without per-metric animation or delayed focus', !!detailEntrance && body.querySelector('dd').getAnimations().length === 0 && document.activeElement === heading);

            api.leaveSurface(history);
            check('Rapid Back cancels obsolete entrance and removes the previous exit', detailEntrance.playState === 'idle' && !firstCopy.isConnected && copies().length === 1);
            history.dataset.motionDestination = 'list';
            history.dataset.motionReady = 'false';
            body.replaceChildren();
            api.syncMotion(false);
            check('Back loading does not consume list entrance', body.getAnimations().length === 0);
            body.innerHTML = list;
            history.dataset.motionReady = 'true';
            api.syncMotion(false);
            check('Actual ready list still receives its entrance after Back', body.getAnimations().length === 1);
            await wait();
            history.dataset.motionReady = 'false';
            body.replaceChildren();
            api.syncMotion(false);
            history.dataset.motionReady = 'true';
            body.innerHTML = list;
            api.syncMotion(false);
            check('Background reload of the same destination stays static', active().length === 0);

            history.dataset.motionDestination = 'run:two';
            api.syncMotion(false);
            api.leaveSurface(history);
            api.setSystemMotion(true);
            check('Native reduced motion cancels all effects and exit copies immediately', active().length === 0 && copies().length === 0);
            api.setSystemMotion(false);
            api.syncMotion(false);
            history.dataset.motionDestination = 'run:three';
            api.syncMotion(false);
            Object.defineProperty(document, 'hidden', { value: true, configurable: true });
            document.dispatchEvent(new Event('visibilitychange'));
            api.syncMotion(false);
            check('Hidden state cancels effects and prevents new effects', active().length === 0 && copies().length === 0);
            Object.defineProperty(document, 'hidden', { value: false, configurable: true });
            document.dispatchEvent(new Event('visibilitychange'));
            api.setSystemMotion(false);

            // Child teardown must not cancel the new parent's destination effects.
            history.dataset.motionDestination = 'run:four';
            api.syncMotion(false);
            const detachedEffect = body.getAnimations()[0];
            history.remove();
            history.replaceChildren();
            main.dataset.motionSurface = 'training';
            main.innerHTML = '<h2 id="start-heading" data-motion-group>Training</h2><section data-motion-group>Core</section>';
            api.syncMotion(false);
            api.disposeSurface('history-test');
            check('Stable owner cleanup cancels separately detached effects without element lookup or cancelling the new destination', detachedEffect.playState === 'idle' && active().length === 2);
            api.leaveSurface();
            api.dispose();
            check('Shared owner disposal cancels all effects and copies', active().length === 0 && copies().length === 0);
            return passed;
        });
        const output = path.join(__dirname, '../../design/mockups/qa');
        fs.mkdirSync(output, { recursive: true });
        const report = { scope: 'Actual shared runtime module in representative History DOM; not Razor routing, Android, TalkBack, frame pacing or battery acceptance', passed: checks.length, checks };
        fs.writeFileSync(path.join(output, 'history-motion.json'), JSON.stringify(report, null, 2) + '\n', 'utf8');
        console.log(JSON.stringify(report, null, 2));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
