const app = document.querySelector('#app');
const dialog = document.querySelector('#dialog');
const reviewEvent = document.querySelector('#review-event');
const variants = {
  'C1.1': { title: 'Sprint intervals (SIT)', work: '30 sec', recovery: 4, count: 6, main: 23, rpe: '10 / 2', purpose: 'Repeated maximal, all-out sprints with substantial easy recovery. A maximal-effort option, not an automatic progression.' },
  'C1.2': { title: 'Aerobic intervals (HIIT)', work: '3 min', recovery: 2, count: 5, main: 23, rpe: '7 / 3', purpose: 'Develop aerobic power through sustained, controlled hard efforts. Keep the efforts repeatable.' },
  'C1.3': { title: 'Sustained intervals (Threshold)', work: '6 min', recovery: 3, count: 3, main: 24, rpe: '6 / 3', purpose: 'Practise a controlled, sustained effort near your individual threshold. Prioritise sustainable pacing.' }
};
const settings = { warmup: 4, cooldown: 3, overridden: false };
const targets = { 'C1.1': { mode: 'none' }, 'C1.2': { mode: 'none' }, 'C1.3': { mode: 'none' }, C2: { mode: 'none' } };
let selected = 'C1.2';
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

function total(id) {
  return variants[id].main + settings.warmup + settings.cooldown;
}

function targetLabel(target) {
  if (target.mode === 'none') return 'No additional pace or heart-rate target';
  const values = [ `${target.continuous ? 'Continuous' : 'Work'}: ${target.work || 'not set'}` ];
  if (!target.continuous) values.push(target.recovery ? `Recovery: ${target.recovery}` : 'Recovery: RPE only');
  return `${target.mode === 'pace' ? 'Target pace' : 'Target zone'} · ${values.join(' · ')}`;
}

function rpe(id) {
  return `<div class="rpe"><p>Target RPE <strong>${id === 'C2' ? '3' : variants[id].rpe}</strong>${id === 'C2' ? '' : ' · work / recovery'}</p><button type="button" class="info" data-action="rpe" data-id="${id}" aria-label="Explain target RPE for ${id}"><span aria-hidden="true">ⓘ</span></button></div>`;
}

function render() {
  const v = variants[selected];
  const prep = settings;
  let html = '<p class="eyebrow">LOOP Cx</p><h2>Start a run</h2>';
  html += `<section class="card"><div class="card-heading"><h3>C1 · Intervals</h3><button type="button" class="text" data-action="change">Change</button></div><p class="dose"><strong>${selected} · ${v.title}</strong><br>${v.count} × ${v.work} work · ${v.count - 1} × ${v.recovery} min recovery</p><p>${v.purpose}</p>${rpe(selected)}<p>${durationLabel(v.main)} main · <strong>${durationLabel(total(selected))} total</strong><small>${prep.warmup} min warm-up · ${prep.cooldown} min cool-down<br>${prep.overridden ? 'Settings override' : 'Official variant defaults'}</small></p><p class="small">${escapeText(targetLabel(targets[selected]))}</p><div class="actions"><button type="button" class="primary" data-action="start" data-id="${selected}">Start ${selected}</button><button type="button" data-action="adapt" data-id="${selected}">Adapt target</button></div><button type="button" class="text" data-action="favourite" data-id="${selected}">Save shortcut</button></section>`;
  html += `<section class="card"><h3>C2 · Zone 2</h3><p class="dose">Easy, conversational running.<br><strong>45 min main / total</strong></p>${rpe('C2')}<p class="small">Zone 2 is the intended effort, not a measured value. No warm-up or cool-down.</p><p class="small">${escapeText(targetLabel(targets.C2))}</p><div class="actions"><button type="button" class="primary" data-action="start" data-id="C2">Start C2</button><button type="button" data-action="adapt" data-id="C2">Adapt target</button></div><button type="button" class="text" data-action="favourite" data-id="C2">Save shortcut</button></section>`;
  if (favourites.length) html += '<h3>Saved shortcuts</h3>';
  for (const favourite of favourites) {
    html += `<section class="card"><h3>${escapeText(favourite.name)}</h3><p class="dose">${favourite.variant} · ${durationLabel(favourite.variant === 'C2' ? 45 : total(favourite.variant))} total</p><p class="small">${escapeText(targetLabel(favourite.target))}</p><button type="button" class="primary" data-action="start-favourite" data-id="${favourite.id}">Start ${escapeText(favourite.name)}</button></section>`;
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
    C2: 'Target 3: easy, conversational running at a relaxed, steady effort.'
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
    body += `<label class="choice"><input type="radio" name="variant" value="${id}" ${selected === id ? 'checked' : ''}><span><strong>${id} · ${v.title}</strong><small>${v.count} × ${v.work} / ${v.recovery} min recovery<br>RPE ${v.rpe} · ${durationLabel(v.main)} main / ${durationLabel(total(id))} total</small>${v.purpose}</span></label>`;
  }
  body += '</fieldset><div class="actions"><button class="primary" type="submit">Use variant</button><button type="button" data-dialog="close">Cancel</button></div></form>';
  openDialog('Choose C1', body, opener);
}

