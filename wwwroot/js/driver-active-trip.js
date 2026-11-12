(() => {
    const page = document.getElementById("driver-active-trip");
    if (!page) return;

    const token = page.querySelector('[name="__RequestVerificationToken"]').value;
    const money = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD" });
    const postedRideId = page.dataset.postedRideId;
    const statusEl = document.getElementById("dispatch-status");
    const onlineToggle = document.getElementById("online-toggle");
    const requestEmpty = document.getElementById("request-empty");
    const requestCard = document.getElementById("request-card");
    const tripControls = document.getElementById("trip-controls");
    const mapChip = document.getElementById("map-chip");
    const data = JSON.parse(document.getElementById("active-route-data").textContent);
    let countdownTimer;

    const actions = { Accept: 0, Decline: 1, Arrived: 2, Start: 3, Complete: 4 };
    const statusNames = { 0: "Requested", 1: "Assigned", 2: "Arrived", 3: "InProgress", 4: "Completed", 5: "Cancelled" };

    drawMap(data);
    if (data.pending) showRequest(data.pending);
    refreshTripButtons();

    onlineToggle.addEventListener("change", async () => {
        const result = await postJson(page.dataset.toggleUrl, { online: onlineToggle.checked });
        if (!result.ok) {
            onlineToggle.checked = !onlineToggle.checked;
            statusEl.textContent = result.message || "Could not update availability.";
            return;
        }
        mapChip.textContent = result.body.isOnline ? "Online" : "Offline";
        statusEl.textContent = result.body.isOnline
            ? "You are online and accepting riders."
            : "You are offline.";
    });

    document.getElementById("simulate-request").addEventListener("click", async () => {
        const result = await postJson(page.dataset.simulateUrl, { postedRideId });
        if (!result.ok) {
            statusEl.textContent = result.message || "Simulation failed.";
            return;
        }
        showRequest(result.body);
        onlineToggle.checked = true;
        mapChip.textContent = "Online";
        statusEl.textContent = "Demo rider request dispatched — 30 seconds to respond.";
    });

    document.getElementById("accept-request").addEventListener("click", () => respond("Accept"));
    document.getElementById("decline-request").addEventListener("click", () => respond("Decline"));

    page.querySelectorAll("[data-advance]").forEach((button) => {
        button.addEventListener("click", async () => {
            const bookingId = tripControls.dataset.bookingId;
            if (!bookingId) return;
            const result = await postJson(respondUrl(bookingId), { action: actions[button.dataset.advance] });
            if (!result.ok) {
                statusEl.textContent = result.message || "Could not advance trip.";
                return;
            }
            const next = normalizeStatus(result.body.status);
            tripControls.dataset.status = next;
            const rider = document.getElementById("trip-status-label").textContent.split("·")[1] || "";
            document.getElementById("trip-status-label").textContent = `${statusNames[next] || next} ·${rider}`;
            refreshTripButtons();
            statusEl.textContent = next === "Completed" || next === 4
                ? "Trip completed. Nice work."
                : `Trip updated to ${statusNames[next] || next}.`;
        });
    });

    async function respond(actionName) {
        const bookingId = requestCard.dataset.bookingId;
        if (!bookingId) return;
        const result = await postJson(respondUrl(bookingId), { action: actions[actionName] });
        if (!result.ok) {
            statusEl.textContent = result.message || "Response failed.";
            return;
        }
        clearInterval(countdownTimer);
        const riderName = document.getElementById("request-name").textContent;
        const pickup = document.getElementById("request-pickup").textContent;
        hideRequest();
        if (actionName === "Accept") {
            const next = normalizeStatus(result.body.status);
            tripControls.hidden = false;
            tripControls.dataset.bookingId = result.body.id;
            tripControls.dataset.status = next;
            document.getElementById("trip-status-label").textContent = `${statusNames[next] || next} · ${riderName}`;
            document.getElementById("trip-pickup").textContent = pickup;
            refreshTripButtons();
            statusEl.textContent = "Request accepted. Advance the trip when you arrive.";
        } else {
            statusEl.textContent = "Request declined. Waiting for the next rider.";
        }
    }

    function showRequest(request) {
        requestEmpty.hidden = true;
        requestCard.hidden = false;
        requestCard.dataset.bookingId = request.bookingId;
        document.getElementById("request-name").textContent = request.riderName;
        document.getElementById("request-rating").textContent = `★ ${Number(request.riderRating).toFixed(1)}`;
        document.getElementById("request-pickup").textContent = request.pickupLabel;
        document.getElementById("request-passengers").textContent = `${request.passengers} passenger(s)`;
        document.getElementById("request-payout").textContent = money.format(request.guaranteedPayout);
        startCountdown(request.acceptanceDeadlineUtc, request.serverUtc);
    }

    function hideRequest() {
        requestCard.hidden = true;
        requestCard.dataset.bookingId = "";
        requestEmpty.hidden = false;
    }

    function startCountdown(deadlineIso, serverIso) {
        clearInterval(countdownTimer);
        const deadline = Date.parse(deadlineIso);
        const server = Date.parse(serverIso);
        const offset = Number.isFinite(server) ? server - Date.now() : 0;
        const total = 30000;
        const tick = () => {
            const remaining = Math.max(0, deadline - (Date.now() + offset));
            document.getElementById("countdown-fill").style.transform = `scaleX(${remaining / total})`;
            document.getElementById("countdown-label").textContent = `${Math.ceil(remaining / 1000)}s`;
            if (remaining <= 0) {
                clearInterval(countdownTimer);
                hideRequest();
                statusEl.textContent = "Request expired. Simulate another demo offer when ready.";
            }
        };
        tick();
        countdownTimer = setInterval(tick, 200);
    }

    function refreshTripButtons() {
        const status = normalizeStatus(tripControls.dataset.status);
        page.querySelectorAll("[data-advance]").forEach((button) => {
            const action = button.dataset.advance;
            button.disabled =
                (action === "Arrived" && status !== "Assigned" && status !== "1") ||
                (action === "Start" && status !== "Arrived" && status !== "2") ||
                (action === "Complete" && status !== "InProgress" && status !== "3");
        });
    }

    function normalizeStatus(status) {
        if (statusNames[status] != null) return statusNames[status];
        return status;
    }

    function respondUrl(bookingId) {
        return `/api/driver/requests/${bookingId}/respond`;
    }

    async function postJson(url, body) {
        try {
            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    RequestVerificationToken: token
                },
                body: JSON.stringify(body)
            });
            const text = await response.text();
            let parsed = null;
            try { parsed = text ? JSON.parse(text) : null; } catch { parsed = null; }
            if (!response.ok) {
                return { ok: false, message: parsed?.message || parsed?.title || text || "Request failed." };
            }
            return { ok: true, body: parsed };
        } catch (error) {
            return { ok: false, message: error.message || "Network error." };
        }
    }

    function drawMap(payload) {
        const base = document.getElementById("active-map-basemap");
        const overlay = document.getElementById("active-map-route");
        if (!base || !overlay) return;
        const places = payload.places || [];
        const routes = payload.routes || [];
        const project = (point) => [45 + (point.longitude + 79.73) * 1430, 45 + (43.72 - point.latitude) * 1650];
        const line = (points) => points.map((point) => project(point).join(",")).join(" ");
        const add = (parent, tag, attrs) => {
            const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
            for (const [name, value] of Object.entries(attrs)) el.setAttribute(name, value);
            parent.append(el);
        };
        add(base, "rect", { x: 0, y: 0, width: 600, height: 385, class: "ride-map-land" });
        const route = routes.find((item) =>
            item.pickupId === payload.originPlaceId && item.destinationId === payload.destinationPlaceId);
        if (!route) return;
        for (const [className, width] of [["ride-map-route-edge", 9], ["ride-map-route", 5]]) {
            add(overlay, "polyline", {
                points: line(route.points), fill: "none", class: className,
                "stroke-width": width, "stroke-linecap": "round", "stroke-linejoin": "round"
            });
        }
        [route.points[0], route.points.at(-1)].forEach((point, index) => {
            const [x, y] = project(point);
            add(overlay, "circle", {
                cx: x, cy: y, r: 8,
                class: index ? "ride-map-destination" : "ride-map-pickup",
                "stroke-width": 3
            });
        });
        for (const place of places) {
            const [x, y] = project(place);
            add(base, "circle", { cx: x, cy: y, r: 3.5, class: "ride-map-stop", "stroke-width": 1.5 });
        }
    }
})();
