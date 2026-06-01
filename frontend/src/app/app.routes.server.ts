import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // SSR on demand: pages depend on a live API + SignalR, so static prerender isn't appropriate.
    path: '**',
    renderMode: RenderMode.Server
  }
];
