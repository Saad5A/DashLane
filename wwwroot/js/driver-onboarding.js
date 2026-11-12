(() => {
    const root = document.getElementById("driver-onboarding");
    if (!root) return;

    const capacityInput = root.querySelector("#PassengerCapacity");
    const capacityOutput = root.querySelector("#capacity-output");
    const accessible = root.querySelector("#IsWheelchairAccessible");
    const tierInputs = [...root.querySelectorAll('input[name="Tier"]')];
    const make = root.querySelector("#Make");
    const model = root.querySelector("#Model");
    const plate = root.querySelector("#LicensePlate");
    const summaryVehicle = root.querySelector("#summary-vehicle");
    const summaryPlate = root.querySelector("#summary-plate");
    const summaryTier = root.querySelector("#summary-tier");
    const summarySeats = root.querySelector("#summary-seats");

    const tierLabels = {
        Standard: "Standard",
        Comfort: "Comfort",
        XL: "XL",
        WheelchairAccessible: "Wheelchair-Accessible"
    };

    const caps = {
        Standard: 4,
        Comfort: 4,
        XL: 6,
        WheelchairAccessible: 4
    };

    function selectedTier() {
        return tierInputs.find((input) => input.checked)?.value || "Standard";
    }

    function clampCapacity(value) {
        const max = caps[selectedTier()] || 6;
        return Math.min(Math.max(value, 1), max);
    }

    function setCapacity(value) {
        const next = clampCapacity(value);
        capacityInput.value = String(next);
        capacityOutput.textContent = String(next);
        if (summarySeats) summarySeats.textContent = String(next);
    }

    function selectTier(value) {
        const match = tierInputs.find((input) => input.value === value);
        if (match) match.checked = true;
        setCapacity(Number(capacityInput.value) || 4);
        if (summaryTier) summaryTier.textContent = tierLabels[value] || value;
    }

    function refreshSummary() {
        const vehicle = `${make?.value.trim() || ""} ${model?.value.trim() || ""}`.trim();
        if (summaryVehicle) summaryVehicle.textContent = vehicle || "Not saved yet";
        if (summaryPlate) summaryPlate.textContent = plate?.value.trim() || "—";
        if (summaryTier) summaryTier.textContent = tierLabels[selectedTier()] || selectedTier();
        if (summarySeats) summarySeats.textContent = capacityInput.value;
    }

    root.querySelectorAll("[data-stepper] [data-step]").forEach((button) => {
        button.addEventListener("click", () => {
            const delta = Number(button.getAttribute("data-step")) || 0;
            setCapacity((Number(capacityInput.value) || 1) + delta);
        });
    });

    tierInputs.forEach((input) => {
        input.addEventListener("change", () => {
            if (input.value === "WheelchairAccessible" && accessible) accessible.checked = true;
            if (input.value !== "WheelchairAccessible" && accessible?.checked) accessible.checked = false;
            setCapacity(Number(capacityInput.value) || 4);
            refreshSummary();
        });
    });

    accessible?.addEventListener("change", () => {
        if (accessible.checked) selectTier("WheelchairAccessible");
        else if (selectedTier() === "WheelchairAccessible") selectTier("Standard");
        refreshSummary();
    });

    [make, model, plate].forEach((field) => field?.addEventListener("input", refreshSummary));

    setCapacity(Number(capacityInput?.value) || 4);
    refreshSummary();
})();
