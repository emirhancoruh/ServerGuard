import { Injectable, OnDestroy, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiRoutes, HubEvents } from '../api-routes';
import { ConnectionState } from '../models/connection-state';
import { ServerMetric } from '../models/server-metric';
import { SecurityAlert, toSecurityAlert } from '../models/security-alert';
import { TrafficLog, toTrafficLog } from '../models/traffic';

/**
 * MonitoringHub'a bağlanır ve gelen verileri Observable olarak yayınlar.
 * Bağlantı koparsa withAutomaticReconnect() sessizce yeniden dener; UI bloklanmaz.
 */
@Injectable({ providedIn: 'root' })
export class MonitoringHubService implements OnDestroy {
  /** Yeniden bağlanma denemeleri arası bekleme (ms). Son değerden sonra sabit aralıkla devam eder. */
  private static readonly reconnectDelaysMs = [0, 2_000, 5_000, 10_000, 30_000];

  /** İlk bağlantı kurulamazsa tekrar denemeden önce beklenen süre (ms). */
  private static readonly initialRetryDelayMs = 5_000;

  private readonly metricSubject = new Subject<ServerMetric>();
  private readonly alertSubject = new Subject<SecurityAlert>();
  private readonly trafficSubject = new Subject<TrafficLog>();
  private readonly connectionStateSignal = signal<ConnectionState>('disconnected');
  private connection?: HubConnection;

  /** Hub'dan gelen canlı metrik akışı. */
  readonly metrics$: Observable<ServerMetric> = this.metricSubject.asObservable();

  /** Hub'dan gelen canlı alarm akışı. Bozuk kayıtlar bu akışa hiç girmez. */
  readonly alerts$: Observable<SecurityAlert> = this.alertSubject.asObservable();

  /** Hub'dan gelen canlı trafik akışı. Bozuk kayıtlar bu akışa hiç girmez. */
  readonly trafficLogs$: Observable<TrafficLog> = this.trafficSubject.asObservable();

  /** Bağlantı durumu; şablonda göstergeye bağlanır. */
  readonly connectionState = this.connectionStateSignal.asReadonly();

  async start(): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}${ApiRoutes.monitoringHub}`)
      .withAutomaticReconnect([...MonitoringHubService.reconnectDelaysMs])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on(HubEvents.receiveMetric, (metric: ServerMetric) => {
      this.metricSubject.next(metric);
    });

    this.connection.on(HubEvents.receiveAlert, (raw: unknown) => {
      this.emit(raw, toSecurityAlert, this.alertSubject, 'alarm');
    });

    this.connection.on(HubEvents.receiveTrafficLog, (raw: unknown) => {
      this.emit(raw, toTrafficLog, this.trafficSubject, 'trafik kaydı');
    });

    this.connection.onreconnecting(() => this.connectionStateSignal.set('reconnecting'));
    this.connection.onreconnected(() => this.connectionStateSignal.set('connected'));
    this.connection.onclose(() => this.connectionStateSignal.set('disconnected'));

    await this.connect();
  }

  ngOnDestroy(): void {
    this.metricSubject.complete();
    this.alertSubject.complete();
    this.trafficSubject.complete();
    void this.connection?.stop();
  }

  /**
   * Gelen kaydı doğrulayıp ilgili akışa yayınlar. Bozuk bir kayıt yalnızca konsola yazılır
   * ve atlanır; hub bağlantısı ve UI etkilenmez.
   */
  private emit<T>(
    raw: unknown,
    convert: (value: unknown) => T | null,
    subject: Subject<T>,
    label: string
  ): void {
    try {
      const value = convert(raw);

      if (value === null) {
        console.warn(`Geçersiz ${label} verisi atlandı.`, raw);
        return;
      }

      subject.next(value);
    } catch (error) {
      console.error(`${label} işlenirken hata oluştu, atlandı.`, error);
    }
  }

  /**
   * İlk bağlantıyı kurar. withAutomaticReconnect yalnızca kurulmuş bir bağlantı
   * koptuğunda devreye girer, ilk denemenin başarısızlığını kapsamaz.
   */
  private async connect(): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connectionStateSignal.set('connecting');

    try {
      await this.connection.start();
      this.connectionStateSignal.set('connected');
    } catch {
      // Backend henüz ayakta olmayabilir; UI'ı bloklamadan tekrar denenir.
      this.connectionStateSignal.set('disconnected');
      setTimeout(() => void this.connect(), MonitoringHubService.initialRetryDelayMs);
    }
  }
}
