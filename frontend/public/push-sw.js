/* Web Push u service worker-u (uvozi ga Workbox, vite.config.ts → workbox.importScripts).
   Poruka sa servera: { title, body, url, tag }. Klik otvara aplikaciju na toj ruti (ili fokusira otvoren prozor). */
self.addEventListener('push', (event) => {
  let d = {};
  try {
    d = event.data ? event.data.json() : {};
  } catch {
    d = { title: 'TPM pregledi', body: event.data ? event.data.text() : '' };
  }
  event.waitUntil(
    self.registration.showNotification(d.title || 'TPM pregledi', {
      body: d.body || '',
      tag: d.tag,
      icon: '/favicon.svg',
      badge: '/favicon.svg',
      data: { url: d.url || '/' },
    }),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = new URL(event.notification.data?.url || '/', self.location.origin).href;
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((prozori) => {
      const otvoren = prozori.find((p) => p.url.startsWith(self.location.origin));
      if (otvoren) return otvoren.focus().then(() => otvoren.navigate(url));
      return self.clients.openWindow(url);
    }),
  );
});
