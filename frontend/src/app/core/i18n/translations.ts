import type { Locale } from './locale';

export interface ApplicationCopy {
  brand: string;
  browseCourses: string;
  dashboard: string;
  signIn: string;
  switchLocale: string;
  switchTheme: string;
  skipToContent: string;
  offline: string;
}

export const applicationCopy: Record<Locale, ApplicationCopy> = {
  ar: {
    brand: 'دروسك',
    browseCourses: 'استكشف المسارات',
    dashboard: 'مساحتي',
    signIn: 'تسجيل الدخول',
    switchLocale: 'English',
    switchTheme: 'تغيير المظهر',
    skipToContent: 'انتقل إلى المحتوى',
    offline: 'أنت غير متصل. سنعرض ما هو متاح على الجهاز.',
  },
  en: {
    brand: 'Dorosak',
    browseCourses: 'Explore pathways',
    dashboard: 'My workspace',
    signIn: 'Sign in',
    switchLocale: 'العربية',
    switchTheme: 'Change theme',
    skipToContent: 'Skip to content',
    offline: 'You are offline. Available device content remains accessible.',
  },
};
