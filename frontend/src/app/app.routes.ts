import { Routes } from '@angular/router';
import { AboutComponent } from './pages/about/about.component';
import { BookComponent } from './pages/book/book.component';
import { BookManageComponent } from './pages/book-manage/book-manage.component';
import { FlowComponent } from './pages/flow/flow.component';
import { HomeComponent } from './pages/home/home.component';
import { PrivacyComponent } from './pages/privacy/privacy.component';
import { ProjectsComponent } from './pages/projects/projects.component';
import { StatusComponent } from './pages/status/status.component';

export const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    data: {
      title: 'Hakim - Full-stack Developer',
      description:
        'Personal portfolio - .NET/Angular developer building access-management tools, backend APIs, and graph-routing projects. Charleroi → Brussels.',
    },
  },
  {
    path: 'flow',
    component: FlowComponent,
    data: {
      title: 'Backend Flow - Hakim',
      description:
        'Visual walkthrough of the iamhakim.com architecture: Caddy reverse proxy, ASP.NET Core API, SignalR realtime hub, Angular SSR frontend.',
    },
  },
  {
    path: 'status',
    component: StatusComponent,
    data: {
      title: 'Live Status - Hakim',
      description:
        'Real-time uptime, latency and traffic stats for iamhakim.com. Streamed over SignalR from the ASP.NET Core backend.',
    },
  },
  {
    path: 'projects',
    component: ProjectsComponent,
    data: {
      title: 'Projects - Hakim',
      description:
        'Selected projects from Hakim: A* routing engine, ADAMO governed access-management system, RootShell Discord bot, and more.',
    },
  },
  {
    path: 'about',
    component: AboutComponent,
    data: {
      title: 'About - Hakim',
      description:
        'About Hakim - .NET/Angular developer at Infrabel (Belgian railway), CS Master\u2019s student at UMONS Charleroi. Background in IAM, graph algorithms, and architecture.',
    },
  },
  {
    path: 'book',
    component: BookComponent,
    data: {
      title: 'Book a slot - Hakim',
      description:
        'Schedule a 30-minute call with Hakim to discuss .NET, Angular, IAM, graph algorithms, or career advice.',
    },
  },
  {
    path: 'book/manage',
    component: BookManageComponent,
    data: {
      title: 'Manage booking - Hakim',
      description: 'Edit or cancel your booking with Hakim.',
    },
  },
  {
    path: 'privacy',
    component: PrivacyComponent,
    data: {
      title: 'Privacy - Hakim',
      description: 'Privacy policy for iamhakim.com - what we track, how long we keep it, and why.',
    },
  },
  { path: '**', redirectTo: '' },
];
