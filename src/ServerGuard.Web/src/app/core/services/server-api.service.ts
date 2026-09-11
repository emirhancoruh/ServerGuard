import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiRoutes } from '../api-routes';
import { ServerSummary, toServerSummary } from '../models/server-summary';
import { API_BASE_URL } from '../runtime-config';

@Injectable({ providedIn: 'root' })
export class ServerApiService {
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  getKnownServers(): Observable<ServerSummary[]> {
    return this.http
      .get<unknown[]>(`${this.apiBaseUrl}${ApiRoutes.servers}`)
      .pipe(
        map((items) =>
          (items ?? [])
            .map(toServerSummary)
            .filter((server): server is ServerSummary => server !== null)
        )
      );
  }
}
