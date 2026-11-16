(() => {
    const get = id => document.getElementById(id);
    const page = get('trip');
    if (!page) return;
    const stages = ['Requested', 'Assigned', 'Arrived', 'InProgress', 'Completed'];
    const money = new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' });
    const time = new Intl.DateTimeFormat('en-CA', { timeStyle: 'short', timeZone: 'America/Toronto' });
    const root = page.dataset.url.replace(/\/status$/, '');
    const token = page.querySelector('[name="__RequestVerificationToken"]').value;
    let trip = JSON.parse(get('trip-data').textContent);
    let clockOffset = 0;
    let expectedFee;
    let polling = false;
    let saving = false;
    let receipt = [];
    let lastChat;

    const utc = value => Date.parse(value.endsWith('Z') || /[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z');
    const text = (id, value) => {
        if (get(id).textContent !== value) get(id).textContent = value;
    };
    const hidden = (id, hide) => { get(id).hidden = hide; };

    function updateCountdown() {
        if (!trip.acceptanceDeadlineUtc) return;
        const seconds = Math.max(0, Math.min(30, Math.ceil(
            (utc(trip.acceptanceDeadlineUtc) - Date.now() - clockOffset) / 1000)));
        text('trip-countdown', seconds ? `0:${String(seconds).padStart(2, '0')}` : 'Checking for the next driver…');
    }

    function drawMap() {
        const map = get('trip-map');
        const points = [trip.pickup, trip.destination, ...(trip.driverPosition ? [trip.driverPosition] : [])];
        const coords = points.map(point => [point.longitude * .725, -point.latitude]);
        const xs = coords.map(point => point[0]);
        const ys = coords.map(point => point[1]);
        const width = Math.max(...xs) - Math.min(...xs);
        const height = Math.max(...ys) - Math.min(...ys);
        const scale = Math.min(360 / Math.max(width, .01), 185 / Math.max(height, .01));
        const projected = coords.map(([x, y]) => [
            240 + (x - Math.min(...xs) - width / 2) * scale,
            140 + (y - Math.min(...ys) - height / 2) * scale
        ]);
        const add = (tag, attributes) => {
            const item = document.createElementNS('http://www.w3.org/2000/svg', tag);
            for (const [key, value] of Object.entries(attributes)) item.setAttribute(key, value);
            map.append(item);
        };
        map.replaceChildren();
        add('rect', { width: 480, height: 280, class: 'ride-map-land' });
        for (let x = 0; x < 480; x += 40) add('path', { d: `M${x} 0v280`, class: 'ride-map-grid' });
        for (let y = 0; y < 280; y += 40) add('path', { d: `M0 ${y}h480`, class: 'ride-map-grid' });
        add('polyline', { points: projected.slice(0, 2).join(' '), fill: 'none', class: 'ride-map-route',
            'stroke-width': 5, 'stroke-dasharray': '8 5' });
        projected.forEach(([x, y], index) => add('circle', { cx: x, cy: y, r: index === 2 ? 9 : 7,
            class: ['ride-map-pickup', 'ride-map-destination', 'ride-map-driver'][index], 'stroke-width': 3 }));
    }

    function render() {
        const stage = stages.indexOf(trip.status);
        text('trip-state', trip.status === 'InProgress' ? 'In Progress' : trip.status);
        Array.from(get('trip-timeline').children).forEach((item, index) => {
            item.toggleAttribute('data-done', index < stage);
            if (index === stage) item.setAttribute('aria-current', 'step');
            else item.removeAttribute('aria-current');
        });
        hidden('trip-match', trip.status !== 'Requested');
        if (!trip.acceptanceDeadlineUtc) text('trip-countdown', 'Finding an eligible driver…');
        updateCountdown();
        hidden('trip-late', !trip.driverLate);
        hidden('trip-advance-bar', !trip.canAdvance);
        hidden('trip-cancel', trip.cancellationQuote === null);
        hidden('trip-cancelled', trip.status !== 'Cancelled');
        if (trip.status === 'Cancelled') {
            const reason = trip.cancellationReason === 'RiderCancelled' ? 'Ride cancelled.'
                : 'No driver accepted this ride.';
            text('trip-cancelled', `${reason} Cancellation fee: ${money.format(trip.cancellationFee ?? 0)} CAD.`);
        }
        text('trip-driver', trip.driver ?? 'Driver details appear after acceptance.');
        text('trip-vehicle', [trip.vehicle, trip.plate].filter(Boolean).join(' · '));
        text('trip-instructions', trip.pickupInstructions ? `Pickup instructions: ${trip.pickupInstructions}` : '');
        text('trip-eta', trip.pickupEtaUtc ? `${time.format(utc(trip.pickupEtaUtc))} ET` : '—');
        const active = stage >= 1 && stage <= 3;
        get('trip-chat').disabled = !active;
        get('trip-sos').disabled = !active;
        if (get('trip-location')) get('trip-location').disabled = !active;
        const chat = JSON.stringify(trip.messages);
        if (chat !== lastChat) {
            const list = get('trip-messages');
            list.replaceChildren(...trip.messages.map(message => {
                const item = document.createElement('li');
                item.textContent = `${message.sender}: ${message.text}`;
                return item;
            }));
            list.scrollTop = list.scrollHeight;
            lastChat = chat;
        }

        hidden('trip-review', trip.status !== 'Completed');
        hidden('review-form', trip.reviewed);
        hidden('trip-reviewed', !trip.reviewed);
        hidden('trip-receipt', trip.isDriver);
        hidden('receipt-actions', trip.status !== 'Completed' || trip.paid === null);
        receipt = [['Booking', trip.id], ['Locked fare', money.format(trip.lockedFare)],
            ['Surge multiplier', `${trip.surge.toFixed(2)}×`], ['Discount', money.format(trip.discount)]];
        if (trip.includedTax !== null) receipt.push(['Included HST', money.format(trip.includedTax)]);
        if (trip.paid !== null) receipt.push(['Simulated payment · CAD', money.format(trip.paid)],
            ['Payment method', trip.paymentMethod], ['Payment reference', trip.paymentReference]);
        if (trip.cancellationFee !== null) receipt.push(['Cancellation fee recorded', money.format(trip.cancellationFee)]);
        get('receipt-lines').replaceChildren(...receipt.map(([label, value]) => {
            const row = document.createElement('div');
            const dt = document.createElement('dt');
            const dd = document.createElement('dd');
            dt.textContent = label;
            dd.textContent = value;
            row.append(dt, dd);
            return row;
        }));
        drawMap();
    }

    async function refresh(force = false) {
        if (polling || (saving && !force)) return;
        polling = true;
        try {
            const response = await fetch(page.dataset.url, { cache: 'no-store', redirect: 'error' });
            if (!response.ok) throw new Error();
            trip = await response.json();
            clockOffset = utc(trip.serverUtc) - Date.now();
            render();
            if (get('trip-feedback').textContent === 'Live updates unavailable. Retrying…') text('trip-feedback', '');
        } catch {
            text('trip-feedback', 'Live updates unavailable. Retrying…');
        } finally {
            polling = false;
        }
    }

    async function action(name, body, feedback, onSuccess) {
        if (saving) return;
        saving = true;
        feedback.textContent = '';
        try {
            const response = await fetch(`${root}/${name}`, {
                method: 'POST', redirect: 'error', headers: {
                    'Content-Type': 'application/json', RequestVerificationToken: token
                }, body: JSON.stringify(body)
            });
            if (!response.ok) {
                const error = await response.json().catch(() => ({}));
                throw new Error(error.message || 'This action is unavailable. Refresh and try again.');
            }
            onSuccess?.();
        } catch (error) {
            feedback.textContent = error instanceof TypeError ? 'Connection unavailable. Try again.' : error.message;
        } finally {
            saving = false;
            await refresh(true);
            if (name === 'cancel' && get('cancel-dialog').open && trip.cancellationQuote !== null) {
                expectedFee = trip.cancellationQuote;
                text('cancel-fee', money.format(expectedFee));
            }
        }
    }

    for (const button of page.querySelectorAll('[data-open]')) {
        button.addEventListener('click', () => {
            const dialog = get(button.dataset.open);
            dialog.querySelector('.dialog-feedback').textContent = '';
            if (dialog.id === 'cancel-dialog') {
                expectedFee = trip.cancellationQuote;
                text('cancel-fee', money.format(expectedFee));
            }
            if (dialog.id === 'sos-dialog') {
                const point = trip.driverPosition ?? trip.pickup;
                text('sos-point', `Latest map coordinates: ${point.latitude.toFixed(5)}, ${point.longitude.toFixed(5)}`);
            }
            dialog.showModal();
        });
    }
    for (const button of page.querySelectorAll('[data-close]')) {
        button.addEventListener('click', () => button.closest('dialog').close());
    }
    const dialogFeedback = id => get(id).querySelector('.dialog-feedback');
    get('trip-advance').addEventListener('click', () =>
        action('advance', { expectedStatus: stages.indexOf(trip.status) }, get('trip-feedback')));
    get('trip-location')?.addEventListener('click', () => {
        const feedback = get('trip-feedback');
        if (!navigator.geolocation) {
            feedback.textContent = 'Location sharing is unavailable in this browser.';
            return;
        }
        navigator.geolocation.getCurrentPosition(({ coords }) => {
            action('location', { latitude: coords.latitude, longitude: coords.longitude }, feedback,
                () => { feedback.textContent = 'Location updated for this trip.'; });
        }, () => { feedback.textContent = 'Could not read your current location.'; }, { timeout: 10000 });
    });
    get('confirm-cancel').addEventListener('click', () =>
        action('cancel', { expectedFee }, dialogFeedback('cancel-dialog'), () => get('cancel-dialog').close()));
    get('confirm-sos').addEventListener('click', () =>
        action('sos', {}, dialogFeedback('sos-dialog'), () => {
            dialogFeedback('sos-dialog').textContent = 'Simulated emergency contact notification logged with trip coordinates.';
        }));
    for (const name of ['chat', 'incident']) {
        get(`${name}-form`).addEventListener('submit', event => {
            event.preventDefault();
            const input = get(`${name}-text`);
            if (!input.value.trim()) return;
            action(name, { text: input.value.trim() }, dialogFeedback(`${name}-dialog`), () => {
                input.value = '';
                if (name === 'incident') dialogFeedback('incident-dialog').textContent = 'Incident report saved.';
            });
        });
    }
    get('review-form').addEventListener('submit', event => {
        event.preventDefault();
        action('review', { rating: Number(event.target.elements.rating.value), comment: get('review-text').value },
            get('review-feedback'));
    });
    get('print-receipt').addEventListener('click', () => window.print());
    get('download-receipt').addEventListener('click', () => {
        const url = URL.createObjectURL(new Blob(['DashLane simulated receipt\n',
            ...receipt.map(([label, value]) => `${label}: ${value}\n`)], { type: 'text/plain' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = `dashlane-${trip.id}.txt`;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
    clockOffset = utc(trip.serverUtc) - Date.now();
    render();
    setInterval(updateCountdown, 250);
    setInterval(refresh, 4000);
})();
