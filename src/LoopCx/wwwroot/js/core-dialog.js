let trigger;
let cancel;
let keys;
export function open(opener, owner) {
    const dialog = document.getElementById('core-dialog');
    trigger = opener;
    cancel = event => { event.preventDefault(); owner.invokeMethodAsync('CloseDialog'); };
    keys = event => {
        if (event.key !== 'Tab') return;
        const controls = [...dialog.querySelectorAll('button, input, select')].filter(node => !node.disabled && node.getClientRects().length);
        if (!controls.length) return;
        const first = controls[0], last = controls.at(-1), title = document.getElementById('core-dialog-title');
        if (event.shiftKey && (document.activeElement === first || document.activeElement === title)) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    dialog.addEventListener('cancel', cancel);
    dialog.addEventListener('keydown', keys);
    dialog.showModal();
    document.getElementById('core-dialog-title').focus();
}
export function close() {
    const dialog = document.getElementById('core-dialog');
    if (dialog) { dialog.removeEventListener('cancel', cancel); dialog.removeEventListener('keydown', keys); dialog.close(); }
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