function targetForm(id, opener, retained = null) {
  targetDraft = { id, ...clone(retained || targets[id]) };
  openDialog(`Target · ${id}`, '<form id="target-form" novalidate><p>RPE stays fixed. Add one optional measurable target type; this is a target, not a live reading.</p><div id="target-errors" role="alert"></div><div class="field"><label for="target-mode">Additional target</label><select id="target-mode"><option value="none">None · RPE only</option><option value="pace">Pace</option><option value="zone">Heart-rate zone</option></select></div><div id="target-fields"></div><div class="actions"><button type="submit" class="primary">Use target</button><button type="button" data-dialog="close">Cancel</button></div></form>', opener);
  dialog.querySelector('#target-mode').value = targetDraft.mode;
  renderTargetFields();
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
  const body = `<form id="settings-form" novalidate><p>One global pair applies to all C1 variants at their next start, including saved shortcuts. Official defaults are 4 min warm-up and 3 min cool-down.</p><p>C2 has no preparation. Started and past runs keep their frozen plan.</p><div id="settings-errors" role="alert"></div><div class="field"><label for="c1-warmup">C1 warm-up (minutes)</label><input id="c1-warmup" name="c1-warmup" type="number" min="1" step="1" value="${settings.warmup}"></div><div class="field"><label for="c1-cooldown">C1 cool-down (minutes)</label><input id="c1-cooldown" name="c1-cooldown" type="number" min="1" step="1" value="${settings.cooldown}"></div><p class="small">During a run, +1 min and skip remain available without changing these Settings.</p><div class="actions"><button type="submit" class="primary">Use Settings preview</button><button type="button" data-dialog="close">Cancel</button></div></form>`;
  openDialog('C1 preparation Settings', body, opener);
}

function start(id, target, name = '') {
  const plan = { variant: id, sourceRevision: 'proposed variant set', name, target: clone(target), rpe: id === 'C2' ? '3' : variants[id].rpe, main: id === 'C2' ? 45 : variants[id].main, preparation: id === 'C2' ? { warmup: 0, cooldown: 0 } : clone(settings) };
  plan.total = plan.main + plan.preparation.warmup + plan.preparation.cooldown;
  starts.push(plan);
  document.querySelector('#snapshots').innerHTML = starts.map((value, index) => `<li>Preview ${index + 1}: ${value.variant} · ${durationLabel(value.main)} main / ${durationLabel(value.total)} total · preparation ${value.preparation.warmup}/${value.preparation.cooldown} · ${escapeText(targetLabel(value.target))}</li>`).join('');
  reviewEvent.textContent = `Frozen synthetic start preview: ${id}. No real recording started. Earlier snapshots remain unchanged.`;
}

app.addEventListener('click', event => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;
  const id = button.dataset.id;
  switch (button.dataset.action) {
    case 'change': chooseVariant(button); break;
    case 'rpe': openRpe(id, button); break;
    case 'adapt': targetForm(id, button); break;
    case 'start': start(id, targets[id]); break;
    case 'start-favourite': {
      const favourite = favourites.find(value => value.id === Number(id));
      start(favourite.variant, favourite.target, favourite.name);
      break;
    }
    case 'favourite':
      openDialog('Save a shortcut', `<form id="favourite-form"><p>Save ${id} and its current target. Future starts use the current preparation Settings; previous runs keep their frozen plan.</p><div class="field"><label for="shortcut-name">Shortcut name</label><input id="shortcut-name" required autocomplete="off"></div><div class="actions"><button type="submit" class="primary">Save shortcut</button><button type="button" data-dialog="close">Cancel</button></div><input type="hidden" name="variant" value="${id}"></form>`, button);
      break;
  }
});

dialog.addEventListener('click', event => {
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
    render();
  } else if (event.target.id === 'target-form') {
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
    render();
  } else if (event.target.id === 'favourite-form') {
    const name = event.target.elements['shortcut-name'].value.trim();
    if (!name) return;
    const id = event.target.elements.variant.value;
    favourites.push({ id: nextFavouriteId++, variant: id, name, target: clone(targets[id]) });
    render();
  } else if (event.target.id === 'settings-form') {
    const warmup = Number(event.target.elements['c1-warmup'].value);
    const cooldown = Number(event.target.elements['c1-cooldown'].value);
    if (![warmup, cooldown].every(value => Number.isInteger(value) && value > 0)) {
      dialog.querySelector('#settings-errors').innerHTML = '<p class="error">For this preview, use positive whole minutes.</p>';
      return;
    }
    Object.assign(settings, { warmup, cooldown, overridden: warmup !== 4 || cooldown !== 3 });
    render();
  }
  closeDialog();
});

document.querySelector('#review-settings').addEventListener('click', event => settingsDialog(event.currentTarget));
window.prototypeState = () => clone({ selected, settings, targets, favourites, starts });
render();
