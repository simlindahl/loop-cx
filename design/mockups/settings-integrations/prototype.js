const app = document.querySelector('#app');
const reviewEvent = document.querySelector('#review-event');
const phone = document.querySelector('.phone');
let screen = 'settings';
let sensor = 'not-connected';
let zones = { resting: 50, maximum: 190 };
let audio = { mode: 'time', interval: '5:00' };
const status = { 'not-connected': 'Not connected', available: 'Sensor available', connected: 'Connected', missing: 'Signal missing', empty: 'No sensor found', denied: 'Bluetooth access needed' };
const escapeText = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');

function show(html) {
  app.innerHTML = html;
  phone.scrollTop = 0;
  app.querySelector('h2').focus({ preventScroll: true });
}

function heading(title) {
  return `<button type="button" class="text" data-action="settings">← Settings</button><h2 tabindex="-1">${title}</h2>`;
}

function settings() {
  screen = 'settings';
  show(`<p class="eyebrow">LOOP Cx</p><h2 tabindex="-1">Settings</h2><p>Set once. Keep running.</p><button type="button" class="setting-row" data-action="devices"><span>Heart-rate connection<small>${status[sensor]}</small></span><span aria-hidden="true">→</span></button><button type="button" class="setting-row" data-action="zones"><span>Heart-rate zones<small>Personal boundaries</small></span><span aria-hidden="true">→</span></button><button type="button" class="setting-row" data-action="audio"><span>Audio coaching<small>Saved shortcut · Easy 45</small></span><span aria-hidden="true">→</span></button><nav aria-label="Existing destinations"><span>Training</span><span>History</span><span aria-current="page">Settings</span></nav>`);
}

function devices() {
  screen = 'devices';
  let content = '';
  if (sensor === 'connected') content = '<p><strong>Connected</strong></p><p>WHOOP · heart-rate broadcast</p><p>Heart rate <strong>64 bpm</strong></p><p class="small">Synthetic reading for layout review.</p><button type="button" data-action="disconnect">Disconnect</button>';
  else if (sensor === 'available') content = '<p><strong>Choose your sensor</strong></p><p>Confirm the sensor is yours before connecting. No device is chosen automatically.</p><section class="card"><h3>WHOOP</h3><p>Heart-rate broadcast sensor · synthetic example</p><button type="button" class="primary" data-action="connect">Connect selected WHOOP</button></section>';
  else if (sensor === 'missing') content = '<p><strong>Signal missing</strong></p><p>Heart rate —</p><p>Your run can continue. Heart-rate coaching waits for valid readings. Reconnecting…</p><button type="button" data-action="find">Find a sensor</button>';
  else if (sensor === 'empty') content = '<p><strong>No sensor found yet</strong></p><p>Enable Bluetooth heart-rate broadcast if your sensor requires it, keep it nearby and check Nearby devices access.</p><button type="button" class="primary" data-action="find">Try again</button><button type="button" class="text" data-action="permissions">Review Android access</button>';
  else if (sensor === 'denied') content = '<p><strong>Bluetooth access needed</strong></p><p>Heart-rate connection is unavailable without Nearby devices access. Runs can continue without heart rate.</p><button type="button" data-action="permissions">Review Android access</button>';
  else content = '<p><strong>Not connected</strong></p><p>Connect a compatible Bluetooth LE Heart Rate sensor. If your sensor requires heart-rate broadcast, turn it on and keep the sensor nearby.</p><button type="button" class="primary" data-action="find">Find a sensor</button><p class="small">You can run without heart rate. Missing readings stay unknown.</p>';
  show(heading('Heart-rate connection') + '<p class="small">For sensors that broadcast the standard Bluetooth LE Heart Rate service. This does not imply support for every watch, ANT+ or cloud connections. For example, a WHOOP band needs heart-rate broadcast enabled.</p>' + content);
}

