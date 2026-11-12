(() => {
    const get = id => document.getElementById(id);
    const page = get('ride-confirm');
    if (!page) return;

    const { points, drivers, pollUrl } = JSON.parse(get('confirm-data').textContent);
    const choice = get('driver-choice');
    const map = get('confirm-map');
    const eta = get('confirm-eta');
    const instructions = get('pickup-notes');
    const note = get('driver-note');
    const noteStatus = get('driver-note-status');
    const dialog = get('message-dialog');
    const message = get('message-text');
    let approaches = drivers;

    function element(name, attributes) {
        const item = document.createElementNS('http://www.w3.org/2000/svg', name);
        for (const [key, value] of Object.entries(attributes)) item.setAttribute(key, value);
        map.append(item);
    }

    function draw() {
        const driver = approaches.find(item => item.id === Number(choice.value));
        eta.textContent = driver ? `~${driver.etaMinutes} min` : 'Unavailable';
        const all = driver ? [...points, driver.position] : points;
        const coordinates = all.map(point => [point.longitude * .725, -point.latitude]);
        const xs = coordinates.map(point => point[0]);
        const ys = coordinates.map(point => point[1]);
        const minX = Math.min(...xs);
        const minY = Math.min(...ys);
        const width = Math.max(...xs) - minX;
        const height = Math.max(...ys) - minY;
        const scale = Math.min(394 / Math.max(width, .01), 175 / Math.max(height, .01));
        const projected = coordinates.map(([x, y]) =>
            [240 + (x - minX - width / 2) * scale, 125 + (y - minY - height / 2) * scale]);

        map.replaceChildren();
        element('rect', { width: 480, height: 280, class: 'ride-map-land' });
        for (let x = 0; x < 480; x += 40) {
            element('path', { d: `M${x} 0v280`, class: 'ride-map-grid' });
        }
        for (let y = 0; y < 280; y += 40) {
            element('path', { d: `M0 ${y}h480`, class: 'ride-map-grid' });
        }
        element('polyline', { points: projected.slice(0, points.length).join(' '), fill: 'none',
            class: 'ride-map-route', 'stroke-width': 5, 'stroke-linejoin': 'round', 'stroke-linecap': 'round' });
        const [pickupX, pickupY] = projected[0];
        if (driver) {
            const [x, y] = projected.at(-1);
            element('path', { d: `M${x} ${y}L${pickupX} ${pickupY}`, class: 'ride-map-approach',
                'stroke-width': 3, 'stroke-dasharray': '5 6' });
            element('circle', { cx: x, cy: y, r: 7, class: 'ride-map-driver', 'stroke-width': 2 });
        }
        const [endX, endY] = projected[points.length - 1];
        element('circle', { cx: endX, cy: endY, r: 7, class: 'ride-map-destination', 'stroke-width': 2 });
        element('circle', { cx: pickupX, cy: pickupY, r: 8, class: 'ride-map-pickup', 'stroke-width': 3 });
    }

    choice.addEventListener('change', () => {
        for (const card of page.querySelectorAll('[data-driver]')) {
            card.hidden = card.dataset.driver !== choice.value;
        }
        if (note.value) {
            note.value = '';
            noteStatus.textContent = 'Driver changed. Attach a new note if needed.';
        }
        draw();
    });

    const updateCount = () => { get('pickup-count').textContent = `${instructions.value.length} / 200`; };
    instructions.addEventListener('input', updateCount);
    for (const chip of page.querySelectorAll('[data-chip]')) {
        chip.addEventListener('click', () => {
            const text = instructions.value.trim();
            const snippet = chip.dataset.chip;
            if (text.includes(snippet)) return;
            const combined = text ? `${text}${/[.!?]$/.test(text) ? ' ' : '. '}${snippet}` : snippet;
            if (combined.length <= instructions.maxLength) instructions.value = combined;
            instructions.focus();
            updateCount();
        });
    }

    page.querySelector('[data-dialog]').addEventListener('click', () => {
        message.value = note.value;
        dialog.returnValue = '';
        dialog.showModal();
    });
    dialog.addEventListener('close', () => {
        if (dialog.returnValue !== 'save') return;
        note.value = message.value.trim();
        noteStatus.textContent = note.value ? 'Pre-trip note attached to this draft.' : 'Pre-trip note cleared.';
    });

    async function poll() {
        try {
            if (!document.hidden) {
                const response = await fetch(pollUrl, { cache: 'no-store' });
                if (response.ok && response.headers.get('content-type')?.includes('application/json')) {
                    approaches = await response.json();
                    for (const option of choice.options) {
                        const driver = approaches.find(item => item.id === Number(option.value));
                        const name = option.dataset.name;
                        option.textContent = driver ? `${name} · ~${driver.etaMinutes} min away` : `${name} · unavailable`;
                    }
                    draw();
                }
            }
        } catch {
            // Keep the last available estimate during a temporary connection failure.
        }
        window.setTimeout(poll, 10000);
    }

    draw();
    if (pollUrl) window.setTimeout(poll, 10000);
})();
