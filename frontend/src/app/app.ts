import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LiveConnectionService } from './core/live-connection.service';
import { ClickTrackerService } from './core/click-tracker.service';
import { VisitTrackerService } from './core/visit-tracker.service';
import { SeoService } from './core/seo.service';
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
  private readonly clickTracker = inject(ClickTrackerService);
  private readonly visitTracker = inject(VisitTrackerService);
  private readonly seo = inject(SeoService);
  private readonly router = inject(Router);

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
  }
}
