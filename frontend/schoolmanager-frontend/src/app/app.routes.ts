import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { AppShell } from './layout/app-shell/app-shell';
import { Alumnos } from './pages/alumnos/alumnos';
import { Matriculas } from './pages/matriculas/matriculas';
import { Mensualidades } from './pages/mensualidades/mensualidades';
import { PortalPadre } from './pages/portal-padre/portal-padre';
import { Configuracion } from './pages/configuracion/configuracion';
import { ConfiguracionCiclos } from './pages/configuracion-ciclos/configuracion-ciclos';
import { ConfiguracionEstructuraAcademica } from './pages/configuracion-estructura-academica/configuracion-estructura-academica';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'home', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard },
  {
    path: 'alumnos',
    component: AppShell,
    children: [
      { path: '', component: Alumnos }
    ]
  },
  { path: 'matriculas', component: Matriculas },
  { path: 'mensualidades', component: Mensualidades },
  { path: 'portal-padre', component: PortalPadre },
  { path: 'configuracion', component: Configuracion },
  { path: 'configuracion/ciclos', component: ConfiguracionCiclos },
  { path: 'configuracion/estructura-academica', component: ConfiguracionEstructuraAcademica },
  {
    path: 'configuracion/conceptos-financieros',
    loadComponent: () => import('./pages/configuracion-conceptos-financieros/configuracion-conceptos-financieros').then(m => m.ConfiguracionConceptosFinancieros)
  },
  {
    path: 'configuracion/planes-pago',
    loadComponent: () => import('./pages/configuracion-planes-pago/configuracion-planes-pago').then(m => m.ConfiguracionPlanesPago)
  },
  {
    path: 'responsables',
    loadComponent: () => import('./pages/responsables/responsables').then(m => m.Responsables)
  },
  { path: '**', redirectTo: 'login' }
];
