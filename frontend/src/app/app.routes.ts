import { Routes } from '@angular/router';
import { AboutComponent } from './pages/about/about.component';
import { BookComponent } from './pages/book/book.component';
import { BookManageComponent } from './pages/book-manage/book-manage.component';
import { FlowComponent } from './pages/flow/flow.component';
import { HomeComponent } from './pages/home/home.component';
import { PrivacyComponent } from './pages/privacy/privacy.component';
import { ProjectsComponent } from './pages/projects/projects.component';
import { ServicesComponent } from './pages/services/services.component';
import { StatusComponent } from './pages/status/status.component';
import { langMatchGuard, rootLangRedirect } from './core/lang.guard';
import type { Lang } from './i18n/translations';

/** Per-route SEO copy, localized. The SeoService reads `data.seo[lang]` at navigation. */
export type RouteSeoMap = Record<Lang, { title: string; description: string }>;

const seo = {
  home: {
    en: {
      title: 'Hakim Id Brahim - Full-stack Developer, Charleroi & Brussels',
      description:
        'Personal portfolio - .NET/Angular developer building access-management tools, backend APIs, and graph-routing projects. Charleroi → Brussels.',
    },
    fr: {
      title: 'Hakim Id Brahim - Développeur full-stack à Bruxelles',
      description:
        'Portfolio personnel - développeur .NET/Angular spécialisé en gestion des accès, APIs back-end et algorithmes de routage de graphes. Charleroi → Bruxelles.',
    },
    nl: {
      title: 'Hakim Id Brahim - Full-stack Developer, toegangssystemen',
      description:
        'Persoonlijk portfolio - .NET/Angular-ontwikkelaar gespecialiseerd in toegangsbeheer, backend-APIs en graafroutering. Charleroi → Brussel.',
    },
  },
  projects: {
    en: {
      title: 'Projects - Hakim Id Brahim, .NET/Angular Developer',
      description:
        'Selected projects from Hakim Id Brahim: A* routing engine, internal access-management platform, RootShell Discord bot, and more.',
    },
    fr: {
      title: 'Projets - Hakim Id Brahim, développeur .NET/Angular',
      description:
        'Projets sélectionnés : moteur de routage A*, plateforme interne de gestion des accès, bot Discord RootShell, et plus encore.',
    },
    nl: {
      title: 'Projecten - Hakim Id Brahim, .NET/Angular-ontwikkelaar',
      description:
        'Geselecteerde projecten: A*-routeringsengine, intern toegangsbeheerplatform, RootShell Discord-bot, en meer.',
    },
  },
  services: {
    en: {
      title: 'Freelance .NET & Angular Services - Hakim Id Brahim',
      description:
        'Freelance .NET and Angular development, application refactoring and identity & access integration in Belgium.',
    },
    fr: {
      title: 'Services freelance .NET & Angular - Hakim Id Brahim',
      description:
        'Développement freelance .NET et Angular, refactoring d’applications et intégration identité & accès en Belgique.',
    },
    nl: {
      title: 'Freelance .NET & Angular diensten - Hakim Id Brahim',
      description:
        'Freelance .NET- en Angular-ontwikkeling, applicatie-refactoring en identity & access-integratie in België.',
    },
  },
  about: {
    en: {
      title: 'About Hakim Id Brahim - Full-stack .NET/Angular Developer',
      description:
        'About Hakim Id Brahim - .NET/Angular developer at Infrabel (Belgian railway), CS Master\u2019s student at UMONS Charleroi. IAM, graph algorithms, architecture.',
    },
    fr: {
      title: 'À propos de Hakim Id Brahim - développeur full-stack',
      description:
        'À propos de Hakim Id Brahim - développeur .NET/Angular chez Infrabel (chemins de fer belges), étudiant Master en Informatique à l\u2019UMONS Charleroi. IAM, algorithmes de graphes, architecture.',
    },
    nl: {
      title: 'Over Hakim Id Brahim - Full-stack .NET/Angular-ontwikkelaar',
      description:
        'Over Hakim Id Brahim - .NET/Angular-ontwikkelaar bij Infrabel (Belgische spoorwegen), masterstudent Informatica aan UMONS Charleroi. IAM, graafalgoritmes, architectuur.',
    },
  },
  flow: {
    en: {
      title: 'Backend Flow - How iamhakim.com Is Built, Hakim Id Brahim',
      description:
        'Visual walkthrough of the iamhakim.com architecture: Caddy reverse proxy, ASP.NET Core API, SignalR realtime hub, Angular SSR frontend.',
    },
    fr: {
      title: 'Architecture back-end - Comment iamhakim.com est construit',
      description:
        'Visite guidée de l\u2019architecture d\u2019iamhakim.com : reverse proxy Caddy, API ASP.NET Core, hub temps réel SignalR, front-end Angular SSR.',
    },
    nl: {
      title: 'Backend Flow - Hoe iamhakim.com technisch is gebouwd',
      description:
        'Visuele rondleiding door de architectuur van iamhakim.com: Caddy reverse proxy, ASP.NET Core API, SignalR realtime hub, Angular SSR frontend.',
    },
  },
  status: {
    en: {
      title: 'Live Status - Real-time Uptime & Traffic for iamhakim.com',
      description:
        'Real-time uptime, latency and traffic stats for iamhakim.com. Streamed over SignalR from the ASP.NET Core backend.',
    },
    fr: {
      title: 'Statut en direct - Disponibilité de iamhakim.com',
      description:
        'Disponibilité, latence et trafic en temps réel pour iamhakim.com. Diffusion via SignalR depuis le back-end ASP.NET Core.',
    },
    nl: {
      title: 'Live Status - Realtime uptime voor iamhakim.com',
      description:
        'Realtime uptime, latentie en verkeer voor iamhakim.com. Gestreamd via SignalR vanaf de ASP.NET Core backend.',
    },
  },
  book: {
    en: {
      title: 'Book a Slot - Schedule a Call with Hakim Id Brahim',
      description:
        'Schedule a 30-minute call with Hakim Id Brahim to discuss .NET, Angular, IAM, graph algorithms, or career advice.',
    },
    fr: {
      title: 'Réserver un créneau - Appel avec Hakim Id Brahim',
      description:
        'Planifiez un appel de 30 minutes avec Hakim Id Brahim pour parler .NET, Angular, IAM, algorithmes de graphes ou conseils de carrière.',
    },
    nl: {
      title: 'Boek een afspraak - Gesprek met Hakim Id Brahim',
      description:
        'Plan een gesprek van 30 minuten met Hakim Id Brahim om .NET, Angular, IAM, graafalgoritmes of carrièreadvies te bespreken.',
    },
  },
  bookManage: {
    en: {
      title: 'Manage Your Booking Request - Hakim Id Brahim',
      description: 'Edit or cancel your booking with Hakim Id Brahim.',
    },
    fr: {
      title: 'Gérer votre réservation - Hakim Id Brahim',
      description: 'Modifier ou annuler votre réservation avec Hakim Id Brahim.',
    },
    nl: {
      title: 'Boeking beheren of annuleren - Hakim Id Brahim',
      description: 'Bewerk of annuleer uw boeking met Hakim Id Brahim.',
    },
  },
  privacy: {
    en: {
      title: 'Privacy Policy - iamhakim.com, Hakim Id Brahim',
      description: 'Privacy policy for iamhakim.com - what we track, how long we keep it, and why.',
    },
    fr: {
      title: 'Politique de confidentialité - iamhakim.com',
      description: 'Politique de confidentialité d\u2019iamhakim.com - ce qui est suivi, combien de temps, et pourquoi.',
    },
    nl: {
      title: 'Privacybeleid - iamhakim.com, Hakim Id Brahim',
      description: 'Privacybeleid voor iamhakim.com - wat we bijhouden, hoe lang we het bewaren, en waarom.',
    },
  },
} satisfies Record<string, RouteSeoMap>;

