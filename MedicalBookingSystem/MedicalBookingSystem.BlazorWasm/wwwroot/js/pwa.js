// Registers the offline-fallback service worker (installable app on phones and desktops).
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/service-worker.js').catch(() => { /* the app works without it */ });
  });
}
