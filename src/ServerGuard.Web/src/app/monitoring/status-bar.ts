import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { TrafficQueryDefaults } from '../core/api-routes';
import { LoadState } from '../core/models/load-state';
import { MonitoringOverview } from '../core/models/monitoring';
import { MonitoringApiService } from '../core/services/monitoring-api.service';
import { ServerSelectionService } from '../core/services/server-selection.service';

/** Bu oranın üzerindeki 5xx yüzdesi kritik sayılır (backend'deki eşikle aynı). */
const ERROR_RATE_CRITICAL_PERCENT = 10;
const ERROR_RATE_WARNING_PERCENT = 1;

/** Özet, canlı akıştan bağımsız olarak bu aralıkla tazelenir. */
const REFRESH_INTERVAL_MS = 30_000;

type OverallState = 'healthy' | 'degraded' | 'critical';

@Component({
  selector: 'sg-status-bar',
  imports: [DecimalPipe],
  templateUrl: './status-bar.html',
  styleUrl: './status-bar.scss'
})
export class StatusBar {
  private readonly monitoringApi = inject(MonitoringApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly overviewSignal = signal<MonitoringOverview | null>(null);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly overview = this.overviewSignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly windowMinutes = TrafficQueryDefaults.minutes;

  /**
   * Sistemin genel durumu. En kötü sinyal kazanır: bir sunucu erişilemezse veya hata oranı
   * kritik eşiği aşarsa, geri kalan her şey iyi olsa da durum kritiktir.
   */
  readonly overallState = computed<OverallState>(() => {
    const overview = this.overviewSignal();

    if (overview === null) {
      return 'critical';
    }

    if (overview.offlineServers > 0 || overview.errorRatePercent >= ERROR_RATE_CRITICAL_PERCENT) {
      return 'critical';
    }

    const degraded =
      overview.worstServerStatus !== 'Online' ||
      overview.errorRatePercent >= ERROR_RATE_WARNING_PERCENT ||
      overview.highSeverityAlertCount > 0;

    return degraded ? 'degraded' : 'healthy';
  });

  /** Tek cümlelik özet; okumadan önce renkle, sonra metinle anlaşılır. */
  readonly headline = computed(() => {
    const overview = this.overviewSignal();

    if (overview === null) {
      return 'Durum bilgisi alınamadı';
    }

    const parts: string[] = [];

    parts.push(
      overview.offlineServers > 0
        ? `${overview.offlineServers} sunucu erişilemiyor`
        : `${overview.onlineServers}/${overview.totalServers} sunucu çevrimiçi`
    );

    if (overview.errorRatePercent >= ERROR_RATE_WARNING_PERCENT) {
      parts.push(`hata oranı %${overview.errorRatePercent.toFixed(1)}`);
    }

    if (overview.highSeverityAlertCount > 0) {
      parts.push(`${overview.highSeverityAlertCount} yüksek öncelikli alarm`);
    }

    return parts.length === 1 && this.overallState() === 'healthy'
      ? `${parts[0]} · her şey yolunda`
      : parts.join(' · ');
  });

  constructor() {
    effect(() => this.load(this.serverSelection.selected()));

    // Sunucunun sustuğunu fark etmek için özet periyodik tazelenir; bunu canlı akış söyleyemez,
    // çünkü veri gelmemesi de bir sinyaldir.
    const timer = setInterval(() => this.reload(), REFRESH_INTERVAL_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  reload(): void {
    this.load(this.serverSelection.selected());
  }

  errorRateClass(): string {
    const overview = this.overviewSignal();

    if (overview === null) {
      return '';
    }

    if (overview.errorRatePercent >= ERROR_RATE_CRITICAL_PERCENT) {
      return 'kpi--critical';
    }

    return overview.errorRatePercent >= ERROR_RATE_WARNING_PERCENT ? 'kpi--warning' : '';
  }

  private load(serverName: string | null): void {
    this.monitoringApi
      .getOverview({ serverName: serverName ?? undefined, minutes: TrafficQueryDefaults.minutes })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (overview) => {
          this.overviewSignal.set(overview);
          this.stateSignal.set('ready');
        },
        error: () => this.stateSignal.set('error')
      });
  }
}
