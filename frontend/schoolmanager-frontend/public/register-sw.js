if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // La aplicación sigue funcionando online aunque el registro offline falle.
    });
  });
}
