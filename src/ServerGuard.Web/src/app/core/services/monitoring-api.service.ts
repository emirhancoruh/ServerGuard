import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import {
  MonitoringOverview,
  ServiceHealth,
  toMonitoringOverview,
  toServiceHealth
} from '../models/monitoring';

export interface MonitoringRangeQuery {
  serverName?: string;
  minutes?: number;
}

@Injectable({ providedIn: 'root' })
export class MonitoringApiService {
  private readonly http = inject(HttpClient);

  getOverview(query: MonitoringRangeQuery): Observable<MonitoringOverview | null> {
    return this.http
      .get<unknown>(`${environment.apiBaseUrl}${ApiRoutes.overview}`, { params: toParams(query) })
      .pipe(map(toMonitoringOverview));
  }

  getServiceHealth(query: MonitoringRangeQuery): Observable<ServiceHealth[]> {
    return this.http
      .get<unknown[]>(`${environment.apiBaseUrl}${ApiRoutes.serviceHealth}`, { params: toParams(query) })
      .pipe(
        map((items) =>
          (items ?? [])
            .map(toServiceHealth)
            .filter((item): item is ServiceHealth => item !== null)
        )
      );
  }
}

function toParams(query: object): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
