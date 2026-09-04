/** ServerGuard.Shared/Dtos/ServerSummaryDto.cs karşılığı. */
export interface ServerSummary {
  serverName: string;
  lastSeenAt: Date;
}

/** API'den gelen sunucu kaydını doğrular; bozuksa null döner. */
export function toServerSummary(raw: unknown): ServerSummary | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as { serverName?: unknown; lastSeenAt?: unknown };

  if (typeof candidate.serverName !== 'string' || candidate.serverName.length === 0) {
    return null;
  }

  const lastSeen =
    typeof candidate.lastSeenAt === 'string' && !Number.isNaN(Date.parse(candidate.lastSeenAt))
      ? new Date(candidate.lastSeenAt)
      : new Date(0);

  return { serverName: candidate.serverName, lastSeenAt: lastSeen };
}
