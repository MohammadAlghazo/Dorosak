import type { Routes } from '@angular/router';

export const MODERATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./moderation-queue-page.component').then(
        (module) => module.ModerationQueuePageComponent,
      ),
    data: {
      titleAr: 'طابور الإشراف',
      titleEn: 'Moderation queue',
      indexing: 'noindex',
      renderMode: 'client',
    },
  },
  {
    path: ':caseId',
    loadComponent: () =>
      import('./moderation-case-page.component').then(
        (module) => module.ModerationCasePageComponent,
      ),
    data: {
      titleAr: 'تفاصيل قضية الإشراف',
      titleEn: 'Moderation case details',
      indexing: 'noindex',
      renderMode: 'client',
    },
  },
];
