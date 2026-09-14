import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth';
import { resolverRutaInicial } from '../services/landing-route';

const LOGIN_URL = '/login';
const ACCESO_PENDIENTE_URL = '/acceso-pendiente';

/**
 * Guard de navegación basado en permisos concretos. Admite un permiso único,
 * una lista OR y, solo cuando la ruta lo declara expresamente,
 * `permitirSuperadministrador`. El backend/RLS siguen siendo la autoridad de
 * seguridad y vuelven a validar cada operación.
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
  const permisosCualquiera = route.data?.['permisosCualquiera'] as string[] | undefined;
  const permitirSuperadministrador = route.data?.['permitirSuperadministrador'] === true;
  const exigePermiso = Boolean(permiso)
    || Boolean(permisosCualquiera?.length)
    || permitirSuperadministrador;

  if (!exigePermiso) {
    if (rutaInicial === '/dashboard') return true;
    return router.createUrlTree([rutaInicial ?? ACCESO_PENDIENTE_URL]);
  }

  const autorizado = Boolean(permiso && auth.tienePermiso(permiso))
    || Boolean(permisosCualquiera?.some(codigo => auth.tienePermiso(codigo)))
    || (permitirSuperadministrador && auth.esSuperadministrador());

  if (!autorizado) {
    return router.createUrlTree([rutaInicial ?? ACCESO_PENDIENTE_URL]);
  }

  return true;
};
