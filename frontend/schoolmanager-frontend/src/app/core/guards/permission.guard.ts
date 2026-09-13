import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth';
import { resolverRutaInicial } from '../services/landing-route';

const LOGIN_URL = '/login';
const ACCESO_PENDIENTE_URL = '/acceso-pendiente';

/**
 * Guard de navegación basado en permisos concretos. Lee el permiso requerido
 * de `route.data['permiso']` y lo comprueba contra el modelo de permisos del
 * usuario cargado por `AuthService` (backed por /auth/me).
 *
 * El backend/RLS siguen siendo la autoridad de seguridad: este guard solo
 * evita incoherencias de navegación/UI, nunca sustituye la autorización
 * del servidor.
 *
 * Cuando la ruta no exige un permiso concreto (por ejemplo el AppShell), el
 * guard también valida que el perfil tenga un destino administrativo. Así un
 * responsable no cae accidentalmente en /dashboard y un perfil futuro sin
 * módulo disponible recibe un estado accionable en vez de un logout.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree([LOGIN_URL]);
  }

  const usuario = auth.usuarioActual();
  const rutaInicial = usuario ? resolverRutaInicial(usuario) : null;
  const permiso = route.data?.['permiso'] as string | undefined;

  if (!permiso) {
    if (rutaInicial === '/dashboard') return true;
    return router.createUrlTree([rutaInicial ?? ACCESO_PENDIENTE_URL]);
  }

  if (!auth.tienePermiso(permiso)) {
    return router.createUrlTree([rutaInicial ?? ACCESO_PENDIENTE_URL]);
  }

  return true;
};
