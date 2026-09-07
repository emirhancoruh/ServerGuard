import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes } from '../api-routes';
import { ReportSummary, toReportSummary } from '../models/report-summary';

export interface ReportSummaryQuery {
  serverName?: string;
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  private readonly http = inject(HttpClient);

  getSummary(query: ReportSummaryQuery): Observable<ReportSummary | null> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http
      .get<unknown>(`${environment.apiBaseUrl}${ApiRoutes.reportSummary}`, { params })
      .pipe(map(toReportSummary));
  }
}
