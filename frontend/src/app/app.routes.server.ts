import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Server / prerender configuration.
 *
 * Strategy: prerender every public, localized page at build time. This gives
 * the strongest possible SEO signal - crawlers (including ones that don't
 * execute JS) get a fully-rendered HTML document with the localized <title>,
 * <meta description>, <link rel="canonical">, hreflang alternates and
 * JSON-LD structured data already in place.
 *
 * `book/manage` is intentionally not prerendered: it's a private flow
 * (Disallow'd in robots.txt) where the URL carries a one-time token.
 */
const LANGS = ['en', 'fr', 'nl'];
const langParams = async () => LANGS.map((lang) => ({ lang }));

export const serverRoutes: ServerRoute[] = [
  // Each localized page is generated 3 times (en / fr / nl) at build.
  { path: ':lang',          renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/projects', renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/services', renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/about',    renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/flow',     renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/status',   renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/book',     renderMode: RenderMode.Prerender, getPrerenderParams: langParams },
  { path: ':lang/privacy',  renderMode: RenderMode.Prerender, getPrerenderParams: langParams },

  // book/manage: private, token-based, never indexed → render in the client only.
  { path: ':lang/book/manage', renderMode: RenderMode.Client },

  // Everything else (legacy un-prefixed redirects, 404, root '/') → client-render
  // fallback. The legacy paths and root are actually turned into real HTTP 301s
  // by Express middleware in server.ts (Angular's own redirectTo doesn't emit
  // an HTTP-level redirect in either Client or Server render mode - it just
  // renders the destination page's content inline at a 200), so this rule
  // rarely even fires for them in production; it's the true-404 fallback.
  { path: '**', renderMode: RenderMode.Client },
];
