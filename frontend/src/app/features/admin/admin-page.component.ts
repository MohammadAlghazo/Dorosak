import { ChangeDetectionStrategy, Component } from '@angular/core';
@Component({
  selector: 'drs-admin-page',
  template: `<section>
    <header>
      <p>ADMIN / CONTROL PLANE</p>
      <h1>Operational overview</h1>
    </header>
    <table>
      <caption>
        Foundation capabilities
      </caption>
      <thead>
        <tr>
          <th scope="col">Area</th>
          <th scope="col">State</th>
          <th scope="col">Phase</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>Identity controls</td>
          <td>Planned</td>
          <td>05</td>
        </tr>
        <tr>
          <td>Catalog operations</td>
          <td>Planned</td>
          <td>06</td>
        </tr>
        <tr>
          <td>Audit workflow</td>
          <td>Planned</td>
          <td>11</td>
        </tr>
      </tbody>
    </table>
  </section>`,
  styles: `
    section {
      max-inline-size: 80rem;
      margin-inline: auto;
    }
    header p {
      color: var(--color-brand);
    }
    h1 {
      font-size: clamp(2rem, 4vw, 3.5rem);
    }
    table {
      inline-size: 100%;
      margin-block-start: var(--space-7);
      border-collapse: collapse;
      background: var(--color-surface);
    }
    caption {
      text-align: start;
      padding-block: var(--space-3);
      font-weight: 700;
    }
    th,
    td {
      padding: var(--space-4);
      border-block: 1px solid var(--color-border);
      text-align: start;
    }
    th {
      color: var(--color-muted);
      font-size: 0.8rem;
      text-transform: uppercase;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPageComponent {}
