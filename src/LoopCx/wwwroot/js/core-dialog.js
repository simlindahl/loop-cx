const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
const motionEffects = new Set();
const nodeEffects = new WeakMap();
const exitCopies = new Set();
const motionEase = 'cubic-bezier(.2,0,0,1)';
let listening = false;
let lastSurface;
let lastState;
let lastDisclosure;

function clearMotion() {
    motionEffects.forEach(effect => effect.cancel());
    exitCopies.forEach(copy => copy.remove());
    exitCopies.clear();
}
function listenForMotion() {
    if (listening) return;
    motionPreference.addEventListener('change', clearMotion);
    listening = true;
}
function effect(node, frames, duration, delay = 0) {
    if (!node || motionPreference.matches || !node.getClientRects().length) return;
    listenForMotion();
    nodeEffects.get(node)?.cancel();
    const animation = node.animate(frames, { duration, delay, easing: motionEase });
    nodeEffects.set(node, animation);
    motionEffects.add(animation);
    const release = () => {
        motionEffects.delete(animation);
        if (nodeEffects.get(node) === animation) nodeEffects.delete(node);
    };
    animation.finished.then(release, release);
    return animation;
}
function enter(node, duration = 220, delay = 0) {
    return effect(node, [{ opacity: .5, transform: 'translateY(4px)' }, { opacity: 1, transform: 'none' }], duration, delay);
}
function leaveVisual(node) {
    if (!node || motionPreference.matches) return;
    const rect = node.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const copy = node.cloneNode(true);
    [copy, ...copy.querySelectorAll('*')].forEach(item => {
        item.removeAttribute('id');
        item.removeAttribute('autofocus');
        item.removeAttribute('for');
        item.removeAttribute('aria-labelledby');
        item.removeAttribute('aria-describedby');
    });
    copy.setAttribute('aria-hidden', 'true');
    copy.setAttribute('inert', '');
    copy.querySelectorAll('button, input, select, a, [tabindex]').forEach(item => item.setAttribute('tabindex', '-1'));
    Object.assign(copy.style, { position: 'fixed', top: `${rect.top}px`, left: `${rect.left}px`,
        width: `${rect.width}px`, height: `${rect.height}px`, maxWidth: 'none', maxHeight: 'none',
        margin: '0', pointerEvents: 'none', zIndex: '9999', overflow: 'hidden' });
    // Inert visual copies never retain focus, callbacks or a second accessible dialog.
    const host = node.closest('dialog') && node.tagName !== 'DIALOG' ? node.closest('dialog') : document.body;
    host.append(copy);
    copy.scrollTop = node.scrollTop;
    copy.scrollLeft = node.scrollLeft;
    exitCopies.add(copy);
    const animation = effect(copy, [{ opacity: 1, transform: 'none' }, { opacity: 0, transform: 'translateY(-4px)' }], 120);
    const remove = () => { copy.remove(); exitCopies.delete(copy); };
    if (animation) animation.finished.then(remove, remove); else remove();
}
export function leaveSurface() {
    leaveVisual(document.querySelector('main h2'));
}
export function leaveDisclosure() {
    leaveVisual(document.getElementById('target-fields'));
}
export function syncMotion(saved) {
    listenForMotion();
    const main = document.querySelector('main[data-motion-surface]');
    const surface = main?.dataset.motionSurface;
    if (surface !== lastSurface) {
        // One effect per real group, at most three. No per-row or per-tick replay.
        [...main.querySelectorAll('[data-motion-group]')].slice(0, 3).forEach((node, index) => enter(node, 220, index * 24));
        lastSurface = surface;
    }
    const phase = document.getElementById('phase-heading');
    const state = phase?.dataset.motionState;
    if (state && state !== lastState) enter(phase, 160);
    lastState = state;
    const fields = document.getElementById('target-fields');
    const disclosure = fields?.dataset.targetMode;
    if (disclosure && disclosure !== lastDisclosure && fields.children.length) enter(fields);
    lastDisclosure = disclosure;
    if (saved) {
        const result = document.getElementById('shortcut-list') ?? main;
        // A confirmed save changes only a visual outline, never animates doses or measurements.
        effect(result, [{ boxShadow: '0 0 0 2px var(--cx-accent)' }, { boxShadow: '0 0 0 0 transparent' }], 160);
    }
}
export function dispose() {
    clearMotion();
    motionPreference.removeEventListener('change', clearMotion);
    listening = false;
    lastSurface = lastState = lastDisclosure = undefined;
}

