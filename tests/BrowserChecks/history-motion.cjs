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

            for (const [depth, title] of [['splits', 'Splits & phases'], ['zones', 'Time in zones']]) {
                api.leaveSurface(history);
                check(`Opening ${depth} cancels previous entrance and keeps one exit`, active().length === 1 && copies().length === 1);
                history.dataset.motionDestination = `detail:one:${depth}`;
                history.dataset.motionReady = 'false';
                body.replaceChildren();
                api.syncMotion(false);
                check(`Busy ${depth} does not consume entrance`, body.getAnimations().length === 0);
                heading.textContent = title;
                body.innerHTML = '<dl><dt>Active</dt><dd>5:00</dd></dl><button>Frozen facts</button>';
                history.dataset.motionReady = 'true';
                api.focus('history-heading');
                api.syncMotion(false);
                const effect = body.getAnimations()[0];
                check(`Ready ${depth} uses group entrance and heading focus`, !!effect
                    && effect.effect.getTiming().duration === 220 && effect.effect.getTiming().delay === 24
                    && document.activeElement === heading && body.querySelector('dd').getAnimations().length === 0);
                const frozen = body.querySelector('button');
                frozen.focus();
                body.querySelector('dd').textContent = 'Unavailable';
                api.syncMotion(false);
                check(`Refresh at ${depth} keeps effect and focus`, body.getAnimations()[0] === effect && document.activeElement === frozen);

                api.leaveSurface(history);
                history.dataset.motionDestination = 'run:one';
                heading.textContent = 'C1 · Intervals';
                body.innerHTML = `<button id="history-${depth}">${title}</button>`;
                api.focus(`history-${depth}`);
                api.syncMotion(false);
                check(`Back from ${depth} cancels detail effect and restores opener`, effect.playState === 'idle'
                    && document.activeElement.id === `history-${depth}`);
            }

            history.dataset.motionDestination = 'detail:two:splits';
            api.syncMotion(false);
            const anotherRun = body.getAnimations()[0];
            history.dataset.motionDestination = 'detail:one:splits';
            api.syncMotion(false);
            check('The same detail depth on another recording has independent entrance identity', anotherRun.playState === 'idle'
                && body.getAnimations().length === 1);

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

            body.innerHTML = '<button id="history-pending">Interrupted Ghost Run</button>';
            api.leaveSurface(history);
            history.dataset.motionDestination = 'interrupted:pending';
            history.dataset.motionReady = 'false';
            api.syncMotion(false);
            history.dataset.motionDestination = 'list';
            history.dataset.motionReady = 'true';
            api.focus('history-pending');
            api.syncMotion(false);
            check('Back from pending interrupted Ghost retains list and opener without a detail entrance',
                body.getAnimations().length === 0 && document.activeElement.id === 'history-pending');

            for (const kind of ['core', 'ghost']) {
                api.leaveSurface(history);
                const destination = `interrupted:${kind}`;
                history.dataset.motionDestination = destination;
                history.dataset.motionReady = 'false';
                api.syncMotion(false);
                check(`Interrupted ${kind} loading stays static`, body.getAnimations().length === 0);

                heading.textContent = kind === 'core' ? 'Interrupted run' : 'Ghost Run';
                body.innerHTML = '<button id="recovery-save">Save recorded part</button>';
                history.dataset.motionReady = 'true';
                api.focus('history-heading');
                api.syncMotion(false);
                const entrance = body.getAnimations()[0];
                check(`Interrupted ${kind} ready view starts grouped entrance`, !!entrance
                    && entrance.effect.getTiming().delay === 24 && document.activeElement === heading);
                history.dataset.motionReady = 'false';
                api.syncMotion(false);
                history.dataset.motionReady = 'true';
                body.firstElementChild.textContent = 'Retry save';
                api.syncMotion(false);
                check(`Interrupted ${kind} failed save leaves the existing entrance`, body.getAnimations()[0] === entrance);

                const confirmation = `saved-${kind}`;
                body.innerHTML = `<p role="status" data-motion-recorded-part="${confirmation}">Partial recording saved locally.</p>`;
                api.syncMotion(false);
                api.confirmRecordedPart(history, 'history-test', destination, confirmation);
                const saved = body.querySelector('[role="status"]');
                check(`Interrupted ${kind} acknowledgement uses study 06 independently of entrance`,
                    body.getAnimations()[0] === entrance && saved.getAnimations()[0].effect.getTiming().duration === 160);

                api.leaveSurface(history);
                history.dataset.motionDestination = 'list';
                body.innerHTML = `<button id="history-${kind}">Stored partial</button>`;
                api.focus(`history-${kind}`);
                api.syncMotion(false);
                check(`Interrupted ${kind} Back restores the same opener and cuts off the old effects`,
                    document.activeElement.id === `history-${kind}` && entrance.playState === 'idle');
                api.leaveSurface(history);
                history.dataset.motionDestination = destination;
                body.innerHTML = '<p role="status">Partial recording saved locally.</p>';
                api.syncMotion(false);
                api.confirmRecordedPart(history, 'history-test', destination, confirmation);
                check(`Interrupted ${kind} same-run reopen enters without a stale acknowledgement`,
                    body.getAnimations().length === 1 && body.querySelector('[role="status"]').getAnimations().length === 0);
            }

            history.dataset.motionDestination = 'run:one';
            api.syncMotion(false);
            api.leaveSurface(history);
            history.dataset.motionDestination = 'share:one';
            heading.textContent = 'Your run, as data.';
            body.innerHTML = '<p>Contains your route, timestamps and heart rate.</p><button id="share-open">Open Android share sheet</button><p role="status"></p>';
            api.focus('history-heading');
            api.syncMotion(false);
            const disclosure = body.getAnimations()[0];
            check('Share disclosure enters with the existing groups and heading focus', !!disclosure
                && disclosure.effect.getTiming().delay === 24 && document.activeElement === heading);
            const shareOpen = body.querySelector('#share-open');
            shareOpen.focus();
            const shareStatus = body.querySelector('[role="status"]');
            for (const status of ['Preparing JSON file…', 'Share failed. Try again.', '']) {
                shareStatus.textContent = status;
                api.syncMotion(false);
                check(`Share status update stays on the same disclosure: ${status || 'cleared'}`, body.getAnimations()[0] === disclosure
                    && document.activeElement === shareOpen);
            }
            Object.defineProperty(document, 'hidden', { value: true, configurable: true });
            document.dispatchEvent(new Event('visibilitychange'));
            Object.defineProperty(document, 'hidden', { value: false, configurable: true });
            document.dispatchEvent(new Event('visibilitychange'));
            api.setSystemMotion(false);
            api.syncMotion(false);
            check('Returning to the same disclosure after background or chooser does not replay it', active().length === 0);
            api.leaveSurface(history);
            history.dataset.motionDestination = 'run:one';
            heading.textContent = 'C1 · Intervals';
            body.innerHTML = '<button id="history-share">Share workout data</button>';
            api.focus('history-share');
            api.syncMotion(false);
            check('Share Back restores the same selected run and original opener', document.activeElement.id === 'history-share');

            api.leaveSurface(history);
            history.dataset.motionDestination = 'detail:one:route';
            history.dataset.motionReady = 'false';
            body.removeAttribute('data-motion-group');
            heading.textContent = 'Recorded route';
            body.innerHTML = '<p role="status">Loading recorded route…</p>';
            api.syncMotion(false);
            check('Route loading does not consume the heading entrance', heading.getAnimations().length === 0);
            body.innerHTML = '<button id="route-scope">Whole run</button><figure><svg id="map-slot" width="220" height="140"><path d="M10 10 L80 90 M120 90 L155 25"/></svg></figure>';
            const slot = body.querySelector('#map-slot');
            const slotRect = () => {
                const { x, y, width, height } = slot.getBoundingClientRect();
                return [x, y, width, height];
            };
            const settledRect = slotRect();
            history.dataset.motionReady = 'true';
            api.focus('history-heading');
            api.syncMotion(false);
            const routeEntrance = heading.getAnimations()[0];
            check('Ready Route animates only the independent heading with 220 ms and no stagger', !!routeEntrance
                && routeEntrance.effect.getTiming().duration === 220 && routeEntrance.effect.getTiming().delay === 0
                && document.activeElement === heading && history.firstElementChild.getAnimations().length === 0);
            const samples = [];
            do {
                await new Promise(requestAnimationFrame);
                samples.push(slotRect());
            } while (routeEntrance.playState === 'running' && samples.length < 60);
            check('Route slot and ancestors stay static throughout heading entrance', samples.length > 1
                && samples.every(rect => rect.every((value, i) => value === settledRect[i]))
                && [slot, slot.parentElement, body, history, main].every(node => node.getAnimations().length === 0));
            body.querySelector('#route-scope').textContent = 'Main set';
            api.syncMotion(false);
            check('Changing Route scope and rendering the same selected run do not replay entrance', heading.getAnimations().length === 0);
            api.leaveSurface(history);
            history.dataset.motionDestination = 'run:one';
            heading.textContent = 'C1 · Intervals';
            body.innerHTML = '<button id="history-route">Route</button>';
            api.focus('history-route');
            api.syncMotion(false);
            check('Route Back keeps the original opener and content still', document.activeElement.id === 'history-route'
                && body.getAnimations().length === 0);
            body.setAttribute('data-motion-group', '');

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

            body.innerHTML = [
                ['plan', 'Workout plan'], ['coverage', 'Recorded measurements and coverage'], ['other', 'Other details']
            ].map(([id, caption]) => `<details id="${id}" ${id === 'other' ? '' : 'data-history-disclosure'}>
                <summary style="min-height:48px">${caption}</summary>
                <div data-history-disclosure-content><p>Stored facts stay in reading order.</p><button>Disclosure action</button></div>
            </details>`).join('');
            const toggle = async (details) => {
                const promise = new Promise(resolve => details.addEventListener('toggle', resolve, { once: true }));
                details.firstElementChild.click();
                await promise;
            };
            for (const id of ['plan', 'coverage']) {
                const details = body.querySelector(`#${id}`);
                const summary = details.querySelector('summary');
                const panel = details.lastElementChild;
                summary.focus();
                await toggle(details);
                const reveal = panel.getAnimations()[0];
                check(`Native ${id} opens and only its content animates for 220 ms`, details.open
                    && reveal.effect.getTiming().duration === 220 && summary.getAnimations().length === 0
                    && document.activeElement === summary && summary.getBoundingClientRect().height >= 48);
                panel.querySelector('p').textContent = 'Refreshed stored facts';
                api.syncMotion(false);
                check(`Same-open ${id} refresh keeps the existing disclosure effect`, panel.getAnimations()[0] === reveal);
                await toggle(details);
                panel.querySelector('button').focus();
                check(`Native ${id} collapse cancels reveal and preserves the summary as focus owner`, !details.open
                    && reveal.playState === 'idle' && document.activeElement === summary);
                api.setSystemMotion(true);
                await toggle(details);
                check(`Reduced motion gives immediate expanded ${id} content`, details.open
                    && panel.getAnimations().length === 0 && panel.getBoundingClientRect().height > 0);
                await toggle(details);
                api.setSystemMotion(false);
            }
            const other = body.querySelector('#other');
            await toggle(other);
            check('Unrelated disclosure uses only native behavior', other.open && other.lastElementChild.getAnimations().length === 0);

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
        await page.evaluate(() => {
            document.querySelector('main').innerHTML = '<details data-history-disclosure><summary>Workout plan</summary><div data-history-disclosure-content><p>Plan facts</p><button>Inside</button></div></details>';
            document.body.style.fontSize = '200%';
            window.motion.setSystemMotion(true);
            window.motion.syncMotion(false);
            document.querySelector('summary').focus();
        });
        await page.keyboard.press('Enter');
        if (!(await page.locator('details').evaluate(node => node.open && node.firstElementChild === document.activeElement))) {
            throw new Error('Keyboard opens native disclosure at 200% and retains summary focus');
        }
        await page.keyboard.press('Space');
        if (!(await page.locator('details').evaluate(node => !node.open && node.firstElementChild === document.activeElement))) {
            throw new Error('Keyboard closes native disclosure at 200% and retains summary focus');
        }
        checks.push('Keyboard opens and closes native disclosure at 200% with summary focus');
        const output = path.join(__dirname, '../../design/mockups/qa');
        fs.mkdirSync(output, { recursive: true });
        const report = { scope: 'Actual shared runtime module in representative History DOM; not Razor routing, Android, TalkBack, frame pacing or battery acceptance', passed: checks.length, checks };
        fs.writeFileSync(path.join(output, 'history-motion.json'), JSON.stringify(report, null, 2) + '\n', 'utf8');
        console.log(JSON.stringify(report, null, 2));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
