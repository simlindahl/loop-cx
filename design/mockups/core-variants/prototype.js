const app = document.querySelector('#app');
const dialog = document.querySelector('#dialog');
const reviewEvent = document.querySelector('#review-event');
const variants = {
  'C1.1': { title: 'Sprint intervals (SIT)', minutes: 0.5, minCount: 4, maxCount: 18, work: '30 sec', recovery: 3, count: 7, main: 21.5, rpe: '10 / 2', purpose: 'Repeated maximal, all-out sprints with substantial easy recovery. A maximal-effort option, not an automatic progression.' },
  'C1.2': { title: 'Aerobic intervals (HIIT)', minutes: 3, minCount: 3, maxCount: 12, work: '3 min', recovery: 2, count: 5, main: 23, rpe: '7 / 3', purpose: 'Develop aerobic power through sustained, controlled hard efforts. Keep the efforts repeatable.' },
  'C1.3': { title: 'Sustained intervals (Threshold)', minutes: 6, minCount: 2, maxCount: 7, work: '6 min', recovery: 3, count: 3, main: 24, rpe: '6 / 3', purpose: 'Practise a controlled, sustained effort near your individual threshold. Prioritise sustainable pacing.' }
};
const counts = Object.fromEntries(Object.entries(variants).map(([id, value]) => [id, value.count]));
const settings = { warmup: 4, cooldown: 3, overridden: false };
const targets = { 'C1.1': { mode: 'none' }, 'C1.2': { mode: 'none' }, 'C1.3': { mode: 'none' }, C2: { mode: 'none' } };
let selected = 'C1.2';
let c2Duration = 45;
let favourites = [];
let starts = [];
let trigger = null;
let targetDraft = null;
let nextFavouriteId = 1;
const escapeText = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const clone = value => JSON.parse(JSON.stringify(value));

function durationLabel(value) {
  return Number.isInteger(value) ? `${value} min` : `${Math.floor(value)}:${String(Math.round((value % 1) * 60)).padStart(2, '0')} min:sec`;
}

function mainMinutes(id, count = counts[id]) {
  return count * variants[id].minutes + (count - 1) * variants[id].recovery;
}

function total(id, count = counts[id]) {
  return mainMinutes(id, count) + settings.warmup + settings.cooldown;
}

function targetLabel(target) {
  if (target.mode === 'none') return 'No additional pace or heart-rate target';
  const values = [ `${target.continuous ? 'Continuous' : 'Work'}: ${target.work || 'not set'}` ];
  if (!target.continuous) values.push(target.recovery ? `Recovery: ${target.recovery}` : 'Recovery: RPE only');
  return `${target.mode === 'pace' ? 'Target pace' : 'Target zone'} · ${values.join(' · ')}`;
}

function rpe(id) {
  return `<div class="rpe"><p>Target RPE <strong>${id === 'C2' ? '4' : variants[id].rpe}</strong>${id === 'C2' ? '' : ' · work / recovery'}</p><button type="button" class="info" data-action="rpe" data-id="${id}" aria-label="Explain target RPE for ${id}"><span aria-hidden="true">ⓘ</span></button></div>`;
}

