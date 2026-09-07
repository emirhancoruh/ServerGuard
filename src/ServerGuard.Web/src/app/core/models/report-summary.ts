/** ServerGuard.Shared/Dtos/ReportSummaryDto.cs karşılığı. */
export interface AlertTypeCount {
  alertType: string;
  count: number;
}

export interface ReportSummary {
  serverName: string | null;
  from: string;
  to: string;
  totalRequestCount: number;
  /** Aralıkta hiç ölçüm yoksa null; "ölçüm yok" ile "ortalama sıfır" aynı şey değildir. */
  averageCpuUsagePercent: number | null;
  averageRamUsagePercent: number | null;
  metricSampleCount: number;
  totalAlertCount: number;
  alertCountsByType: AlertTypeCount[];
}

/** API'den gelen özeti doğrular; bozuksa null döner. */
export function toReportSummary(raw: unknown): ReportSummary | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Partial<ReportSummary>;

  if (typeof candidate.totalRequestCount !== 'number' || typeof candidate.from !== 'string') {
    return null;
  }

  return {
    serverName: typeof candidate.serverName === 'string' ? candidate.serverName : null,
    from: candidate.from,
    to: typeof candidate.to === 'string' ? candidate.to : candidate.from,
    totalRequestCount: candidate.totalRequestCount,
    averageCpuUsagePercent: numberOrNull(candidate.averageCpuUsagePercent),
    averageRamUsagePercent: numberOrNull(candidate.averageRamUsagePercent),
    metricSampleCount: typeof candidate.metricSampleCount === 'number' ? candidate.metricSampleCount : 0,
    totalAlertCount: typeof candidate.totalAlertCount === 'number' ? candidate.totalAlertCount : 0,
    alertCountsByType: Array.isArray(candidate.alertCountsByType)
      ? candidate.alertCountsByType
          .map(toAlertTypeCount)
          .filter((item): item is AlertTypeCount => item !== null)
      : []
  };
}

function toAlertTypeCount(raw: unknown): AlertTypeCount | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Partial<AlertTypeCount>;

  return typeof candidate.alertType === 'string' && typeof candidate.count === 'number'
    ? { alertType: candidate.alertType, count: candidate.count }
    : null;
}

function numberOrNull(value: unknown): number | null {
  return typeof value === 'number' ? value : null;
}
