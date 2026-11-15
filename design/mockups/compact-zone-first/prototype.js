const app = document.querySelector('#app');
const dialog = document.querySelector('#dialog');
const status = document.querySelector('#review-status');
const protocols = {
  'C1.1': ['Sprint intervals', '7 × 30 sec · 6 × 3 min recovery', '21:30 main · 28:30 total', 'Repeated maximal sprints with substantial easy recovery.'],
  'C1.2': ['Aerobic intervals', '5 × 3 min · 4 × 2 min recovery', '23 min main · 30 min total', 'Sustained, controlled hard efforts. Keep them repeatable.'],
  'C1.3': ['Threshold intervals', '3 × 6 min · 2 × 3 min recovery', '24 min main · 31 min total', 'Practise a controlled, sustainable effort near your threshold.']
};
let view = 'training';
let selected = 'C1.2';
let target = '';
let recoveryTarget = '';
let paused = false;
let continuous = false;
let c2Target = '';
let editingC2 = false;
let opener;
const zones = [[1, 106, 133, '#8cb8d3'], [2, 134, 147, '#8fc2b4'], [3, 148, 161, '#d8ce83'], [4, 162, 175, '#dca16f'], [5, 176, 190, '#d98279']];
const zoneLabel = () => target ? `Work Zone ${target}${recoveryTarget ? ` · recovery Zone ${recoveryTarget}` : ' · recovery target not set'}` : 'Target zone not set';
const nav = () => `<nav aria-label="Main"><button class="quiet" data-view="training" ${view === 'training' ? 'aria-current="page"' : ''}>Training</button><button class="quiet" data-view="history" ${['history', 'detail'].includes(view) ? 'aria-current="page"' : ''}>History</button><button class="quiet" data-view="settings" ${view === 'settings' ? 'aria-current="page"' : ''}>Settings</button></nav>`;

function aggregateTable() {
  return '<table><caption>Main set / this split</caption><thead><tr><th scope="col">Metric</th><th scope="col">Main</th><th scope="col">Split</th></tr></thead><tbody><tr><th scope="row">Active time</th><td>8:46</td><td>0:46</td></tr><tr><th scope="row">Distance · km</th><td>1.62</td><td>0.14</td></tr><tr><th scope="row">Avg pace · min/km</th><td>5:25</td><td>5:29</td></tr><tr><th scope="row">Avg heart rate · bpm</th><td>146</td><td>148</td></tr></tbody></table><p class="muted">Synthetic recorded values · GPS/heart-rate coverage incomplete.</p>';
}

