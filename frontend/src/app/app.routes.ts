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
  { path: '', component: HomeComponent, title: 'I am Hakim' },
  { path: 'flow', component: FlowComponent, title: 'I am Hakim · Backend Flow' },
  { path: 'status', component: StatusComponent, title: 'I am Hakim · Live Status' },
  { path: 'projects', component: ProjectsComponent, title: 'I am Hakim · Projects' },
  { path: 'about', component: AboutComponent, title: 'I am Hakim · About' },
  { path: 'book', component: BookComponent, title: 'I am Hakim · Book a slot' },
  { path: 'book/manage', component: BookManageComponent, title: 'I am Hakim · Manage booking' },
  { path: 'privacy', component: PrivacyComponent, title: 'I am Hakim · Privacy' },
  { path: '**', redirectTo: '' }
];
