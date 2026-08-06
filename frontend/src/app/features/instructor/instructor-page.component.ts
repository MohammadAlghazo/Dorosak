import { ChangeDetectionStrategy, Component } from '@angular/core';
@Component({
  selector: 'drs-instructor-page',
  template: `<section>
    <p>INSTRUCTOR / FOUNDATION</p>
    <h1>Course work will stay organized around review and release.</h1>
    <div class="lines">
      <span>Draft health</span><strong>Phase 6</strong><span>Review queue</span
      ><strong>Phase 6</strong><span>Media readiness</span><strong>Phase 7</strong>
    </div>
  </section>`,
  styles: `
    section {
      max-inline-size: 70rem;
      margin-inline: auto;
    }
    section > p {
      color: var(--color-brand);
    }
    h1 {
      max-inline-size: 18ch;
      font-size: clamp(2.3rem, 5vw, 4.5rem);
      line-height: 1.08;
    }
    .lines {
      display: grid;
      grid-template-columns: 1fr auto;
      margin-block-start: var(--space-8);
      border-block-start: 1px solid var(--color-border);
    }
    .lines > * {
      padding-block: var(--space-4);
      border-block-end: 1px solid var(--color-border);
    }
    .lines span {
      color: var(--color-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstructorPageComponent {}
