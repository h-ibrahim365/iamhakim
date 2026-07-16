import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Legacy un-prefixed URLs and the bare root '/' are pure redirects to the
 * localized route (see app.routes.ts' `redirectTo` rules). Angular's own
 * `redirectTo` doesn't translate into a real HTTP 3xx during SSR - in both
 * RenderMode.Client and RenderMode.Server it just renders the destination
 * page's content inline at a 200, which crawlers see as a thin/duplicate
 * page under the wrong URL. Handling these here, before Angular ever sees
 * the request, gives a genuine single-hop 301 with the right Location header.
 */
const LEGACY_REDIRECTS: Record<string, string> = {
  '/': '/en',
  '/projects': '/en/projects',
  '/about': '/en/about',
  '/flow': '/en/flow',
  '/status': '/en/status',
  '/book': '/en/book',
  '/book/manage': '/en/book/manage',
  '/privacy': '/en/privacy',
};

app.get(Object.keys(LEGACY_REDIRECTS), (req, res) => {
  res.redirect(301, LEGACY_REDIRECTS[req.path]);
});

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
