import type { Routes } from '@angular/router';
import { localeGuard } from './core/i18n/locale.guard';
import { permissionGuard, sessionGuard } from './core/routing/session.guard';
import { CourseEditorStore } from './features/instructor/course-editor.store';

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
          {
            path: 'register',
            loadComponent: () =>
              import('./features/auth/register-page.component').then(
                (module) => module.RegisterPageComponent,
              ),
            data: {
              titleAr: 'إنشاء حساب',
              titleEn: 'Create an account',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
          {
            path: 'verify-email',
            loadComponent: () =>
              import('./features/auth/verify-email-page.component').then(
                (module) => module.VerifyEmailPageComponent,
              ),
            data: {
              titleAr: 'تأكيد البريد الإلكتروني',
              titleEn: 'Verify email',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
          {
            path: 'forgot-password',
            loadComponent: () =>
              import('./features/auth/forgot-password-page.component').then(
                (module) => module.ForgotPasswordPageComponent,
              ),
            data: {
              titleAr: 'نسيت كلمة المرور',
              titleEn: 'Forgot password',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
          {
            path: 'confirm-email-change',
            loadComponent: () =>
              import('./features/auth/confirm-email-change-page.component').then(
                (module) => module.ConfirmEmailChangePageComponent,
              ),
            data: {
              titleAr: 'تأكيد البريد الجديد',
              titleEn: 'Confirm new email',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
          {
            path: 'reset-password',
            loadComponent: () =>
              import('./features/auth/reset-password-page.component').then(
                (module) => module.ResetPasswordPageComponent,
              ),
            data: {
              titleAr: 'تعيين كلمة مرور جديدة',
              titleEn: 'Reset password',
              indexing: 'noindex',
              renderMode: 'server',
            },
          },
          {
            path: 'mfa',
            loadComponent: () =>
              import('./features/auth/mfa-page.component').then(
                (module) => module.MfaPageComponent,
              ),
            data: {
              titleAr: 'التحقق بخطوتين',
              titleEn: 'Two-step verification',
              indexing: 'noindex',
              renderMode: 'server',
              recovery: false,
            },
          },
          {
            path: 'mfa/recovery',
            loadComponent: () =>
              import('./features/auth/mfa-page.component').then(
                (module) => module.MfaPageComponent,
              ),
            data: {
              titleAr: 'رمز الاسترداد',
              titleEn: 'Recovery code',
              indexing: 'noindex',
              renderMode: 'server',
              recovery: true,
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
        path: 'settings',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'security' },
          {
            path: 'security',
            loadComponent: () =>
              import('./features/settings/security-page.component').then(
                (module) => module.SecurityPageComponent,
              ),
            data: {
              titleAr: 'الأمان',
              titleEn: 'Security',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
          {
            path: 'sessions',
            loadComponent: () =>
              import('./features/settings/sessions-page.component').then(
                (module) => module.SessionsPageComponent,
              ),
            data: {
              titleAr: 'الجلسات',
              titleEn: 'Sessions',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
          {
            path: 'teacher-application',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/settings/teacher-application-page.component').then(
                (module) => module.TeacherApplicationPageComponent,
              ),
            data: {
              titleAr: 'طلب التدريس',
              titleEn: 'Teacher application',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'TeacherApplication.CreateOwn',
            },
          },
          {
            path: 'subscription',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/settings/demo-subscription-page.component').then(
                (module) => module.DemoSubscriptionPageComponent,
              ),
            data: {
              titleAr: 'الاشتراك التجريبي',
              titleEn: 'Demo subscription',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Subscription.ManageOwn',
            },
          },
        ],
      },
      {
        path: 'certificates/verify/:verificationCode',
        loadComponent: () =>
          import('./features/credentials/certificate-page.component').then(
            (module) => module.CertificatePageComponent,
          ),
        data: {
          titleAr: 'التحقق من الشهادة',
          titleEn: 'Verify certificate',
          indexing: 'noindex',
          renderMode: 'server',
        },
      },
      {
        path: 'certificates',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/credentials/certificates-page.component').then(
                (module) => module.CertificatesPageComponent,
              ),
            data: {
              titleAr: 'شهاداتي',
              titleEn: 'My certificates',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Certificate.ReadOwn',
            },
          },
          {
            path: ':certificateId',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/credentials/certificate-page.component').then(
                (module) => module.CertificatePageComponent,
              ),
            data: {
              titleAr: 'الشهادة',
              titleEn: 'Certificate',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Certificate.ReadOwn',
            },
          },
        ],
      },
      {
        path: 'my-learning',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/learning/my-learning-page.component').then(
                (module) => module.MyLearningPageComponent,
              ),
            data: {
              titleAr: 'مساراتي',
              titleEn: 'My learning',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
        ],
      },
      {
        path: 'chat',
        canActivate: [sessionGuard],
        data: {
          titleAr: 'المحادثات',
          titleEn: 'Chat',
          indexing: 'noindex',
          renderMode: 'client',
        },
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/communications/chat-list-page.component').then(
                (module) => module.ChatListPageComponent,
              ),
            data: {
              permission: 'Conversation.ReadOwn',
              titleAr: 'المحادثات',
              titleEn: 'Chat',
              renderMode: 'client',
            },
          },
          {
            path: ':conversationId',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/communications/chat-thread-page.component').then(
                (module) => module.ChatThreadPageComponent,
              ),
            data: {
              permission: 'Conversation.ReadOwn',
              titleAr: 'محادثة',
              titleEn: 'Conversation',
              renderMode: 'client',
            },
          },
        ],
      },
      {
        path: 'notifications',
        canActivate: [sessionGuard],
        data: {
          titleAr: 'الإشعارات',
          titleEn: 'Notifications',
          indexing: 'noindex',
          renderMode: 'client',
        },
        loadComponent: () =>
          import('./shells/workspace-shell/workspace-shell.component').then(
            (module) => module.WorkspaceShellComponent,
          ),
        children: [
          {
            path: '',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/communications/notifications-page.component').then(
                (module) => module.NotificationsPageComponent,
              ),
            data: {
              permission: 'Notification.ReadOwn',
              titleAr: 'الإشعارات',
              titleEn: 'Notifications',
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
            path: ':enrollmentId',
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
          {
            path: ':enrollmentId/lessons/:lessonId',
            loadComponent: () =>
              import('./features/learning/learning-page.component').then(
                (module) => module.LearningPageComponent,
              ),
            data: {
              titleAr: 'مساحة الدرس',
              titleEn: 'Lesson workspace',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
          {
            path: 'media/:assetId',
            loadComponent: () =>
              import('./features/media-upload/protected-media-page.component').then(
                (module) => module.ProtectedMediaPageComponent,
              ),
            data: {
              titleAr: 'وسائط الدرس',
              titleEn: 'Lesson media',
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
            path: '',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/instructor/instructor-page.component').then(
                (module) => module.InstructorPageComponent,
              ),
            data: {
              titleAr: 'مساحة المدرس',
              titleEn: 'Instructor workspace',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Course.ReadOwn',
            },
          },
          {
            path: 'create',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/instructor/course-create-page.component').then(
                (module) => module.CourseCreatePageComponent,
              ),
            data: {
              titleAr: 'إنشاء دورة',
              titleEn: 'Create course',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Course.Create',
            },
          },
          {
            path: ':courseId',
            canActivate: [permissionGuard],
            data: { permission: 'Course.ReadOwn' },
            providers: [CourseEditorStore],
            children: [
              {
                path: '',
                loadComponent: () =>
                  import('./features/instructor/course-detail-page.component').then(
                    (module) => module.CourseDetailPageComponent,
                  ),
                data: {
                  titleAr: 'بيانات الدورة',
                  titleEn: 'Course metadata',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
              {
                path: 'curriculum',
                loadComponent: () =>
                  import('./features/instructor/curriculum-page.component').then(
                    (module) => module.CurriculumPageComponent,
                  ),
                data: {
                  titleAr: 'منهج الدورة',
                  titleEn: 'Course curriculum',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
              {
                path: 'media',
                loadComponent: () =>
                  import('./features/media-upload/media-upload-page.component').then(
                    (module) => module.MediaUploadPageComponent,
                  ),
                data: {
                  titleAr: 'وسائط الدورة',
                  titleEn: 'Course media',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
              {
                path: 'assessments',
                loadComponent: () =>
                  import('./features/instructor/assessments-page.component').then(
                    (module) => module.AssessmentsPageComponent,
                  ),
                data: {
                  titleAr: 'تقييمات الدورة',
                  titleEn: 'Course assessments',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
              {
                path: 'publication',
                loadComponent: () =>
                  import('./features/instructor/publication-page.component').then(
                    (module) => module.PublicationPageComponent,
                  ),
                data: {
                  titleAr: 'حالة النشر',
                  titleEn: 'Publication status',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
              {
                path: 'announcements',
                canActivate: [permissionGuard],
                loadComponent: () =>
                  import('./features/communications/announcements-page.component').then(
                    (module) => module.AnnouncementsPageComponent,
                  ),
                data: {
                  permission: 'Announcement.ManageCourse',
                  titleAr: 'إعلانات الدورة',
                  titleEn: 'Course announcements',
                  indexing: 'noindex',
                  renderMode: 'client',
                },
              },
            ],
          },
        ],
      },
      {
        path: 'admin',
        canActivate: [sessionGuard],
        loadComponent: () =>
          import('./shells/admin-shell/admin-shell.component').then(
            (module) => module.AdminShellComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/admin/admin-page.component').then(
                (module) => module.AdminPageComponent,
              ),
            data: {
              titleAr: 'الإدارة',
              titleEn: 'Administration',
              indexing: 'noindex',
              renderMode: 'client',
            },
          },
          {
            path: 'teacher-applications',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/admin/teacher-applications-page.component').then(
                (module) => module.TeacherApplicationsPageComponent,
              ),
            data: {
              titleAr: 'مراجعة طلبات المدرسين',
              titleEn: 'Teacher application review',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'TeacherApplication.ReviewAny',
            },
          },
          {
            path: 'publication-reviews',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/admin/publication-reviews-page.component').then(
                (module) => module.PublicationReviewsPageComponent,
              ),
            data: {
              titleAr: 'مراجعة النشر',
              titleEn: 'Publication review',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Course.ReviewAny',
            },
          },
          {
            path: 'moderation',
            canActivate: [permissionGuard],
            loadChildren: () =>
              import('./features/admin/moderation/moderation.routes').then(
                (module) => module.MODERATION_ROUTES,
              ),
            data: {
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Moderation.ReviewAny',
            },
          },
          {
            path: 'taxonomy',
            canActivate: [permissionGuard],
            loadComponent: () =>
              import('./features/admin/taxonomy-page.component').then(
                (module) => module.TaxonomyPageComponent,
              ),
            data: {
              titleAr: 'إدارة التصنيف',
              titleEn: 'Taxonomy management',
              indexing: 'noindex',
              renderMode: 'client',
              permission: 'Catalog.ManageTaxonomy',
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
      {
        path: '**',
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
    ],
  },
  {
    path: '**',
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
];
