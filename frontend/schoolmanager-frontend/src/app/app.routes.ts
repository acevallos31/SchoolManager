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

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'home', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  // Dashboard: autenticación pura (sin permiso concreto en Permisos.cs).
  { path: 'dashboard', component: Dashboard, canActivate: [permissionGuard] },
  {
    path: 'alumnos',
    component: AppShell,
    canActivate: [permissionGuard],
    data: { permiso: 'academico.alumnos.ver' },
    children: [
      { path: '', component: Alumnos }
    ]
  },
  {
    path: 'matriculas',
    component: Matriculas,
    canActivate: [permissionGuard],
    data: { permiso: 'academico.matriculas.ver' }
  },
  // portal-padre (bloque 022): consume la API .NET (PortalResponsableController)
  // en modo lectura. Sin guard adicional: la autorización la valida el backend.
  { path: 'portal-padre', component: PortalPadre },
  // Configuracion y sus vistas genéricas no tienen permiso concreto en
  // Permisos.cs (solo autenticación). Los submenús financieros sí.
  { path: 'configuracion', component: Configuracion, canActivate: [permissionGuard] },
  { path: 'configuracion/ciclos', component: ConfiguracionCiclos, canActivate: [permissionGuard] },
  { path: 'configuracion/estructura-academica', component: ConfiguracionEstructuraAcademica, canActivate: [permissionGuard] },
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
  },
  { path: '**', redirectTo: 'login' }
];
