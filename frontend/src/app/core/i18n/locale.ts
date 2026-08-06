export const supportedLocales = ['ar', 'en'] as const;

export type Locale = (typeof supportedLocales)[number];

export const isLocale = (value: string | undefined): value is Locale =>
  supportedLocales.some((locale) => locale === value);
