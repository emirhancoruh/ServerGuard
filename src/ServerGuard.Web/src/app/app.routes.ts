import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/auth.guard';
import { Dashboard } from './dashboard/dashboard';
import { Login } from './login/login';
import { Reports } from './reports/reports';

export const routes: Routes = [
  {
    path: 'login',
    component: Login,
    title: 'ServerGuard · Giriş',
    canActivate: [guestGuard]
  },
  {
    path: '',
    component: Dashboard,
    title: 'ServerGuard · Panel',
    canActivate: [authGuard]
  },
  {
    path: 'reports',
    component: Reports,
    title: 'ServerGuard · Rapor',
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: '' }
];
