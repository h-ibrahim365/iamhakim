import { Component, NgZone, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, take } from 'rxjs/operators';
import { LiveConnectionService } from './core/live-connection.service';
import { ClickTrackerService } from './core/click-tracker.service';
import { VisitTrackerService } from './core/visit-tracker.service';
import { SeoService } from './core/seo.service';
import { ThemeService } from './core/theme.service';
import { TranslationService } from './i18n/translation.service';
import { TranslatePipe } from './i18n/translate.pipe';
import { LocalizedLinkPipe } from './i18n/localized-link.pipe';
import { Lang, LANGUAGES } from './i18n/translations';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, LocalizedLinkPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly live = inject(LiveConnectionService);
  protected readonly i18n = inject(TranslationService);
  protected readonly theme = inject(ThemeService);
  private readonly clickTracker = inject(ClickTrackerService);
  private readonly visitTracker = inject(VisitTrackerService);
  private readonly seo = inject(SeoService);
  private readonly router = inject(Router);
  private readonly ngZone = inject(NgZone);

  /**
   * Switch the active language by navigating to the same page in the target language.
   * Preserves the path after the language segment, so /fr/projects → /en/projects.
   */
  protected setLang(code: string): void {
    if (!LANGUAGES.some((l) => l.code === code)) return;
    const tree = this.router.parseUrl(this.router.url);
    const segments = tree.root.children['primary']?.segments ?? [];
    // Replace the first segment (the lang) with the new one
    const rest = segments.slice(1).map((s) => s.path);
    void this.router.navigate(['/', code, ...rest]);
  }

  protected readonly statusLabel = computed(() => {
    const state = this.live.state();
    if (state === 'connected') return this.i18n.t('live.connected');
    if (state === 'reconnecting') return this.i18n.t('live.reconnecting');
    return this.i18n.t('live.offline');
  });

  constructor() {
    void this.live.start();
    this.clickTracker.start();
    this.visitTracker.start();
    this.seo.start();
    this.watchRouteScroll();
  }

  /**
   * Replaces Angular's built-in scroll restoration entirely (both options
   * are off in app.config.ts):
   *  - plain navigation (no #fragment) → scroll to top, same as
   *    scrollPositionRestoration:'top' would have done.
   *  - navigation with a #fragment → scrollIntoView() on that element, which
   *    (unlike Angular's own anchorScrolling, a raw getBoundingClientRect()
   *    + scrollTo()) respects `scroll-margin-top` (--topbar-scroll-offset)
   *    and clears the sticky topbar correctly.
   * Leaving scrollPositionRestoration:'top' enabled while anchorScrolling
   * stayed off used to force *every* navigation - fragment or not - back to
   * (0,0) right after our own scrollIntoView ran, undoing it. Both have to
   * be handled here together to avoid that fight.
   */
  private watchRouteScroll(): void {
    if (typeof window === 'undefined') return;
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe(() => {
      const fragment = this.router.parseUrl(this.router.url).fragment;

      const scroll = () => {
        requestAnimationFrame(() => {
          if (fragment) {
            document.getElementById(fragment)?.scrollIntoView({ behavior: 'auto', block: 'start' });
          } else {
            window.scrollTo(0, 0);
          }
        });
      };

      // A full route change may still have pending rendering work right when
      // NavigationEnd fires - wait for Angular's zone to settle before
      // touching the DOM/scroll position.
      if (this.ngZone.isStable) {
        scroll();
      } else {
        this.ngZone.onStable.pipe(take(1)).subscribe(scroll);
      }
    });
  }
}
