import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import { PagedResult } from '../models/paged-result';
import { SecurityAlert, toSecurityAlert } from '../models/security-alert';

export interface AlertQuery {
  serverName?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Geçmiş alarmları sayfalayarak okur. */
@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly http = inject(HttpClient);

  query(query: AlertQuery): Observable<PagedResult<SecurityAlert>> {
    return this.http
      .get<PagedResult<unknown>>(`${environment.apiBaseUrl}${ApiRoutes.alerts}`, {
        params: this.toParams(query)
      })
      .pipe(map((result) => this.sanitize(result)));
  }

  private toParams(query: AlertQuery): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return params;
  }

  /** Bozuk kayıtlar listeye alınmaz; sağlam olanlar gösterilmeye devam eder. */
  private sanitize(result: PagedResult<unknown>): PagedResult<SecurityAlert> {
    const items = (result.items ?? [])
      .map((item) => toSecurityAlert(item))
      .filter((alert): alert is SecurityAlert => alert !== null);

    return { ...result, items };
  }
}
