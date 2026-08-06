import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocaleService } from '../../core/i18n/locale.service';
import { ToastService } from '../../shared/ui/toast/toast.service';

@Component({
  selector: 'drs-sign-in-page',
  imports: [ReactiveFormsModule],
  template: `
    <section class="sign-in" aria-labelledby="sign-in-title">
      <p>{{ locale.locale() === 'ar' ? 'مرحبًا بعودتك' : 'Welcome back' }}</p>
      <h1 id="sign-in-title">{{ locale.copy().signIn }}</h1>
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label for="email">{{
          locale.locale() === 'ar' ? 'البريد الإلكتروني' : 'Email address'
        }}</label>
        <input id="email" type="email" formControlName="email" autocomplete="email" />
        @if (form.controls.email.touched && form.controls.email.invalid) {
          <p class="field-error">
            {{ locale.locale() === 'ar' ? 'أدخل بريدًا صحيحًا.' : 'Enter a valid email.' }}
          </p>
        }
        <label for="password">{{ locale.locale() === 'ar' ? 'كلمة المرور' : 'Password' }}</label>
        <input
          id="password"
          type="password"
          formControlName="password"
          autocomplete="current-password"
        />
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <p class="field-error">
            {{ locale.locale() === 'ar' ? 'كلمة المرور مطلوبة.' : 'Password is required.' }}
          </p>
        }
        <button type="submit">{{ locale.copy().signIn }}</button>
      </form>
      <small>{{
        locale.locale() === 'ar'
          ? 'سيتم ربط الهوية الآمنة في Phase 5.'
          : 'Secure identity is connected in Phase 5.'
      }}</small>
    </section>
  `,
  styles: `
    .sign-in {
      inline-size: min(100%, 28rem);
    }
    .sign-in > p {
      color: var(--color-brand);
    }
    .sign-in h1 {
      margin-block: var(--space-2) var(--space-7);
      font-size: clamp(2.2rem, 5vw, 3.5rem);
    }
    .sign-in form {
      display: grid;
      gap: var(--space-3);
    }
    .sign-in label {
      margin-block-start: var(--space-3);
      font-weight: 650;
    }
    .sign-in input {
      min-block-size: 52px;
      padding-inline: var(--space-3);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
    }
    .sign-in button {
      min-block-size: 52px;
      margin-block-start: var(--space-4);
      color: var(--color-on-brand);
      background: var(--color-brand);
      border: 0;
      border-radius: var(--radius-2);
    }
    .field-error {
      margin: 0;
      color: var(--color-danger);
    }
    .sign-in small {
      display: block;
      margin-block-start: var(--space-5);
      color: var(--color-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly toasts = inject(ToastService);
  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.valid) this.toasts.announce('Identity endpoints arrive in Phase 5.');
  }
}
