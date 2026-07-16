# Fonts directory

Self-host your Google Fonts here as `.woff2` files.

## What to download

From https://gwfh.mranftl.com/fonts (Google Webfonts Helper):

### Fraunces (display)
Pick: latin charset, woff2 only
Weights needed: 600, 700, 800
Rename so files end up like:
- fraunces-600.woff2
- fraunces-700.woff2
- fraunces-800.woff2

### Geist (body)
Weights needed: 400, 500, 600
- geist-400.woff2
- geist-500.woff2
- geist-600.woff2

### JetBrains Mono (code)
Weights needed: 400, 500, 700
- jetbrains-mono-400.woff2
- jetbrains-mono-500.woff2
- jetbrains-mono-700.woff2

## Then

1. Drop the @font-face block from FIXES.md (Tour 2) at the top of `src/styles.scss`
2. Remove the three Google Fonts `<link>` lines from `src/index.html`
3. Rebuild