function render() {
  const p = protocols[selected];
  let html = nav();
  if (view === 'training') {
    html += `<h2 tabindex="-1" id="heading">Start a run</h2><section class="card"><div class="row-head"><h3>C1 · Intervals</h3><button class="quiet" data-action="change" aria-label="Change C1 protocol">Change</button></div><p class="dose"><strong>${selected} · ${p[0]}</strong><br>${p[1]}</p><p>${p[2]}<small>4 min warm-up · 3 min cool-down</small></p><p>${p[3]}</p><p class="muted">${zoneLabel()}</p><div class="actions"><button class="primary" data-action="start">Start C1</button><button data-action="personalise">Personalise</button></div></section><section class="card"><div class="row-head"><h3>C2 · Continuous</h3><button class="quiet" data-action="c2">Change</button></div><p class="dose">45 min · steady, conversational running<small>No warm-up or cool-down · intended Zone 2</small></p><p class="muted">${c2Target ? `Selected target · Zone ${c2Target}` : 'Personal zone target not set'}</p><div class="actions"><button class="primary" data-action="start-c2">Start C2</button><button data-action="c2-target">Personalise</button></div></section><section class="card"><h3>Saved shortcuts</h3><p>Lunch intervals<small>C1.2 · 5 efforts · no zone target set · synthetic shortcut</small></p><button class="primary" data-action="shortcut">Start Lunch intervals</button><details><summary>Manage shortcuts</summary><p>Existing Edit/Delete/reorder remains unchanged; not duplicated in this hierarchy study.</p></details></section>`;
  } else if (view === 'work') {
    const liveTarget = continuous ? c2Target : target;
    const sensor = document.querySelector('#sensor').value;
    html = `<p class="eyebrow">${continuous ? 'C2' : selected} · SYNTHETIC RUN</p><div class="row-head"><h2 id="heading" tabindex="-1">${paused ? 'Paused' : continuous ? 'Continuous' : 'Work · 2 of 5'}</h2><span>${paused ? 'Manual pause' : 'Main set'}</span></div><p class="clock">2:14<small>remaining · 12:46 active</small></p><p class="target">${liveTarget ? `Target · Zone ${liveTarget}` : 'Target zone not set'}<small>Intended effort · not a measured zone</small></p>${aggregateTable()}<div class="live"><div><span class="muted">Live pace</span><strong>${paused ? 'Unavailable' : '5:20'}</strong><small>${paused ? 'Paused' : 'min/km'}</small></div><div><span class="muted">Heart rate</span><strong>${sensor === 'missing' ? 'Unavailable' : sensor === 'outside' ? '100' : '148'}${sensor === 'missing' ? '' : ' ♡'}</strong><small>${sensor === 'missing' ? 'No current reading' : sensor === 'outside' ? 'bpm · below Zone 1' : 'bpm · measured Zone 3'}</small></div></div><div class="actions"><button class="primary" id="pause" data-action="pause">${paused ? 'Resume' : 'Pause'}</button></div><p class="muted">${sensor === 'missing' ? 'No current heart-rate measurement. Your timer and controls still work.' : 'Valid heart rate remains visible while paused. It does not count active time.'}</p><button class="quiet" data-view="training">Back to study start</button>`;
  } else if (view === 'history') {
    html += `<h2 id="heading" tabindex="-1">History</h2><p class="muted">Saved locally · synthetic example</p><button class="list-row" data-view="detail"><span>C1.2 · Aerobic intervals<small>14 Nov 2026 · partial recording</small></span><span class="value">Last saved 12:46 active · distance unavailable</span><span class="chevron" aria-hidden="true">›</span></button>`;
  } else if (view === 'detail') {
    html += `<button class="quiet" data-view="history">← History</button><h2 id="heading" tabindex="-1">C1.2 · Partial recording</h2><p>Saved locally · not exported</p><dl><div><dt>Last saved active time</dt><dd>12:46</dd></div><div><dt>Distance</dt><dd>Unavailable</dd></div><div><dt>Actual stop time</dt><dd>Unknown</dd></div><div><dt>Frozen target</dt><dd>Zone not set</dd></div></dl><details><summary>Recorded measurements and coverage</summary><p>No measurements are inferred from the intended effort. The historical recording and original export data remain unchanged.</p></details><details><summary>Workout plan</summary><p>5 × 3 min work · 4 × 2 min recovery · warm-up 4 min / cool-down 3 min.</p></details>`;
  } else {
    html += `<h2 id="heading" tabindex="-1">Settings</h2><button class="list-row" data-action="connection"><span>Heart-rate connection<small>Selected: example sensor · not connected</small></span><span class="chevron" aria-hidden="true">›</span></button><button class="list-row" data-action="zones"><span>Personal heart-rate zones<small>Heart-rate reserve · synthetic profile 50 / 190 bpm</small></span><span class="chevron" aria-hidden="true">›</span></button><button class="list-row" data-action="warmup"><span>Warm-up and cool-down<small>4 min / 3 min · new C1 starts</small></span><span class="chevron" aria-hidden="true">›</span></button><p class="muted">No account or network is needed to start a run. Missing heart-rate data stays unknown.</p>`;
  }
  app.innerHTML = html;
}

function open(title, body, trigger) {
  opener = trigger;
  dialog.innerHTML = `<h2 id="dialog-title" tabindex="-1">${title}</h2>${body}<button data-close>Close</button>`;
  dialog.showModal();
  dialog.querySelector('h2').focus();
}

function close() {
  dialog.close();
  if (opener?.isConnected) opener.focus();
}