const localizedRoutes: Routes = [
  { path: '', component: HomeComponent, data: { seo: seo.home, path: '' } },
  {
    path: 'projects',
    component: ProjectsComponent,
    data: {
      seo: seo.projects,
      path: 'projects',
      extraJsonLd: [
        {
          '@context': 'https://schema.org',
          '@type': 'ItemList',
          name: 'Projects by Hakim Id Brahim',
          itemListElement: [
            {
              '@type': 'ListItem',
              position: 1,
              item: {
                // CreativeWork, not SoftwareApplication: these are portfolio
                // pieces, not rated/purchasable app-store listings, so the
                // stricter SoftwareApplication type (which Google's rich
                // results require aggregateRating/offers for) doesn't fit -
                // and fabricating ratings/pricing to satisfy it would be
                // dishonest markup.
                '@type': 'CreativeWork',
                name: 'Internal access-management platform',
                description:
                  'Governed access-management platform built around directory services and cloud identity, supporting controlled access workflows in a professional infrastructure environment.',
                keywords: 'Business application, Web',
                author: { '@id': 'https://iamhakim.com/#person' },
              },
            },
            {
              '@type': 'ListItem',
              position: 2,
              item: {
                '@type': 'CreativeWork',
                name: 'Clean A* router',
                description:
                  'Railway routing engine refactored around a clean A* implementation with reverse-Dijkstra heuristic. Java / Spring Boot. ×2.6 to ×7.1 speedup vs legacy on five benchmark scenarios.',
                keywords: 'Java, Spring Boot, Linux',
                author: { '@id': 'https://iamhakim.com/#person' },
              },
            },
            {
              '@type': 'ListItem',
              position: 3,
              item: {
                '@type': 'CreativeWork',
                name: 'iamhakim.com',
                description:
                  'Personal portfolio website. Angular 21 frontend, ASP.NET Core API, SignalR realtime hub, MySQL, Caddy reverse proxy, deployed on Hetzner Cloud behind Cloudflare.',
                keywords: 'Angular, ASP.NET Core, Web',
                url: 'https://iamhakim.com',
                author: { '@id': 'https://iamhakim.com/#person' },
              },
            },
          ],
        },
      ],
    },
  },
  { path: 'services', component: ServicesComponent, data: { seo: seo.services, path: 'services' } },
  { path: 'about', component: AboutComponent, data: { seo: seo.about, path: 'about' } },
  { path: 'flow', component: FlowComponent, data: { seo: seo.flow, path: 'flow' } },
  { path: 'status', component: StatusComponent, data: { seo: seo.status, path: 'status' } },
  { path: 'book', component: BookComponent, data: { seo: seo.book, path: 'book' } },
  { path: 'book/manage', component: BookManageComponent, data: { seo: seo.bookManage, path: 'book/manage' } },
  { path: 'privacy', component: PrivacyComponent, data: { seo: seo.privacy, path: 'privacy' } },
];

export const routes: Routes = [
  // Root → redirect to preferred language
  { path: '', pathMatch: 'full', redirectTo: rootLangRedirect },

  // Localized routes: only match when first segment is en|fr|nl
  {
    path: ':lang',
    canMatch: [langMatchGuard],
    children: localizedRoutes,
  },

  // Backward compatibility: legacy un-prefixed URLs
  { path: 'projects', pathMatch: 'full', redirectTo: '/en/projects' },
  { path: 'services', pathMatch: 'full', redirectTo: '/en/services' },
  { path: 'about', pathMatch: 'full', redirectTo: '/en/about' },
  { path: 'flow', pathMatch: 'full', redirectTo: '/en/flow' },
  { path: 'status', pathMatch: 'full', redirectTo: '/en/status' },
  { path: 'book', pathMatch: 'full', redirectTo: '/en/book' },
  { path: 'book/manage', pathMatch: 'full', redirectTo: '/en/book/manage' },
  { path: 'privacy', pathMatch: 'full', redirectTo: '/en/privacy' },

  // Anything else
  { path: '**', redirectTo: '/en' },
];
