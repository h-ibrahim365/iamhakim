import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocalizedLinkPipe } from '../../i18n/localized-link.pipe';

interface ProjectLink { labelKey: string; href: string; }
interface ProjectCard {
  index: string;
  titleKey: string;
  tagKey: string;
  contextKey: string;
  roleKey: string;
  descriptionKey: string;
  pointKeys: string[];
  stack: string[];
  links: ProjectLink[];
}

@Component({
  selector: 'app-projects',
  imports: [TranslatePipe, RouterLink, LocalizedLinkPipe],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss'
})
export class ProjectsComponent {
  protected readonly projects: ProjectCard[] = [
    {
      index: '01',
      titleKey: 'projects.01.title',
      tagKey: 'projects.01.tag',
      contextKey: 'projects.01.context',
      roleKey: 'projects.01.role',
      descriptionKey: 'projects.01.description',
      pointKeys: ['projects.01.point.1', 'projects.01.point.2', 'projects.01.point.3', 'projects.01.point.4'],
      stack: ['C#', 'ASP.NET Core', 'Angular', 'Entra ID', 'Azure DevOps'],
      links: [{ labelKey: 'projects.01.link.internal', href: '#' }]
    },
    {
      index: '02',
      titleKey: 'projects.02.title',
      tagKey: 'projects.02.tag',
      contextKey: 'projects.02.context',
      roleKey: 'projects.02.role',
      descriptionKey: 'projects.02.description',
      pointKeys: ['projects.02.point.1', 'projects.02.point.2', 'projects.02.point.3', 'projects.02.point.4'],
      stack: ['Java', 'JMH', 'Graph algorithms', 'A* / Dijkstra'],
      links: [{ labelKey: 'projects.02.link.run', href: '/' }]
    },
    {
      index: '03',
      titleKey: 'projects.03.title',
      tagKey: 'projects.03.tag',
      contextKey: 'projects.03.context',
      roleKey: 'projects.03.role',
      descriptionKey: 'projects.03.description',
      pointKeys: ['projects.03.point.1', 'projects.03.point.2', 'projects.03.point.3', 'projects.03.point.4'],
      stack: ['Angular 21', 'ASP.NET Core', 'SignalR', 'MySQL'],
      links: [{ labelKey: 'projects.03.link.github', href: 'https://github.com/h-ibrahim365/iamhakim' }, { labelKey: 'projects.03.link.status', href: '/status' }]
    }
  ];
}
