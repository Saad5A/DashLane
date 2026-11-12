(() => {
    const page = document.getElementById("driver-post-route");
    if (!page) return;

    const data = JSON.parse(document.getElementById("post-route-data").textContent);
    const places = data.places || [];
    const routes = data.routes || [];
    const placeById = Object.fromEntries(places.map((place) => [place.id, place]));
    const placeByLabel = Object.fromEntries(places.map((place) => [`${place.name} · ${place.address}`, place]));

    const origin = document.getElementById("OriginPlaceId");
    const destination = document.getElementById("DestinationPlaceId");
    const swap = document.getElementById("route-swap");
    const stopsList = document.getElementById("stops-list");
    const template = document.getElementById("stop-template");
    const addStop = document.getElementById("add-stop");
    const stopsJson = document.getElementById("StopsJson");
    const distanceInput = document.getElementById("DistanceKm");
    const durationInput = document.getElementById("DurationMin");
    const distanceLabel = document.getElementById("route-distance");
    const durationLabel = document.getElementById("route-duration");
    const outline = document.getElementById("route-outline");
    const form = document.getElementById("post-route-form");
    const map = createMap();
    const maxStops = Number(stopsList.dataset.max || 5);

    function rows() {
        return [...stopsList.querySelectorAll("[data-stop-row]")];
    }

    function resolvePlace(address, placeId) {
        if (placeId && placeById[placeId]) return placeById[placeId];
        return placeByLabel[address?.trim()] || null;
    }

    function readStops() {
        return rows().map((row, index) => {
            const address = row.querySelector("[data-stop-address]").value.trim();
            const placeId = row.querySelector("[data-stop-place]").value || resolvePlace(address)?.id || "";
            const offset = Number(row.querySelector("[data-stop-offset]").value) || 10;
            return { address, placeId: placeId || null, arrivalOffsetMin: offset, order: index, row };
        }).filter((stop) => stop.address);
    }

    function reindex() {
        rows().forEach((row, index) => {
            row.querySelector("[data-stop-index]").textContent = String(index + 1);
            row.querySelector("[data-stop-up]").disabled = index === 0;
            row.querySelector("[data-stop-down]").disabled = index === rows().length - 1;
        });
        addStop.disabled = rows().length >= maxStops;
        refreshRoute();
    }

    function bindRow(row) {
        const address = row.querySelector("[data-stop-address]");
        const placeInput = row.querySelector("[data-stop-place]");
        const syncPlace = () => {
            const match = resolvePlace(address.value, placeInput.value);
            placeInput.value = match?.id || "";
            if (match) address.value = `${match.name} · ${match.address}`;
            refreshRoute();
        };
        address.addEventListener("change", syncPlace);
        address.addEventListener("input", () => {
            placeInput.value = "";
            refreshRoute();
        });
        row.querySelector("[data-stop-offset]").addEventListener("input", refreshRoute);
        row.querySelector("[data-stop-up]").addEventListener("click", () => {
            const previous = row.previousElementSibling;
            if (previous) stopsList.insertBefore(row, previous);
            reindex();
        });
        row.querySelector("[data-stop-down]").addEventListener("click", () => {
            const next = row.nextElementSibling;
            if (next) stopsList.insertBefore(next, row);
            reindex();
        });
        row.querySelector("[data-stop-remove]").addEventListener("click", () => {
            row.remove();
            reindex();
        });
    }

    function addStopRow(initial) {
        if (rows().length >= maxStops) return;
        const node = template.content.firstElementChild.cloneNode(true);
        if (initial) {
            node.querySelector("[data-stop-address]").value = initial.address || "";
            node.querySelector("[data-stop-place]").value = initial.placeId || "";
            node.querySelector("[data-stop-offset]").value = String(initial.arrivalOffsetMin || 10);
        }
        stopsList.append(node);
        bindRow(node);
        reindex();
    }

    function findRoute(fromId, toId) {
        return routes.find((route) => route.pickupId === fromId && route.destinationId === toId) || null;
    }

    function measure(originId, destinationId, stops) {
        const corridor = findRoute(originId, destinationId);
        if (!corridor) return null;

        const ids = [originId, ...stops.map((stop) => stop.placeId).filter(Boolean), destinationId];
        let distance = 0;
        let duration = 0;
        let chained = true;
        for (let i = 0; i < ids.length - 1; i++) {
            const segment = findRoute(ids[i], ids[i + 1]);
            if (!segment) {
                chained = false;
                break;
            }
            distance += segment.distanceKm;
            duration += segment.durationMin;
        }

        if (chained && ids.length > 2) {
            return { corridor, distanceKm: round(distance), durationMin: duration, mode: "chained" };
        }

        const unknown = stops.filter((stop) => !stop.placeId).length;
        const known = Math.max(0, stops.length - unknown);
        const durationExtra = stops.reduce((sum, stop) => sum + Math.max(5, Math.floor(stop.arrivalOffsetMin / 2)), 0);
        return {
            corridor,
            distanceKm: round(corridor.distanceKm + known * 3.5 + unknown * 4),
            durationMin: corridor.durationMin + durationExtra + unknown * 4,
            mode: "base"
        };
    }

    function round(value) {
        return Math.round(value * 10) / 10;
    }

    function refreshRoute() {
        const originId = origin.value;
        const destinationId = destination.value;
        const stops = readStops();
        const metrics = originId && destinationId && originId !== destinationId
            ? measure(originId, destinationId, stops)
            : null;

        distanceLabel.textContent = metrics ? `${metrics.distanceKm.toFixed(1)} km` : "—";
        durationLabel.textContent = metrics ? `${metrics.durationMin} min` : "—";
        distanceInput.value = metrics ? String(metrics.distanceKm) : "";
        durationInput.value = metrics ? String(metrics.durationMin) : "";
        stopsJson.value = JSON.stringify(stops.map(({ address, placeId, arrivalOffsetMin }) => ({
            address, placeId, arrivalOffsetMin
        })));

        const originPlace = placeById[originId];
        const destinationPlace = placeById[destinationId];
        outline.replaceChildren();
        const append = (label, meta) => {
            const item = document.createElement("li");
            item.append(label);
            if (meta) {
                const span = document.createElement("span");
                span.className = "post-outline-meta";
                span.textContent = meta;
                item.append(span);
            }
            outline.append(item);
        };

        if (!originPlace || !destinationPlace) {
            append("Choose origin and destination");
            map.draw(null, []);
            return;
        }

        append(`Origin · ${originPlace.name}`);
        stops.forEach((stop, index) => {
            append(`Stop ${index + 1} · ${stop.address}`, `+${stop.arrivalOffsetMin} min`);
        });
        append(`Destination · ${destinationPlace.name}`,
            metrics ? `${metrics.distanceKm.toFixed(1)} km · ${metrics.durationMin} min` : undefined);

        map.draw(metrics?.corridor || null, stops);
    }

    swap.addEventListener("click", () => {
        const previous = origin.value;
        origin.value = destination.value;
        destination.value = previous;
        swap.classList.toggle("is-swapped");
        refreshRoute();
    });

    addStop.addEventListener("click", () => addStopRow());
    origin.addEventListener("change", refreshRoute);
    destination.addEventListener("change", refreshRoute);

    form.addEventListener("submit", () => {
        // Emit indexed fields so MVC model binding works without relying only on StopsJson.
        rows().forEach((row) => row.querySelectorAll("[name]").forEach((input) => input.remove()));
        readStops().forEach((stop, index) => {
            const fields = [
                ["Stops[" + index + "].Address", stop.address],
                ["Stops[" + index + "].PlaceId", stop.placeId || ""],
                ["Stops[" + index + "].ArrivalOffsetMin", String(stop.arrivalOffsetMin)]
            ];
            for (const [name, value] of fields) {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = name;
                input.value = value;
                form.append(input);
            }
        });
        refreshRoute();
    });

    (data.stops || []).forEach((stop) => addStopRow(stop));
    refreshRoute();

    function createMap() {
        const svg = document.getElementById("post-map");
        const title = document.getElementById("post-map-title");
        const base = document.getElementById("post-map-basemap");
        const overlay = document.getElementById("post-map-route");
        const bounds = [0, 0, 600, 385];
        let view = [...bounds];
        let drawn = null;
        let drag = null;
        const project = (point) => [45 + (point.longitude + 79.73) * 1430, 45 + (43.72 - point.latitude) * 1650];
        const line = (points) => points.map((point) => project(point).join(",")).join(" ");

        function add(parent, tag, attrs, label) {
            const element = document.createElementNS("http://www.w3.org/2000/svg", tag);
            for (const [name, value] of Object.entries(attrs)) element.setAttribute(name, value);
            if (label) element.textContent = label;
            parent.append(element);
        }

        add(base, "rect", { x: 0, y: 0, width: 600, height: 385, class: "ride-map-land" });
        add(base, "path", {
            d: "M0 385 L45 385 L78 350 L124 334 L164 314 L215 283 L250 264 L290 258 L329 225 L375 211 L410 190 L470 186 L500 171 L535 157 L600 136 V385 Z",
            class: "ride-map-water", "stroke-width": 2
        });
        for (const route of routes.slice(0, Math.ceil(routes.length / 2))) {
            add(base, "polyline", { points: line(route.points), fill: "none", class: "ride-map-road-edge",
                "stroke-width": 7, "stroke-linecap": "round", "stroke-linejoin": "round" });
            add(base, "polyline", { points: line(route.points), fill: "none", class: "ride-map-road",
                "stroke-width": 2, "stroke-linecap": "round", "stroke-linejoin": "round" });
        }
        add(base, "text", { x: 380, y: 300, class: "ride-map-water-label", "font-size": 15, "font-style": "italic" }, "Lake Ontario");
        const labels = { oakville: "Oakville", "port-credit": "Port Credit", "square-one": "Mississauga",
            pearson: "Pearson", union: "Toronto" };
        for (const place of places) {
            const [x, y] = project(place);
            add(base, "circle", { cx: x, cy: y, r: 4, class: "ride-map-stop", "stroke-width": 2 });
            add(base, "text", { x: x + 9, y: y - 8, class: "ride-map-label", "font-size": 15, "font-weight": 700,
                "stroke-width": 4, "paint-order": "stroke" }, labels[place.id]);
        }

        function setView(x, y, width) {
            const height = width * bounds[3] / bounds[2];
            view = [Math.max(0, Math.min(bounds[2] - width, x)),
                Math.max(0, Math.min(bounds[3] - height, y)), width, height];
            svg.setAttribute("viewBox", view.join(" "));
        }

        function fit(points) {
            if (!points?.length) {
                setView(...bounds.slice(0, 3));
                return;
            }
            const projected = points.map(project);
            const xs = projected.map((point) => point[0]);
            const ys = projected.map((point) => point[1]);
            const minX = Math.min(...xs);
            const maxX = Math.max(...xs);
            const minY = Math.min(...ys);
            const maxY = Math.max(...ys);
            const width = Math.min(600, Math.max(245, maxX - minX + 150, (maxY - minY + 105) * 600 / 385));
            setView((minX + maxX - width) / 2, (minY + maxY - width * 385 / 600) / 2, width);
        }

        for (const control of page.querySelectorAll("[data-zoom]")) {
            control.addEventListener("click", () => {
                if (control.dataset.zoom === "reset") {
                    fit(drawn?.points);
                    return;
                }
                const [x, y, w] = view;
                const width = Math.min(600, Math.max(180, w * (control.dataset.zoom === "in" ? 0.75 : 1.33)));
                setView(x + (w - width) / 2, y + (w * 385 / 600 - width * 385 / 600) / 2, width);
            });
        }

        svg.addEventListener("pointerdown", (event) => {
            if (event.button !== 0) return;
            drag = { x: event.clientX, y: event.clientY, view };
            svg.setPointerCapture(event.pointerId);
        });
        svg.addEventListener("pointermove", (event) => {
            if (!drag) return;
            const [x, y, width] = drag.view;
            const scale = width / svg.clientWidth;
            setView(x - (event.clientX - drag.x) * scale, y - (event.clientY - drag.y) * scale, width);
        });
        svg.addEventListener("lostpointercapture", () => { drag = null; });

        function pin(point, label, filled) {
            const [x, y] = project(point);
            add(overlay, "circle", {
                cx: x, cy: y, r: 11,
                class: filled ? "ride-map-destination" : "ride-map-pickup",
                "stroke-width": 3
            });
            add(overlay, "text", {
                x, y: y + 4,
                class: filled ? "ride-map-pin-text" : "ride-map-pin-text-hollow",
                "font-size": 10, "text-anchor": "middle"
            }, label);
        }

        return {
            draw(route, stops) {
                drawn = route;
                overlay.replaceChildren();
                const originPlace = placeById[origin.value];
                const destinationPlace = placeById[destination.value];
                title.textContent = route && originPlace && destinationPlace
                    ? `Route from ${originPlace.name} to ${destinationPlace.name}`
                    : "Greater Toronto Area route map";

                if (route) {
                    for (const [className, width] of [["ride-map-route-edge", 9], ["ride-map-route", 5]]) {
                        add(overlay, "polyline", {
                            points: line(route.points), fill: "none", class: className,
                            "stroke-width": width, "stroke-linecap": "round", "stroke-linejoin": "round"
                        });
                    }

                    pin(route.points[0], "A", false);
                    const midPoints = route.points.slice(1, -1);
                    stops.forEach((stop, index) => {
                        const known = stop.placeId ? placeById[stop.placeId] : null;
                        const point = known || midPoints[Math.min(index, Math.max(0, midPoints.length - 1))] ||
                            route.points[Math.floor(route.points.length / 2)];
                        pin(point, String(index + 1), true);
                    });
                    pin(route.points.at(-1), "B", true);
                    fit(route.points);
                } else {
                    fit(null);
                }
            }
        };
    }
})();
