import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { TrafficQueryDefaults } from '../core/api-routes';
import { LoadState } from '../core/models/load-state';
import { ServiceHealth } from '../core/models/monitoring';
import { MonitoringApiService } from '../core/services/monitoring-api.service';
import { ServerSelectionService } from '../core/services/server-selection.service';

/** Backend'deki MonitoringConstraints ile aynı eşikler. */
const ERROR_RATE_CRITICAL_PERCENT = 10;
const ERROR_RATE_WARNING_PERCENT = 1;

/** Bu süreyi aşan ortalama yanıt yavaş sayılır. */
const SLOW_RESPONSE_MS = 1000;

const REFRESH_INTERVAL_MS = 30_000;

@Component({
  selector: 'sg-service-health-table',
  imports: [DecimalPipe],
  templateUrl: './service-health-table.html',
  styleUrl: './service-health-table.scss'
})
export class ServiceHealthTable {
  private readonly monitoringApi = inject(MonitoringApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly rowsSignal = signal<ServiceHealth[]>([]);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly rows = this.rowsSignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly windowMinutes = TrafficQueryDefaults.minutes;

  readonly hasRows = computed(() => this.rowsSignal().length > 0);

  /** Sorunlu servis sayısı; başlıkta gösterilir. */
  readonly unhealthyCount = computed(() =>
    this.rowsSignal().filter((row) => row.errorRatePercent >= ERROR_RATE_WARNING_PERCENT).length
  );

  constructor() {
    effect(() => this.load(this.serverSelection.selected()));

    const timer = setInterval(() => this.reload(), REFRESH_INTERVAL_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  reload(): void {
    this.load(this.serverSelection.selected());
  }

  errorRateClass(row: ServiceHealth): string {
    if (row.errorRatePercent >= ERROR_RATE_CRITICAL_PERCENT) {
      return 'cell--critical';
    }

    return row.errorRatePercent >= ERROR_RATE_WARNING_PERCENT ? 'cell--warning' : '';
  }

  rowClass(row: ServiceHealth): string {
    if (row.errorRatePercent >= ERROR_RATE_CRITICAL_PERCENT) {
      return 'row--critical';
    }

    return row.errorRatePercent >= ERROR_RATE_WARNING_PERCENT ? 'row--warning' : '';
  }

  latencyClass(averageMs: number): string {
    return averageMs >= SLOW_RESPONSE_MS ? 'cell--warning' : '';
  }

  private load(serverName: string | null): void {
    this.monitoringApi
      .getServiceHealth({ serverName: serverName ?? undefined, minutes: TrafficQueryDefaults.minutes })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => {
          this.rowsSignal.set(rows);
          this.stateSignal.set('ready');
        },
        error: () => this.stateSignal.set('error')
      });
  }
}
