(() => {
    const get = id => document.getElementById(id);
    const page = get('ride-request');
    if (!page) return;

    const { places, routes, initial } = JSON.parse(get('ride-route-data').textContent);
    const form = get('ride-form');
    const pickup = get('ride-pickup');
    const destination = get('ride-destination');
    const accessible = get('ride-accessible');
    const schedule = get('ride-scheduled');
    const cards = Array.from(page.querySelectorAll('[data-tier]'));
    const status = get('ride-status');
    const retry = get('ride-retry');
    const continueButton = get('ride-continue');
    const money = new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' });
    const map = createMap();
    let passengers = initial?.passengers ?? 1;
    let selected = initial?.tier ?? null;
    let route = null;
    let fares = [];
    let quoteLoaded = false;
    let pendingQuote;
    let submitting = false;

    if (initial) {
        pickup.value = initial.pickupId;
        destination.value = initial.destinationId;
        accessible.checked = initial.requiresWheelchairAccess;
        schedule.value = initial.scheduledLocal ?? '';
        schedule.closest('details').open = Boolean(schedule.value);
    }

    function updatePassengers() {
        get('ride-count').textContent = passengers;
        get('ride-minus').disabled = passengers === 1;
        get('ride-plus').disabled = passengers === 6;
    }

    function showFares() {
        for (const card of cards) {
            const tier = Number(card.dataset.tier);
            const fare = fares.find(item => item.tier === tier);
            const radio = card.querySelector('input');
            radio.disabled = !fare;
            radio.checked = Boolean(fare) && selected === tier;
            card.toggleAttribute('data-unavailable', quoteLoaded && !fare);
            card.querySelector('[data-price]').textContent = fare ? money.format(fare.fare)
                : quoteLoaded ? 'Unavailable' : '—';
        }
        const fare = fares.find(item => item.tier === selected);
        get('ride-breakdown').hidden = !fare;
        get('ride-surge').hidden = !(fare?.surgeMultiplier > 1);
        continueButton.disabled = !fare || submitting;
        if (!fare) return;

        const cents = value => Math.round(value * 100);
        const distance = cents(route.distanceKm * fare.ratePerKm);
        const time = cents(route.durationMin * fare.ratePerMin);
        const base = cents(fare.baseFee);
        const subtotal = base + distance + time;
        const surged = cents((fare.baseFee + route.distanceKm * fare.ratePerKm +
            route.durationMin * fare.ratePerMin) * fare.surgeMultiplier);
        const total = cents(fare.fare);
        const multiplier = `${fare.surgeMultiplier.toFixed(2)}×`;

        get('ride-surge-text').textContent = multiplier;
        get('fare-base').textContent = money.format(fare.baseFee);
        get('fare-distance-label').textContent = `${route.distanceKm} km × ${money.format(fare.ratePerKm)}`;
        get('fare-time-label').textContent = `${route.durationMin} min × ${money.format(fare.ratePerMin)}`;
        get('fare-distance').textContent = money.format(distance / 100);
        get('fare-time').textContent = money.format(time / 100);
        const surgeAdjustment = fare.surgeMultiplier > 1 ? surged - subtotal : 0;
        get('fare-adjustment-row').hidden = fare.surgeMultiplier <= 1;
        get('fare-adjustment-label').textContent = `Surge adjustment (${multiplier})`;
        get('fare-adjustment').textContent = money.format(surgeAdjustment / 100);
        const minimumAdjustment = total - subtotal - surgeAdjustment;
        get('fare-minimum-row').hidden = minimumAdjustment === 0;
        get('fare-minimum').textContent = money.format(minimumAdjustment / 100);
        get('fare-total').textContent = money.format(fare.fare);
    }

    async function refreshQuote(message) {
        pendingQuote?.abort();
        pendingQuote = null;
        fares = [];
        quoteLoaded = false;
        retry.hidden = true;
        route = routes.find(item => item.pickupId === pickup.value && item.destinationId === destination.value);
        get('ride-distance').textContent = route ? `${route.distanceKm.toFixed(1)} km` : '—';
        get('ride-duration').textContent = route ? `${route.durationMin} min` : '—';
        map.draw(route);
        showFares();

        if (!route) {
            status.textContent = pickup.value && destination.value
                ? 'Choose two different stops.' : 'Choose a pickup and destination to see fares.';
            return;
        }

        status.textContent = 'Updating fares…';
        const controller = new AbortController();
        pendingQuote = controller;
        try {
            const origin = route.points[0];
            const response = await fetch(page.dataset.quoteUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                signal: controller.signal,
                body: JSON.stringify({
                    pickupId: route.pickupId, destinationId: route.destinationId,
                    pickupLatitude: origin.latitude, pickupLongitude: origin.longitude,
                    distanceKm: route.distanceKm, durationMin: route.durationMin,
                    passengers, requiresWheelchairAccess: accessible.checked
                })
            });
            if (!response.ok) throw new Error('Quote unavailable');
            const quotes = await response.json();
            if (controller !== pendingQuote) return;
            fares = quotes;
            quoteLoaded = true;
            if (!fares.some(fare => fare.tier === selected)) selected = fares[0]?.tier ?? null;
            status.textContent = fares.length ? message || ''
                : 'No tier fits these options. Adjust the passenger count or accessibility filter.';
            showFares();
        } catch {
            if (controller !== pendingQuote) return;
            status.textContent = 'Could not load fares. Try again.';
            retry.hidden = false;
        }
    }

    pickup.addEventListener('change', () => refreshQuote());
    destination.addEventListener('change', () => refreshQuote());
    accessible.addEventListener('change', () => refreshQuote());
    retry.addEventListener('click', () => refreshQuote());
    get('ride-swap').addEventListener('click', event => {
        [pickup.value, destination.value] = [destination.value, pickup.value];
        event.currentTarget.classList.toggle('is-swapped');
        refreshQuote();
    });
    get('ride-minus').addEventListener('click', () => {
        passengers--;
        updatePassengers();
        refreshQuote();
    });
    get('ride-plus').addEventListener('click', () => {
        passengers++;
        updatePassengers();
        refreshQuote();
    });
    for (const card of cards) {
        card.querySelector('input').addEventListener('change', event => {
            selected = Number(event.target.value);
            showFares();
        });
    }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        const quote = fares.find(fare => fare.tier === selected);
        if (!quote || !route || submitting) return;

        submitting = true;
        const selector = form.querySelector('.ride-selector');
        selector.inert = true;
        continueButton.disabled = true;
        status.textContent = 'Saving your route and fare…';
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    RequestVerificationToken: form.elements.__RequestVerificationToken.value
                },
                body: JSON.stringify({
                    pickupId: route.pickupId, destinationId: route.destinationId, tier: selected,
                    passengers, requiresWheelchairAccess: accessible.checked, scheduledLocal: schedule.value || null,
                    fare: quote.fare, surgeMultiplier: quote.surgeMultiplier
                })
            });
            if (response.ok) {
                const { redirectUrl } = await response.json();
                window.location.assign(redirectUrl);
                return;
            }

            const error = await response.json().catch(() => ({}));
            if (response.status === 409) {
                await refreshQuote(error.message);
            } else {
                status.textContent = error.message || 'Could not save your ride. Refresh the page and try again.';
            }
        } catch {
            status.textContent = 'Could not save your ride. Try again.';
        } finally {
            submitting = false;
            selector.inert = false;
            showFares();
        }
    });

    updatePassengers();
    refreshQuote();

    function createMap() {
        const svg = get('ride-map');
        const title = get('ride-map-title');
        const base = get('ride-map-basemap');
        const overlay = get('ride-map-route');
        const bounds = [0, 0, 600, 385];
        let view = [...bounds];
        let drawn;
        let drag;
        const project = point => [45 + (point.longitude + 79.73) * 1430, 45 + (43.72 - point.latitude) * 1650];
        const line = points => points.map(point => project(point).join(',')).join(' ');

        function add(parent, tag, attrs, label) {
            const element = document.createElementNS('http://www.w3.org/2000/svg', tag);
            for (const [name, value] of Object.entries(attrs)) element.setAttribute(name, value);
            if (label) element.textContent = label;
            parent.append(element);
        }

        add(base, 'rect', { x: 0, y: 0, width: 600, height: 385, class: 'ride-map-land' });
        add(base, 'path', {
            d: 'M0 385 L45 385 L78 350 L124 334 L164 314 L215 283 L250 264 L290 258 L329 225 L375 211 L410 190 L470 186 L500 171 L535 157 L600 136 V385 Z',
            class: 'ride-map-water', 'stroke-width': 2
        });
        for (const route of routes.slice(0, routes.length / 2)) {
            add(base, 'polyline', { points: line(route.points), fill: 'none', class: 'ride-map-road-edge',
                'stroke-width': 7, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
            add(base, 'polyline', { points: line(route.points), fill: 'none', class: 'ride-map-road',
                'stroke-width': 2, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
        }
        add(base, 'text', { x: 380, y: 300, class: 'ride-map-water-label', 'font-size': 15, 'font-style': 'italic' }, 'Lake Ontario');
        const labels = { oakville: 'Oakville', 'port-credit': 'Port Credit', 'square-one': 'Mississauga',
            pearson: 'Pearson', union: 'Toronto' };
        for (const place of places) {
            const [x, y] = project(place);
            add(base, 'circle', { cx: x, cy: y, r: 4, class: 'ride-map-stop', 'stroke-width': 2 });
            add(base, 'text', { x: x + 9, y: y - 8, class: 'ride-map-label', 'font-size': 15, 'font-weight': 700,
                'stroke-width': 4, 'paint-order': 'stroke' }, labels[place.id]);
        }

        function setView(x, y, width) {
            const height = width * bounds[3] / bounds[2];
            view = [Math.max(0, Math.min(bounds[2] - width, x)),
                Math.max(0, Math.min(bounds[3] - height, y)), width, height];
            svg.setAttribute('viewBox', view.join(' '));
        }

        function fit() {
            if (!drawn) {
                setView(...bounds.slice(0, 3));
                return;
            }
            const points = drawn.points.map(project);
            const xs = points.map(point => point[0]);
            const ys = points.map(point => point[1]);
            const minX = Math.min(...xs);
            const maxX = Math.max(...xs);
            const minY = Math.min(...ys);
            const maxY = Math.max(...ys);
            const width = Math.min(600, Math.max(245, maxX - minX + 150, (maxY - minY + 105) * 600 / 385));
            setView((minX + maxX - width) / 2, (minY + maxY - width * 385 / 600) / 2, width);
        }

        for (const control of page.querySelectorAll('[data-zoom]')) {
            control.addEventListener('click', () => {
                if (control.dataset.zoom === 'reset') {
                    fit();
                    return;
                }
                const [x, y, w, h] = view;
                const width = Math.min(600, Math.max(180, w * (control.dataset.zoom === 'in' ? .75 : 1.33)));
                setView(x + (w - width) / 2, y + (h - width * 385 / 600) / 2, width);
            });
        }

        svg.addEventListener('pointerdown', event => {
            if (event.button !== 0) return;
            drag = { x: event.clientX, y: event.clientY, view };
            svg.setPointerCapture(event.pointerId);
        });
        svg.addEventListener('pointermove', event => {
            if (!drag) return;
            const [x, y, width] = drag.view;
            const scale = width / svg.clientWidth;
            setView(x - (event.clientX - drag.x) * scale, y - (event.clientY - drag.y) * scale, width);
        });
        svg.addEventListener('lostpointercapture', () => { drag = null; });

        return {
            draw(route) {
                if (route === drawn) return;
                drawn = route;
                overlay.replaceChildren();
                const origin = places.find(place => place.id === route?.pickupId);
                const end = places.find(place => place.id === route?.destinationId);
                title.textContent = route ? `Approximate route from ${origin.name} to ${end.name}`
                    : 'Greater Toronto Area route map';
                if (route) {
                    for (const [className, width] of [['ride-map-route-edge', 9], ['ride-map-route', 5]]) {
                        add(overlay, 'polyline', { points: line(route.points), fill: 'none', class: className,
                            'stroke-width': width, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
                    }
                    const endpoints = [route.points[0], route.points.at(-1)];
                    endpoints.forEach((point, index) => {
                        const [x, y] = project(point);
                        add(overlay, 'circle', { cx: x, cy: y, r: 7, class: index ? 'ride-map-destination' : 'ride-map-pickup', 'stroke-width': 3 });
                    });
                }
                fit();
            }
        };
    }
})();
