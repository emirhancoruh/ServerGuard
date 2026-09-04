import { Component, OnInit, inject, signal } from '@angular/core';
import { DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LoadState } from '../core/models/load-state';
import { ServerSummary } from '../core/models/server-summary';
import { ServerApiService } from '../core/services/server-api.service';
import { ServerSelectionService } from '../core/services/server-selection.service';

/** "Tümü" seçeneğinin option değeri; boş dize seçim yapılmadığını belirtir. */
const ALL_SERVERS_VALUE = '';

@Component({
  selector: 'sg-server-selector',
  templateUrl: './server-selector.html',
  styleUrl: './server-selector.scss'
})
export class ServerSelector implements OnInit {
  private readonly serverApi = inject(ServerApiService);
  private readonly selection = inject(ServerSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly serversSignal = signal<ServerSummary[]>([]);
  private readonly stateSignal = signal<LoadState>('loading');

  readonly servers = this.serversSignal.asReadonly();
  readonly state = this.stateSignal.asReadonly();
  readonly selected = this.selection.selected;
  readonly allServersValue = ALL_SERVERS_VALUE;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.stateSignal.set('loading');

    this.serverApi
      .getKnownServers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (servers) => {
          this.serversSignal.set(servers);
          this.stateSignal.set('ready');
          this.dropSelectionIfMissing(servers);
        },
        error: () => this.stateSignal.set('error')
      });
  }

  onSelectionChange(value: string): void {
    this.selection.select(value === ALL_SERVERS_VALUE ? null : value);
  }

  /** Seçili sunucu artık listede yoksa "tümü"ne dönülür; ekran boş kalmasın. */
  private dropSelectionIfMissing(servers: ServerSummary[]): void {
    const selected = this.selection.selected();

    if (selected !== null && !servers.some((server) => server.serverName === selected)) {
      this.selection.select(null);
    }
  }
}
