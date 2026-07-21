import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44307/',
  redirectUri: baseUrl,
  clientId: 'Forge_App',
  responseType: 'code',
  scope: 'offline_access Forge',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'Forge',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44307',
      rootNamespace: 'Forge',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
  remoteEnv: {
    url: '/getEnvConfig',
    mergeStrategy: 'deepmerge'
  }
} as Environment;