function render() {
  const v = variants[selected];
  const prep = settings;
  let html = '<p class="eyebrow">LOOP Cx</p><h2>Start a run</h2>';
  html += `<section class="card"><div class="card-heading"><h3>C1 · Intervals</h3><button type="button" class="text" data-action="change">Change</button></div><p class="dose"><strong>${selected} · ${v.title}</strong><br>${counts[selected]} × ${v.work} work · ${counts[selected] - 1} × ${v.recovery} min recovery</p><p>${v.purpose}</p>${rpe(selected)}<p>${durationLabel(mainMinutes(selected))} main · <strong>${durationLabel(total(selected))} total</strong><small>${prep.warmup} min warm-up · ${prep.cooldown} min cool-down<br>${prep.overridden ? 'Settings override' : 'Official variant defaults'}</small></p><p class="small">${escapeText(targetLabel(targets[selected]))}</p><div class="actions"><button type="button" class="primary" data-action="start" data-id="${selected}">Start ${selected}</button><button type="button" data-action="adapt" data-id="${selected}">Personalise</button></div><button type="button" class="text" data-action="favourite" data-id="${selected}">Save shortcut</button></section>`;
  html += `<section class="card"><div class="card-heading"><h3>C2 · Zone 2</h3><button type="button" class="text" data-action="change-c2" aria-label="Change C2 duration">Change</button></div><p class="dose">Steady, conversational running.<br><strong>${c2Duration} min main / total</strong></p>${rpe('C2')}<p class="small">Zone 2 is the intended effort, not a measured value. No warm-up or cool-down.</p><p class="small">${escapeText(targetLabel(targets.C2))}</p><div class="actions"><button type="button" class="primary" data-action="start" data-id="C2">Start C2</button><button type="button" data-action="adapt" data-id="C2">Adapt target</button></div><button type="button" class="text" data-action="favourite" data-id="C2">Save shortcut</button></section>`;
  if (favourites.length) html += '<h3>Saved shortcuts</h3>';
  for (const favourite of favourites) {
    html += `<section class="card"><h3>${escapeText(favourite.name)}</h3><p class="dose">${favourite.variant} · ${favourite.variant === 'C2' ? `${favourite.duration} min main / total` : `${favourite.count} work / ${favourite.count - 1} recovery<br>${durationLabel(mainMinutes(favourite.variant, favourite.count))} main / ${durationLabel(total(favourite.variant, favourite.count))} total`}<br>Target RPE ${favourite.variant === 'C2' ? '4' : variants[favourite.variant].rpe}</p><p class="small">${escapeText(targetLabel(favourite.target))}</p><button type="button" class="primary" data-action="start-favourite" data-id="${favourite.id}">Start ${escapeText(favourite.name)}</button></section>`;
  }
  app.innerHTML = html;
}

function openDialog(title, body, opener) {
  trigger = opener;
  dialog.innerHTML = `<h2 id="dialog-title" tabindex="-1">${title}</h2>${body}`;
  dialog.showModal();
  dialog.querySelector('#dialog-title').focus();
}

function closeDialog() {
  dialog.close();
}

dialog.addEventListener('keydown', event => {
  if (event.key !== 'Tab') return;
  const controls = [...dialog.querySelectorAll('button, input:not([type="hidden"]), select, [tabindex="0"]')].filter(element => !element.disabled && element.getClientRects().length);
  const first = controls[0];
  const last = controls.at(-1);
  if (!first) return;
  if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog.querySelector('#dialog-title'))) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
});

dialog.addEventListener('close', () => {
  if (!trigger) return;
  const replacement = trigger.dataset?.action ? app.querySelector(`[data-action="${trigger.dataset.action}"]${trigger.dataset.id ? `[data-id="${trigger.dataset.id}"]` : ''}`) : trigger;
  (replacement || document.querySelector('#review-settings')).focus({ preventScroll: true });
});

function openRpe(id, opener) {
  const cues = {
    'C1.1': 'Work 10: maximal, all-out sprinting. Recovery 2: easy.',
    'C1.2': 'Work 7: hard but repeatable. Recovery 3: easy.',
    'C1.3': 'Work 6: controlled, sustained effort. Recovery 3: easy.',
    C2: 'Target 4: steady, conversational running. Keep the effort sustainable throughout the run.'
  };
  const guidance = id === 'C1.1'
    ? 'This is a maximal-effort option, not an automatic next step. Keep recoveries easy. Stop rather than force a sprint when control is lost. Perceived effort is not a measured heart-rate value.'
    : id === 'C2'
    ? 'Keep the effort comfortable rather than trying to run faster. The target is a guide, not a score to beat. Perceived effort describes how you feel today, not a measured heart-rate value.'
    : 'Aim for the planned effort. Stay in control so you can repeat the next effort; this is not an all-out sprint. Perceived effort describes how you feel, not a measured heart-rate value.';
  openDialog('What does RPE mean?', `<p>RPE means Rating of Perceived Exertion: how hard the effort feels, from 1 (very easy) to 10 (maximum).</p><p><strong>${id}: ${cues[id]}</strong></p><p>${guidance}</p><button type="button" data-dialog="close">Close explanation</button>`, opener);
}

