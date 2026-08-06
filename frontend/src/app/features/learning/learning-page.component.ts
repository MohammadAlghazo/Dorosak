import { ChangeDetectionStrategy, Component } from '@angular/core';
@Component({
  selector: 'drs-learning-page',
  template: `<section>
    <p>LEARNING / FOCUS MODE</p>
    <h1>Lesson workspace foundation</h1>
    <div class="stage">
      <span>Protected media remains disabled offline.</span
      ><strong>Player arrives with Learning and Media phases.</strong>
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
export class LearningPageComponent {}
