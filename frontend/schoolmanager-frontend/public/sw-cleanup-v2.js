(async () => {
  if (!('serviceWorker' in navigator)) return;

  try {
    const registrations = await navigator.serviceWorker.getRegistrations();
    await Promise.all(registrations.map((registration) => registration.unregister()));

    if ('caches' in window) {
      const keys = await caches.keys();
      await Promise.all(
        keys
          .filter((key) => key.startsWith('schoolmanager-static-'))
          .map((key) => caches.delete(key))
      );
    }
  } catch {
    // La aplicación continúa online aunque la limpieza del service worker falle.
  }
})();
