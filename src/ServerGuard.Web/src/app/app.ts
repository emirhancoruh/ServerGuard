import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/services/auth.service';
import { MonitoringHubService } from './core/services/monitoring-hub.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly hub = inject(MonitoringHubService);
  private readonly router = inject(Router);

  /** Menü yalnızca oturum açıkken gösterilir; giriş ekranında gezinme bağlantısı olmaz. */
  readonly isAuthenticated = this.auth.isAuthenticated;

  readonly userName = this.auth.userName;

  /**
   * Çıkışta canlı bağlantı da kapatılır. Aksi halde hub, geçersizleşmiş token'la
   * yeniden bağlanmayı denemeye devam ederdi.
   */
  async logout(): Promise<void> {
    await this.hub.stop();
    this.auth.logout();
    await this.router.navigate(['/login']);
  }
}
