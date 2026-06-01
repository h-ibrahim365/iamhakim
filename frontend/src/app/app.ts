import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LiveConnectionService } from './core/live-connection.service';
import { ClickTrackerService } from './core/click-tracker.service';
import { VisitTrackerService } from './core/visit-tracker.service';
import { TranslationService } from './i18n/translation.service';
import { TranslatePipe } from './i18n/translate.pipe';
import { Lang } from './i18n/translations';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly live = inject(LiveConnectionService);
  protected readonly i18n = inject(TranslationService);
  private readonly clickTracker = inject(ClickTrackerService);
  private readonly visitTracker = inject(VisitTrackerService);

  protected setLang(code: string): void {
    this.i18n.setLang(code as Lang);
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
  }
}
