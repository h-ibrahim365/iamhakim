import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocalizedLinkPipe } from '../../i18n/localized-link.pipe';
import { BookCtaComponent } from '../../shared/book-cta/book-cta.component';

interface ServiceSection {
  /** Anchor id on this page. */
  id: string;
  index: string;
  titleKey: string;
  bodyKey: string;
  workKeys: string[];
  stackKey: string;
  /** Reuses the matching project's own title key so the name stays in sync with /projects. */
  proofTitleKey: string;
  proofBodyKey: string;
  /** Anchor id of the matching project card on /projects. */
  proofFragment: string;
  /** Shown for proof projects that are internal with no public demo (see project 02: the router itself is internal). */
  proofStatusKey?: string;
}

interface WorkStep { index: string; titleKey: string; bodyKey: string; }

@Component({
  selector: 'app-services',
  imports: [RouterLink, TranslatePipe, LocalizedLinkPipe, BookCtaComponent],
  templateUrl: './services.component.html',
  styleUrl: './services.component.scss'
})
export class ServicesComponent {
  protected readonly services: ServiceSection[] = [
    {
      id: 'application-development',
      index: '01',
      titleKey: 'services.01.title',
      bodyKey: 'services.01.body',
      workKeys: ['services.01.work.1', 'services.01.work.2', 'services.01.work.3', 'services.01.work.4', 'services.01.work.5', 'services.01.work.6'],
      stackKey: 'services.01.stack',
      proofTitleKey: 'projects.03.title',
      proofBodyKey: 'services.01.proof.body',
      proofFragment: 'this-site'
    },
    {
      id: 'codebase-improvement',
      index: '02',
      titleKey: 'services.02.title',
      bodyKey: 'services.02.body',
      workKeys: ['services.02.work.1', 'services.02.work.2', 'services.02.work.3', 'services.02.work.4', 'services.02.work.5', 'services.02.work.6'],
      stackKey: 'services.02.stack',
      proofTitleKey: 'projects.02.title',
      proofBodyKey: 'services.02.proof.body',
      proofFragment: 'railway-routing',
      proofStatusKey: 'projects.status.internal'
    },
    {
      id: 'identity-access',
      index: '03',
      titleKey: 'services.03.title',
      bodyKey: 'services.03.body',
      workKeys: ['services.03.work.1', 'services.03.work.2', 'services.03.work.3', 'services.03.work.4', 'services.03.work.5', 'services.03.work.6'],
      stackKey: 'services.03.stack',
      proofTitleKey: 'projects.01.title',
      proofBodyKey: 'services.03.proof.body',
      proofFragment: 'access-tool'
    }
  ];

  protected readonly workSteps: WorkStep[] = [
    { index: '01', titleKey: 'services.work.01.title', bodyKey: 'services.work.01.body' },
    { index: '02', titleKey: 'services.work.02.title', bodyKey: 'services.work.02.body' },
    { index: '03', titleKey: 'services.work.03.title', bodyKey: 'services.work.03.body' }
  ];

  protected readonly fitKeys: string[] = [
    'services.fit.1',
    'services.fit.2',
    'services.fit.3',
    'services.fit.4',
    'services.fit.5',
    'services.fit.6'
  ];
}
