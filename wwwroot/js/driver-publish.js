(() => {
    const element = document.getElementById("publish-map");
    if (!element) return;
    const data = JSON.parse(document.getElementById("publish-route-data").textContent);
    DriverMap.create(element, data).draw(data.originPlaceId, data.destinationPlaceId, data.stops);
})();
