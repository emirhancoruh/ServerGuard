import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiRoutes } from '../api-routes';
import { API_BASE_URL } from '../runtime-config';
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
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  getTimeline(query: TrafficRangeQuery & { bucketSeconds?: number }): Observable<TrafficTimelinePoint[]> {
    return this.http
      .get<unknown[]>(`${this.apiBaseUrl}${ApiRoutes.trafficTimeline}`, {
        params: TrafficApiService.toParams(query)
      })
      .pipe(map((items) => TrafficApiService.sanitize(items, toTimelinePoint)));
  }

  getTopClientIps(query: TrafficRangeQuery & { take?: number }): Observable<TopClientIp[]> {
    return this.http
      .get<unknown[]>(`${this.apiBaseUrl}${ApiRoutes.topClientIps}`, {
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
