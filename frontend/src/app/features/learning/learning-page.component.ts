import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LocaleService } from '../../core/i18n/locale.service';
@Component({
  selector: 'drs-learning-page',
  template: `<section>
    <p>{{ locale.locale() === 'ar' ? 'التعلم / وضع التركيز' : 'LEARNING / FOCUS MODE' }}</p>
    <h1>{{ locale.locale() === 'ar' ? 'مساحة الدرس' : 'Lesson workspace' }}</h1>
    <div class="stage">
      <span>{{
        locale.locale() === 'ar'
          ? 'لا تتوفر الوسائط المحمية دون اتصال.'
          : 'Protected media remains unavailable offline.'
      }}</span
      ><strong>{{
        locale.locale() === 'ar'
          ? 'اختر وسائط الدرس عند توفرها.'
          : 'Open lesson media when it becomes available.'
      }}</strong>
    </div>
  </section>`,
  styles: `
    section {
      max-inline-size: 80rem;
      margin-inline: auto;
    }
    h1 {
      font-size: clamp(2rem, 5vw, 4rem);
    }
    .stage {
      display: grid;
      place-items: center;
      min-block-size: 55dvh;
      margin-block-start: var(--space-7);
      padding: var(--space-7);
      border: 1px solid #334155;
      background: #0f1b2e;
      text-align: center;
    }
    .stage span {
      color: #94a3b8;
    }
    .stage strong {
      margin-block-start: var(--space-4);
      font-size: 1.3rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningPageComponent {
  protected readonly locale = inject(LocaleService);
}
