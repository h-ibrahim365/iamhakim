import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from './translation.service';

/**
 * Usage: {{ 'home.title.accent' | t }} or {{ 'astar.result.win' | t:{ n: score() } }}
 * Impure so it re-evaluates when the language signal changes.
 */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: string, params?: Record<string, string | number>): string {
    // touch the signal so Angular re-runs this pipe on language change
    this.i18n.lang();
    return this.i18n.t(key, params);
  }
}
