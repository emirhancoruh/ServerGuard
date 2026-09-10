/** ServerGuard.Shared/Dtos/TrafficLogDto.cs karşılığı. */
export interface TrafficLog {
  serverName: string;
  clientIp: string;
  requestPath: string;
  statusCode: number;
  responseTimeMs: number;
  timestamp: string;
}

/** ServerGuard.Shared/Dtos/TrafficTimelinePointDto.cs karşılığı. */
export interface TrafficTimelinePoint {
  /** Dilimin başlangıcı. Grafik ekseni için Date'e çevrilir. */
  timestamp: Date;
  requestCount: number;
  successCount: number;
  clientErrorCount: number;
  serverErrorCount: number;
}

/** ServerGuard.Shared/Dtos/TopClientIpDto.cs karşılığı. */
export interface TopClientIp {
  clientIp: string;
  requestCount: number;
}

/**
 * Hub'dan gelen ham trafik kaydını doğrular.
 * Bozuk/eksik kayıtta null döner; çağıran onu atlayıp çalışmaya devam eder.
 */
export function toTrafficLog(raw: unknown): TrafficLog | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Partial<TrafficLog>;

  const isValid =
    typeof candidate.serverName === 'string' &&
    typeof candidate.timestamp === 'string' &&
    !Number.isNaN(Date.parse(candidate.timestamp));

  if (!isValid) {
    return null;
  }

  return {
    serverName: candidate.serverName!,
    clientIp: candidate.clientIp ?? '-',
    requestPath: candidate.requestPath ?? '',
    statusCode: candidate.statusCode ?? 0,
    responseTimeMs: candidate.responseTimeMs ?? 0,
    timestamp: candidate.timestamp!
  };
}

/** API'den gelen zaman serisi noktasını doğrular ve Date'e çevirir. */
export function toTimelinePoint(raw: unknown): TrafficTimelinePoint | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as { timestamp?: unknown; requestCount?: unknown };

  if (typeof candidate.timestamp !== 'string' || Number.isNaN(Date.parse(candidate.timestamp))) {
    return null;
  }

  const count = (key: string): number => {
    const value = (raw as Record<string, unknown>)[key];
    return typeof value === 'number' ? value : 0;
  };

  return {
    timestamp: new Date(candidate.timestamp),
    requestCount: count('requestCount'),
    successCount: count('successCount'),
    clientErrorCount: count('clientErrorCount'),
    serverErrorCount: count('serverErrorCount')
  };
}

/** API'den gelen top IP satırını doğrular. */
export function toTopClientIp(raw: unknown): TopClientIp | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Partial<TopClientIp>;

  if (typeof candidate.clientIp !== 'string' || typeof candidate.requestCount !== 'number') {
    return null;
  }

  return { clientIp: candidate.clientIp, requestCount: candidate.requestCount };
}
