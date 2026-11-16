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
    const map = DriverMap.create(document.getElementById("post-map"), data, id => {
        if (!origin.value || destination.value) { origin.value = id; destination.value = ""; }
        else if (origin.value !== id) destination.value = id;
        refreshRoute();
    });
    let fareVersion = 0;
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
            const match = resolvePlace(address.value);
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

    function refreshRoute() {
        const originId = origin.value;
        const destinationId = destination.value;
        const stops = readStops();
        const metrics = originId && destinationId && originId !== destinationId
            ? true
            : null;

        distanceLabel.textContent = metrics ? "Calculating..." : "—";
        durationLabel.textContent = metrics ? "Calculating..." : "—";
        updateFare(metrics);
        distanceInput.value = "0";
        durationInput.value = "0";
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
            map.draw(originId, destinationId, stops);
            return;
        }

        append(`Origin · ${originPlace.name}`);
        stops.forEach((stop, index) => {
            append(`Stop ${index + 1} · ${stop.address}`, `+${stop.arrivalOffsetMin} min`);
        });
        append(`Destination: ${destinationPlace.name}`);

        map.draw(originId, destinationId, stops);
    }

    async function updateFare(metrics) {
        const version = ++fareVersion;
        const label = document.getElementById("route-fare");
        const total = document.getElementById("route-total");
        const status = document.getElementById("route-map-status");
        const fareValue = document.getElementById("route-fare-value");
        const totalValue = document.getElementById("route-total-value");
        const seats = Number(document.getElementById("AvailableSeats").value);
        fareValue.value = totalValue.value = "";
        label.textContent = total.textContent = metrics ? "Calculating..." : "Choose a route";
        status.textContent = "";
        if (!metrics || !document.getElementById("AvailableSeats").checkValidity()) return;
        try {
            const response = await fetch(form.dataset.previewUrl, {
                method: "POST", headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ originPlaceId: origin.value, destinationPlaceId: destination.value,
                    stops: readStops().map(({ address, placeId, arrivalOffsetMin }) => ({ address, placeId, arrivalOffsetMin })),
                    availableSeats: seats })
            });
            const quote = await response.json();
            if (version !== fareVersion) return;
            if (!response.ok) throw new Error(quote.message || "Route preview unavailable.");
            const money = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD" });
            label.textContent = money.format(quote.estimatedPricePerSeat);
            total.textContent = money.format(quote.estimatedTotalEarnings);
            fareValue.value = quote.estimatedPricePerSeat;
            totalValue.value = quote.estimatedTotalEarnings;
            distanceInput.value = quote.distanceKm;
            durationInput.value = quote.durationMin;
            distanceLabel.textContent = `${Number(quote.distanceKm).toFixed(1)} km`;
            durationLabel.textContent = `${quote.durationMin} min`;
            status.textContent = "Recommended fare for your registered vehicle. Potential earnings assume every listed seat is booked.";
        } catch (error) {
            if (version !== fareVersion) return;
            label.textContent = total.textContent = "Review at next step";
            distanceLabel.textContent = durationLabel.textContent = "Unavailable";
            status.textContent = error.message || "Could not refresh estimates. Continue to obtain a server quote.";
        }
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
    document.getElementById("AvailableSeats").addEventListener("input", refreshRoute);

    form.addEventListener("submit", () => {
        // Emit indexed fields so MVC model binding works without relying only on StopsJson.
        form.querySelectorAll("[data-generated-stop]").forEach(input => input.remove());
        readStops().forEach((stop, index) => {
            const fields = [
                ["Stops[" + index + "].Address", stop.address],
                ["Stops[" + index + "].PlaceId", stop.placeId || ""],
                ["Stops[" + index + "].ArrivalOffsetMin", String(stop.arrivalOffsetMin)]
            ];
            for (const [name, value] of fields) {
                const input = document.createElement("input");
                input.type = "hidden";
                input.dataset.generatedStop = "true";
                input.name = name;
                input.value = value;
                form.append(input);
            }
        });
    });

    (data.stops || []).forEach((stop) => addStopRow(stop));
    refreshRoute();

})();
