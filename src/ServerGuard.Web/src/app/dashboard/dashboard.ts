import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { DxCircularGaugeModule } from 'devextreme-angular';

import { AlertList } from '../alerts/alert-list';
import { ServerSelector } from '../shared/server-selector';
import { TopIpsTable } from '../traffic/top-ips-table';
import { TrafficChart } from '../traffic/traffic-chart';
import { MonitoringHubService } from '../core/services/monitoring-hub.service';
import { ServerSelectionService } from '../core/services/server-selection.service';
import { ServerMetric } from '../core/models/server-metric';
import { ServerStatus, toServerStatus } from '../core/models/server-status';

/** Gauge eşikleri: bu yüzdelerin üstü uyarı ve kritik bölge sayılır. */
const WARNING_THRESHOLD_PERCENT = 70;
const CRITICAL_THRESHOLD_PERCENT = 90;
const MIN_PERCENT = 0;
const MAX_PERCENT = 100;

@Component({
  selector: 'sg-dashboard',
  imports: [
    DxCircularGaugeModule,
    DatePipe,
    DecimalPipe,
    AlertList,
    TrafficChart,
    TopIpsTable,
    ServerSelector
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly hub = inject(MonitoringHubService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  /** Sunucu adına göre en güncel durum. Aynı sunucudan yeni veri geldikçe üzerine yazılır. */
  private readonly statusByServer = signal(new Map<string, ServerStatus>());

  readonly connectionState = this.hub.connectionState;

  /** Yalnızca seçili sunucu gösterilir; seçim yoksa tümü. */
  readonly servers = computed(() => {
    const selected = this.serverSelection.selected();

    return [...this.statusByServer().values()]
      .filter((status) => selected === null || status.serverName === selected)
      .sort((left, right) => left.serverName.localeCompare(right.serverName));
  });

  readonly minPercent = MIN_PERCENT;
  readonly maxPercent = MAX_PERCENT;
  readonly warningThreshold = WARNING_THRESHOLD_PERCENT;
  readonly criticalThreshold = CRITICAL_THRESHOLD_PERCENT;

  ngOnInit(): void {
    this.hub.metrics$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((metric) => this.applyMetric(metric));

    void this.hub.start();
  }

  connectionLabel(): string {
    switch (this.connectionState()) {
      case 'connected':
        return 'Canlı';
      case 'connecting':
        return 'Bağlanıyor';
      case 'reconnecting':
        return 'Yeniden bağlanıyor';
      default:
        return 'Bağlantı yok';
    }
  }

  private applyMetric(metric: ServerMetric): void {
    const status = toServerStatus(metric);

    this.statusByServer.update((current) => {
      const next = new Map(current);
      next.set(status.serverName, status);
      return next;
    });
  }
}
