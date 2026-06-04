import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, resolve } from 'node:path';

const DIST_DIR = process.env.CSP_DIST_DIR ?? 'dist/frontend/browser';
const OUTPUT   = process.env.CSP_OUTPUT   ?? 'dist/csp.caddy';

const hashes = new Set();

function walk(dir) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) { walk(full); continue; }
    if (!entry.endsWith('.html')) continue;

    const html = readFileSync(full, 'utf8');
    const re = /<script\b([^>]*)>([\s\S]*?)<\/script>/gi;
    let m;
    while ((m = re.exec(html)) !== null) {
      const attrs = m[1];
      const body = m[2];
      if (/\bsrc\s*=/.test(attrs)) continue;
      const typeMatch = attrs.match(/\btype\s*=\s*["']?([^"'\s>]+)/i);
      if (typeMatch) {
        const t = typeMatch[1].toLowerCase();
        if (t !== 'text/javascript' && t !== 'module') continue;
      }
      if (body.trim() === '') continue;
      const hash = createHash('sha256').update(body, 'utf8').digest('base64');
      hashes.add(`'sha256-${hash}'`);
    }
  }
}

walk(resolve(DIST_DIR));

const hashList = [...hashes].join(' ');
const csp =
  `default-src 'self'; ` +
  `script-src 'self' ${hashList} https://challenges.cloudflare.com; ` +
  `script-src-elem 'self' ${hashList} https://challenges.cloudflare.com; ` +
  `script-src-attr 'unsafe-inline'; ` +
  `style-src 'self' 'unsafe-inline'; ` +
  `font-src 'self' data:; ` +
  `img-src 'self' data: https:; ` +
  `connect-src 'self' https://iamhakim.com wss://iamhakim.com https://challenges.cloudflare.com; ` +
  `frame-src https://challenges.cloudflare.com; ` +
  `child-src https://challenges.cloudflare.com; ` +
  `frame-ancestors 'none'; base-uri 'self'; form-action 'self'; ` +
  `object-src 'none'; upgrade-insecure-requests`;

writeFileSync(OUTPUT, `header Content-Security-Policy "${csp}"\n`);
console.log(`✓ ${hashes.size} hashes inline → ${OUTPUT}`);