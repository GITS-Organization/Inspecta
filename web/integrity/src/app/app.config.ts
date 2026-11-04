import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { CERTIFICATION_API } from './api/certification.api';
import { MockCertificationApi } from './api/mock-certification.api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    // The backend endpoints for direct certificates and gears are not final yet: the tab runs on the in-memory mock.
    { provide: CERTIFICATION_API, useClass: MockCertificationApi },
  ],
};
