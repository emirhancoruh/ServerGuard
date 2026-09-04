/** ServerGuard.Shared/Dtos/ServerMetricDto.cs karşılığı (JSON camelCase olarak gelir). */
export interface ServerMetric {
  serverName: string;
  cpuUsagePercent: number;
  ramUsagePercent: number;
  timestamp: string;
}
