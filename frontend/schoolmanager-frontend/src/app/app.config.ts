import { isPlatformBrowser } from '@angular/common';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  PLATFORM_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { JwtInterceptor } from './core/interceptors/jwt.interceptor';
import { AuthService } from './core/services/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    // La app corre en Angular 20+ (22.x), donde bootstrapApplication arranca en
    // cambio de detección ZONELESS por defecto (ZONELESS_ENABLED=true) aunque
    // zone.js esté en polyfills: la vista NO se actualiza al resolver promises
    // (async/await + asignación a propiedad plana) hasta que ocurre un evento,
    // señal, markForCheck o detectChanges. Esto dejaba páginas como Alumnos y
    // Matrículas atascadas en «Cargando...» tras el merge del Bloque 030.
    // provideZoneChangeDetection() restaura el CD por zona (zone.js ya está
    // cargado), de modo que la resolución de promises vuelve a disparar la
    // detección automáticamente. Es el fix mínimo y central: no se agregan
    // detectChanges() a ninguna página.
    provideZoneChangeDetection(),
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
    // Barrera de inicialización: carga la sesión persistida y los permisos
    // (auth/me) ANTES de resolver la primera ruta en el navegador. Durante SSG
    // no existe una sesión de usuario que restaurar y la inicialización termina
    // inmediatamente, evitando llamadas browser-only en el prerender.
    provideAppInitializer(() => {
      const platformId = inject(PLATFORM_ID);
      return isPlatformBrowser(platformId)
        ? inject(AuthService).asegurarUsuarioInicial()
        : Promise.resolve();
    })
  ]
};
