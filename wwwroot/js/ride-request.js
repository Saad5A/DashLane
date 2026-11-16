/* Presentation only: every displayed fare and corridor measurement comes from C#. */
(() => {
    'use strict';
    const form = document.getElementById('ride-request-form');
    if (!form) return;
    const data = JSON.parse(document.getElementById('ride-live-data').textContent);
    const field = name => form.elements.namedItem(`Form.${name}`);
    const pickup = field('PickupId'), destination = field('DestinationId');
    const wav = form.querySelector('input[type=checkbox]');
    const tiers = [...form.querySelectorAll('input[name="Form.Tier"]')];
    const status = document.getElementById('quote-status');
    const next = document.getElementById('ride-continue');
    const money = new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' });
    const routes = new Map(data.routes.map(r => [`${r.pickupId}/${r.destinationId}`, r]));
    let map, line, pins, request, generation = 0, mapKey;
    if (window.L) {
        map = L.map('request-map', { scrollWheelZoom: false }).setView([43.60, -79.56], 10);
        const attribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19, attribution
        }).addTo(map);
        pins = L.layerGroup().addTo(map);
        data.places.forEach(place => {
            L.circleMarker([place.latitude, place.longitude], { radius: 7, color: '#121110', fillColor: '#ffbd21', fillOpacity: 1 })
                .addTo(map).bindTooltip(place.name).on('click', () => {
                    if (!pickup.value || destination.value) { pickup.value = place.id; destination.value = ''; }
                    else if (pickup.value !== place.id) destination.value = place.id;
                    update();
                });
        });
    } else {
        document.getElementById('request-map').textContent = 'Map could not load. Choose stops above; live fares remain available.';
    }
    function render() {
        const key = `${pickup.value}/${destination.value}`;
        const route = routes.get(key);
        const passengers = Number(field('Passengers').value);
        const validPassengers = Number.isInteger(passengers) && passengers >= 1 && passengers <= 6;
        if (wav.checked) tiers.find(t => t.value === 'WheelchairAccessible').checked = true;
        tiers.forEach(tier => {
            const card = tier.closest('.ride-tier');
            const fare = route?.fares.find(f => f.tier === tier.value);
            const blockedWav = wav.checked && tier.value !== 'WheelchairAccessible';
            const blockedCapacity = !!fare && passengers > fare.capacity;
            tier.disabled = blockedWav || blockedCapacity;
            card.classList.toggle('is-disabled', tier.disabled);
            card.setAttribute('aria-disabled', String(tier.disabled));
            const badge = card.querySelector('.tier-disabled-reason');
            badge.hidden = !tier.disabled;
            badge.textContent = blockedWav ? 'Disabled — WAV required' : 'Not enough seats';
            card.querySelector('.ride-tier-price').textContent = fare ? money.format(fare.fare) : '—';
        });
        const selected = tiers.find(t => t.checked);
        const fare = route?.fares.find(f => f.tier === selected?.value);
        const ready = !!fare && !selected.disabled && validPassengers && field('ScheduledLocal').checkValidity();
        field('ExpectedFare').value = ready ? fare.fare : '';
        field('ExpectedSurge').value = ready ? fare.surgeMultiplier : '';
        next.disabled = !ready;
        document.getElementById('selected-tier').textContent = selected?.closest('.ride-tier').querySelector('.ride-tier-name').textContent || 'Choose a tier';
        document.getElementById('selected-fare').textContent = ready ? money.format(fare.fare) : '—';
        document.getElementById('selected-surge').textContent = fare ? `${fare.surgeMultiplier.toFixed(2)}×` : '—';
        document.getElementById('route-distance').textContent = route ? `${route.distanceKm} km` : '— km';
        document.getElementById('route-duration').textContent = route ? `${route.durationMin} min` : '— min';
        document.getElementById('route-caption').textContent = route ? `${data.places.find(p => p.id === pickup.value).name} → ${data.places.find(p => p.id === destination.value).name}` : '';
        status.textContent = ready ? 'Upfront quote ready. Your fare locks when you continue. Availability is rechecked at booking.' : route ? 'Choose a tier with enough seats for your accessibility needs.' : 'Choose two different stops to see your fare.';
        if (map && mapKey !== key) {
            mapKey = key;
            if (line) map.removeLayer(line);
            pins.clearLayers();
            if (route) {
                const points = route.points.map(p => [p.latitude, p.longitude]);
                line = L.polyline(points, { color: '#a85b00', weight: 5 }).addTo(map);
                [points[0], points[points.length - 1]].forEach((point, index) =>
                    L.marker(point, { icon: L.divIcon({ className: 'request-route-pin', html: index ? 'B' : 'A', iconSize: [28, 28], iconAnchor: [14, 14] }) }).addTo(pins));
                map.fitBounds(line.getBounds(), { padding: [32, 32], maxZoom: 13, animate: true });
            }
        }
    }
    async function update() {
        render(); // Immediate server-calculated cached quote; no browser pricing formulas.
        const key = `${pickup.value}/${destination.value}`;
        const version = ++generation;
        request?.abort();
        if (!routes.has(key)) return;
        request = new AbortController();
        try {
            const url = new URL(form.dataset.quotesUrl, location.origin);
            url.search = new URLSearchParams({ pickupId: pickup.value, destinationId: destination.value });
            const response = await fetch(url, { signal: request.signal, cache: 'no-store' });
            if (!response.ok) throw new Error('Quote unavailable');
            const [route] = await response.json();
            if (version !== generation) return;
            routes.set(key, route);
            render();
        } catch (error) {
            if (error.name !== 'AbortError' && version === generation)
                status.textContent = 'Showing the last server quote. Continue will recheck the current fare.';
        }
    }
    tiers.forEach(tier => tier.addEventListener('change', () => {
        if (tier.value === 'WheelchairAccessible' && tier.checked) wav.checked = true;
    }));
    form.addEventListener('change', update);
    field('Passengers').addEventListener('input', update);
    update();
})();
