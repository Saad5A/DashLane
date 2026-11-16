// Keyboard-accessible zoom for driver review maps; the route editor has its own controls.
(() => {
    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("[data-driver-coverage]").forEach(container => {
            const data = JSON.parse(container.querySelector("script").textContent);
            const svg = container.querySelector("svg");
            const project = point => [45 + (point.longitude + 79.73) * 1430, 45 + (43.72 - point.latitude) * 1650];
            const add = (tag, attrs, text) => {
                const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
                Object.entries(attrs).forEach(([key, value]) => el.setAttribute(key, value));
                if (text) el.textContent = text;
                svg.append(el);
            };
            add("rect", { width: 600, height: 385, class: "ride-map-land" });
            const match = label => data.places.find(place => label?.startsWith(place.name));
            const origin = match(data.origin), destination = match(data.destination);
            const route = data.routes.find(route => route.pickupId === origin?.id && route.destinationId === destination?.id);
            if (route) add("polyline", { points: route.points.map(point => project(point).join(",")).join(" "),
                fill: "none", stroke: "#a85b00", "stroke-width": 5 });
            data.places.forEach(place => {
                const [x,y] = project(place);
                add("circle", { cx: x, cy: y, r: 5, class: "ride-map-stop" });
                add("text", { x: x + 8, y: y - 7, class: "ride-map-label", "font-size": 14 }, place.name);
            });
        });
        document.querySelectorAll(".post-ride-page .ride-map svg:not(#post-map)").forEach(svg => {
            const initial = svg.getAttribute("viewBox") || "0 0 600 385";
            let width = 600;
            const zoom = direction => {
                width = direction === "reset" ? 600 : Math.min(600, Math.max(180, width * (direction === "in" ? .75 : 1.33)));
                svg.setAttribute("viewBox", direction === "reset" ? initial :
                    `${(600-width)/2} ${(385-width*385/600)/2} ${width} ${width*385/600}`);
            };
            const controls = document.createElement("div");
            controls.className = "ride-map-controls";
            controls.setAttribute("role", "group");
            controls.setAttribute("aria-label", "Route map controls");
            for (const [direction, label, text] of [["in", "Zoom in", "+"], ["out", "Zoom out", "-"], ["reset", "Reset map", "Reset"]]) {
                const button = document.createElement("button");
                button.type = "button"; button.className = "ride-round";
                button.textContent = text; button.setAttribute("aria-label", label);
                button.addEventListener("click", () => zoom(direction)); controls.append(button);
            }
            svg.after(controls);
        });
    });
})();