function chooseVariant(opener) {
  let body = '<form id="variant-form"><fieldset><legend>Official C1 protocol</legend>';
  for (const [id, v] of Object.entries(variants)) {
    body += `<label class="choice"><input type="radio" name="variant" value="${id}" ${selected === id ? 'checked' : ''}><span><strong>${id} · ${v.title}</strong><small>${v.count} × ${v.work} / ${v.recovery} min recovery<br>RPE ${v.rpe} · ${durationLabel(v.main)} main / ${durationLabel(total(id, v.count))} total</small>${v.purpose}</span></label>`;
  }
  body += '</fieldset><p class="small">Selecting a protocol uses its official repetition default. Saved shortcuts keep their own count.</p><div class="actions"><button class="primary" type="submit">Use variant</button><button type="button" data-dialog="close">Cancel</button></div></form>';
  openDialog('Choose C1', body, opener);
}

function chooseC2Duration(opener) {
  openDialog('Choose C2 duration', `<form id="c2-duration-form" novalidate><p>Continuous running · Target RPE 4 · No warm-up or cool-down</p><div id="duration-errors" role="alert"></div><div class="field"><label for="c2-duration">Duration: <output id="c2-duration-value" for="c2-duration">${c2Duration} minutes</output></label><input id="c2-duration" name="duration" type="range" min="20" max="120" step="5" value="${c2Duration}" aria-valuetext="${c2Duration} minutes"><p class="small">20–120 minutes, in 5-minute steps. Main time equals total time.</p></div><div class="actions"><button type="submit" class="primary">Use duration</button><button type="button" data-dialog="close">Cancel</button></div></form>`, opener);
}

function targetForm(id, opener, retained = null) {
  targetDraft = { id, count: counts[id], ...clone(retained || targets[id]) };
  openDialog(`${id === 'C2' ? 'Target' : 'Personalise'} · ${id}`, '<form id="target-form" novalidate><p>RPE stays fixed. Add one optional measurable target type; this is a target, not a live reading.</p><div id="target-errors" role="alert"></div><div class="field"><label for="target-mode">Additional target</label><select id="target-mode"><option value="none">None · RPE only</option><option value="pace">Pace</option><option value="zone">Heart-rate zone</option></select></div><div id="target-fields"></div><div class="actions"><button type="submit" class="primary">Use target</button><button type="button" data-dialog="close">Cancel</button></div></form>', opener);
  dialog.querySelector('#target-mode').value = targetDraft.mode;
  if (id !== 'C2') {
    const v = variants[id];
    dialog.querySelector('#target-form > p').textContent = 'Choose the repetition count. Work/recovery lengths and RPE stay fixed. You can also add pace or a heart-rate zone.';
    dialog.querySelector('#target-form button[type="submit"]').textContent = 'Use changes';
    dialog.querySelector('#target-mode').closest('.field').insertAdjacentHTML('beforebegin', `<div class="field"><label for="rep-count">Work repetitions</label><div class="count-controls"><button type="button" data-count="-1" aria-label="One fewer work effort">−</button><input id="rep-count" type="number" min="${v.minCount}" max="${v.maxCount}" step="1" inputmode="numeric" value="${counts[id]}" aria-describedby="count-help"><button type="button" data-count="1" aria-label="One more work effort">+</button></div><p class="small" id="count-help">${v.minCount}–${v.maxCount} efforts · work ${v.work} / recovery ${v.recovery} min fixed. Main set includes recovery and must be 10–60 min; warm-up and cool-down are outside that limit.</p><p>Target RPE <strong>${v.rpe}</strong> · work / recovery</p><p id="dose-preview" role="status"></p></div>`);
    updateDosePreview();
  }
  renderTargetFields();
}

function updateDosePreview() {
  const input = dialog.querySelector('#rep-count');
  if (!input) return;
  const value = Number(input.value);
  const v = variants[targetDraft.id];
  const valid = Number.isInteger(value) && value >= v.minCount && value <= v.maxCount;
  dialog.querySelector('#dose-preview').textContent = valid ? `${value} work / ${value - 1} recovery · ${durationLabel(mainMinutes(targetDraft.id, value))} main / ${durationLabel(total(targetDraft.id, value))} total` : 'Use a whole repetition count within the displayed limits.';
  dialog.querySelector('[data-count="-1"]').disabled = !valid || value === v.minCount;
  dialog.querySelector('[data-count="1"]').disabled = !valid || value === v.maxCount;
}

