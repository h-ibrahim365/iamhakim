import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocalizedLinkPipe } from '../../i18n/localized-link.pipe';

/**
 * One consistent end-of-page booking prompt, reused across the pages that
 * lead toward /book (home, projects, services, flow, status, about).
 * The question changes per page (`titleKey` / `accentKey`); the eyebrow and
 * button label stay identical everywhere so the CTA reads as one convention
 * rather than a different banner on every page.
 */
@Component({
  selector: 'app-book-cta',
  imports: [RouterLink, TranslatePipe, LocalizedLinkPipe],
  templateUrl: './book-cta.component.html',
  styleUrl: './book-cta.component.scss'
})
export class BookCtaComponent {
  readonly titleKey = input.required<string>();
  readonly accentKey = input.required<string>();
  /** Optional explanatory line under the question - most pages don't need one. */
  readonly bodyKey = input<string | undefined>(undefined);
}
