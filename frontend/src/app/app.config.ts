import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    // Both options deliberately off: Angular's built-in scroll restoration
    // ignores scroll-margin-top on anchors (lands fragments flush under the
    // sticky topbar), AND - if only anchorScrolling stays off - it still
    // force-scrolls every fragment navigation to (0,0) as a side effect of
    // scrollPositionRestoration:'top'. App.watchRouteScroll() replaces both
    // behaviors (top-on-plain-nav, scrollIntoView-on-fragment) itself.
    provideRouter(
      routes,
      withInMemoryScrolling({ scrollPositionRestoration: 'disabled', anchorScrolling: 'disabled' })
    ),
    provideClientHydration(withEventReplay())
  ]
};
