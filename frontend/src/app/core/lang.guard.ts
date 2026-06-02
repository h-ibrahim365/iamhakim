import { inject } from '@angular/core';
import { Router, type CanMatchFn, type UrlSegment } from '@angular/router';
import { TranslationService } from '../i18n/translation.service';
import { LANGUAGES, type Lang } from '../i18n/translations';

const SUPPORTED: readonly Lang[] = LANGUAGES.map((l) => l.code);

/**
 * Matches a route only when its first segment is a supported language code.
 *
 *   /en/projects  →  matches, lang set to 'en'
 *   /fr           →  matches, lang set to 'fr'
 *   /projects     →  does NOT match (falls through to legacy redirects)
 *
 * The lang code is also pushed to the TranslationService so all UI text and
 * the <html lang> attribute stay in sync.
 */
export const langMatchGuard: CanMatchFn = (_route, segments: UrlSegment[]) => {
  if (segments.length === 0) return false;
  const code = segments[0].path;
  if (!SUPPORTED.includes(code as Lang)) return false;
  inject(TranslationService).setLangFromRoute(code as Lang);
  return true;
};

/**
 * For the root path "": redirect to the user's preferred language
 * (localStorage saved → navigator.language → 'en').
 */
export const rootLangRedirect = (): string => {
  const router = inject(Router);
  const i18n = inject(TranslationService);
  const lang = i18n.preferredLang();
  return `/${lang}`;
};
