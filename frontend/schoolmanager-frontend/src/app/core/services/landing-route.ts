import { UsuarioActual } from './auth';

const ROL_PORTAL_RESPONSABLE = new Set(['padre', 'parent']);

function tieneCapacidadDeAplicacion(usuario: UsuarioActual): boolean {
  return usuario.permisos.some(permiso =>
    permiso.startsWith('academico.') ||
    permiso.startsWith('configuracion.') ||
    permiso.startsWith('identidad.') ||
    permiso.startsWith('reportes.') ||
    permiso.startsWith('tickets.') ||
    permiso.startsWith('platform.')
  );
}

/**
 * Resuelve el destino inicial sin acoplar la navegación a un nombre fijo de
 * rol institucional. Los permisos efectivos llevan a la aplicación
 * administrativa; solo se conserva un alias centralizado para el portal del
 * responsable mientras ese portal aún no tiene una capacidad propia.
 */
export function resolverRutaInicial(usuario: UsuarioActual): string | null {
  if (tieneCapacidadDeAplicacion(usuario)) {
    return '/dashboard';
  }

  if (usuario.roles.some(rol => ROL_PORTAL_RESPONSABLE.has(rol))) {
    return '/portal-padre';
  }

  return null;
}