function renderTargetFields() {
  const mode = targetDraft.mode;
  const zone = label => `<option value="">Not set</option>${[1, 2, 3, 4, 5].map(value => `<option value="Zone ${value}">Zone ${value}</option>`).join('')}`;
  let html = '';
  if (mode !== 'none') {
    for (const key of targetDraft.id === 'C2' ? ['work'] : ['work', 'recovery']) {
      const label = key === 'work' ? targetDraft.id === 'C2' ? 'Continuous target' : 'Work target' : 'Recovery target (optional)';
      html += `<div class="field"><label for="target-${key}">${label}${mode === 'pace' ? ' (min/km)' : ''}</label>${mode === 'pace' ? `<input id="target-${key}" value="${escapeText(targetDraft[key] || '')}" placeholder="5:30" inputmode="numeric">` : `<select id="target-${key}">${zone(label)}</select>`}</div>`;
    }
    if (mode === 'zone') html += '<p class="small">Zone boundaries are personal Settings, frozen at start. Missing heart-rate readings remain unknown.</p>';
  }
  dialog.querySelector('#target-fields').innerHTML = html;
  if (mode === 'zone') for (const key of ['work', 'recovery']) dialog.querySelector(`#target-${key}`) && (dialog.querySelector(`#target-${key}`).value = targetDraft[key] || '');
}

function settingsDialog(opener) {
  const body = `<form id="settings-form" novalidate><p>One global pair applies to all C1 variants at their next start, including saved shortcuts. Official defaults are 4 min warm-up and 3 min cool-down.</p><p>C2 has no warm-up or cool-down. Started and past runs keep their frozen plan.</p><div id="settings-errors" role="alert"></div><div class="field"><label for="c1-warmup">C1 warm-up (minutes; 0 is Off)</label><input id="c1-warmup" name="c1-warmup" type="number" min="0" max="15" step="1" value="${settings.warmup}"></div><div class="field"><label for="c1-cooldown">C1 cool-down (minutes; 0 is Off)</label><input id="c1-cooldown" name="c1-cooldown" type="number" min="0" max="15" step="1" value="${settings.cooldown}"></div><p class="small">During a run, +1 min and skip remain available without changing these Settings.</p><div class="actions"><button type="submit" class="primary">Use Settings preview</button><button type="button" data-dialog="close">Cancel</button></div></form>`;
  openDialog('C1 warm-up and cool-down Settings', body, opener);
}

function start(id, target, name = '', duration = c2Duration, count = counts[id]) {
  const plan = { variant: id, sourceRevision: 'proposed variant set', name, target: clone(target), rpe: id === 'C2' ? '4' : variants[id].rpe, count: id === 'C2' ? null : count, recoveryCount: id === 'C2' ? null : count - 1, main: id === 'C2' ? duration : mainMinutes(id, count), preparation: id === 'C2' ? { warmup: 0, cooldown: 0 } : clone(settings) };
  plan.total = plan.main + plan.preparation.warmup + plan.preparation.cooldown;
  starts.push(plan);
  document.querySelector('#snapshots').innerHTML = starts.map((value, index) => `<li>Preview ${index + 1}: ${value.variant} · ${durationLabel(value.main)} main / ${durationLabel(value.total)} total · warm-up/cool-down ${value.preparation.warmup}/${value.preparation.cooldown} · ${escapeText(targetLabel(value.target))}</li>`).join('');
  reviewEvent.textContent = `Frozen synthetic start preview: ${id}. No real recording started. Earlier snapshots remain unchanged.`;
}

app.addEventListener('click', event => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;
  const id = button.dataset.id;
  switch (button.dataset.action) {
    case 'change': chooseVariant(button); break;
    case 'change-c2': chooseC2Duration(button); break;
    case 'rpe': openRpe(id, button); break;
    case 'adapt': targetForm(id, button); break;
    case 'start': start(id, targets[id]); break;
    case 'start-favourite': {
      const favourite = favourites.find(value => value.id === Number(id));
      start(favourite.variant, favourite.target, favourite.name, favourite.duration, favourite.count);
      break;
    }
    case 'favourite':
      openDialog('Save a shortcut', `<form id="favourite-form"><p>Save ${id}${id === 'C2' ? ` at ${c2Duration} minutes` : ` with ${counts[id]} work efforts`} and its current target. C1 starts use the current warm-up and cool-down Settings. C2 has no warm-up or cool-down. Previous runs keep their frozen plan.</p><div class="field"><label for="shortcut-name">Shortcut name</label><input id="shortcut-name" required autocomplete="off"></div><div class="actions"><button type="submit" class="primary">Save shortcut</button><button type="button" data-dialog="close">Cancel</button></div><input type="hidden" name="variant" value="${id}"></form>`, button);
      break;
  }
});

