(() => {
    const root = document.getElementById("driver-post-details");
    if (!root) return;

    const money = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD" });
    const estimateUrl = root.dataset.estimateUrl;
    const tier = root.dataset.tier;
    const distanceKm = Number(root.dataset.distance);
    const durationMin = Number(root.dataset.duration);
    const maxSeats = Number(root.dataset.maxSeats) || 4;

    const seatsInput = document.getElementById("AvailableSeats");
    const seatsOutput = document.getElementById("seats-output");
    const instructions = document.getElementById("PickupInstructions");
    const autoApprove = document.getElementById("AutoApproveRiders");
    const bookingHint = document.getElementById("booking-mode-hint");
    const status = document.getElementById("payout-status");
    const breakdown = document.getElementById("payout-breakdown");
    const seatHidden = document.getElementById("EstimatedPricePerSeat");
    const totalHidden = document.getElementById("EstimatedTotalEarnings");
    let pending;

    function setSeats(value) {
        const next = Math.min(Math.max(value, 1), maxSeats);
        seatsInput.value = String(next);
        seatsOutput.textContent = String(next);
        root.querySelectorAll("[data-seat-stepper] [data-step]").forEach((button) => {
            const step = Number(button.dataset.step);
            button.disabled = (step < 0 && next <= 1) || (step > 0 && next >= maxSeats);
        });
        refreshPayout();
    }

    function syncChips() {
        const text = instructions.value;
        root.querySelectorAll("[data-chip]").forEach((chip) => {
            chip.classList.toggle("is-active", text.includes(chip.dataset.chip));
        });
    }

    function toggleChip(label) {
        const current = instructions.value.trim();
        if (current.includes(label)) {
            instructions.value = current
                .split(/\n+/)
                .map((line) => line.trim())
                .filter((line) => line && line !== label)
                .join("\n");
        } else {
            instructions.value = current ? `${current}\n${label}` : label;
        }
        syncChips();
    }

    function updateBookingHint() {
        bookingHint.textContent = autoApprove.checked
            ? "Riders who match your rules are booked instantly."
            : "You’ll manually approve each rider request.";
    }

    async function refreshPayout() {
        pending?.abort();
        pending = new AbortController();
        status.hidden = false;
        status.textContent = "Calculating payout…";
        breakdown.hidden = true;

        try {
            const response = await fetch(estimateUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    Accept: "application/json"
                },
                signal: pending.signal,
                body: JSON.stringify({
                    tier,
                    distanceKm,
                    durationMin,
                    availableSeats: Number(seatsInput.value)
                })
            });

            if (!response.ok) {
                const error = await response.json().catch(() => ({}));
                throw new Error(error.message || error.title || "Unable to estimate payout.");
            }

            const payout = await response.json();
            document.getElementById("payout-base").textContent = money.format(payout.baseFee);
            document.getElementById("payout-distance-label").textContent =
                `${distanceKm.toFixed(1)} km × ${money.format(payout.ratePerKm)}`;
            document.getElementById("payout-distance").textContent =
                money.format(distanceKm * payout.ratePerKm);
            document.getElementById("payout-time-label").textContent =
                `${durationMin} min × ${money.format(payout.ratePerMin)}`;
            document.getElementById("payout-time").textContent =
                money.format(durationMin * payout.ratePerMin);
            document.getElementById("payout-minimum").textContent = money.format(payout.minimum);
            document.getElementById("payout-seat").textContent = money.format(payout.estimatedPricePerSeat);
            document.getElementById("payout-total").textContent = money.format(payout.estimatedTotalEarnings);
            seatHidden.value = payout.estimatedPricePerSeat;
            totalHidden.value = payout.estimatedTotalEarnings;
            status.hidden = true;
            breakdown.hidden = false;
        } catch (error) {
            if (error.name === "AbortError") return;
            status.hidden = false;
            status.textContent = error.message || "Payout preview unavailable.";
            breakdown.hidden = true;
        }
    }

    root.querySelectorAll("[data-seat-stepper] [data-step]").forEach((button) => {
        button.addEventListener("click", () => {
            setSeats((Number(seatsInput.value) || 1) + Number(button.dataset.step));
        });
    });

    root.querySelectorAll("[data-chip]").forEach((chip) => {
        chip.addEventListener("click", () => toggleChip(chip.dataset.chip));
    });

    autoApprove?.addEventListener("change", updateBookingHint);
    instructions?.addEventListener("input", syncChips);

    syncChips();
    updateBookingHint();
    setSeats(Number(seatsInput.value) || 1);
})();
