import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),

    // XSRF protection is ON by default: for POST/PUT/DELETE to our own origin,
    // HttpClient copies the XSRF-TOKEN cookie into the X-XSRF-TOKEN header.
    provideHttpClient(),

    // Before the app starts, ask the API to issue the antiforgery cookies.
    provideAppInitializer(() => firstValueFrom(inject(HttpClient).get('/api/antiforgery/token'))),
  ],
};
