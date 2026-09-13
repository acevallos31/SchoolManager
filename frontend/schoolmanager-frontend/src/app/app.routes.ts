import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { AuthCallback } from './pages/auth-callback/auth-callback';
import { Dashboard } from './pages/dashboard/dashboard';
import { AppShell } from './layout/app-shell/app-shell';
import { Alumnos } from './pages/alumnos/alumnos';
import { Matriculas } from './pages/matriculas/matriculas';
import { PortalPadre } from './pages/portal-padre/portal-padre';
import { Configuracion } from './pages/configuracion/configuracion';
import { ConfiguracionCiclos } from './pages/configuracion-ciclos/configuracion-ciclos';
import { ConfiguracionEstructuraAcademica } from './pages/configuracion-estructura-academica/configuracion-estructura-academica';
import { permissionGuard } from './core/guards/permission.guard';

// Ruta de panel usada como destino de compatibilidad para URLs legacy.
const PANEL = '/dashboard';

/**
 * Rutas de área autenticada de administración: todas las secciones cuelgan del
 * AppShell global (topbar + sidebar + drawer responsive). El guard en la ruta
 * padre exige sesión y una capacidad administrativa; los guards en las rutas
 * hijas exigen permisos concretos. /portal-padre, /acceso-pendiente y /login
 * quedan fuera del shell por diseño.
 */
export const routes: Routes = [
  {
    path: '',
    component: AppShell,
    canActivate: [permissionGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

      { path: 'dashboard', component: Dashboard },

      {
        path: 'alumnos',
        component: Alumnos,
        canActivate: [permissionGuard],
        data: { permiso: 'academico.alumnos.ver' }
      },

      {
        path: 'matriculas',
        component: Matriculas,
        canActivate: [permissionGuard],
        data: { permiso: 'academico.matriculas.ver' }
      },

      { path: 'configuracion', component: Configuracion },
      {
        path: 'configuracion/seguridad-acceso',
        canActivate: [permissionGuard],
        data: { permiso: 'identidad.roles.ver' },
        loadComponent: () => import('./pages/configuracion-seguridad-acceso/configuracion-seguridad-acceso').then(m => m.ConfiguracionSeguridadAcceso)
      },
      {
        path: 'configuracion/ciclos',
        component: ConfiguracionCiclos,
        canActivate: [permissionGuard],
        data: { permiso: 'academico.ciclos.ver' }
      },
      {
        path: 'configuracion/estructura-academica',
        component: ConfiguracionEstructuraAcademica,
        canActivate: [permissionGuard],
        data: { permiso: 'academico.estructura.ver' }
      },
      {
        path: 'configuracion/conceptos-financieros',
        canActivate: [permissionGuard],
        data: { permiso: 'configuracion.conceptos_financieros.ver' },
        loadComponent: () => import('./pages/configuracion-conceptos-financieros/configuracion-conceptos-financieros').then(m => m.ConfiguracionConceptosFinancieros)
      },
      {
        path: 'configuracion/planes-pago',
        canActivate: [permissionGuard],
        data: { permiso: 'configuracion.planes_pago.ver' },
        loadComponent: () => import('./pages/configuracion-planes-pago/configuracion-planes-pago').then(m => m.ConfiguracionPlanesPago)
      },
      {
        path: 'responsables',
        canActivate: [permissionGuard],
        data: { permiso: 'academico.responsables.ver' },
        loadComponent: () => import('./pages/responsables/responsables').then(m => m.Responsables)
      },
      {
        path: 'cargos',
        canActivate: [permissionGuard],
        data: { permiso: 'academico.cargos.ver' },
        loadComponent: () => import('./pages/cargos/cargos').then(m => m.Cargos)
      },
      {
        path: 'pagos',
        canActivate: [permissionGuard],
        data: { permiso: 'academico.pagos.ver' },
        loadComponent: () => import('./pages/pagos/pagos').then(m => m.Pagos)
      }
    ]
  },

  { path: 'login', component: Login },
  { path: 'auth/callback', component: AuthCallback },

  // Portal responsable en modo lectura. La autorización funcional la valida
  // el backend y permanece fuera del AppShell administrativo.
  { path: 'portal-padre', component: PortalPadre },

  // Perfil autenticado y válido, pero sin una sección disponible todavía.
  {
    path: 'acceso-pendiente',
    loadComponent: () => import('./pages/acceso-pendiente/acceso-pendiente').then(m => m.AccesoPendiente)
  },

  { path: 'home', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: PANEL }
];
