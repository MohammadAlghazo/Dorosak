import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'drs-public-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './public-shell.component.html',
  styleUrl: './public-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicShellComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly theme = inject(ThemeService);
}
