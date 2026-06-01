import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Title, Meta } from '@angular/platform-browser';
import { Router, ActivatedRoute, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs/operators';

export interface RouteSeo {
  /** SEO title (used in <title>, og:title, twitter:title). */
  title?: string;
  /** Meta description (used in <meta description>, og:description, twitter:description). */
  description?: string;
}

const ORIGIN = 'https://iamhakim.com';
const DEFAULT_TITLE = 'Hakim - Full-stack Developer';
const DEFAULT_DESCRIPTION =
  'Personal portfolio - .NET/Angular developer building access-management tools, backend APIs, and graph-routing projects. Charleroi → Brussels.';

/**
 * Updates per-route SEO tags on every navigation:
 *  - <title>
 *  - <meta name="description">
 *  - <link rel="canonical">
 *  - Open Graph: og:title, og:description, og:url
 *  - Twitter: twitter:title, twitter:description
 *
 * Per-route SEO values are read from the `data` field of each Route:
 *   { path: 'projects', component: ProjectsComponent, data: { title: '...', description: '...' } }
 *
 * Falls back to global defaults if a route doesn't define them.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);

  start(): void {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        map(() => {
          let route = this.activatedRoute;
          while (route.firstChild) route = route.firstChild;
          return {
            url: this.router.url,
            data: (route.snapshot.data ?? {}) as RouteSeo,
          };
        }),
      )
      .subscribe(({ url, data }) => this.apply(url, data));
  }

  private apply(url: string, data: RouteSeo): void {
    const title = data.title ?? DEFAULT_TITLE;
    const description = data.description ?? DEFAULT_DESCRIPTION;
    const fullUrl = `${ORIGIN}${url.split('?')[0]}`;

    this.title.setTitle(title);
    this.meta.updateTag({ name: 'description', content: description });

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: fullUrl });

    // Twitter
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });

    // Canonical link (Meta service doesn't manage <link> tags, do it manually)
    if (isPlatformBrowser(this.platformId)) {
      const head = this.document.head;
      let link = head.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
      if (!link) {
        link = this.document.createElement('link');
        link.setAttribute('rel', 'canonical');
        head.appendChild(link);
      }
      link.setAttribute('href', fullUrl);
    }
  }
}
