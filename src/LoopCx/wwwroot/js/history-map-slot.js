export function observe(slot, receiver, owner) {
    let frame = 0;
    let disposed = false;
    let previous;

    function measure() {
        frame = 0;
        if (disposed) return;

        const bounds = slot.getBoundingClientRect();
        const width = window.innerWidth;
        const height = window.innerHeight;
        const fits = document.visibilityState === 'visible' && slot.isConnected
            && bounds.width > 0 && bounds.height > 0 && bounds.left >= 0 && bounds.top >= 0
            && bounds.right <= width && bounds.bottom <= height
            && (window.visualViewport?.scale ?? 1) === 1;
        const position = fits ? [bounds.left, bounds.top, bounds.width, bounds.height, width] : [0, 0, 0, 0, width];
        const current = position.join(',');

        if (current !== previous) {
            previous = current;
            receiver.invokeMethodAsync('PositionMap', owner, ...position).catch(() => {});
        }
    }

    function schedule() {
        if (!disposed && frame === 0) frame = requestAnimationFrame(measure);
    }

    function visibilityChanged() {
        previous = undefined;
        schedule();
    }

    const sizes = new ResizeObserver(schedule);
    sizes.observe(slot);
    sizes.observe(document.documentElement);
    document.addEventListener('scroll', schedule, true);
    document.addEventListener('visibilitychange', visibilityChanged);
    window.addEventListener('resize', schedule);
    window.visualViewport?.addEventListener('resize', schedule);
    schedule();

    return {
        dispose() {
            disposed = true;
            cancelAnimationFrame(frame);
            sizes.disconnect();
            document.removeEventListener('scroll', schedule, true);
            document.removeEventListener('visibilitychange', visibilityChanged);
            window.removeEventListener('resize', schedule);
            window.visualViewport?.removeEventListener('resize', schedule);
        }
    };
}
