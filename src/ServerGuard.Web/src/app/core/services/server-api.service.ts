import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import { ServerSummary, toServerSummary } from '../models/server-summary';

@Injectable({ providedIn: 'root' })
export class ServerApiService {
  private readonly http = inject(HttpClient);

  getKnownServers(): Observable<ServerSummary[]> {
    return this.http
      .get<unknown[]>(`${environment.apiBaseUrl}${ApiRoutes.servers}`)
      .pipe(
        map((items) =>
          (items ?? [])
            .map(toServerSummary)
            .filter((server): server is ServerSummary => server !== null)
        )
      );
  }
}
