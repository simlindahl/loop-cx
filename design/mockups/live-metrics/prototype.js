const media = matchMedia('(prefers-reduced-motion: reduce)');
const heart = document.querySelector('.heart-icon');
let bpm = 156;
let ready = false;
let effect = null;

function update() {
  effect?.cancel();
  effect = null;
  const quality = document.querySelector('#quality').value;
  const pause = document.querySelector('#pause').value;
  const reduced = media.matches || document.querySelector('#reduced').checked;
  const hidden = document.hidden || document.querySelector('#background').checked;
  document.querySelector('#bpm-value').textContent = quality === 'valid' ? bpm : 'Unavailable';
  document.querySelector('#sensor-copy').textContent = `Heart rate: ${quality}.`;
  document.querySelector('#pause-copy').textContent = pause === 'running' ? 'Training running.' : `${pause === 'manual' ? 'Manual' : 'Auto'} pause · valid heart rate may continue.`;
  document.querySelector('.indicator').style.visibility = quality === 'valid' ? 'visible' : 'hidden';
  const reason = !ready ? 'Motion preference pending' : reduced ? 'Reduced motion' : hidden ? 'View hidden / background' : quality !== 'valid' ? `Signal ${quality}` : null;
  if (reason) {
    document.querySelector('#motion-status').textContent = `${reason}. No animation.`;
    return;
  }
  const duration = 60000 / bpm;
  const frames = [{ transform: 'scale(1)' }, { transform: 'scale(1.18)', offset: .35 }, { transform: 'scale(1)' }];
  effect = heart.animate(frames, { duration, iterations: Infinity, easing: 'ease-in-out' });
  document.querySelector('#motion-status').textContent = `Symbolic rhythm · ${bpm} bpm · ${Math.round(duration)} ms per cycle. Not measured beat timing.`;
}

document.querySelectorAll('[data-bpm]').forEach(button => button.addEventListener('click', () => { bpm = Number(button.dataset.bpm); update(); }));
document.querySelectorAll('#quality,#pause,#reduced,#background').forEach(input => input.addEventListener('change', update));
document.querySelector('#ready').addEventListener('click', () => { ready = true; update(); });
media.addEventListener('change', update);
document.addEventListener('visibilitychange', update);
window.addEventListener('pagehide', () => { effect?.cancel(); effect = null; });
window.previewState = () => ({ bpm, ready, style: 'heart', animations: document.getAnimations().filter(animation => animation.playState === 'running').length, duration: effect?.effect.getTiming().duration ?? null });
update();

// Exploration only: these are synthetic comparable windows, not a GPS estimator.
let trendEffect = null;
let previousWindow = 320;
let currentWindow = 320;
let paceQuality = 'valid';
let trendLabel = 'Steady';
let animatedDirection = null;
let trendDuration = null;

function renderTrend() {
  const paused = document.querySelector('#pause').value !== 'running';
  const label = paused ? 'Paused' : paceQuality === 'stale' ? 'Stale' : paceQuality === 'gap' ? 'Acquiring' : previousWindow === null ? 'Acquiring' : currentWindow - previousWindow <= -5 ? 'Faster' : currentWindow - previousWindow >= 5 ? 'Slower' : 'Steady';

  document.querySelector('#pace-value').textContent = paused || paceQuality !== 'valid' ? 'Unavailable' : `${Math.floor(currentWindow / 60)}:${String(currentWindow % 60).padStart(2, '0')}`;
  document.querySelector('#trend-label').textContent = label;
  document.querySelector('#trend-arrow').textContent = label === 'Faster' ? '↗' : label === 'Slower' ? '↘' : '—';
  const animate = !paused && paceQuality === 'valid' && previousWindow !== null && ['Faster', 'Slower'].includes(label) && ready && !media.matches && !document.querySelector('#reduced').checked && !document.hidden && !document.querySelector('#background').checked;
  if (!animate) {
    trendEffect?.cancel();
    trendEffect = null;
    animatedDirection = null;
    trendDuration = null;
  } else {
    const duration = Math.max(900, Math.min(1800, currentWindow * 4));
    if (trendEffect && animatedDirection === label) {
      const progress = trendEffect.effect.getComputedTiming().progress ?? 0;
      trendEffect.effect.updateTiming({ duration });
      trendEffect.currentTime = progress * duration;
    } else {
      trendEffect?.cancel();
      const offset = label === 'Faster' ? -3 : 3;
      trendEffect = document.querySelector('#trend-arrow').animate([{ transform: 'translate(0,0)' }, { transform: `translate(2px,${offset}px)`, offset: .5 }, { transform: 'translate(0,0)' }], { duration, iterations: Infinity, easing: 'ease-in-out' });
    }
    animatedDirection = label;
    trendDuration = duration;
  }
  trendLabel = label;
}

document.querySelectorAll('[data-pace]').forEach(button => button.addEventListener('click', () => {
  const scenario = button.dataset.pace;
  const speed = /^(fast|slow)(\d+)$/.exec(scenario);
  currentWindow = speed ? Number(speed[2]) : scenario === 'faster' ? 310 : scenario === 'slower' ? 330 : scenario === 'steady' ? 318 : 320;
  previousWindow = ['acquiring', 'gap', 'phase'].includes(scenario) ? null : speed ? currentWindow + (speed[1] === 'fast' ? 10 : -10) : 320;
  paceQuality = scenario === 'stale' ? 'stale' : scenario === 'gap' ? 'gap' : 'valid';
  document.querySelector('#trend-review-status').textContent = `${scenario}: synthetic reference ${previousWindow ?? 'reset'} sec/km; recent ${currentWindow} sec/km. Not a calibrated rule.`;
  renderTrend();
}));
document.querySelector('#pause').addEventListener('change', () => {previousWindow = null; renderTrend();});
document.querySelectorAll('#reduced,#background').forEach(input => input.addEventListener('change', renderTrend));
document.querySelector('#ready').addEventListener('click', renderTrend);
media.addEventListener('change', renderTrend);
document.addEventListener('visibilitychange', renderTrend);
window.addEventListener('pagehide', () => { trendEffect?.cancel(); trendEffect = null; animatedDirection = null; trendDuration = null; });
window.trendState = () => ({previousWindow,currentWindow,paceQuality,label:trendLabel,duration:trendDuration,arrowAnimations:document.getAnimations().filter(a=>a.playState==='running' && a.effect.target.id==='trend-arrow').length});
renderTrend();
