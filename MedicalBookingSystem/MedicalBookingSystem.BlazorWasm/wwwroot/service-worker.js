// Deliberately small service worker: the app always loads from the network (no stale versions after a
// deployment, runtime appsettings.json stays fresh), and only a page navigation without network falls back to
// a cached offline page. Medical data is never cached here.
const CACHE = 'medical-offline-v1';
const OFFLINE_ASSETS = ['/offline.html', '/icon-192.png', '/css/bootstrap/bootstrap.min.css'];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(OFFLINE_ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(key => key !== CACHE).map(key => caches.delete(key))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
  if (event.request.mode !== 'navigate') return; // API calls, framework files and images go straight to the network
  event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')));
});
