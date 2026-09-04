/** ServerGuard.Shared/Enums/AlertSeverity.cs karşılığı. */
export type AlertSeverity = 'Low' | 'Medium' | 'High' | 'Critical';

/** ServerGuard.Shared/Enums/AlertType.cs karşılığı. */
export type AlertType = 'BruteForceAttempt';

/** ServerGuard.Shared/Dtos/SecurityAlertDto.cs karşılığı. */
export interface SecurityAlert {
  id: number;
  serverName: string;
  alertType: AlertType | string;
  severity: AlertSeverity | string;
  sourceIp: string;
  observedCount: number;
  description: string;
  timestamp: string;
}

/**
 * Hub'dan veya API'den gelen ham veriyi doğrular.
 * Sunucudan bozuk/eksik bir kayıt gelirse null döner; çağıran tarafın onu atlayıp
 * çalışmaya devam etmesini sağlar, UI çökmez.
 */
export function toSecurityAlert(raw: unknown): SecurityAlert | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Partial<SecurityAlert>;

  const hasRequiredFields =
    typeof candidate.id === 'number' &&
    typeof candidate.serverName === 'string' &&
    typeof candidate.severity === 'string' &&
    typeof candidate.timestamp === 'string' &&
    !Number.isNaN(Date.parse(candidate.timestamp));

  if (!hasRequiredFields) {
    return null;
  }

  return {
    id: candidate.id!,
    serverName: candidate.serverName!,
    alertType: candidate.alertType ?? 'Bilinmiyor',
    severity: candidate.severity!,
    sourceIp: candidate.sourceIp ?? '-',
    observedCount: candidate.observedCount ?? 0,
    description: candidate.description ?? '',
    timestamp: candidate.timestamp!
  };
}
