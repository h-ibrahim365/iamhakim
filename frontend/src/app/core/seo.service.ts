import { Injectable, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Title, Meta } from '@angular/platform-browser';
import { Router, ActivatedRoute, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs/operators';
import { TranslationService } from '../i18n/translation.service';
import { LANGUAGES, type Lang } from '../i18n/translations';
import type { RouteSeoMap } from '../app.routes';

interface RouteData {
  seo?: RouteSeoMap;
  path?: string;
  /** Extra JSON-LD blocks injected on this route (e.g. ItemList of SoftwareApplications). */
  extraJsonLd?: object[];
}

const ORIGIN = 'https://iamhakim.com';
const DEFAULT_SEO = {
  en: {
    title: 'Hakim Id Brahim - Full-stack Developer, Charleroi & Brussels',
    description: 'Personal portfolio - .NET/Angular developer.',
  },
  fr: {
    title: 'Hakim Id Brahim - Développeur full-stack à Bruxelles',
    description: 'Portfolio personnel - développeur .NET/Angular.',
  },
  nl: {
    title: 'Hakim Id Brahim - Full-stack Developer, toegangssystemen',
    description: 'Persoonlijk portfolio - .NET/Angular-ontwikkelaar.',
  },
} satisfies RouteSeoMap;

/**
 * Updates SEO tags on every navigation:
 *  - <title>
 *  - <meta name="description">
 *  - <link rel="canonical">
 *  - <link rel="alternate" hreflang="..."> for en/fr/nl + x-default
 *  - Open Graph: og:title, og:description, og:url, og:locale
 *  - Twitter: twitter:title, twitter:description
 *  - <html lang="..."> attribute
 *  - JSON-LD BreadcrumbList (injected per page)
 *
 * Reads localized copy from `data.seo[lang]` on each Route.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);
  private readonly i18n = inject(TranslationService);

  start(): void {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        map(() => {
          let route = this.activatedRoute;
          while (route.firstChild) route = route.firstChild;
          return {
            url: this.router.url,
            data: (route.snapshot.data ?? {}) as RouteData,
          };
        }),
      )
      .subscribe(({ url, data }) => this.apply(url, data));
  }

  private apply(url: string, data: RouteData): void {
    const lang = this.i18n.lang();
    const seoMap = data.seo ?? DEFAULT_SEO;
    const { title, description } = seoMap[lang] ?? seoMap.en;

    // Path of the page in each language (e.g. 'projects' → /en/projects, /fr/projects)
    const pagePath = data.path ?? '';
    const fullUrl = `${ORIGIN}/${lang}${pagePath ? '/' + pagePath : ''}`;
    const ogLocaleMap: Record<Lang, string> = { en: 'en_US', fr: 'fr_BE', nl: 'nl_BE' };

    this.title.setTitle(title);
    this.meta.updateTag({ name: 'description', content: description });

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: fullUrl });
    this.meta.updateTag({ property: 'og:locale', content: ogLocaleMap[lang] });

    // Twitter
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });

    // All DOCUMENT-based mutations below also work in SSR (Angular injects a
    // server-side Document during prerender), so we do them unconditionally.
    // This is critical: it's what makes hreflang / canonical / JSON-LD
    // appear in the prerendered HTML for crawlers that don't run JavaScript.

    // <html lang> (also set by TranslationService for browser localStorage flow)
    this.document.documentElement.lang = lang;

    // Canonical: points to the current language version
    this.setLink('canonical', fullUrl);

    // hreflang alternates: one per supported language + x-default → English
    this.clearAlternates();
    for (const l of LANGUAGES) {
      const altUrl = `${ORIGIN}/${l.code}${pagePath ? '/' + pagePath : ''}`;
      this.addAlternate(l.code, altUrl);
    }
    this.addAlternate('x-default', `${ORIGIN}/en${pagePath ? '/' + pagePath : ''}`);

    // Breadcrumbs JSON-LD: skip on home
    this.setBreadcrumbsLd(pagePath, lang, fullUrl, title);

    // Extra per-route JSON-LD (SoftwareApplication etc.)
    this.setExtraJsonLd(data.extraJsonLd);
  }

  // ============== DOM helpers ==============

  private setLink(rel: string, href: string): void {
    const head = this.document.head;
    let link = head.querySelector(`link[rel="${rel}"]:not([hreflang])`) as HTMLLinkElement | null;
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', rel);
      head.appendChild(link);
    }
    link.setAttribute('href', href);
  }

  private clearAlternates(): void {
    this.document.head.querySelectorAll('link[rel="alternate"][hreflang]').forEach((el) => el.remove());
  }

  private addAlternate(hreflang: string, href: string): void {
    const link = this.document.createElement('link');
    link.setAttribute('rel', 'alternate');
    link.setAttribute('hreflang', hreflang);
    link.setAttribute('href', href);
    this.document.head.appendChild(link);
  }

  private setBreadcrumbsLd(pagePath: string, lang: Lang, fullUrl: string, title: string): void {
    const head = this.document.head;
    // Remove previous breadcrumbs script (if any)
    head.querySelectorAll('script[data-seo="breadcrumbs"]').forEach((el) => el.remove());
    if (!pagePath) return; // No breadcrumbs on home

    const homeLabel = { en: 'Home', fr: 'Accueil', nl: 'Home' }[lang];
    const ld = {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: [
        {
          '@type': 'ListItem',
          position: 1,
          name: homeLabel,
          item: `${ORIGIN}/${lang}`,
        },
        {
          '@type': 'ListItem',
          position: 2,
          name: title.split(' - ')[0], // "Projects" from "Projects - Hakim Id Brahim"
          item: fullUrl,
        },
      ],
    };

    const script = this.document.createElement('script');
    script.setAttribute('type', 'application/ld+json');
    script.setAttribute('data-seo', 'breadcrumbs');
    script.textContent = JSON.stringify(ld);
    head.appendChild(script);
  }

  private setExtraJsonLd(blocks: object[] | undefined): void {
    const head = this.document.head;
    // Remove any previously injected extra blocks
    head.querySelectorAll('script[data-seo="extra"]').forEach((el) => el.remove());
    if (!blocks || blocks.length === 0) return;
    for (const block of blocks) {
      const script = this.document.createElement('script');
      script.setAttribute('type', 'application/ld+json');
      script.setAttribute('data-seo', 'extra');
      script.textContent = JSON.stringify(block);
      head.appendChild(script);
    }
  }
}
