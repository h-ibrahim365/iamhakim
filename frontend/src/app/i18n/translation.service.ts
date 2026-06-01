import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Lang, LANGUAGES, TRANSLATIONS } from './translations';

const STORAGE_KEY = 'iamhakim.lang';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly platformId = inject(PLATFORM_ID);

  readonly lang = signal<Lang>('en');
  readonly languages = LANGUAGES;

  constructor() {
    this.lang.set(this.detectInitialLang());
  }

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

  setLang(lang: Lang): void {
    this.lang.set(lang);
    if (isPlatformBrowser(this.platformId)) {
      try {
        localStorage.setItem(STORAGE_KEY, lang);
        document.documentElement.lang = lang;
      } catch {
        /* ignore */
      }
    }
  }

  private detectInitialLang(): Lang {
    if (!isPlatformBrowser(this.platformId)) return 'en';
    // 1. explicit choice wins
    try {
      const saved = localStorage.getItem(STORAGE_KEY) as Lang | null;
      if (saved && this.isSupported(saved)) return saved;
    } catch {
      /* ignore */
    }
    // 2. browser language
    const candidates = [navigator.language, ...(navigator.languages ?? [])];
    for (const c of candidates) {
      const code = c.slice(0, 2).toLowerCase();
      if (this.isSupported(code)) return code as Lang;
    }
    return 'en';
  }

  private isSupported(code: string): code is Lang {
    return LANGUAGES.some((l) => l.code === code);
  }
}
