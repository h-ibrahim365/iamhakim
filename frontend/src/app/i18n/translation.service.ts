import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Lang, LANGUAGES, TRANSLATIONS } from './translations';

const STORAGE_KEY = 'iamhakim.lang';
const SUPPORTED: readonly Lang[] = LANGUAGES.map((l) => l.code);

/**
 * Holds the active language and translation lookups.
 *
 * Source of truth = the URL (the `:lang` segment, validated by langMatchGuard).
 * The guard calls `setLangFromRoute` on every navigation; the service then
 * mirrors it to <html lang="..."> and to localStorage (for future sessions).
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);

  readonly lang = signal<Lang>('en');
  readonly languages = LANGUAGES;

  /** Translate a key, with optional {placeholder} interpolation. Falls back to EN, then the key. */
  t(key: string, params?: Record<string, string | number>): string {
    const current = TRANSLATIONS[this.lang()];
    let value = current[key] ?? TRANSLATIONS.en[key] ?? key;
    if (params) {
      for (const [k, v] of Object.entries(params)) {
        value = value.replace(`{${k}}`, String(v));
      }
    }
    return value;
  }

  /**
   * Called by the route guard when a localized route is matched.
   * Updates the signal, mirrors to <html lang>, and persists for future visits.
   */
  setLangFromRoute(lang: Lang): void {
    if (this.lang() === lang) return;
    this.lang.set(lang);
    if (isPlatformBrowser(this.platformId)) {
      this.document.documentElement.lang = lang;
      try {
        localStorage.setItem(STORAGE_KEY, lang);
      } catch {
        /* ignore */
      }
    }
  }

  /**
   * Best-guess preferred language for the initial `/` redirect.
   * Used by the root redirect: localStorage > navigator.language > 'en'.
   */
  preferredLang(): Lang {
    if (!isPlatformBrowser(this.platformId)) return 'en';
    try {
      const saved = localStorage.getItem(STORAGE_KEY) as Lang | null;
      if (saved && this.isSupported(saved)) return saved;
    } catch {
      /* ignore */
    }
    const candidates = [navigator.language, ...(navigator.languages ?? [])];
    for (const c of candidates) {
      const code = c.slice(0, 2).toLowerCase();
      if (this.isSupported(code)) return code as Lang;
    }
    return 'en';
  }

  private isSupported(code: string): code is Lang {
    return SUPPORTED.includes(code as Lang);
  }
}
