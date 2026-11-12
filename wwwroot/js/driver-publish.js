(() => {
    const page = document.getElementById("driver-publish");
    if (!page) return;
    const data = JSON.parse(document.getElementById("publish-route-data").textContent);
    drawMap(data);

    function drawMap(data) {
        const svg = document.getElementById("publish-map");
        const base = document.getElementById("publish-map-basemap");
        const overlay = document.getElementById("publish-map-route");
        if (!svg || !base || !overlay) return;

        const places = data.places || [];
        const routes = data.routes || [];
        const project = (point) => [45 + (point.longitude + 79.73) * 1430, 45 + (43.72 - point.latitude) * 1650];
        const line = (points) => points.map((point) => project(point).join(",")).join(" ");
        const add = (parent, tag, attrs) => {
            const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
            for (const [name, value] of Object.entries(attrs)) el.setAttribute(name, value);
            parent.append(el);
        };

        add(base, "rect", { x: 0, y: 0, width: 600, height: 385, class: "ride-map-land" });
        const route = routes.find((item) =>
            item.pickupId === data.originPlaceId && item.destinationId === data.destinationPlaceId);
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
