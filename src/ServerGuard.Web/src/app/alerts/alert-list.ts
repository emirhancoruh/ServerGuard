import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { AlertApiService } from '../core/services/alert-api.service';
import { MonitoringHubService } from '../core/services/monitoring-hub.service';
import { ServerSelectionService } from '../core/services/server-selection.service';
import { SecurityAlert } from '../core/models/security-alert';

/** Listede aynı anda tutulan en fazla alarm sayısı; bellek sınırsız büyümesin. */
const MAX_VISIBLE_ALERTS = 200;

/** İlk yüklemede çekilen sayfa boyutu. */
const INITIAL_PAGE_SIZE = 25;

@Component({
  selector: 'sg-alert-list',
  imports: [DatePipe],
  templateUrl: './alert-list.html',
  styleUrl: './alert-list.scss'
})
export class AlertList {
  private readonly hub = inject(MonitoringHubService);
  private readonly alertApi = inject(AlertApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly alertsSignal = signal<SecurityAlert[]>([]);
  private readonly loadFailedSignal = signal(false);

  /** En yeni alarm en üstte. */
  readonly alerts = this.alertsSignal.asReadonly();
  readonly loadFailed = this.loadFailedSignal.asReadonly();
  readonly totalCount = signal(0);

  readonly hasAlerts = computed(() => this.alertsSignal().length > 0);

  constructor() {
    // Sunucu seçimi değiştiğinde liste sıfırlanıp yeniden yüklenir.
    effect(() => this.load(this.serverSelection.selected()));

    this.hub.alerts$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((alert) => this.prepend(alert));
  }

  severityClass(severity: string): string {
    switch (severity) {
      case 'Critical':
        return 'severity--critical';
      case 'High':
        return 'severity--high';
      case 'Medium':
        return 'severity--medium';
      default:
        return 'severity--low';
    }
  }

  private load(serverName: string | null): void {
    this.alertsSignal.set([]);
    this.totalCount.set(0);

    this.alertApi
      .query({
        serverName: serverName ?? undefined,
        page: 1,
        pageSize: INITIAL_PAGE_SIZE
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.loadFailedSignal.set(false);
          this.totalCount.set(result.totalCount);
          this.alertsSignal.set(this.merge([], result.items));
        },
        error: () => {
          // Geçmiş yüklenemese bile canlı akış çalışmaya devam eder.
          this.loadFailedSignal.set(true);
        }
      });
  }

  private prepend(alert: SecurityAlert): void {
    if (!this.serverSelection.matches(alert.serverName)) {
      return;
    }

    this.totalCount.update((count) => count + 1);
    this.alertsSignal.update((current) => this.merge([alert], current));
  }

  /**
   * Listeleri birleştirir, Id'ye göre tekilleştirir ve en yeniden eskiye sıralar.
   * Canlı yayın ile geçmiş sorgusu aynı alarmı getirdiğinde çift satır oluşmaz.
   */
  private merge(first: SecurityAlert[], second: SecurityAlert[]): SecurityAlert[] {
    const byId = new Map<number, SecurityAlert>();

    for (const alert of [...first, ...second]) {
      byId.set(alert.id, alert);
    }

    return [...byId.values()]
      .sort((left, right) => Date.parse(right.timestamp) - Date.parse(left.timestamp) || right.id - left.id)
      .slice(0, MAX_VISIBLE_ALERTS);
  }
}
