import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'iamhakim.theme';

/**
 * Holds the active light/dark theme. The inline boot script in index.html
 * already set <html data-theme="..."> before Angular loaded (avoids a
 * flash of the wrong theme); this service takes over that same attribute
 * and keeps it in sync with user choice + OS preference changes.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<Theme>(this.resolveInitial());

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;
    this.apply(this.theme());

    // Only follow live OS theme changes while the user hasn't picked one explicitly.
    window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      if (this.hasExplicitPreference()) return;
      this.theme.set(e.matches ? 'dark' : 'light');
      this.apply(this.theme());
    });
  }

  toggle(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
    if (!isPlatformBrowser(this.platformId)) return;
    this.apply(theme);
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      /* ignore */
    }
  }

  private apply(theme: Theme): void {
    this.document.documentElement.setAttribute('data-theme', theme);
  }

  private hasExplicitPreference(): boolean {
    try {
      return localStorage.getItem(STORAGE_KEY) !== null;
    } catch {
      return false;
    }
  }

  private resolveInitial(): Theme {
    if (!isPlatformBrowser(this.platformId)) return 'light';
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'light' || saved === 'dark') return saved;
    } catch {
      /* ignore */
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
