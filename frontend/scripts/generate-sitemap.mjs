import { writeFile } from 'node:fs/promises';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..');
const OUT_PATH = resolve(REPO_ROOT, 'public', 'sitemap.xml');

const ORIGIN = 'https://iamhakim.com';
const LANGS = ['en', 'fr', 'nl'];
const DEFAULT_LANG = 'en';

// Fichiers cross-cutting : si l'un d'eux change, ALL pages bump
const SHARED_PATHS = [
  'src/index.html',
  'src/app/app.routes.ts',
  'src/app/core/seo.service.ts',
  'src/app/i18n/translations.ts',
];

const SITE_PAGES = [
  { path: '',         dir: 'src/app/pages/home',     changefreq: 'weekly',  priority: '1.0' },
  { path: 'projects', dir: 'src/app/pages/projects', changefreq: 'weekly',  priority: '0.9' },
  { path: 'about',    dir: 'src/app/pages/about',    changefreq: 'monthly', priority: '0.8' },
  { path: 'flow',     dir: 'src/app/pages/flow',     changefreq: 'monthly', priority: '0.7' },
  { path: 'book',     dir: 'src/app/pages/book',     changefreq: 'monthly', priority: '0.6' },
  { path: 'status',   dir: 'src/app/pages/status',   changefreq: 'weekly',  priority: '0.5' },
  { path: 'privacy',  dir: 'src/app/pages/privacy',  changefreq: 'yearly',  priority: '0.3' },
];

/** Dernière date de commit qui a touché un ou plusieurs paths, format YYYY-MM-DD. */
function gitLastMod(paths) {
  try {
    const out = execSync(
      `git log -1 --format=%cs -- ${paths.join(' ')}`,
      { cwd: REPO_ROOT, encoding: 'utf8' },
    ).trim();
    return out || new Date().toISOString().slice(0, 10);
  } catch {
    return new Date().toISOString().slice(0, 10);
  }
}

const sharedLastMod = gitLastMod(SHARED_PATHS);

function lastModFor(page) {
  const pageDate = gitLastMod([page.dir]);
  return pageDate > sharedLastMod ? pageDate : sharedLastMod;
}

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

function buildEntry(lang, page, lastmod) {
  return `  <url>
    <loc>${urlFor(lang, page.path)}</loc>
    <lastmod>${lastmod}</lastmod>
    <changefreq>${page.changefreq}</changefreq>
    <priority>${page.priority}</priority>
${buildAlternates(page.path)}
  </url>`;
}

const entries = [];
for (const page of SITE_PAGES) {
  const lastmod = lastModFor(page);
  for (const lang of LANGS) {
    entries.push(buildEntry(lang, page, lastmod));
  }
}

const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:xhtml="http://www.w3.org/1999/xhtml">

${entries.join('\n\n')}

</urlset>
`;

await writeFile(OUT_PATH, xml, 'utf8');
console.log(`✔ sitemap.xml written: ${SITE_PAGES.length * LANGS.length} URLs (per-page lastmod via git)`);