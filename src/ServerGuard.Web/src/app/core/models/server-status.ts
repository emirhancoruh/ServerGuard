import { ServerMetric } from './server-metric';

/** Dashboard'da bir sunucu kartının gösterdiği en güncel durum. */
export interface ServerStatus {
  serverName: string;
  cpuUsagePercent: number;
  ramUsagePercent: number;
  lastSeen: Date;
}

export function toServerStatus(metric: ServerMetric): ServerStatus {
  return {
    serverName: metric.serverName,
    cpuUsagePercent: metric.cpuUsagePercent,
    ramUsagePercent: metric.ramUsagePercent,
    lastSeen: new Date(metric.timestamp)
  };
}
