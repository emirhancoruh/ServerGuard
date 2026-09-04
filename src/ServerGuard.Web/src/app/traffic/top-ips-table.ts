import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { TrafficQueryDefaults } from '../core/api-routes';
import { LoadState } from '../core/models/load-state';
import { TopClientIp } from '../core/models/traffic';
import { ServerSelectionService } from '../core/services/server-selection.service';
import { TrafficApiService } from '../core/services/traffic-api.service';

@Component({
  selector: 'sg-top-ips-table',
  templateUrl: './top-ips-table.html',
  styleUrl: './top-ips-table.scss'
})
export class TopIpsTable {
  private readonly trafficApi = inject(TrafficApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly rowsSignal = signal<TopClientIp[]>([]);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly rows = this.rowsSignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly windowMinutes = TrafficQueryDefaults.minutes;
  readonly topCount = TrafficQueryDefaults.topIpCount;

  /** Çubukların oranlanacağı en yüksek değer; en az 1 olur ki sıfıra bölme olmasın. */
  private readonly maxCount = computed(() =>
    Math.max(1, ...this.rowsSignal().map((row) => row.requestCount))
  );

  constructor() {
    // Sunucu seçimi değiştiğinde liste yeniden çekilir.
    effect(() => this.load(this.serverSelection.selected()));
  }

  reload(): void {
    this.load(this.serverSelection.selected());
  }

  /** Satır içi çubuğun genişliği (yüzde). */
  sharePercent(row: TopClientIp): number {
    return (row.requestCount / this.maxCount()) * 100;
  }

  private load(serverName: string | null): void {
    this.stateSignal.set('loading');

    this.trafficApi
      .getTopClientIps({
        serverName: serverName ?? undefined,
        minutes: TrafficQueryDefaults.minutes,
        take: TrafficQueryDefaults.topIpCount
      })
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
