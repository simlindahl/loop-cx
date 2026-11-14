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
