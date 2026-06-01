import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';

interface Skill { groupKey: string; items: string[]; }
interface Milestone { yearKey: string; titleKey: string; detailKey: string; }

@Component({
  selector: 'app-about',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss'
})
export class AboutComponent {
  protected readonly skills: Skill[] = [
    { groupKey: 'about.skill.backend', items: ['C#', 'ASP.NET Core', 'Java', 'Spring Boot'] },
    { groupKey: 'about.skill.frontend', items: ['Angular', 'TypeScript', 'RxJS', 'Signals'] },
    { groupKey: 'about.skill.identity', items: ['Active Directory', 'LDAP', 'Entra ID', 'IAM / IGA'] },
    { groupKey: 'about.skill.foundations', items: ['Graph algorithms', 'A* / Dijkstra', 'SQL', 'Refactoring'] }
  ];

  protected readonly milestones: Milestone[] = [
    { yearKey: 'about.milestone.joined.year', titleKey: 'about.milestone.joined.title', detailKey: 'about.milestone.joined.detail' },
    { yearKey: 'about.milestone.master.year', titleKey: 'about.milestone.master.title', detailKey: 'about.milestone.master.detail' },
    { yearKey: 'about.milestone.internship.year', titleKey: 'about.milestone.internship.title', detailKey: 'about.milestone.internship.detail' },
    { yearKey: 'about.milestone.next.year', titleKey: 'about.milestone.next.title', detailKey: 'about.milestone.next.detail' }
  ];
}
