import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { Alumnos } from './pages/alumnos/alumnos';
import { Matriculas } from './pages/matriculas/matriculas';
import { Mensualidades } from './pages/mensualidades/mensualidades';
import { PortalPadre } from './pages/portal-padre/portal-padre';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard },
  { path: 'alumnos', component: Alumnos },
  { path: 'matriculas', component: Matriculas },
  { path: 'mensualidades', component: Mensualidades },
  { path: 'portal-padre', component: PortalPadre },
  { path: '**', redirectTo: 'login' }
];
