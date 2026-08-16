import { Component } from '@angular/core';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { BookCtaComponent } from '../../shared/book-cta/book-cta.component';

interface Skill { groupKey: string; items: string[]; }
interface Milestone { yearKey: string; titleKey: string; detailKey: string; }

@Component({
  selector: 'app-about',
  imports: [TranslatePipe, BookCtaComponent],
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss'
})
export class AboutComponent {
  protected readonly skills: Skill[] = [
    { groupKey: 'about.skill.backend', items: ['C#', 'ASP.NET Core', 'Java', 'Spring Boot', 'REST APIs'] },
    { groupKey: 'about.skill.frontend', items: ['Angular', 'TypeScript', 'JavaScript', 'RxJS', 'Signals'] },
    { groupKey: 'about.skill.architecture', items: ['Layered / N-tier', 'Onion', 'Hexagonal / Ports & Adapters', 'CQRS', 'Vertical Slices', 'Dependency Injection', 'Dependency Inversion'] },
    { groupKey: 'about.skill.data', items: ['SQL', 'MySQL', 'Entity Framework Core'] },
    { groupKey: 'about.skill.identity', items: ['Active Directory', 'LDAP', 'Microsoft Entra ID', 'IAM / IGA'] },
    { groupKey: 'about.skill.algorithms', items: ['A*', 'Dijkstra', 'Graph algorithms', 'Railway routing'] },
    { groupKey: 'about.skill.engineering', items: ['Git', 'Testing', 'Refactoring', 'Benchmarking'] },
    { groupKey: 'about.skill.academic', items: ['C'] }
  ];

  protected readonly milestones: Milestone[] = [
    { yearKey: 'about.milestone.joined.year', titleKey: 'about.milestone.joined.title', detailKey: 'about.milestone.joined.detail' },
    { yearKey: 'about.milestone.master.year', titleKey: 'about.milestone.master.title', detailKey: 'about.milestone.master.detail' },
    { yearKey: 'about.milestone.internship.year', titleKey: 'about.milestone.internship.title', detailKey: 'about.milestone.internship.detail' },
    { yearKey: 'about.milestone.next.year', titleKey: 'about.milestone.next.title', detailKey: 'about.milestone.next.detail' }
  ];
}
