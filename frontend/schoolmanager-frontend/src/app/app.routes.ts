import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { Alumnos } from './pages/alumnos/alumnos';
import { Matriculas } from './pages/matriculas/matriculas';
import { Mensualidades } from './pages/mensualidades/mensualidades';
import { PortalPadre } from './pages/portal-padre/portal-padre';
import { Configuracion } from './pages/configuracion/configuracion';
import { ConfiguracionCiclos } from './pages/configuracion-ciclos/configuracion-ciclos';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'home', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard },
  { path: 'alumnos', component: Alumnos },
  { path: 'matriculas', component: Matriculas },
  { path: 'mensualidades', component: Mensualidades },
  { path: 'portal-padre', component: PortalPadre },
  { path: 'configuracion', component: Configuracion },
  { path: 'configuracion/ciclos', component: ConfiguracionCiclos },
  { path: '**', redirectTo: 'login' }
];
