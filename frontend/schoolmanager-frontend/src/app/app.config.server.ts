import { ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { AuthService } from './core/services/auth';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // El login prerenderizado no restaura sesiones. Evita crear el cliente
    // Supabase y sus listeners durante SSG; el navegador usa AuthService real.
    { provide: AuthService, useValue: {} }
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
