import { Routes } from '@angular/router';

import { Dashboard } from './dashboard/dashboard';
import { Reports } from './reports/reports';

export const routes: Routes = [
  { path: '', component: Dashboard, title: 'ServerGuard · Panel' },
  { path: 'reports', component: Reports, title: 'ServerGuard · Rapor' },
  { path: '**', redirectTo: '' }
];