dialog.addEventListener('input', event => {
  if (event.target.id === 'rep-count') {
    event.target.removeAttribute('aria-invalid');
    dialog.querySelector('#target-errors').textContent = '';
    updateDosePreview();
  }
  if (event.target.id === 'c2-duration') {
    dialog.querySelector('#c2-duration-value').textContent = `${event.target.value} minutes`;
    event.target.setAttribute('aria-valuetext', `${event.target.value} minutes`);
  }
});

dialog.addEventListener('click', event => {
  const step = event.target.closest('[data-count]');
  if (step) {
    const input = dialog.querySelector('#rep-count');
    input.value = Number(input.value) + Number(step.dataset.count);
    updateDosePreview();
  }
  if (event.target.closest('[data-dialog="close"]')) closeDialog();
});

dialog.addEventListener('change', event => {
  if (event.target.id !== 'target-mode') return;
  targetDraft.mode = event.target.value;
  delete targetDraft.work;
  delete targetDraft.recovery;
  renderTargetFields();
});

dialog.addEventListener('submit', event => {
  event.preventDefault();
  if (event.target.id === 'variant-form') {
    selected = event.target.elements.variant.value;
    counts[selected] = variants[selected].count;
    render();
  } else if (event.target.id === 'c2-duration-form') {
    const duration = Number(event.target.elements.duration.value);
    if (!Number.isInteger(duration) || duration < 20 || duration > 120 || duration % 5 !== 0) {
      dialog.querySelector('#duration-errors').innerHTML = '<p class="error">Choose 20–120 minutes in 5-minute steps.</p>';
      return;
    }
    c2Duration = duration;
    render();
  } else if (event.target.id === 'target-form') {
    const repInput = dialog.querySelector('#rep-count');
    if (repInput) {
      const value = Number(repInput.value);
      const v = variants[targetDraft.id];
      if (!Number.isInteger(value) || value < v.minCount || value > v.maxCount) {
        dialog.querySelector('#target-errors').innerHTML = '<p class="error">Use a whole repetition count within the displayed limits.</p>';
        repInput.setAttribute('aria-invalid', 'true');
        repInput.focus();
        return;
      }
    }
    const saved = { mode: targetDraft.mode, continuous: targetDraft.id === 'C2' };
    for (const key of ['work', 'recovery']) saved[key] = dialog.querySelector(`#target-${key}`)?.value.trim() || '';
    const validPace = value => /^\d+:[0-5]\d$/.test(value) && Number(value.replace(':', '')) > 0;
    const valid = saved.mode === 'none' || saved.work && (saved.mode === 'zone' || validPace(saved.work) && (!saved.recovery || validPace(saved.recovery)));
    if (!valid) {
      dialog.querySelector('#target-errors').innerHTML = '<p class="error">Set a work target. Use positive pace as min:ss, for example 5:30.</p>';
      dialog.querySelector('#target-work').focus();
      return;
    }
    targets[targetDraft.id] = saved;
    if (repInput) counts[targetDraft.id] = Number(repInput.value);
    render();
  } else if (event.target.id === 'favourite-form') {
    const name = event.target.elements['shortcut-name'].value.trim();
    if (!name) return;
    const id = event.target.elements.variant.value;
    favourites.push({ id: nextFavouriteId++, variant: id, name, count: id === 'C2' ? null : counts[id], duration: id === 'C2' ? c2Duration : null, target: clone(targets[id]) });
    render();
  } else if (event.target.id === 'settings-form') {
    const warmup = Number(event.target.elements['c1-warmup'].value);
    const cooldown = Number(event.target.elements['c1-cooldown'].value);
    if (![warmup, cooldown].every(value => Number.isInteger(value) && value >= 0 && value <= 15)) {
      dialog.querySelector('#settings-errors').innerHTML = '<p class="error">Use Off (0) or whole minutes from 1 to 15.</p>';
      return;
    }
    Object.assign(settings, { warmup, cooldown, overridden: warmup !== 4 || cooldown !== 3 });
    render();
  }
  closeDialog();
});

document.querySelector('#review-settings').addEventListener('click', event => settingsDialog(event.currentTarget));
window.prototypeState = () => clone({ selected, counts, c2Duration, settings, targets, favourites, starts });
render();
