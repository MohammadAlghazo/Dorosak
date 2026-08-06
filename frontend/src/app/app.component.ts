import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConnectivityStore } from './core/pwa/connectivity.store';
import { PwaUpdateService } from './core/pwa/pwa-update.service';
import { NavigationProgressService } from './core/routing/navigation-progress.service';
import { LocaleService } from './core/i18n/locale.service';
import { SeoService } from './core/i18n/seo.service';
import { ToastRegionComponent } from './shared/ui/toast/toast-region.component';

@Component({
  selector: 'drs-root',
  imports: [RouterOutlet, ToastRegionComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  protected readonly connectivity = inject(ConnectivityStore);
  protected readonly locale = inject(LocaleService);
  protected readonly navigation = inject(NavigationProgressService);
  protected readonly updates = inject(PwaUpdateService);
  private readonly seo = inject(SeoService);
}
