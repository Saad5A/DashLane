/* Leaflet and OSM match Request a Ride; no geocoding credentials are required. */
(() => {
    "use strict";
    function create(element, data, selectPlace) {
        if (!window.L) {
            element.textContent = "Map could not load. Use the stop selectors to continue; route estimates remain available.";
            return { draw() {} };
        }
        const places = data.places || [];
        const byId = Object.fromEntries(places.map(place => [place.id, place]));
        const map = L.map(element, { scrollWheelZoom: false }).setView([43.60, -79.56], 10);
        L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);
        const overlay = L.layerGroup().addTo(map);
        let lastKey;
        places.forEach(place => {
            const marker = L.circleMarker([place.latitude, place.longitude], {
                radius: 7, color: "#121110", fillColor: "#ffbd21", fillOpacity: 1
            }).addTo(map).bindTooltip(place.name);
            if (selectPlace) marker.on("click", () => selectPlace(place.id));
        });
        const observer = new ResizeObserver(() => map.invalidateSize({ pan: false }));
        observer.observe(element);
        window.addEventListener("pagehide", () => { observer.disconnect(); map.remove(); }, { once: true });
        return {
            draw(originId, destinationId, stops = []) {
                const resolvedStops = stops.map(stop => byId[stop.placeId] || places.find(place => stop.address?.startsWith(place.name)));
                const waypoints = [byId[originId], ...resolvedStops, byId[destinationId]].filter(Boolean);
                const key = waypoints.map(place => place.id).join("/");
                if (lastKey === key) return;
                lastKey = key;
                overlay.clearLayers();
                let points = [];
                for (let index = 0; index < waypoints.length - 1; index++) {
                    const from = waypoints[index], to = waypoints[index + 1];
                    const route = data.routes.find(route => route.pickupId === from.id && route.destinationId === to.id);
                    if (route) points.push(...route.points.map(point => [point.latitude, point.longitude]));
                }
                if (points.length) L.polyline(points, { color: "#a85b00", weight: 5 }).addTo(overlay);
                waypoints.forEach((place, index) => {
                    const label = place.id === originId && index === 0 ? "A" :
                        place.id === destinationId && index === waypoints.length - 1 ? "B" : String(index);
                    L.marker([place.latitude, place.longitude], { icon: L.divIcon({
                        className: "driver-route-pin", html: label, iconSize: [28,28], iconAnchor: [14,14]
                    }) }).addTo(overlay).bindTooltip(place.name);
                });
                if (waypoints.length) map.fitBounds(L.latLngBounds([
                    ...points, ...waypoints.map(place => [place.latitude, place.longitude])
                ]), { padding: [32,32], maxZoom: 13, animate: false });
            }
        };
    }
    window.DriverMap = { create };
    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("[data-driver-coverage]").forEach(container => {
            const data = JSON.parse(container.querySelector("script").textContent);
            const match = label => data.places.find(place => label?.startsWith(place.name))?.id;
            create(container.querySelector(".driver-live-map"), data).draw(match(data.origin), match(data.destination), data.stops || []);
        });
    });
})();
