import { Routes } from '@angular/router';
import { AppHomeComponent } from './app-home.component';

export const routes: Routes = [
  {
    path: '',
    component: AppHomeComponent
  },
  {
    path: '**',
    redirectTo: ''
  }
];
