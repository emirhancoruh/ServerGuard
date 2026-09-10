import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './core/auth/auth.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Oturum token'ı tek noktadan eklenir; hiçbir servis bunu kendi başına yapmaz.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideRouter(routes)
  ]
};
