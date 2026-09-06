import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { AppShell } from './layout/app-shell/app-shell';
import { Alumnos } from './pages/alumnos/alumnos';
import { Matriculas } from './pages/matriculas/matriculas';
import { PortalPadre } from './pages/portal-padre/portal-padre';
import { Configuracion } from './pages/configuracion/configuracion';
import { ConfiguracionCiclos } from './pages/configuracion-ciclos/configuracion-ciclos';
import { ConfiguracionEstructuraAcademica } from './pages/configuracion-estructura-academica/configuracion-estructura-academica';
import { permissionGuard } from './core/guards/permission.guard';

// Ruta de panel usada como destino cuando el guard niega un permiso.
const PANEL = '/dashboard';

/**
 * Rutas de área autenticada de administración: todas las secciones cuelgan del
 * AppShell global (topbar + sidebar + drawer responsive). El guard en la ruta
 * padre exige sesión; los guards en las rutas hijas exigen permisos concretos.
 * /portal-padre (responsable) y /login quedan fuera del shell por diseño.
 */
export const routes: Routes = [
  // AppShell padre de todas las rutas admin (dashboard, alumnos, matriculas,
  // responsables, cargos, pagos, configuracion y subvistas).
  {
    path: '',
    component: AppShell,
    canActivate: [permissionGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

      // Dashboard: autenticación pura (sin permiso concreto en Permisos.cs).
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

      // Configuracion y sus vistas genéricas no tienen permiso concreto en
      // Permisos.cs (solo autenticación). Los submenús financieros sí.
      { path: 'configuracion', component: Configuracion },
      { path: 'configuracion/ciclos', component: ConfiguracionCiclos },
      {
        path: 'configuracion/estructura-academica',
        component: ConfiguracionEstructuraAcademica
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

  // Rutas fuera del shell admin.
  { path: 'login', component: Login },

  // portal-padre (bloque 022): consume la API .NET (PortalResponsableController)
  // en modo lectura. Sin guard adicional: la autorización la valida el backend.
  { path: 'portal-padre', component: PortalPadre },

  { path: 'home', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: PANEL }
];
