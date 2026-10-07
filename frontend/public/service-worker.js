// Minimal service worker: receives Web Push payloads while the app isn't in the
// foreground and surfaces them as OS notifications. This is the piece that lets a
// "remote service" (the backend's nudge sweep) reach you without the tab being open.

self.addEventListener("push", (event) => {
  let data = { title: "Open Work", body: "A work item needs your attention." };
  try {
    if (event.data) {
      data = { ...data, ...event.data.json() };
    }
  } catch {
    // Non-JSON payload: fall back to the defaults above.
  }

  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: "/icon.svg",
      badge: "/icon.svg",
      tag: data.itemId ?? "open-work-nudge",
      renotify: true
    })
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if ("focus" in client) {
          return client.focus();
        }
      }
      if (self.clients.openWindow) {
        return self.clients.openWindow("/");
      }
    })
  );
});
