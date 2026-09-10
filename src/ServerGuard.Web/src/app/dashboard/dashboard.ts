import { Component, OnInit, inject } from '@angular/core';

import { AlertList } from '../alerts/alert-list';
import { ServerSelector } from '../shared/server-selector';
import { ServerTiles } from '../monitoring/server-tiles';
import { ServiceHealthTable } from '../monitoring/service-health-table';
import { StatusBar } from '../monitoring/status-bar';
import { TopIpsTable } from '../traffic/top-ips-table';
import { TrafficChart } from '../traffic/traffic-chart';
import { MonitoringHubService } from '../core/services/monitoring-hub.service';

@Component({
  selector: 'sg-dashboard',
  imports: [
    StatusBar,
    ServerTiles,
    ServiceHealthTable,
    TrafficChart,
    TopIpsTable,
    AlertList,
    ServerSelector
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly hub = inject(MonitoringHubService);

  readonly connectionState = this.hub.connectionState;

  ngOnInit(): void {
    // Hub bağlantısı burada başlatılır; alt bileşenler yalnızca akışa abone olur.
    void this.hub.start();
  }

  connectionLabel(): string {
    switch (this.connectionState()) {
      case 'connected':
        return 'Canlı';
      case 'connecting':
        return 'Bağlanıyor';
      case 'reconnecting':
        return 'Yeniden bağlanıyor';
      default:
        return 'Bağlantı yok';
    }
  }
}
