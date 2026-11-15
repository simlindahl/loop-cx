const owners = new WeakMap();

// Android WebView TextZoom affects computed text size but not CSS container-query em units.
// Compare the same 21-em threshold using the actual rendered font and available width.
export function observe(element) {
    if (!element?.isConnected) return;
    const existing = owners.get(element);
    if (existing) {
        existing.refresh();
        return;
    }

    let active = true;
    const refresh = () => {
        if (!active || !element.isConnected || document.hidden) return;
        const width = element.getBoundingClientRect().width;
        const font = Number.parseFloat(getComputedStyle(element).fontSize);
        if (!(width > 0 && font > 0 && Number.isFinite(font))) return;
        element.dataset.splitLayout = width <= 21 * font ? 'cards' : 'table';
    };
    const observer = new ResizeObserver(refresh);
    observer.observe(element);
    document.addEventListener('visibilitychange', refresh);
    owners.set(element, { refresh, observer, stop: () => { active = false; } });
    refresh();
}

export function dispose(element) {
    const owner = owners.get(element);
    if (!owner) return;
    owner.stop();
    owner.observer.disconnect();
    document.removeEventListener('visibilitychange', owner.refresh);
    owners.delete(element);
    delete element.dataset.splitLayout;
}
