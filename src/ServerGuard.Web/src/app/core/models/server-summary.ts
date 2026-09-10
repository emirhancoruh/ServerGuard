/** ServerGuard.Shared/Enums/ServerHealthStatus.cs karşılığı. */
export type ServerHealthStatus = 'Online' | 'Stale' | 'Offline';

/** ServerGuard.Shared/Dtos/ServerSummaryDto.cs karşılığı. */
export interface ServerSummary {
  serverName: string;
  lastSeenAt: Date;
  status: ServerHealthStatus;
  secondsSinceLastSeen: number;
  cpuUsagePercent: number | null;
  ramUsagePercent: number | null;
  diskFreePercent: number | null;
}

const KNOWN_STATUSES: readonly string[] = ['Online', 'Stale', 'Offline'];

/** API'den gelen sunucu kaydını doğrular; bozuksa null döner. */
export function toServerSummary(raw: unknown): ServerSummary | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Record<string, unknown>;
  const serverName = candidate['serverName'];

  if (typeof serverName !== 'string' || serverName.length === 0) {
    return null;
  }

  const lastSeenRaw = candidate['lastSeenAt'];
  const lastSeenAt =
    typeof lastSeenRaw === 'string' && !Number.isNaN(Date.parse(lastSeenRaw))
      ? new Date(lastSeenRaw)
      : new Date(0);

  const status = candidate['status'];

  return {
    serverName,
    lastSeenAt,
    // Bilinmeyen bir durum gelirse en kötüsü varsayılır; sessizce "sağlıklı" göstermek yanıltıcı olur.
    status: typeof status === 'string' && KNOWN_STATUSES.includes(status)
      ? (status as ServerHealthStatus)
      : 'Offline',
    secondsSinceLastSeen: numberOr(candidate['secondsSinceLastSeen'], 0),
    cpuUsagePercent: numberOrNull(candidate['cpuUsagePercent']),
    ramUsagePercent: numberOrNull(candidate['ramUsagePercent']),
    diskFreePercent: numberOrNull(candidate['diskFreePercent'])
  };
}

function numberOrNull(value: unknown): number | null {
  return typeof value === 'number' ? value : null;
}

function numberOr(value: unknown, fallback: number): number {
  return typeof value === 'number' ? value : fallback;
}
