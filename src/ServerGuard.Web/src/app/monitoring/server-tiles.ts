import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LoadState } from '../core/models/load-state';
import { ServerHealthStatus, ServerSummary } from '../core/models/server-summary';
import { ServerApiService } from '../core/services/server-api.service';
import { ServerSelectionService } from '../core/services/server-selection.service';

/** Kullanım bu yüzdelerin üzerindeyse çubuk uyarı/kritik rengine geçer. */
const USAGE_WARNING_PERCENT = 70;
const USAGE_CRITICAL_PERCENT = 90;

/** Boş disk bu yüzdenin altına inerse kritik sayılır. */
const DISK_FREE_CRITICAL_PERCENT = 10;
const DISK_FREE_WARNING_PERCENT = 20;

/**
 * Sunucu durumu periyodik olarak tazelenir. Canlı akış bunu söyleyemez:
 * bir sunucunun çökmesi, "veri gelmemesi" ile anlaşılır.
 */
const REFRESH_INTERVAL_MS = 15_000;

const SECONDS_PER_MINUTE = 60;
const SECONDS_PER_HOUR = 3600;

@Component({
  selector: 'sg-server-tiles',
  imports: [DecimalPipe],
  templateUrl: './server-tiles.html',
  styleUrl: './server-tiles.scss'
})
export class ServerTiles {
  private readonly serverApi = inject(ServerApiService);
  private readonly serverSelection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly allServersSignal = signal<ServerSummary[]>([]);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly state = this.stateSignal.asReadonly();

  /** Seçili sunucu varsa yalnızca o gösterilir. */
  readonly servers = computed(() => {
    const selected = this.serverSelection.selected();

    return this.allServersSignal()
      .filter((server) => selected === null || server.serverName === selected)
      .sort((left, right) => left.serverName.localeCompare(right.serverName));
  });

  constructor() {
    this.load();

    const timer = setInterval(() => this.load(), REFRESH_INTERVAL_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    // Seçim değişince filtre computed üzerinden uygulanır; yeniden çekmeye gerek yok.
    effect(() => this.serverSelection.selected());
  }

  load(): void {
    this.serverApi
      .getKnownServers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (servers) => {
          this.allServersSignal.set(servers);
          this.stateSignal.set('ready');
        },
        error: () => this.stateSignal.set('error')
      });
  }

  statusLabel(status: ServerHealthStatus): string {
    switch (status) {
      case 'Online':
        return 'Çevrimiçi';
      case 'Stale':
        return 'Veri gecikti';
      default:
        return 'Erişilemiyor';
    }
  }

  /** "4 dk önce" biçiminde okunabilir süre. */
  lastSeenLabel(seconds: number): string {
    if (seconds < SECONDS_PER_MINUTE) {
      return `${seconds} sn önce`;
    }

    if (seconds < SECONDS_PER_HOUR) {
      return `${Math.floor(seconds / SECONDS_PER_MINUTE)} dk önce`;
    }

    return `${Math.floor(seconds / SECONDS_PER_HOUR)} sa önce`;
  }

  usageClass(percent: number | null): string {
    if (percent === null) {
      return '';
    }

    if (percent >= USAGE_CRITICAL_PERCENT) {
      return 'bar--critical';
    }

    return percent >= USAGE_WARNING_PERCENT ? 'bar--warning' : '';
  }

  /** Diskte az boş alan kötüdür; eşikler diğer metriklerin tersidir. */
  diskClass(freePercent: number | null): string {
    if (freePercent === null) {
      return '';
    }

    if (freePercent <= DISK_FREE_CRITICAL_PERCENT) {
      return 'bar--critical';
    }

    return freePercent <= DISK_FREE_WARNING_PERCENT ? 'bar--warning' : '';
  }

  diskUsedPercent(freePercent: number | null): number {
    return freePercent === null ? 0 : 100 - freePercent;
  }
}
