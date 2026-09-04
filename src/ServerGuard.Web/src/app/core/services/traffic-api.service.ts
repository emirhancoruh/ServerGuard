import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import {
  TopClientIp,
  TrafficTimelinePoint,
  toTimelinePoint,
  toTopClientIp
} from '../models/traffic';

export interface TrafficRangeQuery {
  serverName?: string;
  minutes?: number;
}

@Injectable({ providedIn: 'root' })
export class TrafficApiService {
  private readonly http = inject(HttpClient);

  getTimeline(query: TrafficRangeQuery & { bucketSeconds?: number }): Observable<TrafficTimelinePoint[]> {
    return this.http
      .get<unknown[]>(`${environment.apiBaseUrl}${ApiRoutes.trafficTimeline}`, {
        params: TrafficApiService.toParams(query)
      })
      .pipe(map((items) => TrafficApiService.sanitize(items, toTimelinePoint)));
  }

  getTopClientIps(query: TrafficRangeQuery & { take?: number }): Observable<TopClientIp[]> {
    return this.http
      .get<unknown[]>(`${environment.apiBaseUrl}${ApiRoutes.topClientIps}`, {
        params: TrafficApiService.toParams(query)
      })
      .pipe(map((items) => TrafficApiService.sanitize(items, toTopClientIp)));
  }

  private static toParams(query: object): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return params;
  }

  /** Bozuk kayıtlar elenir; sağlam olanlar gösterilmeye devam eder. */
  private static sanitize<T>(items: unknown[], convert: (raw: unknown) => T | null): T[] {
    return (items ?? [])
      .map(convert)
      .filter((item): item is T => item !== null);
  }
}
