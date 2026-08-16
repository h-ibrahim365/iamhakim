import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocalizedLinkPipe } from '../../i18n/localized-link.pipe';
import { BookCtaComponent } from '../../shared/book-cta/book-cta.component';

interface ProjectLink { labelKey: string; href: string; }
interface ProjectCard {
  /** Anchor id, also the deep-link target from the Services page's proof links. */
  id: string;
  index: string;
  titleKey: string;
  tagKey: string;
  contextKey: string;
  roleKey: string;
  descriptionKey: string;
  pointKeys: string[];
  stack: string[];
  links: ProjectLink[];
  /** Non-interactive status shown instead of links, for internal projects with no public demo. */
  statusKey?: string;
  /** Related service on /services, shown as a subtle "Related service → X" line. */
  serviceLabelKey?: string;
  serviceFragment?: string;
  /**
   * A separate, explicitly-labelled link to a *related* public demo - distinct
   * from `links`/`statusKey` so it never merges "internal, no demo" and
   * "here's a demo" into one confusing CTA (see project 02: the real router
   * is internal, but the A* visualiser on this site demonstrates the concept).
   */
  demoLabelKey?: string;
  demoKey?: string;
  demoHref?: string;
}

@Component({
  selector: 'app-projects',
  imports: [TranslatePipe, RouterLink, LocalizedLinkPipe, BookCtaComponent],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss'
})
export class ProjectsComponent {
  protected readonly projects: ProjectCard[] = [
    {
      id: 'access-tool',
      index: '01',
      titleKey: 'projects.01.title',
      tagKey: 'projects.01.tag',
      contextKey: 'projects.01.context',
      roleKey: 'projects.01.role',
      descriptionKey: 'projects.01.description',
      pointKeys: ['projects.01.point.1', 'projects.01.point.2', 'projects.01.point.3', 'projects.01.point.4'],
      stack: ['C#', 'ASP.NET Core', 'Angular', 'Entra ID', 'Azure DevOps'],
      links: [],
      statusKey: 'projects.status.internal',
      serviceLabelKey: 'projects.service.01',
      serviceFragment: 'identity-access'
    },
    {
      id: 'railway-routing',
      index: '02',
      titleKey: 'projects.02.title',
      tagKey: 'projects.02.tag',
      contextKey: 'projects.02.context',
      roleKey: 'projects.02.role',
      descriptionKey: 'projects.02.description',
      pointKeys: ['projects.02.point.1', 'projects.02.point.2', 'projects.02.point.3', 'projects.02.point.4'],
      stack: ['Java', 'JMH', 'Graph algorithms', 'A* / Dijkstra'],
      links: [],
      statusKey: 'projects.status.internal',
      serviceLabelKey: 'projects.service.02',
      serviceFragment: 'codebase-improvement',
      demoLabelKey: 'projects.demo.label',
      demoKey: 'projects.demo.link',
      demoHref: '/'
    },
    {
      id: 'this-site',
      index: '03',
      titleKey: 'projects.03.title',
      tagKey: 'projects.03.tag',
      contextKey: 'projects.03.context',
      roleKey: 'projects.03.role',
      descriptionKey: 'projects.03.description',
      pointKeys: ['projects.03.point.1', 'projects.03.point.2', 'projects.03.point.3', 'projects.03.point.4'],
      stack: ['Angular 21', 'ASP.NET Core', 'SignalR', 'MySQL'],
      links: [{ labelKey: 'projects.03.link.github', href: 'https://github.com/h-ibrahim365/iamhakim' }, { labelKey: 'projects.03.link.status', href: '/status' }],
      serviceLabelKey: 'projects.service.03',
      serviceFragment: 'application-development'
    }
  ];
}
