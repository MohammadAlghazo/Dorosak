import { Validators, type AbstractControl, type ValidationErrors, type ValidatorFn } from '@angular/forms';
import { ApiProblem } from '../../core/api/api-problem';
import type { Locale } from '../../core/i18n/locale';

const errorCopy: Readonly<Record<string, Readonly<Record<Locale, string>>>> = {
  'AUTH.INVALID_CREDENTIALS': {
    ar: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
    en: 'The email address or password is incorrect.',
  },
  'MFA.INVALID_CODE': {
    ar: 'رمز التحقق غير صحيح.',
    en: 'The verification code is incorrect.',
  },
  'MFA.INVALID_CHALLENGE': {
    ar: 'انتهت صلاحية محاولة التحقق. سجل الدخول مرة أخرى.',
    en: 'The verification attempt expired. Sign in again.',
  },
  'AUTH.EMAIL_VERIFICATION_INVALID': {
    ar: 'الرابط غير صالح أو انتهت صلاحيته.',
    en: 'This link is invalid or has expired.',
  },
  'AUTH.PASSWORD_RESET_INVALID': {
    ar: 'رابط إعادة تعيين كلمة المرور غير صالح أو انتهت صلاحيته.',
    en: 'The password reset link is invalid or has expired.',
  },
  'RATE_LIMIT.EXCEEDED': {
    ar: 'تم تجاوز عدد المحاولات. حاول لاحقًا.',
    en: 'Too many attempts. Try again later.',
  },
  'SECURITY.RATE_LIMIT_UNAVAILABLE': {
    ar: 'خدمة الأمان غير متاحة مؤقتًا. حاول لاحقًا.',
    en: 'The security service is temporarily unavailable. Try again later.',
  },
};

export const matchingFields = (first: string, second: string): ValidatorFn =>
  (control: AbstractControl): ValidationErrors | null =>
    control.get(first)?.value === control.get(second)?.value ? null : { fieldsMismatch: true };

export const requiredValidator: ValidatorFn = (control) => Validators.required(control);

export const emailValidator: ValidatorFn = (control) => Validators.email(control);

export const authErrorMessage = (error: unknown, locale: Locale): string => {
  if (error instanceof ApiProblem) {
    const translated = errorCopy[error.code]?.[locale];
    if (translated) return translated;
  }
  return locale === 'ar'
    ? 'تعذر إكمال الطلب. تحقق من البيانات وحاول مرة أخرى.'
    : 'The request could not be completed. Check the details and try again.';
};
