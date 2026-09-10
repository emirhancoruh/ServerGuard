import { ServerHealthStatus } from './server-summary';

/** ServerGuard.Shared/Dtos/MonitoringOverviewDto.cs karşılığı. */
export interface MonitoringOverview {
  from: string;
  to: string;
  totalServers: number;
  onlineServers: number;
  offlineServers: number;
  worstServerStatus: ServerHealthStatus;
  totalRequestCount: number;
  clientErrorCount: number;
  serverErrorCount: number;
  errorRatePercent: number;
  requestsPerMinute: number;
  averageResponseTimeMs: number;
  maxResponseTimeMs: number;
  activeAlertCount: number;
  highSeverityAlertCount: number;
}

/** ServerGuard.Shared/Dtos/ServiceHealthDto.cs karşılığı. */
export interface ServiceHealth {
  serviceName: string;
  requestCount: number;
  clientErrorCount: number;
  serverErrorCount: number;
  errorRatePercent: number;
  averageResponseTimeMs: number;
  maxResponseTimeMs: number;
}

export function toMonitoringOverview(raw: unknown): MonitoringOverview | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const c = raw as Record<string, unknown>;

  if (typeof c['totalRequestCount'] !== 'number') {
    return null;
  }

  const worst = c['worstServerStatus'];

  return {
    from: stringOr(c['from'], ''),
    to: stringOr(c['to'], ''),
    totalServers: numberOr(c['totalServers'], 0),
    onlineServers: numberOr(c['onlineServers'], 0),
    offlineServers: numberOr(c['offlineServers'], 0),
    worstServerStatus: worst === 'Online' || worst === 'Stale' ? worst : 'Offline',
    totalRequestCount: c['totalRequestCount'],
    clientErrorCount: numberOr(c['clientErrorCount'], 0),
    serverErrorCount: numberOr(c['serverErrorCount'], 0),
    errorRatePercent: numberOr(c['errorRatePercent'], 0),
    requestsPerMinute: numberOr(c['requestsPerMinute'], 0),
    averageResponseTimeMs: numberOr(c['averageResponseTimeMs'], 0),
    maxResponseTimeMs: numberOr(c['maxResponseTimeMs'], 0),
    activeAlertCount: numberOr(c['activeAlertCount'], 0),
    highSeverityAlertCount: numberOr(c['highSeverityAlertCount'], 0)
  };
}

export function toServiceHealth(raw: unknown): ServiceHealth | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const c = raw as Record<string, unknown>;

  if (typeof c['serviceName'] !== 'string' || typeof c['requestCount'] !== 'number') {
    return null;
  }

  return {
    serviceName: c['serviceName'],
    requestCount: c['requestCount'],
    clientErrorCount: numberOr(c['clientErrorCount'], 0),
    serverErrorCount: numberOr(c['serverErrorCount'], 0),
    errorRatePercent: numberOr(c['errorRatePercent'], 0),
    averageResponseTimeMs: numberOr(c['averageResponseTimeMs'], 0),
    maxResponseTimeMs: numberOr(c['maxResponseTimeMs'], 0)
  };
}

function numberOr(value: unknown, fallback: number): number {
  return typeof value === 'number' ? value : fallback;
}

function stringOr(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}
