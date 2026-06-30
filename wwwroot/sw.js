// WorshipSearch Service Worker — network-first, sem cache offline
// (o app depende de servidor e API, cache offline não faz sentido)

self.addEventListener('install', function() {
    self.skipWaiting();
});

self.addEventListener('activate', function(e) {
    e.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', function(e) {
    // Passa todas as requisições direto para a rede
    // Se a rede falhar, o browser mostra o erro padrão
    if (e.request.method !== 'GET') return;
    e.respondWith(fetch(e.request));
});
