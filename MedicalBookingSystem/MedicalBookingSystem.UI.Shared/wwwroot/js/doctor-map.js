// Leaflet + OpenStreetMap helpers for the doctor search map. Popup content is built with textContent,
// so names and addresses coming from the API can never inject markup.
window.medicalMap = {
  tiles(map) {
    window.L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);
  },
  ensure(element) {
    if (!window.L) throw new Error('Leaflet did not load.');
    if (!element._medicalMap) {
      const map = window.L.map(element, { scrollWheelZoom: false });
      this.tiles(map);
      map.setView([52.07, 19.48], 6);
      element._medicalMap = { map, layer: window.L.layerGroup().addTo(map) };
    }
    return element._medicalMap;
  },
  showDoctors(element, centre, clinics) {
    const state = this.ensure(element);
    state.layer.clearLayers();
    const bounds = [];
    if (centre) {
      window.L.circle([centre.latitude, centre.longitude], { radius: centre.radiusKm * 1000, color: '#0b5ed7', weight: 1, fillOpacity: 0.06 })
        .addTo(state.layer);
      window.L.circleMarker([centre.latitude, centre.longitude], { radius: 6, color: '#0b5ed7', fillOpacity: 1 })
        .bindTooltip('Punkt wyszukiwania').addTo(state.layer);
      bounds.push([centre.latitude, centre.longitude]);
    }
    for (const clinic of clinics) {
      const popup = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = clinic.name;
      const address = document.createElement('div');
      address.className = 'small text-muted';
      address.textContent = clinic.address + (clinic.distanceKm != null ? ` · ${clinic.distanceKm.toFixed(1).replace('.', ',')} km` : '');
      popup.append(title, address);
      const list = document.createElement('ul');
      list.className = 'list-unstyled mt-2 mb-0';
      for (const doctor of clinic.doctors) {
        const item = document.createElement('li');
        const name = document.createElement('div');
        name.textContent = doctor.name + (doctor.nextSlot ? ` — ${doctor.nextSlot}` : '');
        const link = document.createElement('a');
        link.href = doctor.bookUrl;
        link.textContent = 'Zarezerwuj wizytę';
        item.append(name, link);
        list.append(item);
      }
      popup.append(list);
      window.L.marker([clinic.latitude, clinic.longitude], { title: clinic.name, alt: clinic.name }).bindPopup(popup).addTo(state.layer);
      bounds.push([clinic.latitude, clinic.longitude]);
    }
    if (bounds.length > 0) state.map.fitBounds(bounds, { padding: [30, 30], maxZoom: 13 });
    setTimeout(() => state.map.invalidateSize(), 0);
  },
  // Administrator pin: click or drag to move; the component receives the new coordinates.
  picker(element, latitude, longitude, component) {
    const state = this.ensure(element);
    state.layer.clearLayers();
    const start = latitude != null && longitude != null ? [latitude, longitude] : [50.0614, 19.9366];
    const marker = window.L.marker(start, { draggable: true, title: 'Położenie placówki' }).addTo(state.layer);
    const report = position => component.invokeMethodAsync('PinMoved', position.lat, position.lng);
    marker.on('dragend', () => report(marker.getLatLng()));
    state.map.on('click', event => { marker.setLatLng(event.latlng); report(event.latlng); });
    state.marker = marker;
    state.map.setView(start, latitude != null ? 16 : 12);
    setTimeout(() => state.map.invalidateSize(), 0);
  },
  setPin(element, latitude, longitude) {
    const state = this.ensure(element);
    if (state.marker) state.marker.setLatLng([latitude, longitude]);
    state.map.setView([latitude, longitude], 16);
  },
  locate() {
    return new Promise((resolve, reject) => {
      if (!navigator.geolocation) { reject(new Error('unsupported')); return; }
      navigator.geolocation.getCurrentPosition(
        position => resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude }),
        error => reject(new Error(error.code === 1 ? 'denied' : 'unavailable')),
        { enableHighAccuracy: false, timeout: 10000, maximumAge: 600000 });
    });
  },
  dispose(element) {
    if (element && element._medicalMap) { element._medicalMap.map.remove(); element._medicalMap = null; }
  }
};