function zoneValues(resting, maximum) {
  if (!Number.isInteger(resting) || !Number.isInteger(maximum) || resting <= 0 || maximum <= resting) return null;
  const lower = [40, 60, 70, 80, 90].map(percent => Math.ceil((resting * 100 + percent * (maximum - resting)) / 100));
  if (lower.some((value, index) => index > 0 && value <= lower[index - 1])) return null;
  return lower;
}

function zonePreview(resting, maximum) {
  const lower = zoneValues(resting, maximum);
  if (!lower) return '<p>Enter valid values to see calculated zones.</p>';
  const percentages = [40, 20, 10, 10, 10, 10];
  const segments = percentages.map((width, index) => `<span style="--portion:${width};--zone-colour:var(--hrr-zone-${index})" class="hrr-segment"><span>${index === 0 ? 'Below 1' : index}</span></span>`).join('');
  const ranges = lower.map((value, index) => `<div style="--zone-colour:var(--hrr-zone-${index + 1})"><dt><span class="hrr-swatch" aria-hidden="true"></span>Zone ${index + 1}</dt><dd>${value}–${index < 4 ? lower[index + 1] - 1 : maximum} <span>bpm</span></dd></div>`).join('');
  return `<figure class="hrr-overview"><figcaption>Your heart-rate range</figcaption><div class="hrr-scale" aria-hidden="true">${segments}</div><div class="hrr-axis" aria-hidden="true"><span>${resting} bpm<br>resting</span><span>${maximum} bpm<br>maximum</span></div><p class="small">Bar widths show heart-rate reserve, not time in zones.</p></figure><dl class="hrr-ranges"><div class="outside-zone"><dt>Below Zone 1</dt><dd>Below ${lower[0]} <span>bpm</span></dd></div>${ranges}<div class="outside-zone"><dt>Above configured maximum</dt><dd>Above ${maximum} <span>bpm</span></dd></div></dl><p class="small">Valid heart-rate readings outside the zones stay known.</p>`;
}

function zoneForm() {
  screen = 'zones';
  show(heading('Your heart-rate zones') + `<p>Heart-rate reserve</p><p>Enter your own resting and maximum heart rates. This study uses synthetic example values, not your measurements.</p><form id="zones-form" novalidate><div id="zone-errors" role="alert"></div><div class="field"><label for="resting-heart-rate">Resting heart rate (bpm)</label><input id="resting-heart-rate" name="resting" type="number" step="1" inputmode="numeric" value="${zones.resting}" required></div><div class="field"><label for="maximum-heart-rate">Maximum heart rate (bpm)</label><input id="maximum-heart-rate" name="maximum" type="number" step="1" inputmode="numeric" value="${zones.maximum}" required></div><div id="zone-summary">${zonePreview(zones.resting, zones.maximum)}</div><p class="small">Save applies to new runs. Active runs and saved history keep their original zones. No automatic updates from your sensor or cloud.</p><div class="actions"><button type="submit" class="primary">Save zones</button><button type="button" data-action="settings">Cancel</button></div></form>`);
}

function audioForm() {
  screen = 'audio';
  show(heading('Audio coaching') + `<p>Saved shortcut · <strong>Easy 45</strong></p><form id="audio-form" novalidate><div id="audio-errors" role="alert"></div><p>Phase announcements include target RPE. Short phase cues take priority; summaries do not overlap.</p><div class="field"><label for="audio-mode">Summary trigger</label><select id="audio-mode" name="mode"><option value="time">Active time</option><option value="distance">Distance</option></select></div><div class="field"><label for="audio-interval" id="audio-label"></label><input id="audio-interval" name="interval" value="${escapeText(audio.interval)}" required></div><p class="small">Summary frequency is independent of kilometre splits. Audio is in English.</p><p>Deviation cues apply only with a measurable pace or heart-rate target and valid data. RPE alone does not trigger deviation coaching.</p><div class="actions"><button type="submit" class="primary">Save audio settings</button><button type="button" data-action="settings">Cancel</button></div></form>`);
  app.querySelector('#audio-mode').value = audio.mode;
  audioLabels();
}