let trigger;
let cancel;
let keys;
export function open(opener, owner) {
    const dialog = document.getElementById('core-dialog');
    trigger = opener;
    lastDisclosure = document.getElementById('target-fields')?.dataset.targetMode;
    cancel = event => { event.preventDefault(); owner.invokeMethodAsync('CloseDialog'); };
    keys = event => {
        if (event.key !== 'Tab') return;
        const controls = [...dialog.querySelectorAll('button, input, select')].filter(node => !node.disabled && !node.closest('[inert]') && node.getClientRects().length);
        if (!controls.length) return;
        const first = controls[0], last = controls.at(-1), title = document.getElementById('core-dialog-title');
        if (event.shiftKey && (document.activeElement === first || document.activeElement === title)) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    dialog.addEventListener('cancel', cancel);
    dialog.addEventListener('keydown', keys);
    dialog.showModal();
    document.getElementById('core-dialog-title').focus();
    enter(dialog);
}
export function close() {
    const dialog = document.getElementById('core-dialog');
    if (dialog) {
        [...exitCopies].filter(copy => dialog.contains(copy)).forEach(copy => { copy.remove(); exitCopies.delete(copy); });
        leaveVisual(dialog);
        dialog.removeEventListener('cancel', cancel); dialog.removeEventListener('keydown', keys); dialog.close(); }
    document.getElementById(trigger)?.focus();
}
export function focus(id) { document.getElementById(id)?.focus(); }


export function manage(owner) {
    const list = document.getElementById('shortcut-list');
    if (!list || list.dataset.bound === 'true') return;
    list.dataset.bound = 'true';
    let drag;
    function clear() {
        list.querySelectorAll('[data-drop], [data-dragging]').forEach(row => { delete row.dataset.drop; delete row.dataset.dragging; });
        drag = null;
    }
    list.addEventListener('pointerdown', event => {
        const handle = event.target.closest('[data-shortcut-handle]');
        if (!handle || (event.pointerType === 'mouse' && event.button !== 0)) return;
        drag = { id: handle.dataset.shortcutHandle, pointer: event.pointerId, handle, target: null };
        handle.setPointerCapture(event.pointerId);
        handle.closest('[data-shortcut-id]').dataset.dragging = 'true';
    });
    list.addEventListener('pointermove', event => {
        if (!drag || drag.pointer !== event.pointerId) return;
        const row = document.elementFromPoint(event.clientX, event.clientY)?.closest('[data-shortcut-id]');
        list.querySelectorAll('[data-drop]').forEach(item => delete item.dataset.drop);
        drag.target = row && list.contains(row) ? row.dataset.shortcutId : null;
        if (row && drag.target !== drag.id) {
            drag.after = event.clientY > row.getBoundingClientRect().top + row.getBoundingClientRect().height / 2;
            row.dataset.drop = drag.after ? 'after' : 'before';
        }
    });
    list.addEventListener('pointerup', event => {
        if (!drag || drag.pointer !== event.pointerId) return;
        const value = drag;
        const row = document.elementFromPoint(event.clientX, event.clientY)?.closest('[data-shortcut-id]');
        const valid = row && list.contains(row) && row.dataset.shortcutId !== value.id;
        const after = valid && event.clientY > row.getBoundingClientRect().top + row.getBoundingClientRect().height / 2;
        clear();
        if (valid) owner.invokeMethodAsync('DropShortcut', value.id, row.dataset.shortcutId, after);
    });
    list.addEventListener('pointercancel', clear);
    list.addEventListener('lostpointercapture', clear);
    list.addEventListener('keydown', event => {
        const handle = event.target.closest('[data-shortcut-handle]');
        if (event.key === 'Escape') { clear(); return; }
        if (handle && ['ArrowUp', 'ArrowDown'].includes(event.key)) {
            event.preventDefault();
            owner.invokeMethodAsync('MoveShortcut', handle.dataset.shortcutHandle, event.key === 'ArrowUp' ? -1 : 1);
        }
    });
}
