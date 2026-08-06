import type { Routes } from '@angular/router';
import { localeGuard } from './core/i18n/locale.guard';
import { permissionGuard, sessionGuard } from './core/routing/session.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'ar' },
  {
    path: ':locale',
    canMatch: [localeGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./shells/public-shell/public-shell.component').then(
            (module) => module.PublicShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/home/home-page.component').then(
                (module) => module.HomePageComponent,
              ),
            data: {
              titleAr: 'تعلم بوضوح',
              titleEn: 'Learn with clarity',
              indexing: 'index',
              renderMode: 'server',
              preload: true,
            },
          },
        ],
      },
      {
        path: 'courses',
        loadComponent: () =>
          import('./shells/public-shell/public-shell.component').then(
            (module) => module.PublicShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/catalog/catalog-page.component').then(
                (module) => module.CatalogPageComponent,
              ),
            data: {
              titleAr: 'المسارات التعليمية',
              titleEn: 'Learning pathways',
              indexing: 'index',
              renderMode: 'server',
            },
          },
          {
            path: ':slug',
            loadComponent: () =>
              import('./features/catalog/course-details-page.component').then(
                (module) => module.CourseDetailsPageComponent,
              ),
            data: {
              titleAr: 'تفاصيل المسار',
              titleEn: 'Pathway details',
              indexing: 'index',
              renderMode: 'server',
            },
          },
        ],
      },
      {
        path: 'search',
        loadComponent: () =>
          import('./shells/public-shell/public-shell.component').then(
            (module) => module.PublicShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/search/search-page.component').then(
                (module) => module.SearchPageComponent,
              ),
            data: {
              titleAr: 'البحث',
              titleEn: 'Search',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
        ],
      },
      {
        path: 'auth',
        loadComponent: () =>
          import('./shells/auth-shell/auth-shell.component').then(
            (module) => module.AuthShellComponent,
          ),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'sign-in' },
          {
            path: 'sign-in',
            loadComponent: () =>
              import('./features/auth/sign-in-page.component').then(
                (module) => module.SignInPageComponent,
              ),
            data: {
              titleAr: 'تسجيل الدخول',
              titleEn: 'Sign in',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
        ],
      },
      {
        path: 'dashboard',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/dashboard/dashboard-page.component').then(
                (module) => module.DashboardPageComponent,
              ),
            data: {
              titleAr: 'مساحتي',
              titleEn: 'My workspace',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
        ],
      },
      {
        path: 'learn',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/learning-shell/learning-shell.component').then(
            (module) => module.LearningShellComponent,
          ),
        children: [
          {
            path: '**',
            loadComponent: () =>
              import('./features/learning/learning-page.component').then(
                (module) => module.LearningPageComponent,
              ),
            data: {
              titleAr: 'مساحة التعلم',
              titleEn: 'Learning space',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
        ],
      },
      {
        path: 'instructor',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '**',
            loadComponent: () =>
              import('./features/instructor/instructor-page.component').then(
                (module) => module.InstructorPageComponent,
              ),
            data: {
              titleAr: 'مساحة المدرس',
              titleEn: 'Instructor workspace',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
        ],
      },
      {
        path: 'admin',
        canActivate: [sessionGuard, permissionGuard],
        data: { permission: 'Admin.Access' },
        loadComponent: () =>
          import('./shells/admin-shell/admin-shell.component').then(
            (module) => module.AdminShellComponent,
          ),
        children: [
          {
            path: '**',
            loadComponent: () =>
              import('./features/admin/admin-page.component').then(
                (module) => module.AdminPageComponent,
              ),
            data: {
              titleAr: 'الإدارة',
              titleEn: 'Administration',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Admin.Access',
            },
          },
        ],
      },
      {
        path: 'offline',
        loadComponent: () =>
          import('./features/system-pages/offline-page.component').then(
            (module) => module.OfflinePageComponent,
          ),
        data: {
          titleAr: 'غير متصل',
          titleEn: 'Offline',
          indexing: 'noindex',
          renderMode: 'server',
        },
      },
      {
        path: 'not-found',
        loadComponent: () =>
          import('./features/system-pages/not-found-page.component').then(
            (module) => module.NotFoundPageComponent,
          ),
        data: {
          titleAr: 'الصفحة غير موجودة',
          titleEn: 'Page not found',
          indexing: 'noindex',
          renderMode: 'server',
        },
      },
      { path: '**', redirectTo: 'not-found' },
    ],
  },
  { path: '**', redirectTo: 'ar/not-found' },
];
