const app = document.querySelector('#app');
let screen = 'summary';
let returnFocus = null;
const heading = title => `<p class="muted">LOOP · Synthetic saved run</p><h1 id="heading" tabindex="-1">${title}</h1>`;
const back = () => '<button type="button" data-action="back">← Run details</button>';
const row = (id, title, sub) => `<button type="button" class="detail-row" id="open-${id}" data-open="${id}"><span>${title}<small>${sub}</small></span><span aria-hidden="true">›</span></button>`;
function show(html, focus = '#heading') {
  app.innerHTML = html;
  requestAnimationFrame(() => app.querySelector(focus)?.focus());
}
function summary(focus) {
  screen = 'summary';
  show(heading('C1.2 · Run details') + '<p>15 Nov 2026 · 07:00 +01:00</p><p><strong>4.80 km total</strong></p><dl><dt>Total active / elapsed</dt><dd>30:00 / 31:15</dd><dt>Main active / elapsed</dt><dd>23:00 / 24:15</dd><dt>Main distance / avg pace</dt><dd>3.80 km / Unavailable</dd><dt>Main average heart rate</dt><dd>149 bpm · partial HR coverage</dd><dt>Health Connect</dt><dd>Not exported · synthetic example</dd></dl>' + row('splits', 'Splits & phases', '3 complete kilometres + final partial split') + row('zones', 'Time in zones', 'Whole run · frozen start boundaries') + row('route', 'Route', 'Whole recording · warm-up through cool-down') + '<details><summary>Workout plan</summary><p>5 × 3 min work · 4 × 2 min recovery. Warm-up 4 min · cool-down 3 min. These are this synthetic run’s frozen values, not recalculated from current Settings.</p></details>', focus);
}
const splits = [
  ['1', '1.00', '6:00', '6:00', '146', 'GPS 6:00 known / 0:00 unknown. HR 5:30 known / 0:30 unknown.'],
  ['2', '1.00', '6:10', 'Unavailable', '151', 'GPS 5:40 known / 0:30 unknown. HR 6:10 known / 0:00 unknown.'],
  ['3', '1.00', '6:05', '6:05', '150', 'GPS 6:05 known / 0:00 unknown. HR 5:35 known / 0:30 unknown.'],
  ['4 · final partial', '0.80', '4:45', 'Unavailable', '148', 'GPS 4:15 known / 0:30 unknown. HR 4:15 known / 0:30 unknown.']
];
function splitView() {
  screen = 'splits';
  show(back() + heading('Splits & phases') + '<h2>Main-set splits</h2><p>23:00 active · 3.80 km recorded. Final partial split included once.</p>' + '<table class="split-table"><caption>Main-set splits · time min:sec · distance km · pace min/km · heart rate bpm</caption><thead><tr><th scope="col">Split</th><th scope="col">Time</th><th scope="col">km</th><th scope="col">Avg pace</th><th scope="col">Avg HR</th></tr></thead><tbody>' + splits.map(s => `<tr><th scope="row">${s[0]}</th><td>${s[2]}</td><td>${s[1]}</td><td>${s[3] === 'Unavailable' ? '<span role="img" aria-label="Unavailable">—</span>' : s[3]}</td><td>${s[4]}</td></tr>`).join('') + '</tbody></table><p class="split-table-legend">— Unavailable. Missing measurements are not zero.</p><details><summary>Split measurement coverage</summary>' + splits.map(s => `<p>Split ${s[0]} · ${s[5]}</p>`).join('') + '</details>' + '<div class="split-cards">' + splits.map(s => `<section class="row"><h3>Split ${s[0]}</h3><dl class="metrics"><div><dt>Active · min:sec</dt><dd>${s[2]}</dd></div><div><dt>Distance · km</dt><dd>${s[1]}</dd></div><div><dt>Avg pace · min/km</dt><dd>${s[3]}</dd></div><div><dt>Avg heart rate · bpm</dt><dd>${s[4]}</dd></div></dl><p class="muted">${s[5]}</p></section>`).join('') + '</div>' + '<h2>Recorded phases</h2><p>Active time excludes pauses. Phase times come from the saved completed-phase records, not the planned dose.</p><table><caption>Completed phases · active min:sec</caption><thead><tr><th scope="col">Phase</th><th scope="col">Active</th></tr></thead><tbody>' + [['Warm-up', '4:00'], ['Work 1', '3:00'], ['Recovery 1', '2:00'], ['Work 2', '3:00'], ['Recovery 2', '2:00'], ['Work 3', '3:00'], ['Recovery 3', '2:00'], ['Work 4', '3:00'], ['Recovery 4', '2:00'], ['Work 5', '3:00'], ['Cool-down', '3:00']].map(s => `<tr><th scope="row">${s[0]}</th><td>${s[1]}</td></tr>`).join('') + '</tbody></table><p>Per-phase elapsed time, distance, pace and heart rate: Unavailable in this example. Values are not divided out of the whole run.</p><details><summary>Missing older records</summary><p>If the saved plan and completed phases cannot be matched, the affected phase time is Unavailable. A planned length is not substituted for a recorded value.</p></details>');
}
function zoneView() {
  screen = 'zones';
  const boundaries = ['106–133', '134–147', '148–161', '162–175', '176–190'];
  const scopes = [
    { id: 'main', name: 'Main set', active: '23:00', times: ['1:00', '6:00', '7:00', '5:00', '2:00'], outside: '0:30', known: '21:30', unknown: '1:30' },
    { id: 'total', name: 'Whole run · includes warm-up and cool-down', active: '30:00', times: ['3:00', '8:00', '7:00', '5:00', '3:00'], outside: '1:00', known: '27:00', unknown: '3:00' }
  ];
  show(back() + heading('Time in zones') + '<p>Frozen boundaries from this run’s start. Time is based on valid recorded heart rate, not the intended target.</p>' + scopes.map(scope => `<section data-scope="${scope.id}"><h2>${scope.name}</h2><p><strong><span class="scope-active">${scope.active}</span> active</strong></p><table><caption>Frozen heart-rate boundaries · bpm · time min:sec</caption><thead><tr><th scope="col">Zone</th><th scope="col">bpm</th><th scope="col">Time</th></tr></thead><tbody>${scope.times.map((time, index) => `<tr><th scope="row">Zone ${index + 1}</th><td>${boundaries[index]}</td><td class="zone-time">${time}</td></tr>`).join('')}</tbody></table><dl class="row"><dt>Known heart rate outside these zones</dt><dd class="outside-time">${scope.outside}${scope.id === 'total' ? ' (60 sec)' : ''}</dd><dt>Unknown heart rate</dt><dd class="unknown-time">${scope.unknown}</dd><dt>Known heart-rate coverage</dt><dd><span class="known-time">${scope.known}</span> of ${scope.active} active</dd></dl></section>`).join('') + '<p>Outside-zone time is known heart rate; unknown time is not assigned to a zone. The stored aggregate does not distinguish below Zone 1 from above maximum.</p><details><summary>Frozen setup</summary><p>Synthetic HRR example · resting 50 bpm, maximum 190 bpm. The saved integer ranges above remain unchanged when current Settings change.</p><p>If old zone durations or boundaries were not saved, show Unavailable rather than reconstructing them using today’s profile.</p></details>');
}
function routeView() {
  screen = 'route';
  const status = document.querySelector('#map-state').value;
  const messages = {
    loading: ['Loading map…', 'Your saved run is available while the map loads. You can go back at any time.'],
    offline: ['Map unavailable offline', 'The map background cannot load. Stored coordinates and the rest of this run remain available.'],
    key: ['Map is not configured', 'Map configuration is missing. Your recording and saved coordinates are unchanged.'],
    failed: ['Map could not load', 'The map failed to load. You can return to the saved run; its measurements remain available.'],
    empty: ['No recorded route', 'This run contains no usable stored coordinates. A route is not inferred from its distance or workout plan.']
  };
  const illustration = '<figure><svg viewBox="0 0 340 240" role="img" aria-labelledby="route-title route-description"><title id="route-title">Synthetic stored route with a gap</title><desc id="route-description">Two separate route segments. Circle marks the beginning, square the end. No joining line is drawn across the illustrative recording gap. This is not a street map.</desc><rect width="340" height="240" fill="#1c1c1c"/><path d="M35 190 L60 165 L70 130 L110 110 L135 78" fill="none" stroke="#c4da73" stroke-width="5"/><path d="M200 60 L225 85 L250 105 L270 150 L300 165" fill="none" stroke="#c4da73" stroke-width="5"/><circle cx="35" cy="190" r="8" fill="#f2f2f2"/><rect x="292" y="157" width="16" height="16" fill="#f2f2f2"/><circle cx="135" cy="78" r="5" fill="#b9b9b9"/><circle cx="200" cy="60" r="5" fill="#b9b9b9"/></svg><figcaption>Synthetic illustration · no Google tiles or SDK. Circle: start. Square: end. Separate segments: recording gap.</figcaption></figure>';
  show(back() + heading('Recorded route') + '<p>Entire stored recording, including warm-up and cool-down.</p>' + (status === 'ready' ? illustration : status === 'legacy' ? '<section class="card"><h2>Older track · quality unknown</h2><p>This older run has stored GPS points, but not enough information about their quality or recording gaps. Points are shown separately; we cannot reliably join them into a route.</p><svg viewBox="0 0 340 150" role="img" aria-label="Synthetic isolated stored points without connecting lines"><g fill="#b9b9b9"><circle cx="35" cy="110" r="4"/><circle cx="70" cy="70" r="4"/><circle cx="135" cy="35" r="4"/><circle cx="200" cy="45" r="4"/><circle cx="270" cy="100" r="4"/></g></svg><p class="muted">Synthetic stored-point illustration only. No connecting line or inferred quality.</p></section>' : `<section class="card"><h2>${messages[status][0]}</h2><p>${messages[status][1]}</p></section>`) + `<dl><dt>Recorded total distance</dt><dd>4.80 km</dd><dt>Route completeness</dt><dd>${status === 'empty' ? 'Unavailable · no stored coordinates' : status === 'legacy' ? 'Unknown · older GPS quality and gaps were not fully saved' : 'Known recording gaps · synthetic example'}</dd></dl><p>No line is drawn across a known gap. The route picture and recorded distance are different views of the saved run.</p><details><summary>Track and privacy</summary><p>Stored GPS points and available accuracy belong to this run. Missing parts are not filled in.</p><p>Sharing precise coordinates remains behind the existing Share workout data disclosure in run details; this study adds no new sharing or location permission.</p></details>`);
}
app.addEventListener('click', event => {
  const control = event.target.closest('button');
  if (!control) return;
  if (control.dataset.open) {
    returnFocus = '#open-' + control.dataset.open;
    ({ splits: splitView, zones: zoneView, route: routeView })[control.dataset.open]();
  }
  if (control.dataset.action === 'back') summary(returnFocus);
});
document.querySelector('#large-text').addEventListener('change', event => document.body.classList.toggle('large', event.target.checked));
document.querySelector('#map-state').addEventListener('change', () => { if (screen === 'route') routeView(); });
window.studyState = () => ({ screen, mapState: document.querySelector('#map-state').value });
summary();
