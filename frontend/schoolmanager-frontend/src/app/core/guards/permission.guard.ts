import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth';

const LOGIN_URL = '/login';
// Ruta segura existente de respaldo cuando el usuario está autenticado pero
// no posee el permiso requerido. No existe una ruta 403 dedicada; se reutiliza
// el dashboard como destino de navegación coherente (limitación documentada en
// docs/technical-debt.md, no se crea un mini-módulo 403).
const RUTA_DENEGADA = '/dashboard';

/**
 * Guard de navegación basado en permisos concretos. Lee el permiso requerido
 * de `route.data['permiso']` y lo comprueba contra el modelo de permisos del
 * usuario cargado por `AuthService` (backed por /auth/me).
 *
 * El backend/RLS siguen siendo la autoridad de seguridad: este guard solo
 * evita incoherencias de navegación/UI, nunca sustituye la autorización
 * del servidor.
 *
 * Comportamiento:
 *  - sin sesión                -> redirige a /login
 *  - sesión sin el permiso     -> redirige a RUTA_DENEGADA (/dashboard)
 *  - sesión con el permiso     -> permite el acceso
 *  - ruta sin data.permiso     -> permite (guard de autenticación puro)
 */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree([LOGIN_URL]);
  }

  const permiso = route.data?.['permiso'] as string | undefined;
  if (permiso && !auth.tienePermiso(permiso)) {
    return router.createUrlTree([RUTA_DENEGADA]);
  }

  return true;
};
