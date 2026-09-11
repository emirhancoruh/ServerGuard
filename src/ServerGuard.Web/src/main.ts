import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { API_BASE_URL, loadApiBaseUrl } from './app/core/runtime-config';
import { App } from './app/app';

// API adresi uygulama açılmadan önce okunur. Servislerin hiçbiri henüz
// oluşturulmadığı için adres, ilk isteğe kadar kesinleşmiş olur.
loadApiBaseUrl()
  .then((apiBaseUrl) =>
    bootstrapApplication(App, {
      ...appConfig,
      providers: [...appConfig.providers, { provide: API_BASE_URL, useValue: apiBaseUrl }]
    })
  )
  .catch((err) => console.error(err));