const selectZone = (id, label, value) => `<label for="${id}">${label}<select id="${id}"><option value="">Not set</option>${zones.map(z => `<option value="${z[0]}" ${value === String(z[0]) ? 'selected' : ''}>Zone ${z[0]}</option>`).join('')}</select></label>`;
app.addEventListener('click', event => {
  const button = event.target.closest('button');
  if (!button) return;
  if (button.dataset.view) {
    view = button.dataset.view;
    render();
    app.querySelector('#heading')?.focus();
    return;
  }
  switch (button.dataset.action) {
    case 'change':
      open('Choose C1 protocol', `<div class="actions">${Object.entries(protocols).map(([id, p]) => `<button data-protocol="${id}">${id} · ${p[0]}</button>`).join('')}</div>`, button);
      break;
    case 'personalise':
    case 'c2-target':
      editingC2 = button.dataset.action === 'c2-target';
      open('Personalise · zone intent', `<p>Layout example only. Official zone defaults and whether pace remains an alternative are product decisions.</p>${selectZone('work-zone', editingC2 ? 'Continuous target zone' : 'Work target zone', editingC2 ? c2Target : target)}${editingC2 ? '' : selectZone('recovery-zone', 'Recovery target zone (optional)', recoveryTarget)}<p>No zone profile? You can still start without a measured zone. A target does not create a sensor reading.</p><button class="primary" data-apply>Use example targets</button>`, button);
      break;
    case 'shortcut':
      selected = 'C1.2'; target = ''; recoveryTarget = '';
    case 'start':
    case 'start-c2':
      continuous = button.dataset.action === 'start-c2';
      view = 'work'; paused = false; render(); app.querySelector('#heading').focus();
      break;
    case 'pause':
      paused = !paused; render(); app.querySelector('#pause').focus();
      break;
    case 'zones':
      open('Personal heart-rate zones', `<p>Heart-rate reserve · synthetic example</p><p>Resting heart rate: 50 bpm<br>Maximum heart rate: 190 bpm</p><p class="muted">Static profile context only. The approved two editable inputs, Save and validation remain unchanged in the full HRR form.</p><div class="zones" aria-hidden="true"><span style="background:#505050;flex:4"></span>${zones.map((z, i) => `<span style="background:${z[3]};flex:${i === 0 ? 2 : 1}"></span>`).join('')}</div><dl>${zones.map(z => `<div><dt><span class="swatch" style="background:${z[3]}"></span>Zone ${z[0]}</dt><dd>${z[1]}–${z[2]} bpm</dd></div>`).join('')}</dl><p>Below 106 bpm: below Zone 1. Above 190 bpm: above configured maximum. These are known readings outside the zones.</p>`, button);
      break;
    case 'connection':
      open('Heart-rate connection', '<p>Example sensor is selected, not connected. Standard Bluetooth LE Heart Rate sensors only; selecting a sensor is separate from receiving a valid reading.</p><p>This study does not scan or request permissions.</p>', button);
      break;
    case 'warmup':
      open('Warm-up and cool-down', '<p>Current example: 4 min warm-up / 3 min cool-down. Existing global Off or 1–15 whole-minute controls remain unchanged. C2 has neither phase. New starts use saved settings; recorded history stays frozen.</p>', button);
      break;
    case 'c2':
      open('C2 duration', '<label>Duration<input type="range" min="20" max="120" step="5" value="45" oninput="this.nextElementSibling.value=this.value+\' min\'"><output>45 min</output></label><p>Existing range and default are unchanged. Local demonstration only.</p>', button);
      break;
  }
});

dialog.addEventListener('click', event => {
  const b = event.target.closest('button');
  if (!b) return;
  if (b.hasAttribute('data-close')) close();
  if (b.dataset.protocol) {
    selected = b.dataset.protocol;
    close(); render(); app.querySelector('[data-action="change"]').focus();
  }
  if (b.hasAttribute('data-apply')) {
    if (editingC2) c2Target = dialog.querySelector('#work-zone').value;
    else { target = dialog.querySelector('#work-zone').value; recoveryTarget = dialog.querySelector('#recovery-zone').value; }
    close(); render(); app.querySelector('[data-action="personalise"]')?.focus();
    status.textContent = 'Synthetic target selection changed. No app default or historical recording was changed.';
  }
});
dialog.addEventListener('cancel', event => { event.preventDefault(); close(); });
document.querySelector('#sensor').addEventListener('change', () => render());
document.querySelector('#large').addEventListener('click', event => {
  const large = document.querySelector('.phone').classList.toggle('large');
  dialog.classList.toggle('large', large);
  event.target.setAttribute('aria-pressed', String(large));
});
render();
