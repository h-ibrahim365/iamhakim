import { Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { TranslationService } from '../../i18n/translation.service';

@Component({
  selector: 'app-privacy',
  imports: [TranslatePipe],
  templateUrl: './privacy.component.html',
  styleUrl: './privacy.component.scss'
})
export class PrivacyComponent {
  private readonly i18n = inject(TranslationService);
  private readonly lastUpdatedDate = new Date(Date.UTC(2026, 4, 31, 12, 0, 0));

  protected readonly lastUpdated = computed(() =>
    new Intl.DateTimeFormat(this.locale(), {
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    }).format(this.lastUpdatedDate)
  );

  private locale(): string {
    switch (this.i18n.lang()) {
      case 'fr': return 'fr-BE';
      case 'nl': return 'nl-BE';
      default: return 'en-GB';
    }
  }
}
