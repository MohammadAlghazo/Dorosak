import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { ThemeService } from '../../core/theme/theme.service';
import { SessionStore } from '../../core/auth/session.store';
import { PublicPortfolioSettingsStore } from '../../features/cms/public-portfolio-settings.store';

@Component({
  selector: 'drs-public-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  providers: [PublicPortfolioSettingsStore],
  templateUrl: './public-shell.component.html',
  styleUrl: './public-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicShellComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly theme = inject(ThemeService);
  protected readonly session = inject(SessionStore);
  protected readonly portfolio = inject(PublicPortfolioSettingsStore);
}
