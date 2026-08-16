import { RouterOutletComponent } from '@abp/ng.core';
import { Routes } from '@angular/router';

export const LOST_FOUND_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    component: RouterOutletComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./components/lost-found.component').then(c => c.LostFoundComponent),
      },
    ],
  },
];
