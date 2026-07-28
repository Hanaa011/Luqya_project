import { eLayoutType, RoutesService } from '@abp/ng.core';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eLostFoundRouteNames } from '../enums/route-names';

export const LOST_FOUND_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routesService = inject(RoutesService);
  routesService.add([
    {
      path: '/lost-found',
      name: eLostFoundRouteNames.LostFound,
      iconClass: 'fas fa-book',
      layout: eLayoutType.application,
      order: 3,
    },
  ]);
}

const LOST_FOUND_PROVIDERS: EnvironmentProviders[] = [...LOST_FOUND_ROUTE_PROVIDERS];

export function provideLostFound() {
  return makeEnvironmentProviders(LOST_FOUND_PROVIDERS);
}