function audioLabels() {
  const distance = app.querySelector('#audio-mode').value === 'distance';
  app.querySelector('#audio-label').textContent = distance ? 'Report every (metres)' : 'Report every (min:ss)';
  app.querySelector('#audio-interval').inputMode = distance ? 'decimal' : 'numeric';
}

app.addEventListener('click', event => {
  const action = event.target.closest('[data-action]')?.dataset.action;
  if (action === 'settings') settings();
  if (action === 'devices') devices();
  if (action === 'zones') zoneForm();
  if (action === 'audio') audioForm();
  if (action === 'find') { sensor = 'available'; devices(); reviewEvent.textContent = 'Synthetic device-selection illustration. No Bluetooth scan or Android permission was requested.'; }
  if (action === 'connect') { sensor = 'connected'; devices(); }
  if (action === 'disconnect') { sensor = 'not-connected'; devices(); }
  if (action === 'permissions') reviewEvent.textContent = 'Production would open the appropriate Android settings/rationale path. This study does not open it or request access.';
});

app.addEventListener('input', event => {
  event.target.removeAttribute('aria-invalid');
  if (screen === 'zones') {
    app.querySelector('#zone-errors').textContent = '';
    const resting = Number(app.querySelector('#resting-heart-rate').value);
    const maximum = Number(app.querySelector('#maximum-heart-rate').value);
    app.querySelector('#zone-summary').innerHTML = zonePreview(resting, maximum);
  }
  if (screen === 'audio') app.querySelector('#audio-errors').textContent = '';
});

app.addEventListener('change', event => {
  if (event.target.id !== 'audio-mode') return;
  app.querySelector('#audio-interval').value = event.target.value === 'distance' ? '500' : '5:00';
  audioLabels();
});

app.addEventListener('submit', event => {
  event.preventDefault();
  if (event.target.id === 'zones-form') {
    const resting = Number(event.target.elements.resting.value);
    const maximum = Number(event.target.elements.maximum.value);
    if (!zoneValues(resting, maximum)) {
      app.querySelector('#zone-errors').innerHTML = '<p class="error">Use positive whole bpm. Maximum must exceed resting heart rate and produce distinct whole-bpm zone ranges.</p>';
      const input = app.querySelector(!Number.isInteger(resting) || resting <= 0 ? '#resting-heart-rate' : '#maximum-heart-rate');
      input.setAttribute('aria-invalid', 'true');
      input.focus();
      return;
    }
    zones = { resting, maximum };
    reviewEvent.textContent = 'Zones saved only in this study for future synthetic starts. No active or historical run changed.';
  }
  if (event.target.id === 'audio-form') {
    const mode = event.target.elements.mode.value;
    const interval = event.target.elements.interval.value.trim();
    const match = /^(\d+):([0-5]\d)$/.exec(interval);
    const valid = mode === 'time' ? match && Number(match[1]) * 60 + Number(match[2]) > 0 : /^\d+(\.\d+)?$/.test(interval) && Number(interval) > 0;
    if (!valid) {
      app.querySelector('#audio-errors').innerHTML = '<p class="error">Use a positive interval: min:ss for time, or metres for distance.</p>';
      app.querySelector('#audio-interval').setAttribute('aria-invalid', 'true');
      app.querySelector('#audio-interval').focus();
      return;
    }
    audio = { mode, interval };
    reviewEvent.textContent = 'Audio settings saved for the synthetic Easy 45 shortcut only. No sound played.';
  }
  settings();
});

document.querySelectorAll('[data-review]').forEach(button => button.addEventListener('click', () => { sensor = button.dataset.review; devices(); }));
window.prototypeState = () => JSON.parse(JSON.stringify({ screen, sensor, zones, audio }));
settings();
