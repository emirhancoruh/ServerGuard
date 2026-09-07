import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LoadState } from '../core/models/load-state';
import { ReportSummary } from '../core/models/report-summary';
import { ReportApiService } from '../core/services/report-api.service';
import { ServerSelectionService } from '../core/services/server-selection.service';
import { ServerSelector } from '../shared/server-selector';
import { downloadCsv } from './csv-export';

/** Backend'deki ReportQueryConstraints karşılığı. */
const MAX_RANGE_DAYS = 90;
const DEFAULT_RANGE_DAYS = 7;

const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

@Component({
  selector: 'sg-reports',
  imports: [DecimalPipe, ServerSelector],
  templateUrl: './reports.html',
  styleUrl: './reports.scss'
})
export class Reports implements OnInit {
  private readonly reportApi = inject(ReportApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly summarySignal = signal<ReportSummary | null>(null);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly summary = this.summarySignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly maxRangeDays = MAX_RANGE_DAYS;

  /** Tarih girişleri <input type="date"> ile yönetildiği için yyyy-MM-dd biçimindedir. */
  readonly fromDate = signal(toDateInput(daysAgo(DEFAULT_RANGE_DAYS)));
  readonly toDate = signal(toDateInput(new Date()));

  /** Aralık üst sınırı aşıyorsa istek gönderilmeden önce uyarılır. */
  readonly rangeError = computed(() => {
    const from = Date.parse(this.fromDate());
    const to = Date.parse(this.toDate());

    if (Number.isNaN(from) || Number.isNaN(to)) {
      return 'Geçerli bir tarih aralığı seçin.';
    }

    if (to < from) {
      return 'Bitiş tarihi başlangıçtan önce olamaz.';
    }

    if ((to - from) / MILLISECONDS_PER_DAY > MAX_RANGE_DAYS) {
      return `Tarih aralığı en fazla ${MAX_RANGE_DAYS} gün olabilir.`;
    }

    return null;
  });

  /** Tablo satırları: hem ekranda hem CSV'de aynı kaynaktan üretilir. */
  readonly rows = computed<readonly (readonly string[])[]>(() => {
    const summary = this.summarySignal();

    if (summary === null) {
      return [];
    }

    const rows: string[][] = [
      ['Sunucu', summary.serverName ?? 'Tüm sunucular'],
      ['Başlangıç', formatDateTime(summary.from)],
      ['Bitiş', formatDateTime(summary.to)],
      ['Toplam istek', String(summary.totalRequestCount)],
      ['Ortalama CPU (%)', formatAverage(summary.averageCpuUsagePercent)],
      ['Ortalama RAM (%)', formatAverage(summary.averageRamUsagePercent)],
      ['Metrik ölçüm sayısı', String(summary.metricSampleCount)],
      ['Toplam alarm', String(summary.totalAlertCount)]
    ];

    for (const item of summary.alertCountsByType) {
      rows.push([`Alarm · ${item.alertType}`, String(item.count)]);
    }

    return rows;
  });

  readonly hasData = computed(() => this.summarySignal() !== null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.rangeError() !== null) {
      return;
    }

    this.stateSignal.set('loading');

    this.reportApi
      .getSummary({
        serverName: this.serverSelection.selected() ?? undefined,
        from: toIsoStart(this.fromDate()),
        to: toIsoEnd(this.toDate())
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.summarySignal.set(summary);
          this.stateSignal.set('ready');
        },
        error: () => this.stateSignal.set('error')
      });
  }

  exportCsv(): void {
    const summary = this.summarySignal();

    if (summary === null) {
      return;
    }

    const header = ['Alan', 'Değer'];
    const scope = summary.serverName ?? 'tum-sunucular';
    const fileName = `serverguard-rapor-${scope}-${this.fromDate()}_${this.toDate()}.csv`;

    downloadCsv(fileName, [header, ...this.rows()]);
  }

  onFromChange(value: string): void {
    this.fromDate.set(value);
  }

  onToChange(value: string): void {
    this.toDate.set(value);
  }
}

function daysAgo(days: number): Date {
  return new Date(Date.now() - days * MILLISECONDS_PER_DAY);
}

function toDateInput(value: Date): string {
  return value.toISOString().slice(0, 10);
}

/** Seçilen günün başlangıcı (yerel gün, UTC'ye çevrilmiş). */
function toIsoStart(date: string): string {
  return new Date(`${date}T00:00:00`).toISOString();
}

/** Seçilen günün sonu; bitiş günü rapora dahil olsun diye. */
function toIsoEnd(date: string): string {
  return new Date(`${date}T23:59:59.999`).toISOString();
}

function formatDateTime(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString('tr-TR');
}

function formatAverage(value: number | null): string {
  return value === null ? 'Ölçüm yok' : value.toFixed(1);
}
