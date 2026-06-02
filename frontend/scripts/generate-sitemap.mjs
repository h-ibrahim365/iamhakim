/**
 * Generate sitemap.xml with multilingual hreflang alternates.
 *
 * Runs as a `prebuild` step in package.json. Writes to public/sitemap.xml so
 * the Angular build picks it up and serves it at /sitemap.xml.
 *
 * Source of truth for routes: the SITE_PAGES array below. Keep in sync with
 * app.routes.ts when you add/remove a page.
 *
 * `lastmod` strategy:
 *   - Each page's lastmod is the current date when the script runs (UTC).
 *   - This is good enough for a small portfolio. Don't over-engineer the
 *     git-log scan unless you have content that genuinely changes per route.
 */

import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_PATH = resolve(__dirname, '..', 'public', 'sitemap.xml');

const ORIGIN = 'https://iamhakim.com';
const LANGS = ['en', 'fr', 'nl'];
const DEFAULT_LANG = 'en';

/** path (without lang prefix), changefreq, priority */
const SITE_PAGES = [
  { path: '',         changefreq: 'weekly',  priority: '1.0' },
  { path: 'projects', changefreq: 'weekly',  priority: '0.9' },
  { path: 'about',    changefreq: 'monthly', priority: '0.8' },
  { path: 'flow',     changefreq: 'monthly', priority: '0.7' },
  { path: 'book',     changefreq: 'monthly', priority: '0.6' },
  { path: 'status',   changefreq: 'weekly',  priority: '0.5' },
  { path: 'privacy',  changefreq: 'yearly',  priority: '0.3' },
];

const today = new Date().toISOString().slice(0, 10);

function urlFor(lang, path) {
  return `${ORIGIN}/${lang}${path ? '/' + path : ''}`;
}

function buildAlternates(path) {
  const alts = LANGS.map(
    (l) => `    <xhtml:link rel="alternate" hreflang="${l}" href="${urlFor(l, path)}"/>`,
  );
  alts.push(`    <xhtml:link rel="alternate" hreflang="x-default" href="${urlFor(DEFAULT_LANG, path)}"/>`);
  return alts.join('\n');
}

function buildEntry(lang, page) {
  return `  <url>
    <loc>${urlFor(lang, page.path)}</loc>
    <lastmod>${today}</lastmod>
    <changefreq>${page.changefreq}</changefreq>
    <priority>${page.priority}</priority>
${buildAlternates(page.path)}
  </url>`;
}

const entries = [];
for (const page of SITE_PAGES) {
  for (const lang of LANGS) {
    entries.push(buildEntry(lang, page));
  }
}

const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:xhtml="http://www.w3.org/1999/xhtml">

${entries.join('\n\n')}

</urlset>
`;

await writeFile(OUT_PATH, xml, 'utf8');
console.log(`✔ sitemap.xml written: ${SITE_PAGES.length * LANGS.length} URLs, lastmod=${today}`);
