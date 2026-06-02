import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from './translation.service';

/**
 * Prepends the active language code to a router path.
 *
 *   '/projects' | loc      → ['/', 'fr', 'projects']  (when lang is 'fr')
 *   ['book', 'manage'] | loc → ['/', 'fr', 'book', 'manage']
 *   '/' | loc              → ['/', 'fr']
 *
 * Use directly with [routerLink]:
 *
 *   <a [routerLink]="'/projects' | loc">Projects</a>
 *
 * Marked `pure: false` so it re-evaluates whenever the active language changes.
 * Cheap to run (string split + array spread) so the perf cost is negligible.
 */
@Pipe({ name: 'loc', standalone: true, pure: false })
export class LocalizedLinkPipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(path: string | string[]): unknown[] {
    const lang = this.i18n.lang();
    const segments = Array.isArray(path)
      ? path
      : path.split('/').filter((s) => s.length > 0);
    return ['/', lang, ...segments];
  }
}
