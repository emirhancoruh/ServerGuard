import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DxChartModule } from 'devextreme-angular';

import { TrafficQueryDefaults } from '../core/api-routes';
import { LoadState } from '../core/models/load-state';
import { TrafficLog, TrafficTimelinePoint } from '../core/models/traffic';
import { MonitoringHubService } from '../core/services/monitoring-hub.service';
import { ServerSelectionService } from '../core/services/server-selection.service';
import { TrafficApiService } from '../core/services/traffic-api.service';

const MILLISECONDS_PER_SECOND = 1000;

@Component({
  selector: 'sg-traffic-chart',
  imports: [DxChartModule],
  templateUrl: './traffic-chart.html',
  styleUrl: './traffic-chart.scss'
})
export class TrafficChart {
  private readonly trafficApi = inject(TrafficApiService);
  private readonly hub = inject(MonitoringHubService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly bucketMs = TrafficQueryDefaults.bucketSeconds * MILLISECONDS_PER_SECOND;

  private readonly pointsSignal = signal<TrafficTimelinePoint[]>([]);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly points = this.pointsSignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly windowMinutes = TrafficQueryDefaults.minutes;

  constructor() {
    // Sunucu seçimi değiştiğinde seri baştan yüklenir.
    effect(() => this.load(this.serverSelection.selected()));

    // Canlı gelen her istek, ait olduğu dilimin sayacını artırır.
    this.hub.trafficLogs$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((log) => this.applyLiveLog(log));
  }

  reload(): void {
    this.load(this.serverSelection.selected());
  }

  private load(serverName: string | null): void {
    this.stateSignal.set('loading');

    this.trafficApi
      .getTimeline({
        serverName: serverName ?? undefined,
        minutes: TrafficQueryDefaults.minutes,
        bucketSeconds: TrafficQueryDefaults.bucketSeconds
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (points) => {
          this.pointsSignal.set(points);
          this.stateSignal.set('ready');
        },
        error: () => this.stateSignal.set('error')
      });
  }

  /**
   * Yeni kaydı ait olduğu dilime ekler. Dilim mevcut serinin sonundan yeniyse seri kaydırılır,
   * böylece grafik her zaman son <c>windowMinutes</c> dakikayı gösterir ve sınırsız büyümez.
   */
  private applyLiveLog(log: TrafficLog): void {
    if (!this.serverSelection.matches(log.serverName)) {
      return;
    }

    const bucketStart = this.bucketStartOf(new Date(log.timestamp));

    this.pointsSignal.update((current) => {
      if (current.length === 0) {
        return current;
      }

      const index = current.findIndex((point) => point.timestamp.getTime() === bucketStart);

      if (index >= 0) {
        const updated = [...current];
        updated[index] = { ...updated[index], requestCount: updated[index].requestCount + 1 };
        return updated;
      }

      const lastBucket = current[current.length - 1].timestamp.getTime();

      // Serinin gerisinde kalan (çok eski) bir kayıt grafiği değiştirmez.
      if (bucketStart <= lastBucket) {
        return current;
      }

      return this.appendBuckets(current, lastBucket, bucketStart);
    });
  }

  /** Aradaki boş dilimleri sıfırla doldurarak yeni dilimi ekler ve pencereyi kaydırır. */
  private appendBuckets(
    current: TrafficTimelinePoint[],
    lastBucket: number,
    newBucket: number
  ): TrafficTimelinePoint[] {
    const extended = [...current];

    for (let bucket = lastBucket + this.bucketMs; bucket < newBucket; bucket += this.bucketMs) {
      extended.push({ timestamp: new Date(bucket), requestCount: 0 });
    }

    extended.push({ timestamp: new Date(newBucket), requestCount: 1 });

    return extended.slice(-current.length);
  }

  private bucketStartOf(moment: Date): number {
    return Math.floor(moment.getTime() / this.bucketMs) * this.bucketMs;
  }
}
