import { ApplicationConfig, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { inject } from '@angular/core';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { JwtInterceptor } from './core/interceptors/jwt.interceptor';
import { AuthService } from './core/services/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
    // Barrera de inicialización: carga la sesión persistida y los permisos
    // (auth/me) ANTES de que Angular resuelva la primera ruta, de modo que los
    // guards evalúen permisos ya poblados y no contra estado sin cargar.
    provideAppInitializer(() => inject(AuthService).asegurarUsuarioInicial())
  ]
};
