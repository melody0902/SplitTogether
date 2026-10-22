import { inject } from '@angular/core';
import { Routes, Router, CanActivateFn } from '@angular/router';
import { Api } from './api';
const signedIn: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    await api.init();
    return api.user() ? true : router.createUrlTree(['/login']);
  } catch {
    return router.createUrlTree(['/login'], { queryParams: { error: 'connection' } });
  }
};
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./login').then((m) => m.Login) },
  { path: 'invite/:token', loadComponent: () => import('./invite').then((m) => m.Invite) },
  {
    path: 'groups',
    canActivate: [signedIn],
    loadComponent: () => import('./groups').then((m) => m.Groups),
  },
  {
    path: 'groups/:id/expenses/new',
    canActivate: [signedIn],
    canDeactivate: [(c: { canLeave: () => boolean }) => c.canLeave()],
    loadComponent: () => import('./expense-editor').then((m) => m.ExpenseEditor),
  },
  {
    path: 'groups/:id/expenses/:expenseId/edit',
    canActivate: [signedIn],
    canDeactivate: [(c: { canLeave: () => boolean }) => c.canLeave()],
    loadComponent: () => import('./expense-editor').then((m) => m.ExpenseEditor),
  },
  {
    path: 'groups/:id/expenses/:expenseId',
    canActivate: [signedIn],
    loadComponent: () => import('./expense-detail').then((m) => m.ExpenseDetail),
  },
  {
    path: 'groups/:id',
    canActivate: [signedIn],
    loadComponent: () => import('./group-detail').then((m) => m.GroupDetail),
  },
  { path: '', pathMatch: 'full', redirectTo: 'groups' },
  { path: '**', redirectTo: 'groups' },
];
