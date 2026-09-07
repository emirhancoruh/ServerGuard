/**
 * Backend'deki ServerGuard.Shared/ApiRoutes.cs karşılığı.
 * Yollar tek yerden yönetilir; component'lerde string yazılmaz.
 */
export const ApiRoutes = {
  monitoringHub: '/hubs/monitoring',
  servers: '/api/servers',
  alerts: '/api/alerts',
  trafficTimeline: '/api/traffic/timeline',
  topClientIps: '/api/traffic/top-ips',
  reportSummary: '/api/reports/summary'
} as const;

/** Hub'ın istemciye gönderdiği event adları (IMonitoringClient karşılığı). */
export const HubEvents = {
  receiveMetric: 'ReceiveMetric',
  receiveAlert: 'ReceiveAlert',
  receiveTrafficLog: 'ReceiveTrafficLog'
} as const;

/** ServerGuard.Shared/TrafficQueryConstraints.cs karşılığı. */
export const TrafficQueryDefaults = {
  minutes: 60,
  bucketSeconds: 60,
  topIpCount: 10
} as const;
